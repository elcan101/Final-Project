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
        }
    }
}