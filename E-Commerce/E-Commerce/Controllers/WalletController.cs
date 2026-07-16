using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Controllers
{
    public class WalletController : Controller
    {
        private readonly AppDbContext _context;

        public WalletController(AppDbContext context)
        {
            _context = context;
        }

        private string GetUserId()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(authUserId))
                {
                    return authUserId;
                }
            }

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("UserId", userId);
            }
            return userId;
        }

        // İstifadəçinin balansı və keşbek məlumatı
        public IActionResult Index()
        {
            var userId = GetUserId();
            var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId && !w.IsDeleted);

            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                _context.Wallets.Add(wallet);
                _context.SaveChanges();
            }

            return View(wallet);
        }
    }
}
