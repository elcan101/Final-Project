using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Hubs;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    // Sifariş vermək (səbətdən checkout) üçün hesaba (mail ilə) giriş şərtdir.
    [Authorize]
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<CourierTrackingHub> _courierHub;
        private readonly DeliveryPricingService _deliveryPricing;

        public OrderController(AppDbContext context, IHubContext<CourierTrackingHub> courierHub, DeliveryPricingService deliveryPricing)
        {
            _context = context;
            _courierHub = courierHub;
            _deliveryPricing = deliveryPricing;
        }

        // İstifadəçi məcburi giriş etmiş olduğu üçün həmişə əsl hesab ID-si qaytarılır
        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

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

            var depot = _deliveryPricing.Depot;
            ViewBag.DepotLat = depot.Latitude;
            ViewBag.DepotLng = depot.Longitude;
            ViewBag.BaseFee = depot.BaseFee;
            ViewBag.PerKmFee = depot.PerKmFee;
            ViewBag.MinFee = depot.MinFee;
            ViewBag.MaxFee = depot.MaxFee;

            return View();
        }

        // Səbətdəki kitablardan sifariş yaradır, kuponu tətbiq edir, keşbek balansa yazılır
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Checkout(double? lat, double? lng, string? addressText, string? phoneNumber)
        {
            // Çatdırılma nöqtəsi seçilməyibsə, geri "xəritədən seç" səhifəsinə qaytarırıq
            if (lat == null || lng == null)
            {
                TempData["Error"] = "Zəhmət olmasa çatdırılma ünvanını xəritədən seçin.";
                return RedirectToAction("ChooseLocation");
            }

            // Kuryerin müştəri ilə əlaqə saxlaya bilməsi üçün əlaqə nömrəsi mütləqdir
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "Zəhmət olmasa əlaqə nömrənizi daxil edin.";
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

                var productTotal = Math.Max(0, subtotal - discount);

                // Depodan çatdırılma ünvanına məsafəyə görə çatdırılma haqqı — bu məbləğ
                // müştəridən tutulur; çatdırıldıqda 70%-i kuryerin balansına köçürülür
                var deliveryFee = _deliveryPricing.CalculateDeliveryFee(lat, lng, out var distanceKm);

                // Müştəridən tutulacaq yekun məbləğ: məhsullar + çatdırılma haqqı
                var total = productTotal + deliveryFee;

                // Ödəniş yalnız saytdakı balansdan tutulur — kartdan birbaşa tutulmur.
                // Balans kifayət etmirsə, istifadəçi əvvəlcə "Kartla balansı artır" səhifəsindən
                // balansını artırmalıdır.
                var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
                if (wallet == null)
                {
                    wallet = new Wallet { UserId = userId };
                    _context.Wallets.Add(wallet);
                }

                if (wallet.Balance < total)
                {
                    TempData["Error"] = $"Balansınız kifayət etmir (çatışmır: {(total - wallet.Balance):0.00} AZN). Zəhmət olmasa əvvəlcə balansınızı artırın.";
                    return RedirectToAction("Index", "Cart");
                }

                wallet.Balance -= total;

                var cashback = Math.Round(productTotal * 0.02m, 2); // Loyallıq: məhsul məbləğinin 2%-i keşbek (çatdırılma haqqı hesaba qatılmır)

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
                    DeliveryAddressText = string.IsNullOrWhiteSpace(addressText) ? null : addressText.Trim(),
                    DeliveryFee = deliveryFee,
                    DeliveryDistanceKm = distanceKm,
                    PhoneNumber = phoneNumber.Trim()
                };

                _context.Orders.Add(order);
                _context.CartItems.RemoveRange(validItems);

                // Sifariş verilən hər kitabın stok sayı azaldılır (mənfiyə düşməsin deyə 0-da saxlanılır)
                foreach (var item in validItems)
                {
                    item.Product.StockCount = Math.Max(0, item.Product.StockCount - item.Quantity);
                }

                // Loyallıq Sistemi: hər alış-verişdən sonra qazanılan keşbek "gözləyən" kimi yazılır —
                // balansa avtomatik keçmir, minimum 5 AZN-ə çatanda istifadəçi özü "Balansa köçür" ilə köçürür.
                wallet.PendingCashback += cashback;
                wallet.TotalCashbackEarned += cashback;

                _context.SaveChanges();

                HttpContext.Session.Remove("AppliedCouponCode");
                HttpContext.Session.Remove("AppliedCouponDiscount");

                TempData["Success"] = $"Sifarişiniz qəbul olundu! {total:0.00} AZN balansınızdan tutuldu (kitablar: {productTotal:0.00} AZN + çatdırılma: {deliveryFee:0.00} AZN), {cashback:0.00} AZN keşbek qazandınız (gözləyən keşbekə əlavə olundu).";
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

        // Depo sifarişi hazır elan edir → bütün boşda kuryerlərə SignalR siqnalı gedir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReady(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = "Hazırdır";
            await _context.SaveChangesAsync();

            // Kuryerlər sifarişi qəbul etmədən əvvəl çatdırılma nöqtəsini və götürüləcək
            // depo məlumatını görsün ki, özlərinə uyğundursa qəbul etsinlər
            await _courierHub.Clients.Group("idle-couriers").SendAsync("NewOrderAvailable", new
            {
                orderId = order.Id,
                total = order.TotalAmount,
                deliveryLat = order.DeliveryLatitude,
                deliveryLng = order.DeliveryLongitude,
                deliveryAddressText = order.DeliveryAddressText,
                depotName = _deliveryPricing.Depot.Name,
                distanceKm = order.DeliveryDistanceKm,
                deliveryFee = order.DeliveryFee,
                courierEarning = order.CourierEarning,
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
            ViewBag.DepotName = _deliveryPricing.Depot.Name;
            ViewBag.DepotLat = _deliveryPricing.Depot.Latitude;
            ViewBag.DepotLng = _deliveryPricing.Depot.Longitude;
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
            // Kuryerin çatdırılma haqqı — depodan ünvana məsafəyə görə əvvəlcədən hesablanıb,
            // kuryer çatdırılma haqqının 70%-ini alır (qalan 30% platforma xidmət haqqıdır)
            courierProfile.CurrentBalance += order.CourierEarning;

            _context.Notifications.Add(new Notification
            {
                UserId = order.UserId,
                Title = "Sifarişiniz çatdırıldı",
                Message = $"#{order.Id} nömrəli sifarişiniz uğurla çatdırıldı. Alış-verişiniz üçün təşəkkür edirik!",
                Url = $"/Order/Track/{order.Id}"
            });

            await _context.SaveChangesAsync();

            await _courierHub.Clients.Group($"order-{id}").SendAsync("OrderDelivered", new { orderId = id });

            TempData["Success"] = "Sifariş çatdırıldı olaraq qeyd olundu!";
            return RedirectToAction("Dashboard", "Courier");
        }
    }
}

