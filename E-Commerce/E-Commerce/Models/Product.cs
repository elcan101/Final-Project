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

        [StringLength(150)]
        public string? Author { get; set; }

        [StringLength(150)]
        public string? Publisher { get; set; } 

        [StringLength(50)]
        public string? Language { get; set; } 

        public int? PageCount { get; set; } 
        [Required]
        public string Description { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int StockCount { get; set; }

        public string? AddedByUserId { get; set; }

        public bool IsDigital { get; set; } = false; 
        public bool IsAudio { get; set; } = false;   
        public bool IsSecondHand { get; set; } = false; 

        public bool IsHardcover { get; set; } = true; 

        
        public string? PdfUrl { get; set; }
        public string? AudioUrl { get; set; }

        
        public string? ImageUrl { get; set; }

        
        public double Rating { get; set; } = 0.0;

       
        [Required]
        public int CategoryId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("CategoryId")]
        [ValidateNever]
        public Category Category { get; set; } = null!;
    }
}