using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public enum SubscriptionPlanType
    {
        Standard = 0,
        Premium = 1
    }

    public class UserSubscription : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public SubscriptionPlanType PlanType { get; set; } = SubscriptionPlanType.Standard;

        [Required]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePaid { get; set; }

        public DateTime? FreeRentalUsedThisMonth { get; set; }

        public static decimal MonthlyPrice(SubscriptionPlanType plan) =>
            plan == SubscriptionPlanType.Premium ? 9.99m : 2.99m;
    }
}
