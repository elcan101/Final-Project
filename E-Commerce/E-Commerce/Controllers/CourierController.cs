using Microsoft.AspNetCore.Mvc;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class CourierController : Controller
    {
        private readonly AppDbContext _context;

        public CourierController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var couriers = _context.CourierProfiles
                .Where(c => !c.IsDeleted)
                .ToList();

            return View(couriers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CourierProfile courier)
        {
            if (ModelState.IsValid)
            {
                _context.CourierProfiles.Add(courier);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(courier);
        }
    }
}
