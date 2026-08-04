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

        public int? OrderId { get; set; }

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [Required]
        public DateTime RentedDate { get; set; } = DateTime.Now;

        [Required]
        public DateTime DueDate { get; set; }

        public DateTime? ReturnedDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; } = 0.20m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyRatePerDay { get; set; } = 0.40m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyAmount { get; set; } = 0.00m;

        public int PenaltyChargedDays { get; set; } = 0;

        public bool IsFreePremiumRental { get; set; } = false;

        public bool DueSoonEmailSent { get; set; } = false;

        public bool LateFineApplied { get; set; } = false;

        [NotMapped]
        public bool IsReturned => ReturnedDate.HasValue;

        public int LateDays(DateTime? asOf = null)
        {
            var reference = ReturnedDate ?? asOf ?? DateTime.Now;
            if (reference <= DueDate) return 0;
            return (int)Math.Ceiling((reference - DueDate).TotalDays);
        }

        public decimal CalculatePenalty(DateTime? asOf = null)
        {
            return LateDays(asOf) * PenaltyRatePerDay;
        }
    }
}
