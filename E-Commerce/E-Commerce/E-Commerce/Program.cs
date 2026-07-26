using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Servislər
builder.Services.AddControllersWithViews();

// Bildirişlər panelindəki fetch() sorğuları antiforgery tokenini HTTP header vasitəsilə göndərir
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Real-time: kuryer izləmə + canlı çat (SignalR Hubs)
builder.Services.AddSignalR();

// Ödəniş: test rejimində mock Stripe (bax Services/MockStripePaymentService.cs)
builder.Services.AddScoped<IPaymentService, MockStripePaymentService>();

// E-poçt: test rejimində mock e-poçt servisi (bax Services/MockEmailService.cs)
builder.Services.AddScoped<IEmailService, MockEmailService>();

// Depodan çatdırılma ünvanına məsafəyə görə çatdırılma haqqı hesablayır (bax Services/DeliveryPricingService.cs)
builder.Services.AddScoped<DeliveryPricingService>();

// Gündəlik hesablaşma: C2C elan haqqı + icarə gecikmə cərimələri avtomatik balansdan tutulur
builder.Services.AddHostedService<DailyBillingService>();

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
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<CourierTrackingHub>("/hubs/courier-tracking");
app.MapHub<ChatHub>("/hubs/chat");

// Rolları (Admin/Customer), ilk admin hesabını və baza kateqoriyaları yaradır.
// Hər şey "yalnız yoxdursa" yaradılır — bazanı silmir, mövcud datanı pozmur.
using (var scope = app.Services.CreateScope())
{
    // Kod yenilənəndə (yeni sütun/cədvəl əlavə olunanda) baza avtomatik yenilənsin deyə
    // tətbiq hər başladıqda gözləyən bütün migration-ları tətbiq edir. Bu, "Invalid column
    // name" kimi köhnəlmiş sxem xətalarının qarşısını alır.
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    await E_Commerce.Data.IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();