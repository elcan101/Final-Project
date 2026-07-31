using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Wallet : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCashbackEarned { get; set; } = 0.00m;

        // Hələ balansa köçürülməmiş, gözləyən keşbek — minimum 5 AZN-ə çatanda
        // istifadəçi "Balansa köçür" düyməsi ilə bunu Balance-a köçürə bilər.
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PendingCashback { get; set; } = 0.00m;

        [NotMapped]
        public const decimal MinCashbackTransfer = 5.00m;
    }
}