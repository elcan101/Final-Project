using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Services
{
    public class DailyBillingService : BackgroundService
    {
        private const decimal FlatLateFine = 5.00m;

        private const string DepotAddress = "28 May, Dilarə Əliyeva küçəsi 239";

        private readonly IServiceProvider _services;
        private readonly ILogger<DailyBillingService> _logger;

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
                        $"<p>Zəhmət olmasa kitabı vaxtında qaytarın, əks halda balansınızdan {FlatLateFine:0.00} AZN sabit cərimə tutulacaq.</p>" +
                        $"<p>Vaxt lazımdırsa, \"İcarələrim\" bölməsindən icarə müddətini uzada bilərsiniz.</p>" +
                        $"<p>Kitabı <b>{DepotAddress}</b> ünvanındakı depona təhvil verməlisiniz.</p>");
                }

                rental.DueSoonEmailSent = true;

                db.Notifications.Add(new Notification
                {
                    UserId = rental.UserId,
                    Title = "İcarə müddətinizin bitməsinə 1 gün qalıb",
                    Message = $"\"{rental.Product?.Title}\" kitabının icarə müddəti {rental.DueDate:dd.MM.yyyy} tarixində bitir. " +
                              $"Vaxtında qaytarın, əks halda {FlatLateFine:0.00} AZN cərimə tətbiq olunacaq. " +
                              $"İstəsəniz, \"İcarələrim\" bölməsindən icarə müddətini uzada bilərsiniz. " +
                              $"Kitabı {DepotAddress} ünvanındakı depona təhvil verməlisiniz.",
                    Url = "/Rental"
                });
            }

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

            wallet.Balance -= amount;
        }
    }
}
