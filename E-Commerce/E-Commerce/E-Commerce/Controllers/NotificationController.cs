using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Data;

namespace E_Commerce.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Bütün bildirişlərin tam siyahısı
        public IActionResult Index()
        {
            var userId = GetUserId();
            var notifications = _context.Notifications
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedDate)
                .ToList();

            return View(notifications);
        }

        // Zəng ikonasının dropdown-u üçün: son bildirişlər + oxunmamış say (JSON)
        [HttpGet]
        public IActionResult Recent()
        {
            var userId = GetUserId();
            var items = _context.Notifications
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedDate)
                .Take(10)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Url,
                    n.IsRead,
                    createdDate = n.CreatedDate.ToString("dd.MM.yyyy HH:mm")
                })
                .ToList();

            var unreadCount = _context.Notifications.Count(n => n.UserId == userId && !n.IsDeleted && !n.IsRead);

            return Json(new { items, unreadCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllRead()
        {
            var userId = GetUserId();
            var unread = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
            foreach (var n in unread) n.IsRead = true;
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = GetUserId();
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}
