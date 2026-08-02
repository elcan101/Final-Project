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

        // Poçtla çatdırılma sifarişləri üçün izləmə kodu yaradır (məs: AZ483920175BK)
        private static string GenerateTrackingCode()
        {
            return $"AZ{Random.Shared.Next(100000000, 999999999)}BK";
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
        public IActionResult Checkout(double? lat, double? lng, string? addressText, string? phoneNumber, string? district, string? postalCode)
        {
            // Rayonlara poçtla çatdırılma: müştəri xəritədən nöqtə seçmək əvəzinə rayon və
            // poçt indeksi daxil edibsə, kuryer təyinatı olmadan birbaşa poçtla göndərilir
            var isPostDelivery = lat == null && lng == null
                && !string.IsNullOrWhiteSpace(district) && !string.IsNullOrWhiteSpace(postalCode);

            // Çatdırılma nöqtəsi seçilməyibsə və rayon/poçt indeksi də daxil edilməyibsə,
            // geri "xəritədən seç" səhifəsinə qaytarırıq
            if (!isPostDelivery && (lat == null || lng == null))
            {
                TempData["Error"] = "Zəhmət olmasa çatdırılma ünvanını xəritədən seçin, ya da rayon və poçt indeksini daxil edin.";
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
                // müştəridən tutulur; çatdırıldıqda 70%-i kuryerin balansına köçürülür.
                // Poçtla çatdırılmada koordinat olmadığı üçün sabit baza haqqı tətbiq olunur.
                double distanceKm;
                var deliveryFee = isPostDelivery
                    ? _deliveryPricing.CalculateDeliveryFee(null, null, out distanceKm)
                    : _deliveryPricing.CalculateDeliveryFee(lat, lng, out distanceKm);

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
                    // Poçtla çatdırılma sifarişi kuryer mərhələlərindən (Hazırlanır → Hazırdır →
                    // Kuryerdədir) keçmir, dərhal "Çatdırıldı" elan olunur.
                    Status = isPostDelivery ? "Çatdırıldı" : "Hazırlanır",
                    DeliveryLatitude = lat,
                    DeliveryLongitude = lng,
                    DeliveryAddressText = string.IsNullOrWhiteSpace(addressText) ? null : addressText.Trim(),
                    DeliveryFee = deliveryFee,
                    DeliveryDistanceKm = distanceKm,
                    PhoneNumber = phoneNumber.Trim(),
                    IsPostDelivery = isPostDelivery,
                    District = isPostDelivery ? district!.Trim() : null,
                    PostalCode = isPostDelivery ? postalCode!.Trim() : null,
                    TrackingCode = isPostDelivery ? GenerateTrackingCode() : null
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

                // Poçtla çatdırılma sifarişinə kuryer bildirişi getmir — bunun əvəzinə
                // müştəriyə birbaşa poçt izləmə kodu ilə bildiriş göndərilir.
                if (isPostDelivery)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = order.UserId,
                        Title = "Sifarişiniz poçtla göndərildi",
                        Message = $"#{order.Id} nömrəli sifarişiniz \"{district}\" rayonuna, {postalCode} poçt indeksinə poçtla göndərildi və çatdırıldı. İzləmə kodu: {order.TrackingCode}",
                        Url = $"/Order/Track/{order.Id}"
                    });
                    _context.SaveChanges();
                }

                HttpContext.Session.Remove("AppliedCouponCode");
                HttpContext.Session.Remove("AppliedCouponDiscount");

                TempData["Success"] = isPostDelivery
                    ? $"Sifarişiniz qəbul olundu və poçtla göndərildi! {total:0.00} AZN balansınızdan tutuldu (kitablar: {productTotal:0.00} AZN + çatdırılma: {deliveryFee:0.00} AZN), {cashback:0.00} AZN keşbek qazandınız. İzləmə kodu: {order.TrackingCode}"
                    : $"Sifarişiniz qəbul olundu! {total:0.00} AZN balansınızdan tutuldu (kitablar: {productTotal:0.00} AZN + çatdırılma: {deliveryFee:0.00} AZN), {cashback:0.00} AZN keşbek qazandınız (gözləyən keşbekə əlavə olundu).";
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

        // Sifarişi ləğv edir. Yalnız kuryer sifarişi HƏLƏ GÖTÜRMƏYİBSƏ (CourierProfileId boşdur)
        // ləğv etməyə icazə verilir — yəni "Hazırlanır" və ya "Hazırdır" mərhələsindədirsə.
        // Kuryer sifarişi qəbul edib götürdükdən sonra (Status = "Kuryerdədir") artıq ləğv
        // oluna bilməz, çünki kitab artıq depodan çıxıb. Poçtla göndərilən sifarişlər də
        // (dərhal "Çatdırıldı" olduğu üçün) ləğv edilə bilməz.
        // Ləğv olunanda: ödənilən məbləğ (TotalAmount) istifadəçinin balansına geri qaytarılır,
        // sifarişdən qazanılmış (hələ balansa keçməmiş) keşbek geri alınır.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetUserId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId && !o.IsDeleted);

            if (order == null) return NotFound();

            if (order.IsPostDelivery || order.CourierProfileId != null
                || order.Status == "Çatdırıldı" || order.Status == "Ləğv edildi")
            {
                TempData["Error"] = "Bu sifarişi artıq ləğv etmək mümkün deyil — kuryer sifarişi götürüb və ya sifariş artıq çatdırılıb.";
                return RedirectToAction("Index");
            }

            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet != null)
            {
                // Ödənilən məbləğ geri qaytarılır
                wallet.Balance += order.TotalAmount;

                // Bu sifarişdən qazanılan (hələ balansa keçirilməmiş) keşbek geri alınır
                if (order.CashbackAmount > 0)
                {
                    wallet.PendingCashback = Math.Max(0, wallet.PendingCashback - order.CashbackAmount);
                    wallet.TotalCashbackEarned = Math.Max(0, wallet.TotalCashbackEarned - order.CashbackAmount);
                }
            }

            order.Status = "Ləğv edildi";

            _context.Notifications.Add(new Notification
            {
                UserId = order.UserId,
                Title = "Sifariş ləğv edildi",
                Message = $"#{order.Id} nömrəli sifariş ləğv edildi və {order.TotalAmount:0.00} AZN balansınıza geri qaytarıldı.",
                Url = $"/Order/Index"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Sifariş ləğv edildi, {order.TotalAmount:0.00} AZN balansınıza geri qaytarıldı.";
            return RedirectToAction("Index");
        }

        // Sifariş hazırlanıb ("Hazırdır" və sonrakı mərhələlər) elan olunduqdan sonra
        // müştəriyə "Okean Kitabevi" başlıqlı çap oluna bilən qəbz göstərir.
        public async Task<IActionResult> Receipt(int id)
        {
            var userId = GetUserId();
            var order = await _context.Orders
                .Include(o => o.Courier)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId && !o.IsDeleted);

            if (order == null) return NotFound();

            // "Hazırlanır" mərhələsində qəbz hələ yoxdur — sifariş hazır elan olunmayıb
            if (order.Status == "Hazırlanır")
            {
                TempData["Error"] = "Bu sifariş hələ hazırlanır, qəbz yalnız sifariş hazır elan olunduqdan sonra mövcud olur.";
                return RedirectToAction("Index");
            }

            return View(order);
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

