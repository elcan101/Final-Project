using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
        {
            _context = context;
        }

        // Giriş etmiş istifadəçi varsa onun əsl ID-si, yoxdursa sessiya üzərindən qonaq ID-si
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

        private Cart GetOrCreateCart(string userId)
        {
            var cart = _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefault(c => c.UserId == userId && !c.IsDeleted);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                _context.SaveChanges();
            }

            return cart;
        }

        // Səbəti göstər
        public IActionResult Index()
        {
            var cart = GetOrCreateCart(GetUserId());
            return View(cart);
        }

        // Kitabı səbətə əlavə et
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToCart(int productId, int quantity = 1)
        {
            var cart = GetOrCreateCart(GetUserId());

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = quantity
                });
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        // Səbətdən məhsulu sil
        public IActionResult Remove(int id)
        {
            var item = _context.CartItems.Find(id);
            if (item != null)
            {
                _context.CartItems.Remove(item);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // Səbətə promokod tətbiqi (Trendyol tipli dinamik endirim)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApplyCoupon(string code)
        {
            var coupon = _context.Coupons.FirstOrDefault(c => c.Code == code && c.IsActive && !c.IsDeleted);
            if (coupon == null)
            {
                TempData["Error"] = "Promokod etibarsızdır.";
                HttpContext.Session.Remove("AppliedCouponCode");
                HttpContext.Session.Remove("AppliedCouponDiscount");
                return RedirectToAction("Index");
            }

            HttpContext.Session.SetString("AppliedCouponCode", coupon.Code);
            HttpContext.Session.SetString("AppliedCouponDiscount", coupon.DiscountAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));

            TempData["Success"] = $"'{coupon.Code}' promokodu tətbiq olundu: -{coupon.DiscountAmount} AZN";
            return RedirectToAction("Index");
        }

        // Səbətdəki miqdarı dəyiş
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity(int id, int quantity)
        {
            var item = _context.CartItems.Find(id);
            if (item != null)
            {
                if (quantity <= 0)
                {
                    _context.CartItems.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}
