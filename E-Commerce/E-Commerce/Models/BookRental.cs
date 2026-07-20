using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class BookRental : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [Required]
        public DateTime RentedDate { get; set; } = DateTime.Now;

        // İstifadəçinin seçdiyi qaytarma tarixi
        [Required]
        public DateTime DueDate { get; set; }

        public DateTime? ReturnedDate { get; set; }

        // "Hər gün üçün 20 qəpikdən hesablanır"
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; } = 0.20m;

        // Vaxtı keçdikdə tətbiq olunan gündəlik cərimə (gündəlik haqqın 2 qatı)
        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyRatePerDay { get; set; } = 0.40m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyAmount { get; set; } = 0.00m;

        // Avtomatik balans çıxılması zamanı təkrar hesablamanın qarşısını almaq üçün
        public int PenaltyChargedDays { get; set; } = 0;

        public bool IsFreePremiumRental { get; set; } = false;

        [NotMapped]
        public bool IsReturned => ReturnedDate.HasValue;

        // Cərimə hesabı: qaytarılıbsa ReturnedDate, qaytarılmayıbsa "indi" əsas götürülür
        public int LateDays(DateTime? asOf = null)
        {
            var reference = ReturnedDate ?? asOf ?? DateTime.Now;
            if (reference <= DueDate) return 0;
            return (int)Math.Ceiling((reference - DueDate).TotalDays);
        }

        // Premium pulsuz icarədə əsas kirayə haqqı yoxdur, amma vaxtı keçdikdə cərimə yenə tətbiq olunur
        public decimal CalculatePenalty(DateTime? asOf = null)
        {
            return LateDays(asOf) * PenaltyRatePerDay;
        }
    }
}
