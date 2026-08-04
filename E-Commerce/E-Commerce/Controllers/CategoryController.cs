using Microsoft.AspNetCore.Authorization;
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

        public IActionResult Index()
        {
            var categories = _context.Categories
                .Include(c => c.Products)
                .Where(c => !c.IsDeleted)
                .ToList();

            return View(categories);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
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
            ViewData["Title"] = category.Name;
            var products = category.Products.Where(p => !p.IsDeleted).ToList();

            ViewBag.Categories = _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToList();
            ViewBag.Authors = _context.Products.Where(p => !p.IsDeleted && p.Author != null)
                .Select(p => p.Author).Distinct().OrderBy(a => a).ToList();
            ViewBag.CategoryId = id;

            return View("~/Views/Product/Index.cshtml", products);
        }
    }
}
