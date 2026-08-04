using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    
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

           
            var firstDayFee = listing.DailyListingFee;
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == listing.SellerId && !w.IsDeleted);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = listing.SellerId };
                _context.Wallets.Add(wallet);
            }
            wallet.Balance -= firstDayFee;
            listing.AccruedFees += firstDayFee;

            _context.SaveChanges();

            TempData["Success"] = $"Elanınız yerləşdirildi! İlk günün elan haqqı olan {firstDayFee:0.00} AZN balansınızdan tutuldu. Elan aktiv qaldığı hər növbəti gün üçün də balansınızdan {listing.DailyListingFee:0.00} AZN elan haqqı avtomatik tutulacaq. İstədiyiniz vaxt \"Mənim elanlarım\" bölməsindən elanı saytdan çıxara bilərsiniz.";
            return RedirectToAction("MyListings");
        }

       
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var listing = await _context.Listings.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
            if (listing == null) return NotFound();
            if (listing.SellerId != GetUserId()) return Forbid();

            ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).ToList();
            return View(listing);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Listing listing, IFormFile? bookFile)
        {
            var existing = _context.Listings.FirstOrDefault(l => l.Id == id && !l.IsDeleted);
            if (existing == null) return NotFound();
            if (existing.SellerId != GetUserId()) return Forbid();

            ModelState.Remove(nameof(Listing.SellerId));
            ModelState.Remove(nameof(Listing.ImageUrl));
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).ToList();
                listing.Id = id;
                return View(listing);
            }

            existing.Title = listing.Title;
            existing.Author = listing.Author;
            existing.Description = listing.Description;
            existing.Price = listing.Price;
            existing.CategoryId = listing.CategoryId;
            existing.ContactPhone = listing.ContactPhone;
            existing.IsHardcover = listing.IsHardcover;
            existing.UpdatedDate = DateTime.Now;

            var newImage = SaveBookFile(bookFile);
            if (newImage != null)
                existing.ImageUrl = newImage;

            _context.SaveChanges();

            TempData["Success"] = "Elan uğurla yeniləndi.";
            return RedirectToAction("MyListings");
        }

       
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
