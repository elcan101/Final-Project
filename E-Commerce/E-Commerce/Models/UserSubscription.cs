using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    // Kitab Pass abunəlik növləri
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

        // Aylıq abunə haqqı (mock ödəniş tarixçəsi üçün)
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePaid { get; set; }

        // Premium: "ayda bir pulsuz icarə (14 günlük müddət)" haqqının bu ay artıq
        // istifadə olunub-olunmadığını izləyir
        public DateTime? FreeRentalUsedThisMonth { get; set; }

        public static decimal MonthlyPrice(SubscriptionPlanType plan) =>
            plan == SubscriptionPlanType.Premium ? 9.99m : 2.99m;
    }
}
