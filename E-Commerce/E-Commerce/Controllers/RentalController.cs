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
        private readonly IPaymentService _payments;

        public RentalController(AppDbContext context, IPaymentService payments)
        {
            _context = context;
            _payments = payments;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public IActionResult Index()
        {
            var userId = GetUserId();
            var rentals = _context.BookRentals
                .Include(r => r.Product)
                .Where(r => r.UserId == userId && !r.IsDeleted)
                .OrderByDescending(r => r.RentedDate)
                .ToList();

            return View(rentals);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rent(int productId, int days = 7)
        {
            if (days <= 0) days = 7;
            var userId = GetUserId();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null) return NotFound();

            var dailyRate = 0.20m;
            var isFree = false;

            // Premium: "fiziki kitab icarəsi — ayda bir pulsuz (14 günlük müddət)"
            var subscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted && s.ExpiryDate > DateTime.Now
                            && s.PlanType == SubscriptionPlanType.Premium)
                .FirstOrDefaultAsync();

            var chargeableDays = days;
            if (subscription != null &&
                (subscription.FreeRentalUsedThisMonth == null ||
                 subscription.FreeRentalUsedThisMonth.Value.Month != DateTime.Now.Month))
            {
                isFree = true;
                chargeableDays = Math.Max(0, days - 14); // yalnız 14 günü keçən hissə ödənişlidir
                subscription.FreeRentalUsedThisMonth = DateTime.Now;
            }
            else if (subscription != null)
            {
                // Premium: fiziki icarəyə 5% endirim (Standard planla eyni endirim şərti tətbiq olunur)
                dailyRate = 0.19m;
            }

            var baseCost = chargeableDays * dailyRate;

            if (baseCost > 0)
            {
                var paid = await PayAsync(userId, baseCost, $"'{product.Title}' kitab icarəsi ({days} gün)");
                if (!paid)
                {
                    TempData["Error"] = "Ödəniş həyata keçirilmədi.";
                    return RedirectToAction("Details", "Product", new { id = productId });
                }
            }

            var rental = new BookRental
            {
                UserId = userId,
                ProductId = productId,
                RentedDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(days),
                DailyRate = dailyRate,
                BaseCost = baseCost,
                IsFreePremiumRental = isFree,
            };

            _context.BookRentals.Add(rental);
            _context.SaveChanges();

            TempData["Success"] = isFree
                ? "Kitab premium pulsuz icarə hüququnuzla icarəyə götürüldü!"
                : "Kitab icarəyə götürüldü!";
            return RedirectToAction("Index");
        }

        // Kitabı qaytar — gecikmə varsa cərimə avtomatik balansdan tutulur
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

        // Balansdan tut, çatmazsa qalan hissəni (mock) kartdan tut
        private async Task<bool> PayAsync(string userId, decimal amount, string description)
        {
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
            }

            if (wallet.Balance >= amount)
            {
                wallet.Balance -= amount;
                return true;
            }

            var shortfall = amount - Math.Max(0, wallet.Balance);
            var result = await _payments.ChargeAsync(userId, shortfall, description);
            if (!result.Success) return false;

            // Mövcud balansın hamısı istifadə olunur, qalan hissə kartdan (mock) ödənilir
            wallet.Balance = 0;
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
            wallet.Balance -= amount; // İcarə cərimələri avtomatik balansdan çıxılır (mənfiyə düşə bilər)
            await Task.CompletedTask;
        }
    }
}
