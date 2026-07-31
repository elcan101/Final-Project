using Microsoft.AspNetCore.Identity;
using E_Commerce.Models;

namespace E_Commerce.Data
{
    // Tətbiq hər başladıqda işə düşür, amma hər şeyi "yalnız yoxdursa" yaradır —
    // buna görə bazanı silmir və artıq mövcud olan heç bir şeyi pozmur.
    public static class IdentitySeeder
    {
        // İLK ADMİN HESABI (bununla giriş edib Admin Panelə keçə bilərsiniz).
        // Təhlükəsizlik üçün ilk girişdən sonra şifrəni dəyişməyiniz tövsiyə olunur.
        public const string DefaultAdminEmail = "admin@okean.az";
        public const string DefaultAdminPassword = "Admin123!";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var context = services.GetRequiredService<AppDbContext>();

            // 1) Rollar
            foreach (var role in new[] { "Admin", "Customer" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2) İlk admin hesabı
            var admin = await userManager.FindByEmailAsync(DefaultAdminEmail);
            if (admin == null)
            {
                admin = new AppUser
                {
                    UserName = DefaultAdminEmail,
                    Email = DefaultAdminEmail,
                    EmailConfirmed = true,
                    FullName = "Baş Admin"
                };
                var result = await userManager.CreateAsync(admin, DefaultAdminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
            else if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            // 3) Baza kateqoriyalar (yalnız heç kateqoriya yoxdursa əlavə olunur)
            if (!context.Categories.Any())
            {
                var names = new[]
                {
                    "Uşaq Ədəbiyyatı",
                    "Bədii Ədəbiyyat",
                    "Elmi-Populyar / Qeyri-Bədii",
                    "Dərsliklər. Hazırlıq",
                    "Biznes və Psixologiya",
                    "Tarix və Hüquq",
                    "Sağlamlıq və Tibb",
                    "Xarici Dillər. Lüğətlər",
                    "Bestseller",
                };

                foreach (var n in names)
                    context.Categories.Add(new Category { Name = n });

                await context.SaveChangesAsync();
            }
        }
    }
}
