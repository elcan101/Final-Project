using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Admin panelində kitablara PDF/audio faylı yükləmək üçün böyük sorğu həcminə icazə (500 MB).
// Səsli kitab faylları böyük ola bilər (uzun kitablar 100-200+ MB) — əvvəlki 100 MB limiti
// bəzi fayllarda "ERR_CONNECTION_RESET" xətasına səbəb olurdu (Kestrel limiti aşanda
// bağlantını dərhal kəsir). Limiti artırdıq.
const long MaxUploadBytes = 500L * 1024 * 1024;

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxUploadBytes;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxUploadBytes;

    // Böyük fayl yavaş internetlə yüklənəndə Kestrel-in defolt minimum sürət tələbi
    // (~240 byte/san) bağlantını vaxtından əvvəl kəsib ERR_CONNECTION_RESET yaradırdı.
    // Bu limiti söndürürük ki, yavaş yükləmələr də uğurla tamamlansın.
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;

    // Böyük faylın tam yüklənməsi üçün kifayət qədər vaxt.
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
});

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

// Sayt daxili "Kitab AI" köməkçisi: kataloqu analiz edib kitab tövsiyə edir və
// sayt haqqında (çatdırılma, qaytarma, kampaniya) suallara cavab verir (bax Services/AiAssistantService.cs)
builder.Services.AddHttpClient<IAiAssistantService, AiAssistantService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

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
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Əvvəllər gözlənilməyən xəta (məs. admin panelində fayl yükləmə zamanı disk/icazə xətası)
    // istifadəçiyə boş/xarab səhifə kimi görünürdü. İndi anlaşılan xəta səhifəsinə yönləndirilir.
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

// Sayt səhifələri (HTML) heç vaxt brauzer/disk keşində saxlanılmasın.
// Səbəb: istifadəçi çıxış edib (Logout) fərqli hesabla (və ya yenidən) giriş etdikdə,
// brauzer əvvəlki (köhnə giriş vəziyyətinə aid) səhifəni keşdən göstərə bilirdi —
// məsələn, səsli kitab pleyeri (abunəlik yoxlamasına görə göstərilir) və ya profil
// məlumatları köhnə/yanlış görünürdü, çünki server yenidən sorğulanmırdı.
// Bu middleware statik fayllardan (şəkil, css, js, pdf, audio) SONRA işə düşür,
// ona görə onlara toxunmur — yalnız controller/view tərəfindən yaradılan dinamik
// səhifələrə tətbiq olunur.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "-1";
        return Task.CompletedTask;
    });
    await next();
});

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

    // Keşbek "gözləyən" balansının sinxronizasiyası: PendingCashback sütunu sonradan əlavə
    // olunduğu üçün bəzi mühitlərdə (məs. migration tarixçəsi əl ilə düzəldilmiş bazalarda)
    // köhnə istifadəçilərin qazandığı keşbek "gözləyən" hissəyə düşməyə bilər. Hər başlanğıcda
    // bu sadə, təhlükəsiz (yalnız 0-a bərabər olan sətirlərə toxunan) bərpanı işə salırıq ki,
    // "Balansa köçür" düyməsi bazanın vəziyyətindən asılı olmayaraq düzgün görünsün.
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE Wallets
            SET PendingCashback = TotalCashbackEarned
            WHERE PendingCashback = 0 AND TotalCashbackEarned > 0;
        ");
    }
    catch
    {
        // Sütun hələ mövcud deyilsə (məs. migration bu anda tətbiq olunmayıbsa) səssizcə keç —
        // növbəti başlanğıcda (migration tətbiq olunandan sonra) avtomatik düzələcək.
    }

    await E_Commerce.Data.IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();