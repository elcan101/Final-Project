using E_Commerce.Data;
using E_Commerce.Models; // Layihənin adından asılı olaraq buranı yoxla
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace E_Commerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        // Bura vasitəsilə bazanı kontrollerə "daxil edirik"
        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Bazadakı məhsulları kateqoriya ilə birlikdə çəkirik
            var products = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .ToList();

            // Məhsulları View-ə göndəririk
            return View(products);
        }
    }
}