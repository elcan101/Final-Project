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

        // Sifarişin vəziyyəti: Hazırlanır, Hazırdır, Kuryerdədir, Çatdırıldı
        [Required]
        public string Status { get; set; } = "Hazırlanır";

        // Tətbiq olunan promokod və endirim (Trendyol tipli kupon sistemi)
        public string? CouponCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0.00m;

        // SignalR ilə canlı izlənən kuryer koordinatları
        public double? CourierLatitude { get; set; }
        public double? CourierLongitude { get; set; }
        public DateTime? LastLocationUpdate { get; set; }

        // Müştərinin sifariş zamanı xəritədən seçdiyi çatdırılma nöqtəsi
        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }
        public string? DeliveryAddressText { get; set; }
    }
}