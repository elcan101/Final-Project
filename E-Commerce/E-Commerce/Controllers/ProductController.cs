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
        private const long MaxUploadBytes = 500L * 1024 * 1024; // 500 MB (bax Program.cs — Kestrel limiti ilə eynidir)

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

        // Wwwroot altındakı köhnə PDF/audio faylını fiziki olaraq silir (fayl artıq yoxdursa səssizcə keçir).
        private void DeleteUploadedFile(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)) return;

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

            var relative = relativeUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(webRoot, relative);

            try
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch
            {
                // Fayl silinərkən xəta olsa belə (icazə, kilidlənmə və s.) əməliyyatı dayandırmırıq —
                // DB-dəki link hər halda təmizlənəcək.
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? pdfFile, IFormFile? audioFile, bool removePdf = false, bool removeAudio = false)
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
            // Yalnız yeni fayl yüklənibsə mövcud PDF/audio linkini əvəz et — əks halda toxunma.
            // "Sil" düyməsi ilə silinmə tələb olunubsa (removePdf/removeAudio) və yeni fayl
            // yüklənməyibsə, köhnə fayl diskdən silinir və link boşaldılır.
            if (pdfUrl != null)
            {
                DeleteUploadedFile(existing.PdfUrl);
                existing.PdfUrl = pdfUrl;
            }
            else if (removePdf)
            {
                DeleteUploadedFile(existing.PdfUrl);
                existing.PdfUrl = null;
            }

            if (audioUrl != null)
            {
                DeleteUploadedFile(existing.AudioUrl);
                existing.AudioUrl = audioUrl;
            }
            else if (removeAudio)
            {
                DeleteUploadedFile(existing.AudioUrl);
                existing.AudioUrl = null;
            }
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

            // E-kitab (PDF) və səsli kitab girişi yalnız aktiv abunəliyə görə verilir:
            // Standard planı → yalnız e-kitab, Premium planı → e-kitab + səsli kitab.
            // Giriş etməmiş və ya abunəliyi olmayan istifadəçiyə məzmun göstərilmir —
            // əvəzinə abunəlik səhifəsinə yönləndirən dəvət göstərilir.
            bool hasEbookAccess = false;
            bool hasAudioAccess = false;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = GetUserId();
                var activeSub = await _context.UserSubscriptions
                    .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive && s.ExpiryDate > DateTime.Now)
                    .OrderByDescending(s => s.ExpiryDate)
                    .FirstOrDefaultAsync();

                if (activeSub != null)
                {
                    hasEbookAccess = true; // Standard və Premium — hər ikisi e-kitaba icazə verir
                    hasAudioAccess = activeSub.PlanType == SubscriptionPlanType.Premium;
                }
            }

            ViewBag.HasEbookAccess = hasEbookAccess;
            ViewBag.HasAudioAccess = hasAudioAccess;

            return View(product);
        }

        // PDF-i saytda ("inline") göstərmək üçün ayrıca endpoint.
        // Əvvəllər səhifə birbaşa /uploads/pdf/xxx.pdf linkinə işarə edirdi — brauzer
        // uzantıları/yükləmə menecerləri (məs. IDM) bunu "yüklənəcək fayl" kimi tanıyıb
        // "Aynı indirme bağlantısı" pəncərəsi açırdı və istifadəçini saytdan kənara aparırdı.
        // Bu problem yalnız URL-in .pdf uzantısını gizlətməklə HƏLL OLUNMADI, çünki IDM
        // cavabı Content-Type başlığına görə də tuturdu (aşağıdakı qeydə bax). Ona görə bu
        // endpoint faylı süni ("camuflaj") bir Content-Type ilə qaytarır, əsl PDF tipini isə
        // yalnız brauzerdəki JavaScript (Details.cshtml) təyin edir.
        // Giriş icazəsi (abunəlik) bu endpointdə də serverdə təkrar yoxlanılır.
        [HttpGet]
        public async Task<IActionResult> ReadPdf(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product == null || string.IsNullOrWhiteSpace(product.PdfUrl))
                return NotFound();

            bool hasAccess = false;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = GetUserId();
                hasAccess = await _context.UserSubscriptions
                    .AnyAsync(s => s.UserId == userId && !s.IsDeleted && s.IsActive && s.ExpiryDate > DateTime.Now);
            }

            if (!hasAccess)
                return Forbid();

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

            var relative = product.PdfUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(webRoot, relative);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            // Bu dinamik cavab qlobal "no-cache" middleware-dən keçir (bax Program.cs), amma
            // ehtiyat üçün burada da açıq şəkildə keş qadağan olunur ki, brauzer köhnə/xarab
            // bir cavabı (məs. əvvəlki uğursuz Range cavabını) heç vaxt keşdən göstərməsin.
            //
            // MÜHÜM: IDM (Internet Download Manager) kimi yükləmə menecerləri brauzer
            // uzantısı vasitəsilə BÜTÜN şəbəkə cavablarını (səhifə keçidi, fetch(), XHR —
            // fərq etmir) "Content-Type" başlığına görə skan edir. Cavab "application/pdf"
            // olaraq gələndə, hətta JavaScript fetch() ilə göndərilsə belə, IDM onu tutub
            // "Aynı indirme bağlantısı" pəncərəsini açır və faylı öz nəzarətinə keçirir —
            // nəticədə səhifədəki fetch() natamam/boş cavab alır və PDF "yüklənə bilmədi"
            // xətası göstərir. Bunun qarşısını almaq üçün server cavabı IDM-in tanıdığı
            // sənəd MIME tiplərindən (application/pdf, application/octet-stream və s.) BİR-İ
            // OLMAYAN, süni bir Content-Type ilə göndərir. Brauzerdəki JavaScript real PDF
            // baytlarını aldıqdan sonra Blob-u "application/pdf" tipi ilə YENİDƏN qurur (bax
            // Details.cshtml) — bu, faylın PDF kimi düzgün göstərilməsini təmin edir, çünki
            // Blob-un tipi tamamilə brauzerdə, JS tərəfindən müəyyən olunur, server başlığından
            // asılı deyil.
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            const string CamouflagedContentType = "application/x-ekitab-stream";

            // QEYD: "Range" dəstəyi burada ARTIQ LAZIM DEYİL və bilərəkdən söndürülüb.
            // Əvvəllər iframe birbaşa bu URL-ə "src" kimi bağlanırdı və brauzerin daxili PDF
            // görüntüləyicisi faylı "Range" sorğuları ilə açırdı. İndi isə fayl JavaScript
            // fetch() ilə TAM (200) cavab kimi endirilib Blob-a çevrilir (bax Details.cshtml),
            // ona görə server tərəfində Range emalına ehtiyac qalmayıb.
            //
            // MÜHÜM (2-ci tur): Content-Type-ı kamuflyaj etmək kifayət ETMƏDİ — IDM əlavəsi
            // fetch() cavabını hələ də "tuturdu" (dialoq artıq açılmır, amma səhifədəki
            // fetch() "Failed to fetch" ilə uğursuz olurdu, çünki IDM bağlantını öz üzərinə
            // götürüb səhifəyə tam cavab çatdırmırdı). Bunun səbəbi: IDM qərarını təkcə
            // Content-Type-a görə deyil, həm də cavabın MƏLUM ÖLÇÜSÜNƏ (Content-Length) görə
            // verir. FileStreamResult stream.Length məlum olduğu üçün Content-Length başlığını
            // avtomatik göndərirdi. Buna görə faylı FileStreamResult ƏVƏZİNƏ, Content-Length
            // başlığı HEÇ VAXT göndərilməyəcək şəkildə (HTTP "chunked" ötürmə ilə) əl ilə axıdırıq —
            // IDM-in "bu böyük fayldır, tut" qərarı üçün lazım olan siqnal artıq mövcud deyil.
            Response.ContentType = CamouflagedContentType;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true))
            {
                await fileStream.CopyToAsync(Response.Body);
            }
            return new EmptyResult();
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
