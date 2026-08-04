using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    
    public class SubscriptionController : Controller
    {
        private readonly AppDbContext _context;

        public SubscriptionController(AppDbContext context)
        {
            _context = context;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        public IActionResult Index()
        {
            UserSubscription? current = null;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = GetUserId();
                current = _context.UserSubscriptions
                    .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive && s.ExpiryDate > DateTime.Now)
                    .OrderByDescending(s => s.ExpiryDate)
                    .FirstOrDefault();
            }

            ViewBag.CurrentPlan = current;
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult Checkout(SubscriptionPlanType planType)
        {
            var userId = GetUserId()!;
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);

            ViewBag.PlanType = planType;
            ViewBag.Price = UserSubscription.MonthlyPrice(planType);
            ViewBag.Balance = wallet?.Balance ?? 0.00m;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Subscribe(SubscriptionPlanType planType)
        {
            var userId = GetUserId()!;
            var price = UserSubscription.MonthlyPrice(planType);

            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
            }

            if (wallet.Balance < price)
            {
                TempData["Error"] = $"Balansınız kifayət etmir (çatışmır: {(price - wallet.Balance):0.00} AZN). Zəhmət olmasa əvvəlcə balansınızı kartla artırın.";
                return RedirectToAction("Checkout", new { planType });
            }

            wallet.Balance -= price;

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

            TempData["Success"] = $"Ödəniş uğurla tamamlandı ({price:0.00} AZN balansınızdan tutuldu). {planType} abunəliyiniz aktivləşdirildi!";
            return RedirectToAction("Index");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel()
        {
            var userId = GetUserId()!;

            var active = _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted)
                .ToList();

            if (!active.Any())
            {
                TempData["Error"] = "Ləğv ediləcək aktiv abunəliyiniz yoxdur.";
                return RedirectToAction("Profile", "Account");
            }

            foreach (var s in active)
            {
                s.IsActive = false;
                s.ExpiryDate = DateTime.Now;
            }

            _context.SaveChanges();

            TempData["Success"] = "Abunəliyiniz ləğv edildi.";
            return RedirectToAction("Profile", "Account");
        }
    }
}
