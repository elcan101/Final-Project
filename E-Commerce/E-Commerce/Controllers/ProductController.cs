using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;

        public ProductController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Formu göstər
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                "Id", "Name");
            return View();
        }

        // 2. Formdan gələn məlumatı qəbul et və bazaya yaz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            // Validasiyalar düzdürsə
            if (ModelState.IsValid)
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Məlumat düşdükdən sonra kitablar siyahısına qayıt
                return RedirectToAction("Index");
            }

            // Xəta varsa, dropdown-u yenidən dolduraraq formanı göstər
            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name),
                "Id", "Name", product.CategoryId);
            return View(product);
        }
        // ProductController-in içində:
        public IActionResult Index()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .ToList();
            return View(products);
        }
    }
}