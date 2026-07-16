using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Coupon : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = null!; // Məs: OKEAN20

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } // Nə qədər endirim edəcək (Məs: 5.00 AZN)

        public bool IsActive { get; set; } = true;
    }
}