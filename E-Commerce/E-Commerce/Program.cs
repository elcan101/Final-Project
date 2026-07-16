using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using E_Commerce.Data;
using E_Commerce.Models;

var builder = WebApplication.CreateBuilder(args);

// Servislər
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Login / Qeydiyyat sistemi (ASP.NET Core Identity)
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // Sadə şifrə qaydaları (tələbə layihəsi üçün rahatlıq məqsədilə)
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Səbət üçün sessiya (giriş etməmiş qonaq istifadəçini izləyir)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Middleware-lər
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SeedData-nı burdan silirik, layihə run olsun
app.Run();