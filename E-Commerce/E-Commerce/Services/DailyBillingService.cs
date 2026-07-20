using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Services
{
    // Hər gün: (1) aktiv C2C elanlarından günlük elan haqqını, (2) vaxtı keçmiş
    // icarələrdən gecikmə cəriməsini avtomatik olaraq istifadəçinin balansından tutur.
    public class DailyBillingService : BackgroundService
    {
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

            // ---- 2) Gecikmiş icarə cərimələri ----
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

            if (dueListings.Count > 0 || overdueRentals.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Gündəlik hesablaşma: {ListingCount} elan haqqı, {RentalCount} icarə cəriməsi tutuldu",
                    dueListings.Count, overdueRentals.Count);
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
