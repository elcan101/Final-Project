using E_Commerce.Data;
using E_Commerce.Models; // Layihənin adından asılı olaraq buranı yoxla
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace E_Commerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .ToList();

            // Ana səhifədə bütün kitabları deyil, ən çox məhsulu olan 3 kateqoriyanı
            // (hər birindən bir neçə kitabla, üfüqi sürüşən sətir şəklində) göstəririk.
            var categoryGroups = products
                .Where(p => p.Category != null)
                .GroupBy(p => p.Category)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => new CategoryShowcase
                {
                    Category = g.Key,
                    Products = g.OrderByDescending(p => p.CreatedDate).Take(10).ToList()
                })
                .ToList();

            ViewBag.CategoryGroups = categoryGroups;

            return View(products);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}