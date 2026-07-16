using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Order : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // Yekun ödəniş

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashbackAmount { get; set; } // Bu sifarişdən qazanılan keşbek

        // Uber/Wolt modeli kuryer əlaqəsi (Sifarişi götürən kuryer)
        public int? CourierProfileId { get; set; }

        [ForeignKey("CourierProfileId")]
        public CourierProfile? Courier { get; set; }

        // Sifarişin vəziyyəti: Hazırlanır, Kuryerdədir, Çatdırıldı
        [Required]
        public string Status { get; set; } = "Hazırlanır";
    }
}