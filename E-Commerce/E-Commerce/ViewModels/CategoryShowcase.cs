using System.Collections.Generic;
using E_Commerce.Models;

namespace E_Commerce.ViewModels
{
    public class CategoryShowcase
    {
        public Category Category { get; set; } = null!;
        public List<Product> Products { get; set; } = new List<Product>();
    }
}
