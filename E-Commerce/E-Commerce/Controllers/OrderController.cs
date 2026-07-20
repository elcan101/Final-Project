using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Hubs;

namespace E_Commerce.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<CourierTrackingHub> _courierHub;

        public OrderController(AppDbContext context, IHubContext<CourierTrackingHub> courierHub)
        {
            _context = context;
            _courierHub = courierHub;
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

        // Sifarişi təsdiqləmədən əvvəl müştəri çatdırılma nöqtəsini xəritədən seçir
        [HttpGet]
        public IActionResult ChooseLocation()
        {
            var userId = GetUserId();
            var hasItems = _context.Carts
                .Include(c => c.CartItems)
                .Any(c => c.UserId == userId && !c.IsDeleted && c.CartItems.Any());

            if (!hasItems)
            {
                TempData["Error"] = "Səbətiniz boşdur.";
                return RedirectToAction("Index", "Cart");
            }

            return View();
        }

        // Səbətdəki kitablardan sifariş yaradır, kuponu tətbiq edir, keşbek balansa yazılır
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Checkout(double? lat, double? lng, string? addressText)
        {
            // Çatdırılma nöqtəsi seçilməyibsə, geri "xəritədən seç" səhifəsinə qaytarırıq
            if (lat == null || lng == null)
            {
                TempData["Error"] = "Zəhmət olmasa çatdırılma ünvanını xəritədən seçin.";
                return RedirectToAction("ChooseLocation");
            }

            var userId = GetUserId();
            var cart = _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefault(c => c.UserId == userId && !c.IsDeleted);

            // silinmiş/olmayan məhsula bağlı sətirləri kənara qoyuruq ki, checkout çökməsin
            var validItems = cart?.CartItems?.Where(ci => ci.Product != null).ToList()
                ?? new List<CartItem>();

            if (cart == null || !validItems.Any())
            {
                TempData["Error"] = "Səbətiniz boşdur.";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                var subtotal = validItems.Sum(ci => ci.Product.Price * ci.Quantity);

                // Trendyol tipli promokod endirimi (səbətə tətbiq olunmuşdusa)
                var couponCode = HttpContext.Session.GetString("AppliedCouponCode");
                var discount = 0m;
                if (!string.IsNullOrEmpty(couponCode))
                {
                    var discountStr = HttpContext.Session.GetString("AppliedCouponDiscount");
                    decimal.TryParse(discountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out discount);
                }

                var total = Math.Max(0, subtotal - discount);
                var cashback = Math.Round(total * 0.02m, 2); // Loyallıq: alış-verişin 2%-i keşbek

                var order = new Order
                {
                    UserId = userId,
                    TotalAmount = total,
                    DiscountAmount = discount,
                    CouponCode = couponCode,
                    CashbackAmount = cashback,
                    Status = "Hazırlanır",
                    DeliveryLatitude = lat,
                    DeliveryLongitude = lng,
                    DeliveryAddressText = string.IsNullOrWhiteSpace(addressText) ? null : addressText.Trim()
                };

                _context.Orders.Add(order);
                _context.CartItems.RemoveRange(validItems);

                // Loyallıq Sistemi: hər alış-verişdən sonra istifadəçinin balansına avtomatik cashback oturur
                var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
                if (wallet == null)
                {
                    wallet = new Wallet { UserId = userId };
                    _context.Wallets.Add(wallet);
                }
                wallet.Balance += cashback;
                wallet.TotalCashbackEarned += cashback;

                _context.SaveChanges();

                HttpContext.Session.Remove("AppliedCouponCode");
                HttpContext.Session.Remove("AppliedCouponDiscount");

                TempData["Success"] = $"Sifarişiniz qəbul olundu! {cashback} AZN keşbek balansınıza əlavə olundu.";
                return RedirectToAction("Index");
            }
            catch
            {
                // Nə səbəbdən olursa olsun xəta düşsə, ağ ekran (unhandled exception) yerinə
                // istifadəçini xoş mesajla səbətə qaytarırıq.
                TempData["Error"] = "Sifariş tamamlanarkən xəta baş verdi, zəhmət olmasa bir daha cəhd edin.";
                return RedirectToAction("Index", "Cart");
            }
        }

        // Mağaza/anbar sifarişi hazır elan edir → bütün boşda kuryerlərə SignalR siqnalı gedir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReady(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = "Hazırdır";
            await _context.SaveChangesAsync();

            // Kuryerlər sifarişi qəbul etmədən əvvəl çatdırılma nöqtəsini görsün ki,
            // özlərinə uyğundursa qəbul etsinlər
            await _courierHub.Clients.Group("idle-couriers").SendAsync("NewOrderAvailable", new
            {
                orderId = order.Id,
                total = order.TotalAmount,
                deliveryLat = order.DeliveryLatitude,
                deliveryLng = order.DeliveryLongitude,
                deliveryAddressText = order.DeliveryAddressText,
            });

            TempData["Success"] = "Sifariş hazırdır, kuryerlərə bildiriş göndərildi.";
            return RedirectToAction("Index");
        }

        // Canlı kuryer izləmə + çat səhifəsi (həm müştəri, həm təyin olunmuş kuryer üçün)
        public async Task<IActionResult> Track(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Courier)
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

            if (order == null) return NotFound();

            var chatHistory = await _context.ChatMessages
                .Where(c => c.OrderId == id && !c.IsDeleted)
                .OrderBy(c => c.CreatedDate)
                .ToListAsync();

            var currentUserId = User.Identity != null && User.Identity.IsAuthenticated
                ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;

            var isCourier = currentUserId != null && order.Courier != null && order.Courier.CourierId == currentUserId;

            // Müştərinin ad-soyadı çatda göstərilir; kuryer həm də e-poçtu görür
            var customer = await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId);
            ViewBag.CustomerName = customer?.FullName ?? "Qonaq müştəri";
            ViewBag.CustomerEmail = customer?.Email ?? "Qonaq (hesab yoxdur)";

            ViewBag.ChatHistory = chatHistory;
            ViewBag.IsCourier = isCourier;
            return View(order);
        }

        // Kuryer çatdırılmanı tamamlayır
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var order = await _context.Orders
                .Include(o => o.Courier)
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

            if (order == null) return NotFound();
            if (order.Courier == null || order.Courier.CourierId != userId)
                return Forbid();

            order.Status = "Çatdırıldı";

            var courierProfile = order.Courier;
            courierProfile.CurrentBalance += order.TotalAmount * 0.1m; // kuryerin çatdırılma haqqı (10%)

            await _context.SaveChangesAsync();

            await _courierHub.Clients.Group($"order-{id}").SendAsync("OrderDelivered", new { orderId = id });

            TempData["Success"] = "Sifariş çatdırıldı olaraq qeyd olundu!";
            return RedirectToAction("Dashboard", "Courier");
        }
    }
}

