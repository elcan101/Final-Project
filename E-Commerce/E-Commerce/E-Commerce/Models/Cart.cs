using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class Cart : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        // Səbətin içindəki məhsulların siyahısı (One-to-Many əlaqəsi)
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}