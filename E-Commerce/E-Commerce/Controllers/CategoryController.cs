using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class CategoryController : Controller
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        // Bütün kateqoriyaları göstər
        public IActionResult Index()
        {
            var categories = _context.Categories
                .Include(c => c.Products)
                .Where(c => !c.IsDeleted)
                .ToList();

            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(category);
        }

        // Kateqoriyaya aid kitabları göstər (kateqoriya üzərinə kliklədikdə)
        public IActionResult Products(int id)
        {
            var category = _context.Categories
                .Include(c => c.Products)
                .FirstOrDefault(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            ViewData["CategoryName"] = category.Name;
            var products = category.Products.Where(p => !p.IsDeleted).ToList();

            return View("~/Views/Product/Index.cshtml", products);
        }
    }
}
