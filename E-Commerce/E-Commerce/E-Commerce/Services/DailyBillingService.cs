using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Services
{
    // Hər gün: (1) aktiv C2C elanlarından günlük elan haqqını, (2) vaxtı keçmiş
    // icarələrdən gecikmə cəriməsini, (3) qaytarma tarixinə 1 gün qalmış icarələr üçün
    // xəbərdarlıq mailini və (4) vaxtında qaytarılmayan icarələrə sabit -5 AZN cəriməni
    // avtomatik olaraq işlədir.
    public class DailyBillingService : BackgroundService
    {
        // Vaxtında qaytarılmadıqda bir dəfəlik tutulan sabit cərimə
        private const decimal FlatLateFine = 5.00m;

        private readonly IServiceProvider _services;
        private readonly ILogger<DailyBillingService> _logger;

        // Demo məqsədilə tez-tez yoxlanılır (real production-da 24 saat kifayətdir);
        // hər dəfə yalnız o günə görə hələ haqqı tutulmamış qeydlər üçün işləyir.
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public DailyBillingService(IServiceProvider services, ILogger<DailyBillingService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunBillingCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gündəlik hesablaşma dövründə xəta baş verdi");
                }

                await Task.Delay(_checkInterval, stoppingToken).ContinueWith(_ => { });
            }
        }

        private async Task RunBillingCycleAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var today = DateTime.Now.Date;

            // ---- 1) C2C elan haqqı ----
            var dueListings = await db.Listings
                .Where(l => !l.IsDeleted && l.Status == ListingStatus.Active && l.LastFeeChargedDate < today)
                .ToListAsync(ct);

            foreach (var listing in dueListings)
            {
                var daysDue = (today - listing.LastFeeChargedDate).Days;
                if (daysDue <= 0) continue;

                var fee = daysDue * listing.DailyListingFee;
                await DeductFromWalletAsync(db, listing.SellerId, fee, ct);

                listing.AccruedFees += fee;
                listing.LastFeeChargedDate = today;
            }

            // ---- 2) Gecikmiş icarə cərimələri (gündəlik) ----
            var overdueRentals = await db.BookRentals
                .Where(r => !r.IsDeleted && r.ReturnedDate == null && r.DueDate < DateTime.Now)
                .ToListAsync(ct);

            foreach (var rental in overdueRentals)
            {
                var totalLateDays = rental.LateDays();
                var newlyChargeableDays = totalLateDays - rental.PenaltyChargedDays;
                if (newlyChargeableDays <= 0) continue;

                var penalty = newlyChargeableDays * rental.PenaltyRatePerDay;
                await DeductFromWalletAsync(db, rental.UserId, penalty, ct);

                rental.PenaltyAmount += penalty;
                rental.PenaltyChargedDays = totalLateDays;
            }

            // ---- 3) Qaytarma tarixinə 1 gün qalmış icarələr üçün xəbərdarlıq maili ----
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var dueSoonRentals = await db.BookRentals
                .Include(r => r.Product)
                .Where(r => !r.IsDeleted && r.ReturnedDate == null && !r.DueSoonEmailSent
                            && r.DueDate.Date == today.AddDays(1))
                .ToListAsync(ct);

            foreach (var rental in dueSoonRentals)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == rental.UserId, ct);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await emailService.SendAsync(
                        user.Email,
                        "İcarə müddətinizin bitməsinə 1 gün qalıb",
                        $"<p>Salam {user.FullName},</p>" +
                        $"<p><b>{rental.Product?.Title}</b> kitabının icarə müddəti <b>{rental.DueDate:dd.MM.yyyy}</b> tarixində bitir.</p>" +
                        $"<p>Zəhmət olmasa kitabı vaxtında qaytarın, əks halda balansınızdan {FlatLateFine:0.00} AZN sabit cərimə tutulacaq.</p>");
                }

                rental.DueSoonEmailSent = true;

                db.Notifications.Add(new Notification
                {
                    UserId = rental.UserId,
                    Title = "İcarə müddətinizin bitməsinə 1 gün qalıb",
                    Message = $"\"{rental.Product?.Title}\" kitabının icarə müddəti {rental.DueDate:dd.MM.yyyy} tarixində bitir. Vaxtında qaytarın, əks halda {FlatLateFine:0.00} AZN cərimə tətbiq olunacaq.",
                    Url = "/Rental"
                });
            }

            // ---- 4) Vaxtında qaytarılmayan icarələrə bir dəfəlik sabit -5 AZN cərimə ----
            var newlyLateRentals = overdueRentals.Where(r => !r.LateFineApplied).ToList();
            foreach (var rental in newlyLateRentals)
            {
                await DeductFromWalletAsync(db, rental.UserId, FlatLateFine, ct);
                rental.PenaltyAmount += FlatLateFine;
                rental.LateFineApplied = true;

                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == rental.UserId, ct);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await emailService.SendAsync(
                        user.Email,
                        "Kitab vaxtında qaytarılmadı — balansınıza cərimə tətbiq olundu",
                        $"<p>Salam {user.FullName},</p>" +
                        $"<p><b>{rental.Product?.Title}</b> kitabı <b>{rental.DueDate:dd.MM.yyyy}</b> tarixinədək qaytarılmadığı üçün balansınızdan " +
                        $"<b>-{FlatLateFine:0.00} AZN</b> sabit cərimə tutuldu.</p>");
                }

                db.Notifications.Add(new Notification
                {
                    UserId = rental.UserId,
                    Title = "Balansınıza cərimə tətbiq olundu",
                    Message = $"\"{rental.Product?.Title}\" kitabı vaxtında qaytarılmadığı üçün balansınızdan -{FlatLateFine:0.00} AZN tutuldu.",
                    Url = "/Rental"
                });
            }

            if (dueListings.Count > 0 || overdueRentals.Count > 0 || dueSoonRentals.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Gündəlik hesablaşma: {ListingCount} elan haqqı, {RentalCount} icarə cəriməsi tutuldu, {WarnCount} xəbərdarlıq maili, {FineCount} sabit cərimə",
                    dueListings.Count, overdueRentals.Count, dueSoonRentals.Count, newlyLateRentals.Count);
            }
        }

        private static async Task DeductFromWalletAsync(AppDbContext db, string userId, decimal amount, CancellationToken ct)
        {
            var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && !w.IsDeleted, ct);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId };
                db.Wallets.Add(wallet);
            }

            // Balans mənfiyə düşə bilər — istifadəçi bir sonrakı ödənişdə borcunu bağlamalıdır
            wallet.Balance -= amount;
        }
    }
}
