using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    public class CourierController : Controller
    {
        private readonly AppDbContext _context;
        private readonly DeliveryPricingService _deliveryPricing;

        public CourierController(AppDbContext context, DeliveryPricingService deliveryPricing)
        {
            _context = context;
            _deliveryPricing = deliveryPricing;
        }

        public IActionResult Index()
        {
            var couriers = _context.CourierProfiles
                .Where(c => !c.IsDeleted)
                .ToList();

            return View(couriers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CourierProfile courier)
        {
            if (ModelState.IsValid)
            {
                _context.CourierProfiles.Add(courier);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(courier);
        }

        // Kuryerin canlı iş paneli: "Hazır" sifarişlərin broadcast siqnalını qəbul edir,
        // ilk basan kuryer sifarişi öz üzərinə götürür (SignalR)
        [Authorize]
        public IActionResult Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var profile = _context.CourierProfiles.FirstOrDefault(c => c.CourierId == userId && !c.IsDeleted);

            if (profile != null)
            {
                var activeOrder = _context.Orders
                    .Where(o => !o.IsDeleted && o.CourierProfileId == profile.Id && o.Status == "Kuryerdədir")
                    .OrderByDescending(o => o.CreatedDate)
                    .FirstOrDefault();
                ViewBag.ActiveOrder = activeOrder;

                // Kuryer online olanda broadcast anını qaçırmış ola bilər —
                // ona görə hazırda gözləyən (sahibsiz) "Hazırdır" sifarişləri də birbaşa göstəririk
                var pendingOrders = _context.Orders
                    .Where(o => !o.IsDeleted && o.Status == "Hazırdır" && o.CourierProfileId == null)
                    .OrderBy(o => o.CreatedDate)
                    .ToList();
                ViewBag.PendingOrders = pendingOrders;
                ViewBag.DepotName = _deliveryPricing.Depot.Name;
            }

            return View(profile);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BecomeCourier(string fullName, string vehicleType)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var existing = _context.CourierProfiles.FirstOrDefault(c => c.CourierId == userId && !c.IsDeleted);

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["Error"] = "Ad Soyad mütləq daxil edilməlidir.";
                return RedirectToAction("Dashboard");
            }

            if (existing == null)
            {
                _context.CourierProfiles.Add(new CourierProfile
                {
                    CourierId = userId,
                    FullName = fullName.Trim(),
                    VehicleType = string.IsNullOrWhiteSpace(vehicleType) ? "Piyada" : vehicleType,
                    IsAvailable = false,
                });
                _context.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }
    }
}
