//using Microsoft.EntityFrameworkCore;
//using E_Commerce.Models;

//namespace E_Commerce.Data
//{
//    public static class SeedData
//    {
//        public static async Task Initialize(IServiceProvider serviceProvider)
//        {
//            var context = serviceProvider.GetRequiredService<AppDbContext>();

//            // BAZANI TƏMİZLƏYİRİK (Artıq SQL-də əllə silməyə ehtiyac yoxdur)
//            await context.Database.EnsureDeletedAsync();
//            await context.Database.EnsureCreatedAsync();

//            var it = new Category { Name = "İnformasiya Texnologiyaları (IT)" };
//            var psych = new Category { Name = "Psixologiya" };
//            var fiction = new Category { Name = "Bədii Ədəbiyyat" };

//            await context.Categories.AddRangeAsync(it, psych, fiction);
//            await context.SaveChangesAsync();

//            await context.Products.AddRangeAsync(
//                new Product { Title = "C# Professional", Price = 24.50m, ImageUrl = "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=300", CategoryId = it.Id },
//                new Product { Title = "Atom Vərdişlər", Price = 14.90m, ImageUrl = "https://images.unsplash.com/photo-1506784983877-45594efa4cbe?w=300", CategoryId = psych.Id },
//                new Product { Title = "Okeanın Səsi", Price = 8.00m, ImageUrl = "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=300", CategoryId = fiction.Id }
//            );

//            await context.SaveChangesAsync();
//        }
//    }
//}