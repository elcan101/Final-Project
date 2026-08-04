using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Areas.AdminPanel.Controllers
{
   
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

            var allOrders = _context.Orders.Where(o => !o.IsDeleted).ToList();
            ViewBag.OrderCount = allOrders.Count;

            var totalTurnover = allOrders.Sum(o => o.TotalAmount);           
            var totalDeliveryFee = allOrders.Sum(o => o.DeliveryFee);      
            var totalBookSales = totalTurnover - totalDeliveryFee;          
            var platformDeliveryShare = Math.Round(totalDeliveryFee * (1 - Order.CourierShareRate), 2); 

            ViewBag.TotalTurnover = totalTurnover;
            ViewBag.TotalBookSales = totalBookSales;
            ViewBag.TotalDeliveryFee = totalDeliveryFee;
            ViewBag.PlatformDeliveryShare = platformDeliveryShare;

            var since = DateTime.Now.Date.AddDays(-13);
            var dailyTotals = allOrders
                .Where(o => o.CreatedDate.Date >= since)
                .GroupBy(o => o.CreatedDate.Date)
                .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

            var chartLabels = new List<string>();
            var chartValues = new List<decimal>();
            for (var d = since; d <= DateTime.Now.Date; d = d.AddDays(1))
            {
                chartLabels.Add(d.ToString("dd.MM"));
                chartValues.Add(dailyTotals.TryGetValue(d, out var v) ? v : 0.00m);
            }
            ViewBag.ChartLabels = chartLabels;
            ViewBag.ChartValues = chartValues;

            return View();
        }

        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.Courier)
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);
            ViewBag.UserEmails = users;

            return View(orders);
        }


        public IActionResult Categories()
        {
            var categories = _context.Categories
                .Include(c => c.Products)
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToList();

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCategory(Category category)
        {
            if (string.IsNullOrWhiteSpace(category?.Name))
            {
                TempData["Error"] = "Kateqoriyanın adı boş ola bilməz.";
                return RedirectToAction("Categories");
            }

            _context.Categories.Add(new Category { Name = category.Name.Trim() });
            _context.SaveChanges();

            TempData["Success"] = "Kateqoriya əlavə olundu.";
            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id)
        {
            var category = _context.Categories.FirstOrDefault(c => c.Id == id && !c.IsDeleted);
            if (category == null)
            {
                TempData["Error"] = "Kateqoriya tapılmadı.";
                return RedirectToAction("Categories");
            }

            category.IsDeleted = true;
            category.UpdatedDate = DateTime.Now;
            _context.SaveChanges();

            TempData["Success"] = $"\"{category.Name}\" kateqoriyası silindi.";
            return RedirectToAction("Categories");
        }


        public IActionResult Couriers()
        {
            var couriers = _context.CourierProfiles
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();

            return View(couriers);
        }


        public async Task<IActionResult> Subscriptions()
        {
            var subscriptions = await _context.UserSubscriptions
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            var userIds = subscriptions.Select(s => s.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);
            ViewBag.UserEmails = users;

            return View(subscriptions);
        }


        public async Task<IActionResult> Rentals()
        {
            var rentals = await _context.BookRentals
                .Include(r => r.Product)
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.RentedDate)
                .ToListAsync();

            var userIds = rentals.Select(r => r.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);
            ViewBag.UserEmails = users;

            return View(rentals);
        }


        public async Task<IActionResult> Listings()
        {
            var listings = await _context.Listings
                .Include(l => l.Category)
                .Where(l => !l.IsDeleted)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            var userIds = listings.Select(l => l.SellerId)
                .Concat(listings.Where(l => l.BuyerId != null).Select(l => l.BuyerId!))
                .Distinct()
                .ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);
            ViewBag.UserEmails = users;

            return View(listings);
        }


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
