using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.ViewModels;

namespace E_Commerce.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly AppDbContext _context;

        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // ---------- QEYDİYYAT ----------

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Bu e-poçt ilə artıq qeydiyyatdan keçilib.");
                return View(model);
            }

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // ---------- GİRİŞ ----------

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                // Admin isə birbaşa admin panelinə (/AdminPanel/Admin), digərləri ana səhifəyə yönləndirilir
                if (user != null && roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Admin", new { area = "AdminPanel" });
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "E-poçt və ya şifrə yanlışdır.");
            return View(model);
        }

        // ---------- ÇIXIŞ ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ---------- PROFİL ----------
        // Mailinə giriş edən istənilən istifadəçi (müştəri, kuryer və s.) üçün ortaq profil
        // səhifəsi: şəxsi məlumatlar + kuryer profili (varsa) + abunəlik tarixçəsi.
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var courierProfile = await _context.CourierProfiles
                .FirstOrDefaultAsync(c => c.CourierId == user.Id && !c.IsDeleted);

            var subscriptions = await _context.UserSubscriptions
                .Where(s => s.UserId == user.Id && !s.IsDeleted)
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            ViewBag.User = user;
            ViewBag.Roles = roles;
            ViewBag.CourierProfile = courierProfile;
            ViewBag.Subscriptions = subscriptions;

            return View();
        }

        // ---------- PROFİLİ REDAKTƏ ET ----------
        // Əvvəllər "Profilim" səhifəsindəki şəxsi məlumatlar yalnız statik mətn kimi
        // göstərilirdi — heç bir redaktə forması/əməliyyatı yox idi. İndi istifadəçi
        // Ad Soyad, Əlaqə nömrəsi və (istəyə bağlı) şifrəsini dəyişə bilər.
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new EditProfileViewModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            // Şifrə sahələri doldurulubsa, şifrəni də yenilə
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
            }

            // FullName dəyişdiyi üçün cookie-dəki claim-lər təzələnsin ki, dərhal
            // (səhifəni yenidən açmadan) hər yerdə yeni ad görünsün
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = "Profiliniz uğurla yeniləndi.";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
