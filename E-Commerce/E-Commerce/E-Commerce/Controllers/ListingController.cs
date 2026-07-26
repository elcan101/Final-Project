using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    // C2C Marketplace: "Elan Ver → Alıcı Tap → Qazanc Əldə Et"
    public class ListingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ListingController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
        public IActionResult Create()
        {
            ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).ToList();
            return View();
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

            TempData["Success"] = "Elanınız yerləşdirildi! Gündəlik 0.10 AZN elan haqqı balansınızdan tutulacaq.";
            return RedirectToAction("MyListings");
        }

        // Qazanc Əldə Et: alıcı elanı alır, satıcının balansına komissiya çıxılaraq köçürülür
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(int id)
        {
            var buyerId = GetUserId();
            var listing = await _context.Listings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (listing == null || listing.Status != ListingStatus.Active)
            {
                TempData["Error"] = "Bu elan artıq satılıb və ya deaktivdir.";
                return RedirectToAction("Index");
            }
            if (listing.SellerId == buyerId)
            {
                TempData["Error"] = "Öz elanınızı ala bilməzsiniz.";
                return RedirectToAction("Index");
            }

            var buyerWallet = _context.Wallets.FirstOrDefault(w => w.UserId == buyerId && !w.IsDeleted)
                ?? NewWallet(buyerId);

            if (buyerWallet.Balance < listing.Price)
            {
                TempData["Error"] = "Balansınız kifayət etmir. Əvvəlcə balansınızı artırın.";
                return RedirectToAction("Index");
            }

            var sellerWallet = _context.Wallets.FirstOrDefault(w => w.UserId == listing.SellerId && !w.IsDeleted)
                ?? NewWallet(listing.SellerId);

            var commission = Math.Round(listing.Price * listing.PlatformCommissionRate, 2);
            var sellerEarning = listing.Price - commission;

            buyerWallet.Balance -= listing.Price;
            sellerWallet.Balance += sellerEarning; // İkinci əl satış qazancının idarə olunması

            listing.Status = ListingStatus.Sold;
            listing.BuyerId = buyerId;
            listing.SoldDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Təbriklər, kitabı əldə etdiniz!";
            return RedirectToAction("Index");
        }

        private Wallet NewWallet(string userId)
        {
            var w = new Wallet { UserId = userId };
            _context.Wallets.Add(w);
            return w;
        }
    }
}
