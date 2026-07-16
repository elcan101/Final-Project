using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        private string GetUserId()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(authUserId))
                {
                    return authUserId;
                }
            }

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("UserId", userId);
            }
            return userId;
        }

        // İstifadəçinin sifarişlərinin siyahısı
        public IActionResult Index()
        {
            var userId = GetUserId();
            var orders = _context.Orders
                .Include(o => o.Courier)
                .Where(o => o.UserId == userId && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedDate)
                .ToList();

            return View(orders);
        }

        // Səbətdəki kitablardan sifariş yaradır və səbəti təmizləyir
        public IActionResult Checkout()
        {
            var userId = GetUserId();
            var cart = _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefault(c => c.UserId == userId && !c.IsDeleted);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Səbətiniz boşdur.";
                return RedirectToAction("Index", "Cart");
            }

            var total = cart.CartItems.Sum(ci => ci.Product.Price * ci.Quantity);

            var order = new Order
            {
                UserId = userId,
                TotalAmount = total,
                CashbackAmount = Math.Round(total * 0.02m, 2), // Sifarişin 2%-i keşbek kimi qaytarılır
                Status = "Hazırlanır"
            };

            _context.Orders.Add(order);
            _context.CartItems.RemoveRange(cart.CartItems);
            _context.SaveChanges();

            TempData["Success"] = "Sifarişiniz qəbul olundu!";
            return RedirectToAction("Index");
        }
    }
}
