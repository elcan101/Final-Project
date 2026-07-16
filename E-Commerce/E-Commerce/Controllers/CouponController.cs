using Microsoft.AspNetCore.Mvc;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class CouponController : Controller
    {
        private readonly AppDbContext _context;

        public CouponController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var coupons = _context.Coupons
                .Where(c => !c.IsDeleted)
                .ToList();

            return View(coupons);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Coupon coupon)
        {
            if (ModelState.IsValid)
            {
                _context.Coupons.Add(coupon);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(coupon);
        }

        // Checkout zamanı kupon kodunun doğruluğunu yoxlamaq üçün (AJAX)
        [HttpGet]
        public IActionResult Validate(string code)
        {
            var coupon = _context.Coupons
                .FirstOrDefault(c => c.Code == code && c.IsActive && !c.IsDeleted);

            if (coupon == null)
            {
                return Json(new { valid = false });
            }

            return Json(new { valid = true, discount = coupon.DiscountAmount });
        }
    }
}
