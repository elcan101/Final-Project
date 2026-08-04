using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class Notification : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = null!;

        [Required]
        public string Message { get; set; } = null!;

        public string? Url { get; set; }

        public bool IsRead { get; set; } = false;
    }
}
