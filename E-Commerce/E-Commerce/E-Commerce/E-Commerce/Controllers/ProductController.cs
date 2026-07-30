using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Yalnız bu uzantılara icazə verilir (təhlükəsizlik: icra oluna bilən fayllar qadağandır)
        private static readonly string[] AllowedPdfExtensions = { ".pdf" };
        private static readonly string[] AllowedAudioExtensions = { ".mp3", ".wav", ".m4a", ".ogg", ".aac" };
        private const long MaxUploadBytes = 100 * 1024 * 1024; // 100 MB

        public ProductController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Yüklənən PDF/audio faylını wwwroot/uploads altına yazır və sayta əlçatan nisbi linki qaytarır.
        // Fayl seçilməyibsə (dəyişiklik yoxdursa) null qaytarır — köhnə fayl linki toxunulmaz qalır.
        private async Task<(string? url, string? error)> SaveUploadedFileAsync(IFormFile? file, string subFolder, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0)
                return (null, null);

            if (file.Length > MaxUploadBytes)
                return (null, $"Fayl həcmi {MaxUploadBytes / (1024 * 1024)} MB-dan çox ola bilməz.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return (null, $"Bu fayl növünə icazə verilmir. İcazəli formatlar: {string.Join(", ", allowedExtensions)}");

            // Fayl sistemi ilə bağlı gözlənilməz xətalar (icazə, disk yeri, yol problemi və s.)
            // düşsə, tətbiqi çökdürüb "ağ ekran" göstərmək əvəzinə, admin panelinə anlaşılan
            // xəta mesajı ilə qayıdırıq — bu, "fayl əlavə edəndə yadda saxlanmır" problemi üçün
            // əsas səbəb idi (unhandled exception → boş/xəta səhifəsi).
            try
            {
                var webRoot = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    // Bəzi hosting mühitlərində wwwroot avtomatik təyin olunmaya bilər
                    webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                }

                var uploadsRoot = Path.Combine(webRoot, "uploads", subFolder);
                Directory.CreateDirectory(uploadsRoot);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return ($"/uploads/{subFolder}/{fileName}", null);
            }
            catch (Exception ex)
            {
                return (null, $"Fayl yadda saxlanarkən xəta baş verdi: {ex.Message}");
            }
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        // 1. Formu göstər — YALNIZ ADMİN yeni kitab (kataloq məhsulu) əlavə edə bilər
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                "Id", "Name");
            return View();
        }

        // 2. Formdan gələn məlumatı qəbul et və bazaya yaz
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? pdfFile, IFormFile? audioFile)
        {
            // Fayl sahələri modeldə deyil, ayrıca parametr kimi gəldiyi üçün onlara görə validasiya xətasını təmizləyirik
            ModelState.Remove(nameof(Product.PdfUrl));
            ModelState.Remove(nameof(Product.AudioUrl));

            if (ModelState.IsValid)
            {
                var (pdfUrl, pdfError) = await SaveUploadedFileAsync(pdfFile, "pdf", AllowedPdfExtensions);
                if (pdfError != null)
                {
                    ModelState.AddModelError(string.Empty, pdfError);
                }

                var (audioUrl, audioError) = await SaveUploadedFileAsync(audioFile, "audio", AllowedAudioExtensions);
                if (audioError != null)
                {
                    ModelState.AddModelError(string.Empty, audioError);
                }

                if (pdfError == null && audioError == null)
                {
                    product.PdfUrl = pdfUrl;
                    product.AudioUrl = audioUrl;
                    product.AddedByUserId = GetUserId();
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Kitab uğurla əlavə olundu!";
                    // Əlavə etdikdən sonra idarəetmə (Kitablarım) siyahısına yönləndiririk
                    return RedirectToAction("Manage");
                }
            }

            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                "Id", "Name", product.CategoryId);
            return View(product);
        }

        // Kitablarım: admin əlavə etdiyi kitabları idarə edir (update / delete)
        [Authorize(Roles = "Admin")]
        public IActionResult Manage()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedDate)
                .ToList();

            return View(products);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id && !p.IsDeleted);
            if (product == null) return NotFound();

            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                "Id", "Name", product.CategoryId);
            return View(product);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? pdfFile, IFormFile? audioFile)
        {
            if (id != product.Id) return NotFound();

            var existing = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (existing == null) return NotFound();

            ModelState.Remove(nameof(Product.AddedByUserId));
            ModelState.Remove(nameof(Product.PdfUrl));
            ModelState.Remove(nameof(Product.AudioUrl));
            if (!ModelState.IsValid)
            {
                // Formda PDF/audio üçün gizli sahə yoxdur, ona görə bağlanan "product"
                // obyektində bu linklər həmişə boş gəlir — səhifə yenidən göstəriləndə
                // mövcud faylın itdiyi təəssüratını yaratmamaq üçün DB-dəki cari linkləri qoruyuruq.
                product.PdfUrl = existing.PdfUrl;
                product.AudioUrl = existing.AudioUrl;
                ViewBag.Categories = new SelectList(
                    _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                    "Id", "Name", product.CategoryId);
                return View(product);
            }

            var (pdfUrl, pdfError) = await SaveUploadedFileAsync(pdfFile, "pdf", AllowedPdfExtensions);
            if (pdfError != null)
            {
                ModelState.AddModelError(string.Empty, pdfError);
            }

            var (audioUrl, audioError) = await SaveUploadedFileAsync(audioFile, "audio", AllowedAudioExtensions);
            if (audioError != null)
            {
                ModelState.AddModelError(string.Empty, audioError);
            }

            if (pdfError != null || audioError != null)
            {
                product.PdfUrl = pdfUrl ?? existing.PdfUrl;
                product.AudioUrl = audioUrl ?? existing.AudioUrl;
                ViewBag.Categories = new SelectList(
                    _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                    "Id", "Name", product.CategoryId);
                return View(product);
            }

            existing.Title = product.Title;
            existing.Author = product.Author;
            existing.Publisher = product.Publisher;
            existing.Language = product.Language;
            existing.PageCount = product.PageCount;
            existing.Description = product.Description;
            existing.Price = product.Price;
            existing.StockCount = product.StockCount;
            existing.IsDigital = product.IsDigital;
            existing.IsAudio = product.IsAudio;
            existing.IsSecondHand = product.IsSecondHand;
            existing.IsHardcover = product.IsHardcover;
            existing.ImageUrl = product.ImageUrl;
            // Yalnız yeni fayl yüklənibsə mövcud PDF/audio linkini əvəz et — əks halda toxunma
            if (pdfUrl != null) existing.PdfUrl = pdfUrl;
            if (audioUrl != null) existing.AudioUrl = audioUrl;
            existing.CategoryId = product.CategoryId;
            existing.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Kitab yeniləndi.";
            return RedirectToAction("Manage");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product != null)
            {
                product.IsDeleted = true;
                product.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Kitab silindi.";
            }
            return RedirectToAction("Manage");
        }

        // ProductController-in içində:
        // Turbo.az tipli filtrasiya: kateqoriya, qiymət aralığı, müəllif, sıralama
        public IActionResult Index(int? categoryId, decimal? minPrice, decimal? maxPrice, string? author, string? q, string? sort)
        {
            var products = FilterProducts(categoryId, minPrice, maxPrice, author, q, sort);

            ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToList();
            ViewBag.Authors = _context.Products.Where(p => !p.IsDeleted && p.Author != null)
                .Select(p => p.Author).Distinct().OrderBy(a => a).ToList();

            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Author = author;
            ViewBag.Q = q;
            ViewBag.Sort = sort;

            return View(products.ToList());
        }

        // AJAX ilə sayfa yenilənmədən nəticələri qaytarır (yalnız kitab şəbəkəsi HTML-i)
        [HttpGet]
        public IActionResult FilterAjax(int? categoryId, decimal? minPrice, decimal? maxPrice, string? author, string? q, string? sort)
        {
            var products = FilterProducts(categoryId, minPrice, maxPrice, author, q, sort);
            return PartialView("_ProductGrid", products.ToList());
        }

        private IQueryable<Product> FilterProducts(int? categoryId, decimal? minPrice, decimal? maxPrice, string? author, string? q, string? sort)
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted);

            if (categoryId.HasValue)
                products = products.Where(p => p.CategoryId == categoryId.Value);
            if (minPrice.HasValue)
                products = products.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                products = products.Where(p => p.Price <= maxPrice.Value);
            if (!string.IsNullOrWhiteSpace(author))
                products = products.Where(p => p.Author == author);
            if (!string.IsNullOrWhiteSpace(q))
                products = products.Where(p => p.Title.Contains(q) || (p.Author != null && p.Author.Contains(q)));

            products = sort switch
            {
                "price_asc" => products.OrderBy(p => p.Price),
                "price_desc" => products.OrderByDescending(p => p.Price),
                "rating" => products.OrderByDescending(p => p.Rating),
                "newest" => products.OrderByDescending(p => p.CreatedDate),
                _ => products.OrderByDescending(p => p.CreatedDate)
            };

            return products;
        }

        // Tək kitab səhifəsi
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
                return NotFound();

            ViewBag.Reviews = await _context.ProductReviews
                .Where(r => r.ProductId == id && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(product);
        }

        // Müştəri kitab səhifəsindən şərh/reytinq yazır
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null) return NotFound();

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] = "Şərh mətni boş ola bilməz.";
                return RedirectToAction("Details", new { id = productId });
            }

            var review = new ProductReview
            {
                ProductId = productId,
                UserId = GetUserId(),
                UserName = User.Identity?.Name ?? "Müştəri",
                Rating = Math.Clamp(rating, 1, 5),
                Comment = comment.Trim()
            };

            _context.ProductReviews.Add(review);
            await _context.SaveChangesAsync();

            // Kitabın ortalama reytinqini yenilə
            var allRatings = await _context.ProductReviews
                .Where(r => r.ProductId == productId && !r.IsDeleted)
                .Select(r => r.Rating)
                .ToListAsync();
            if (allRatings.Any())
            {
                product.Rating = Math.Round(allRatings.Average(), 1);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Şərhiniz əlavə olundu!";
            return RedirectToAction("Details", new { id = productId });
        }
    }
}
