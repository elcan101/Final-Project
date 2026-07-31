using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    // İstifadəçiyə (müştəri və ya kuryer) göndərilən sayt-daxili bildirişlər
    // (məs: "kuryer sifarişi götürdü", "icarə müddəti bitir" və s.)
    public class Notification : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = null!;

        [Required]
        public string Message { get; set; } = null!;

        // İstifadəçi bildirişə kliklədikdə hara yönləndirilsin (məs: sifariş izləmə səhifəsi)
        public string? Url { get; set; }

        public bool IsRead { get; set; } = false;
    }
}
