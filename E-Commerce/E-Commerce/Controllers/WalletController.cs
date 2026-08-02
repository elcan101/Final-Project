using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    // Balans yalnız hesaba (mail ilə) giriş etmiş istifadəçiyə aiddir — qonaqlar üçün yoxdur.
    [Authorize]
    public class WalletController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPaymentService _payments;

        public WalletController(AppDbContext context, IPaymentService payments)
        {
            _context = context;
            _payments = payments;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // İstifadəçinin balansı və keşbek məlumatı
        public IActionResult Index()
        {
            var userId = GetUserId();
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);

            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
                _context.SaveChanges();
            }

            // İstifadəçi eyni zamanda kuryerdirsə, çatdırılmadan yığdığı balansı da göstəririk
            var courierProfile = _context.CourierProfiles.FirstOrDefault(c => c.CourierId == userId && !c.IsDeleted);
            ViewBag.CourierBalance = courierProfile?.CurrentBalance ?? 0.00m;

            return View(wallet);
        }

        // Kuryerin çatdırılma pullarından yığdığı balansı əsas saytdakı balansa köçürür —
        // bundan sonra bu pulla abunəlik/digər ödənişlər aparıla bilər.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TransferCourierBalance()
        {
            var userId = GetUserId();
            var courierProfile = _context.CourierProfiles.FirstOrDefault(c => c.CourierId == userId && !c.IsDeleted);

            if (courierProfile == null || courierProfile.CurrentBalance <= 0.00m)
            {
                TempData["Error"] = "Köçürüləcək kuryer balansınız yoxdur.";
                return RedirectToAction("Index");
            }

            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
            }

            var amount = courierProfile.CurrentBalance;
            wallet.Balance += amount;
            courierProfile.CurrentBalance = 0.00m;
            _context.SaveChanges();

            TempData["Success"] = $"{amount:0.00} AZN kuryer balansından saytdakı balansınıza köçürüldü.";
            return RedirectToAction("Index");
        }

        // Kartla balans artırma səhifəsi — yalnız hesaba (mail ilə) giriş etmiş istifadəçi üçün
        [Authorize]
        [HttpGet]
        public IActionResult TopUp()
        {
            return View();
        }

        // Kart məlumatları heç vaxt bazada saxlanılmır — mock ödəniş servisi ilə "tokenləşdirilir"
        // və test rejimində avtomatik təsdiqlənir (bax Services/MockStripePaymentService.cs)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUp(decimal amount, string cardNumber, string expiry, string cvc, string cardHolder)
        {
            const decimal minTopUp = 5.00m;
            if (amount < minTopUp)
            {
                TempData["Error"] = $"Minimum artırma məbləği {minTopUp:0.00} AZN-dir.";
                return RedirectToAction("TopUp");
            }

            if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(expiry) || string.IsNullOrWhiteSpace(cvc))
            {
                TempData["Error"] = "Zəhmət olmasa bütün kart məlumatlarını daxil edin.";
                return RedirectToAction("TopUp");
            }

            if (!CardValidationHelper.TryValidate(cardNumber, expiry, cvc, out var cardError))
            {
                TempData["Error"] = cardError;
                return RedirectToAction("TopUp");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var (token, brand, last4) = await _payments.TokenizeCardAsync(cardNumber, expiry, cvc);
            var result = await _payments.ChargeAsync(userId, amount, "Balans artırılması (kartla)", token);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage ?? "Ödəniş həyata keçirilmədi.";
                return RedirectToAction("TopUp");
            }

            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
            }
            wallet.Balance += amount;
            _context.SaveChanges();

            TempData["Success"] = $"{brand} ****{last4} kartından {amount:0.00} AZN uğurla balansınıza əlavə olundu.";
            return RedirectToAction("Index");
        }

        // Gözləyən keşbeki balansa köçürmə — minimum 5 AZN toplananda aktiv olur
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TransferCashback()
        {
            var userId = GetUserId();
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);

            if (wallet == null || wallet.PendingCashback < Wallet.MinCashbackTransfer)
            {
                TempData["Error"] = $"Balansa köçürmək üçün gözləyən keşbekiniz minimum {Wallet.MinCashbackTransfer:0.00} AZN olmalıdır.";
                return RedirectToAction("Index");
            }

            var amount = wallet.PendingCashback;
            wallet.Balance += amount;
            wallet.PendingCashback = 0.00m;
            _context.SaveChanges();

            TempData["Success"] = $"{amount:0.00} AZN keşbek balansınıza köçürüldü.";
            return RedirectToAction("Index");
        }

        // Balansdan pul çıxarma (məs. C2C bazarında kitab satışından qazanılan pulu geri almaq üçün)
        [Authorize]
        [HttpGet]
        public IActionResult Withdraw()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);
            ViewBag.Balance = wallet?.Balance ?? 0.00m;
            ViewBag.MinWithdraw = 10.00m;
            return View();
        }

        // Kart/IBAN məlumatları saxlanılmır — test rejimində məbləğ dərhal balansdan çıxılır
        // (real inteqrasiyada bank köçürməsi/kart geri ödənişi API-si buraya bağlanmalıdır)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Withdraw(decimal amount, string destinationCard)
        {
            const decimal minWithdraw = 10.00m;
            if (amount < minWithdraw)
            {
                TempData["Error"] = $"Minimum çıxarma məbləği {minWithdraw:0.00} AZN-dir.";
                return RedirectToAction("Withdraw");
            }

            if (string.IsNullOrWhiteSpace(destinationCard))
            {
                TempData["Error"] = "Zəhmət olmasa pulun köçürüləcəyi kart nömrəsini daxil edin.";
                return RedirectToAction("Withdraw");
            }

            var cardDigitsOnly = new string(destinationCard.Where(char.IsDigit).ToArray());
            if (cardDigitsOnly.Length != 16)
            {
                TempData["Error"] = "Kart nömrəsi mütləq 16 rəqəmdən ibarət olmalıdır.";
                return RedirectToAction("Withdraw");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);

            if (wallet == null || wallet.Balance < amount)
            {
                TempData["Error"] = "Balansınız kifayət etmir.";
                return RedirectToAction("Withdraw");
            }

            wallet.Balance -= amount;
            _context.SaveChanges();

            var last4 = destinationCard.Where(char.IsDigit).ToArray() is { Length: >= 4 } digits
                ? new string(digits[^4..])
                : "0000";

            TempData["Success"] = $"{amount:0.00} AZN balansınızdan çıxarıldı, ****{last4} nömrəli karta göndərildi.";
            return RedirectToAction("Index");
        }
    }
}
