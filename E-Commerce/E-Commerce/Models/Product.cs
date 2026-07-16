using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace E_Commerce.Models
{
    public class Product : BaseEntity
    {
        [Required]
        [StringLength(150)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int StockCount { get; set; }

        // Turbo.az tipli filtrasiya və fərqli kitab növləri üçün statuslar
        public bool IsDigital { get; set; } = false; // E-Kitabdır?
        public bool IsAudio { get; set; } = false;   // Səsli kitabdır?
        public bool IsSecondHand { get; set; } = false; // İkinci əldir?

        // Rəqəmsal kitabların fayl yolları
        public string? PdfUrl { get; set; }
        public string? AudioUrl { get; set; }

        // Şəkil linki
        public string? ImageUrl { get; set; }

        // Reytinq (5 ulduz sistemi üçün ortalama)
        public double Rating { get; set; } = 0.0;

        // KATEQORİYA ƏLAKƏSİ (Foreign Key)
        [Required]
        public int CategoryId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("CategoryId")]
        [ValidateNever]
        public Category Category { get; set; } = null!;
    }
}