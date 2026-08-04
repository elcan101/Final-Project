using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    [Authorize]
    public class RentalController : Controller
    {
        private readonly AppDbContext _context;
        private readonly DeliveryPricingService _deliveryPricing;

        public RentalController(AppDbContext context, DeliveryPricingService deliveryPricing)
        {
            _context = context;
            _deliveryPricing = deliveryPricing;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public IActionResult Index()
        {
            var userId = GetUserId();
            var rentals = _context.BookRentals
                .Include(r => r.Product)
                .Include(r => r.Order)
                    .ThenInclude(o => o!.Courier)
                .Where(r => r.UserId == userId && !r.IsDeleted)
                .OrderByDescending(r => r.RentedDate)
                .ToList();

            return View(rentals);
        }

        [HttpGet]
        public IActionResult ChooseLocation(int productId, int days = 7)
        {
            if (days <= 0) days = 7;

            var product = _context.Products.FirstOrDefault(p => p.Id == productId && !p.IsDeleted);
            if (product == null) return NotFound();

            ViewBag.ProductId = productId;
            ViewBag.ProductTitle = product.Title;
            ViewBag.Days = days;

            var depot = _deliveryPricing.Depot;
            ViewBag.DepotLat = depot.Latitude;
            ViewBag.DepotLng = depot.Longitude;
            ViewBag.BaseFee = depot.BaseFee;
            ViewBag.PerKmFee = depot.PerKmFee;
            ViewBag.MinFee = depot.MinFee;
            ViewBag.MaxFee = depot.MaxFee;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rent(int productId, int days, double? lat, double? lng, string? addressText, string? phoneNumber)
        {
            if (days <= 0) days = 7;

           
            if (lat == null || lng == null)
            {
                TempData["Error"] = "Zəhmət olmasa çatdırılma ünvanını xəritədən seçin.";
                return RedirectToAction("ChooseLocation", new { productId, days });
            }

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "Zəhmət olmasa əlaqə nömrənizi daxil edin.";
                return RedirectToAction("ChooseLocation", new { productId, days });
            }

            var userId = GetUserId();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null) return NotFound();

            var dailyRate = 0.20m;
            var isFree = false;

            var subscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted && s.ExpiryDate > DateTime.Now)
                .FirstOrDefaultAsync();

            var chargeableDays = days;
            if (subscription != null && subscription.PlanType == SubscriptionPlanType.Premium &&
                (subscription.FreeRentalUsedThisMonth == null ||
                 subscription.FreeRentalUsedThisMonth.Value.Month != DateTime.Now.Month))
            {
                isFree = true;
                chargeableDays = Math.Max(0, days - 14); 
                subscription.FreeRentalUsedThisMonth = DateTime.Now;
            }
            else if (subscription != null)
            {
                dailyRate = 0.19m;
            }

            var baseCost = chargeableDays * dailyRate;

           
            var deliveryFee = _deliveryPricing.CalculateDeliveryFee(lat, lng, out var distanceKm);
            var total = baseCost + deliveryFee;

            if (total > 0)
            {
                var paid = await PayAsync(userId, total, $"'{product.Title}' kitab icarəsi ({days} gün) + çatdırılma");
                if (!paid)
                {
                    TempData["Error"] = "Balansınız kifayət etmir. Zəhmət olmasa əvvəlcə balansınızı kartla artırın.";
                    return RedirectToAction("Details", "Product", new { id = productId });
                }
            }

            var order = new Order
            {
                UserId = userId,
                TotalAmount = total,
                Status = "Hazırlanır",
                DeliveryLatitude = lat,
                DeliveryLongitude = lng,
                DeliveryAddressText = string.IsNullOrWhiteSpace(addressText) ? null : addressText.Trim(),
                DeliveryFee = deliveryFee,
                DeliveryDistanceKm = distanceKm,
                PhoneNumber = phoneNumber.Trim()
            };
            _context.Orders.Add(order);

            var rental = new BookRental
            {
                UserId = userId,
                ProductId = productId,
                RentedDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(days),
                DailyRate = dailyRate,
                BaseCost = baseCost,
                IsFreePremiumRental = isFree,
                Order = order,
            };

            _context.BookRentals.Add(rental);
            _context.SaveChanges();

            TempData["Success"] = isFree
                ? $"Kitab premium pulsuz icarə hüququnuzla icarəyə götürüldü! Çatdırılma haqqı: {deliveryFee:0.00} AZN balansınızdan tutuldu. Kuryer hazır olan kimi ünvanınıza çatdıracaq."
                : $"Kitab icarəyə götürüldü! Ümumi {total:0.00} AZN (icarə: {baseCost:0.00} AZN + çatdırılma: {deliveryFee:0.00} AZN) balansınızdan tutuldu. Kuryer hazır olan kimi ünvanınıza çatdıracaq.";
            return RedirectToAction("Index");
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Extend(int id, int additionalDays)
        {
            if (additionalDays <= 0)
            {
                TempData["Error"] = "Uzatma müddəti müsbət gün sayı olmalıdır.";
                return RedirectToAction("Index");
            }

            var userId = GetUserId();
            var rental = await _context.BookRentals.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId && !r.IsDeleted);
            if (rental == null || rental.IsReturned)
            {
                TempData["Error"] = "Bu icarənin müddətini uzatmaq mümkün deyil.";
                return RedirectToAction("Index");
            }

            var extraCost = additionalDays * rental.DailyRate;
            if (extraCost > 0)
            {
                var paid = await PayAsync(userId, extraCost, $"İcarə müddətinin {additionalDays} gün uzadılması");
                if (!paid)
                {
                    TempData["Error"] = "Balansınız kifayət etmir. Müddəti uzatmaq üçün əvvəlcə balansınızı kartla artırın.";
                    return RedirectToAction("Index");
                }
            }

           
            rental.BaseCost += extraCost;

            rental.DueDate = rental.DueDate.AddDays(additionalDays);
            rental.DueSoonEmailSent = false;

            rental.LateFineApplied = false;
            rental.PenaltyChargedDays = 0;

            _context.SaveChanges();

            TempData["Success"] = $"İcarə müddəti {additionalDays} gün uzadıldı (yeni qaytarma tarixi: {rental.DueDate:dd.MM.yyyy}). {extraCost:0.00} AZN balansınızdan tutuldu.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id)
        {
            var userId = GetUserId();
            var rental = await _context.BookRentals.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (rental == null || rental.IsReturned) return RedirectToAction("Index");

            rental.ReturnedDate = DateTime.Now;

            var remainingPenalty = rental.CalculatePenalty() - (rental.PenaltyChargedDays * rental.PenaltyRatePerDay);
            if (remainingPenalty > 0)
            {
                await DeductWalletAsync(userId, remainingPenalty, "Gecikmə cəriməsi");
                rental.PenaltyAmount += remainingPenalty;
            }

            _context.SaveChanges();

            TempData["Success"] = remainingPenalty > 0
                ? $"Kitab qaytarıldı. Gecikmə cəriməsi: {remainingPenalty:0.00} AZN balansınızdan tutuldu."
                : "Kitab vaxtında qaytarıldı, təşəkkürlər!";
            return RedirectToAction("Index");
        }

        private async Task<bool> PayAsync(string userId, decimal amount, string description)
        {
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
            }

            if (wallet.Balance < amount)
            {
                return false;
            }

            wallet.Balance -= amount;
            await Task.CompletedTask;
            return true;
        }

        private async Task DeductWalletAsync(string userId, decimal amount, string description)
        {
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
            }
            wallet.Balance -= amount; 
            await Task.CompletedTask;
        }
    }
}
