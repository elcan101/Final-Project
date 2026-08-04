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
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashbackAmount { get; set; } 

        public int? CourierProfileId { get; set; }

        [ForeignKey("CourierProfileId")]
        public CourierProfile? Courier { get; set; }

        [Required]
        public string Status { get; set; } = "Hazırlanır";

        public string? CouponCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0.00m;

        public double? CourierLatitude { get; set; }
        public double? CourierLongitude { get; set; }
        public DateTime? LastLocationUpdate { get; set; }

        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }
        public string? DeliveryAddressText { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFee { get; set; } = 0.00m;

        public double? DeliveryDistanceKm { get; set; }

        public string? District { get; set; }
        public string? PostalCode { get; set; }
        public bool IsPostDelivery { get; set; } = false;
        public string? TrackingCode { get; set; }

        [NotMapped]
        public const decimal CourierShareRate = 0.70m;

        [NotMapped]
        public decimal CourierEarning => Math.Round(DeliveryFee * CourierShareRate, 2);
    }
}