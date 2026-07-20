using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class CourierProfile : BaseEntity
    {
        [Required]
        public string CourierId { get; set; } = null!;

        // Kuryer qeydiyyatı zamanı tələb olunan Ad Soyad
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = null!;

        // Kuryerin nəqliyyat növü (Məs: Piyada, Velosiped, Skuter, Avtomobil)
        [Required]
        [StringLength(50)]
        public string VehicleType { get; set; } = null!;

        // Kuryer hazırda sifariş qəbul edə bilərmi? (Boşdadır/Məşğuldur)
        public bool IsAvailable { get; set; } = true;

        // Kuryerin çatdırılma pullarından yığdığı balans
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 0.00m;
    }
}