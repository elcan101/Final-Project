using E_Commerce.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace E_Commerce.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Qurduğumuz modellərin SQL-də cədvələ çevrilməsi
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Wallet> Wallets { get; set; } = null!;
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<CourierProfile> CourierProfiles { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Coupon> Coupons { get; set; } = null!;
        public DbSet<UserSubscription> UserSubscriptions { get; set; } = null!;
        public DbSet<BookRental> BookRentals { get; set; } = null!;
        public DbSet<Listing> Listings { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;
        public DbSet<ProductReview> ProductReviews { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Pul və balans dəqiqliyi üçün SQL konfiqurasiyası
            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Wallet>()
                .Property(w => w.Balance)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Wallet>()
                .Property(w => w.TotalCashbackEarned)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Wallet>()
                .Property(w => w.PendingCashback)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CourierProfile>()
                .Property(c => c.CurrentBalance)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.CashbackAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Coupon>()
                .Property(c => c.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<UserSubscription>()
                .Property(s => s.PricePaid)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<BookRental>(b =>
            {
                b.Property(r => r.DailyRate).HasColumnType("decimal(18,2)");
                b.Property(r => r.PenaltyRatePerDay).HasColumnType("decimal(18,2)");
                b.Property(r => r.BaseCost).HasColumnType("decimal(18,2)");
                b.Property(r => r.PenaltyAmount).HasColumnType("decimal(18,2)");
                b.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Listing>(l =>
            {
                l.Property(x => x.Price).HasColumnType("decimal(18,2)");
                l.Property(x => x.DailyListingFee).HasColumnType("decimal(18,2)");
                l.Property(x => x.AccruedFees).HasColumnType("decimal(18,2)");
                l.Property(x => x.PlatformCommissionRate).HasColumnType("decimal(18,2)");
                l.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}