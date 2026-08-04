using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Coupon : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = null!; 

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } 

        public bool IsActive { get; set; } = true;
    }
}