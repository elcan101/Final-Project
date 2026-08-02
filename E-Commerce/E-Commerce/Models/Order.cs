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

        // Kuryerin çatdırılma zamanı müştəri ilə əlaqə saxlaması üçün tələb olunan əlaqə nömrəsi
        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        // Depodan çatdırılma ünvanına məsafəyə görə hesablanan çatdırılma haqqı —
        // bu məbləğ müştəridən tutulur (sifariş yekun məbləğinə daxildir)
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFee { get; set; } = 0.00m;

        public double? DeliveryDistanceKm { get; set; }

        // Rayonlara poçtla çatdırılma: müştəri xəritədən nöqtə seçmək əvəzinə rayon və
        // poçt indeksi daxil edərək sifariş verə bilər. Bu sifarişlərə kuryer təyin olunmur,
        // kuryerlərə heç bir bildiriş getmir — sifariş dərhal "Çatdırıldı" elan olunur və
        // müştəriyə poçt izləmə kodu göndərilir.
        public string? District { get; set; }
        public string? PostalCode { get; set; }
        public bool IsPostDelivery { get; set; } = false;
        public string? TrackingCode { get; set; }

        // Çatdırılma haqqının kuryerə çatan payı — 70%.
        // Qalan 30% platforma xidmət haqqı kimi saxlanılır.
        [NotMapped]
        public const decimal CourierShareRate = 0.70m;

        // Sifariş çatdırıldıqda kuryerin balansına köçürülən məbləğ (çatdırılma haqqının 70%-i)
        [NotMapped]
        public decimal CourierEarning => Math.Round(DeliveryFee * CourierShareRate, 2);
    }
}