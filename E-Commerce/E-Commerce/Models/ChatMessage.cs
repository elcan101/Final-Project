using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class ChatMessage : BaseEntity
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        public string SenderId { get; set; } = null!;

        [Required]
        [StringLength(80)]
        public string SenderName { get; set; } = null!;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = null!;
    }
}
