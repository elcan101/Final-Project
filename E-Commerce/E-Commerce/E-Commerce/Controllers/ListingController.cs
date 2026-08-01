using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    // C2C Marketplace: "Elan Ver → Alıcı Tap → Əlaqə Saxla"
    // Qeyd: bura tap.az prinsipi ilə işləyir — sayt heç bir ödənişə vasitəçilik etmir.
    // Alıcı "Nömrəni göstər" düyməsini basanda yalnız tərəflərin əlaqə məlumatları
    // bir-birinə ötürülür, alış-veriş özləri təyin etdikləri yerdə həyata keçirilir.
    // Satıcı elanı istədiyi vaxt saytdan çıxara bilər; elan aktiv qaldığı hər gün üçün
    // satıcının balansından gündəlik elan haqqı (DailyListingFee) avtomatik tutulur
    // (bax: Services/DailyBillingService.cs).
    public class ListingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<AppUser> _userManager;

        public ListingController(AppDbContext context, IWebHostEnvironment env, UserManager<AppUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        // Şəkil linki əvəzinə istifadəçinin yüklədiyi faylı (şəkil və ya PDF) diskə yazır
        // və saxlanılan faylın nisbi yolunu qaytarır
        private string? SaveBookFile(IFormFile? bookFile)
        {
            if (bookFile == null || bookFile.Length == 0) return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
            var ext = Path.GetExtension(bookFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) return null;

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "listings");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                bookFile.CopyTo(stream);
            }

            return $"/uploads/listings/{fileName}";
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        // Alıcı Tap: aktiv elanların açıq bazarı
        public IActionResult Index(string? q, decimal? minPrice, decimal? maxPrice)
        {
            var listings = _context.Listings
                .Include(l => l.Category)
                .Where(l => !l.IsDeleted && l.Status == ListingStatus.Active);

            if (!string.IsNullOrWhiteSpace(q))
                listings = listings.Where(l => l.Title.Contains(q) || (l.Author != null && l.Author.Contains(q)));
            if (minPrice.HasValue)
                listings = listings.Where(l => l.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                listings = listings.Where(l => l.Price <= maxPrice.Value);

            return View(listings.OrderByDescending(l => l.CreatedDate).ToList());
        }

        // Elana klikləyəndə açılan ətraflı səhifə: satıcının yazdığı təsvir/xüsusiyyətlər
        // və əlaqə nömrəsi bu səhifədə tam görünür.
        public async Task<IActionResult> Details(int id)
        {
            var listing = await _context.Listings
                .Include(l => l.Category)
                .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (listing == null) return NotFound();

            var seller = await _userManager.FindByIdAsync(listing.SellerId);
            ViewBag.SellerName = seller?.FullName;
            ViewBag.SellerEmail = seller?.Email;

            var currentUserId = User.Identity != null && User.Identity.IsAuthenticated ? GetUserId() : null;
            ViewBag.IsOwner = currentUserId != null && currentUserId == listing.SellerId;

            if (TempData["RevealedPhone"] != null)
                ViewBag.RevealedPhone = TempData["RevealedPhone"];
            if (TempData["RevealedEmail"] != null)
                ViewBag.RevealedEmail = TempData["RevealedEmail"];

            return View(listing);
        }

        [Authorize]
        public IActionResult MyListings()
        {
            var userId = GetUserId();
            var mine = _context.Listings
                .Where(l => l.SellerId == userId && !l.IsDeleted)
                .OrderByDescending(l => l.CreatedDate)
                .ToList();
            return View(mine);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).ToList();

            var user = await _userManager.GetUserAsync(User);
            return View(new Listing { ContactPhone = user?.PhoneNumber });
        }

        // Elan Ver
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Listing listing, IFormFile? bookFile)
        {
            listing.SellerId = GetUserId();
            listing.Status = ListingStatus.Active;
            listing.LastFeeChargedDate = DateTime.Now.Date;
            listing.ImageUrl = SaveBookFile(bookFile);

            ModelState.Remove(nameof(Listing.SellerId));
            ModelState.Remove(nameof(Listing.ImageUrl));
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).ToList();
                return View(listing);
            }

            _context.Listings.Add(listing);
            _context.SaveChanges();

            TempData["Success"] = "Elanınız yerləşdirildi! Elan aktiv qaldığı hər gün üçün balansınızdan 0.10 AZN elan haqqı tutulacaq. İstədiyiniz vaxt \"Mənim elanlarım\" bölməsindən elanı saytdan çıxara bilərsiniz.";
            return RedirectToAction("MyListings");
        }

        // Əlaqə Saxla: sayt heç bir ödənişə vasitəçilik etmir — alıcıya satıcının əlaqə
        // nömrəsi/e-poçtu göstərilir, satıcıya isə yalnız "maraqlanan var" bildirişi gedir
        // (müştərinin əlaqə nömrəsi satıcıya ötürülmür — istəsə, müştəri özü satıcının
        // nömrəsinə zəng edib danışa bilər).
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(int id)
        {
            var buyerId = GetUserId();
            var listing = await _context.Listings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (listing == null || listing.Status != ListingStatus.Active)
            {
                TempData["Error"] = "Bu elan artıq saytdan çıxarılıb və ya deaktivdir.";
                return RedirectToAction("Index");
            }
            if (listing.SellerId == buyerId)
            {
                TempData["Error"] = "Öz elanınızla əlaqə saxlaya bilməzsiniz.";
                return RedirectToAction("Details", new { id });
            }

            var buyer = await _userManager.FindByIdAsync(buyerId);
            var seller = await _userManager.FindByIdAsync(listing.SellerId);

            // Satıcıya bildiriş: yalnız kiminsə maraqlandığı bildirilir — müştərinin əlaqə
            // nömrəsi ötürülmür, satıcı istəsə elanın səhifəsindən öz nömrəsinə zəng gözləyə bilər.
            _context.Notifications.Add(new Notification
            {
                UserId = listing.SellerId,
                Title = "Elanınızla maraqlanan var",
                Message = $"\"{listing.Title}\" elanınızla {(buyer?.FullName ?? "bir istifadəçi")} maraqlanır. İstəyərsə, müştəri sizin əlaqə nömrənizə özü zəng edəcək.",
                Url = Url.Action("Details", "Listing", new { id = listing.Id })
            });
            await _context.SaveChangesAsync();

            var sellerContact = !string.IsNullOrWhiteSpace(listing.ContactPhone) ? listing.ContactPhone : seller?.PhoneNumber;
            TempData["RevealedPhone"] = string.IsNullOrWhiteSpace(sellerContact)
                ? "Satıcı əlaqə nömrəsi qeyd etməyib."
                : sellerContact;
            TempData["RevealedEmail"] = seller?.Email;
            TempData["Success"] = "Satıcının əlaqə məlumatları göstərildi və satıcıya bildiriş göndərildi. Alış-verişi satıcı ilə əlaqə quraraq həyata keçirə bilərsiniz — ödəniş burada həyata keçirilmir.";

            return RedirectToAction("Details", new { id });
        }

        // Satıcı elanı istədiyi vaxt saytdan çıxara bilər — bundan sonra gündəlik
        // elan haqqı da artıq tutulmur (bax: DailyBillingService, yalnız Active elanlardan tutur).
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var userId = GetUserId();
            var listing = await _context.Listings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (listing == null || listing.SellerId != userId)
            {
                TempData["Error"] = "Bu elanı idarə etmək icazəniz yoxdur.";
                return RedirectToAction("MyListings");
            }

            if (listing.Status == ListingStatus.Active)
            {
                listing.Status = ListingStatus.Deactivated;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Elan saytdan çıxarıldı. Bundan sonra bu elana görə gündəlik haqq tutulmayacaq.";
            }

            return RedirectToAction("MyListings");
        }
    }
}
