using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Areas.AdminPanel.Controllers
{
    // Bu controller "AdminPanel" adlı ayrıca Area-nın içindədir.
    // Route: /AdminPanel/Admin, /AdminPanel/Admin/Admins və s.
    [Area("AdminPanel")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AdminController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            ViewBag.ProductCount = _context.Products.Count(p => !p.IsDeleted);
            ViewBag.CategoryCount = _context.Categories.Count(c => !c.IsDeleted);
            ViewBag.CouponCount = _context.Coupons.Count(c => !c.IsDeleted);
            ViewBag.ListingCount = _context.Listings.Count(l => !l.IsDeleted);
            ViewBag.OrderCount = _context.Orders.Count(o => !o.IsDeleted);
            return View();
        }

        // ---------- Adminlərin idarə olunması ----------

        public async Task<IActionResult> Admins()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            return View(admins.OrderBy(a => a.Email).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "E-poçt daxil edin.";
                return RedirectToAction("Admins");
            }

            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null)
            {
                TempData["Error"] = "Bu e-poçt ilə istifadəçi tapılmadı. Əvvəlcə həmin şəxs sayt üzərindən qeydiyyatdan keçməlidir.";
                return RedirectToAction("Admins");
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] = "Bu istifadəçi artıq admindir.";
                return RedirectToAction("Admins");
            }

            await _userManager.AddToRoleAsync(user, "Admin");
            TempData["Success"] = $"{user.Email} artıq admindir.";
            return RedirectToAction("Admins");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdmin(string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["Error"] = "Öz admin səlahiyyətinizi özünüz silə bilməzsiniz.";
                return RedirectToAction("Admins");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
                TempData["Success"] = "Admin səlahiyyəti geri alındı.";
            }
            return RedirectToAction("Admins");
        }
    }
}
