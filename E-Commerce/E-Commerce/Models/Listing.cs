using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public enum ListingStatus
    {
        Active = 0,
        Sold = 1,
        Deactivated = 2
    }

    public class Listing : BaseEntity
    {
        [Required]
        public string SellerId { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? Author { get; set; }

        public string? ImageUrl { get; set; }

        [StringLength(30)]
        public string? ContactPhone { get; set; }

        public int? CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public bool IsHardcover { get; set; } = true; 

        public ListingStatus Status { get; set; } = ListingStatus.Active;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyListingFee { get; set; } = 0.10m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccruedFees { get; set; } = 0.00m;

        public DateTime LastFeeChargedDate { get; set; } = DateTime.Now.Date;

        public string? BuyerId { get; set; }

        public DateTime? SoldDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PlatformCommissionRate { get; set; } = 0.08m; 
    }
}
