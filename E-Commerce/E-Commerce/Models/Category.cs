using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class Category : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;

        // Bir kateqoriyada çoxlu kitab ola bilər (One-to-Many əlaqəsi)
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}