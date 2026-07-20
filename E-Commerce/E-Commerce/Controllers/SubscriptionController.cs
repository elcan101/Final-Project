using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPaymentService _payments;

        public SubscriptionController(AppDbContext context, IPaymentService payments)
        {
            _context = context;
            _payments = payments;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Planların müqayisə cədvəli (Standard vs Premium)
        public IActionResult Index()
        {
            var userId = GetUserId();
            var current = _context.UserSubscriptions
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive && s.ExpiryDate > DateTime.Now)
                .OrderByDescending(s => s.ExpiryDate)
                .FirstOrDefault();

            ViewBag.CurrentPlan = current;
            return View();
        }

        // Planı seçəndə birbaşa ödəniş etmək əvəzinə saxta kart-ödəniş səhifəsinə keçirik
        [HttpGet]
        public IActionResult Checkout(SubscriptionPlanType planType)
        {
            ViewBag.PlanType = planType;
            ViewBag.Price = UserSubscription.MonthlyPrice(planType);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(SubscriptionPlanType planType, string cardNumber, string expiry, string cvc, string cardHolder)
        {
            if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(expiry) || string.IsNullOrWhiteSpace(cvc))
            {
                TempData["Error"] = "Kart məlumatlarını tam daxil edin.";
                return RedirectToAction("Checkout", new { planType });
            }

            var userId = GetUserId();
            var price = UserSubscription.MonthlyPrice(planType);

            // Saxta ödəniş sistemi: kart tokenləşdirilir (real kart nömrəsi heç vaxt saxlanılmır), sonra "tutulur"
            var (token, brand, last4) = await _payments.TokenizeCardAsync(cardNumber, expiry, cvc);
            var result = await _payments.ChargeAsync(userId, price, $"Kitab Pass — {planType} abunəlik", token);

            if (!result.Success)
            {
                TempData["Error"] = "Ödəniş uğursuz oldu.";
                return RedirectToAction("Checkout", new { planType });
            }

            // Köhnə aktiv abunəni deaktiv et, yenisini yarat (yüksəltmə/yeniləmə)
            var existing = _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted)
                .ToList();
            foreach (var s in existing) s.IsActive = false;

            var subscription = new UserSubscription
            {
                UserId = userId,
                PlanType = planType,
                StartDate = DateTime.Now,
                ExpiryDate = DateTime.Now.AddDays(30),
                PricePaid = price,
                IsActive = true,
            };

            _context.UserSubscriptions.Add(subscription);
            _context.SaveChanges();

            TempData["Success"] = $"Ödəniş uğurla tamamlandı ({brand} ****{last4}). {planType} abunəliyiniz aktivləşdirildi!";
            return RedirectToAction("Index");
        }
    }
}
