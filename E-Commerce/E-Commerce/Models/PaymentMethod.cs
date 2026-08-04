using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class PaymentMethod : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public string StripeToken { get; set; } = null!; // pm_xxx / tok_xxx (mock rejimdə saxta dəyər)

        [Required]
        [StringLength(20)]
        public string CardBrand { get; set; } = "Visa";

        [Required]
        [StringLength(4)]
        public string Last4 { get; set; } = "0000";

        public bool IsDefault { get; set; } = true;
    }
}
