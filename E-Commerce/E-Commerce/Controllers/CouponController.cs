using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    // Kuponlar yalnız Adminlər tərəfindən yaradıla / idarə oluna bilər.
    // Müştərilər yalnız checkout zamanı kupon KODUNU daxil edib Validate ilə yoxlaya bilirlər.
    [Authorize(Roles = "Admin")]
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
                .OrderByDescending(c => c.CreatedDate)
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
                TempData["Success"] = "Kupon yaradıldı.";
                return RedirectToAction("Index");
            }

            return View(coupon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            var coupon = _context.Coupons.FirstOrDefault(c => c.Id == id && !c.IsDeleted);
            if (coupon != null)
            {
                coupon.IsActive = !coupon.IsActive;
                coupon.UpdatedDate = DateTime.Now;
                _context.SaveChanges();
                TempData["Success"] = coupon.IsActive
                    ? "Kupon aktivləşdirildi."
                    : "Kupon deaktiv edildi.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var coupon = _context.Coupons.FirstOrDefault(c => c.Id == id && !c.IsDeleted);
            if (coupon != null)
            {
                coupon.IsDeleted = true;
                coupon.UpdatedDate = DateTime.Now;
                _context.SaveChanges();
                TempData["Success"] = "Kupon silindi.";
            }
            return RedirectToAction("Index");
        }

        // Checkout zamanı kupon kodunun doğruluğunu yoxlamaq üçün (AJAX) — HAMI istifadə edə bilər
        [AllowAnonymous]
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
