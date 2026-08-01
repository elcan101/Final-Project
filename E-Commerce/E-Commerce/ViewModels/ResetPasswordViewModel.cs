using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Token { get; set; } = null!;

        [Required(ErrorMessage = "Yeni şifrə boş ola bilməz")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifrə ən azı 6 simvoldan ibarət olmalıdır")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifrə")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Şifrənin təkrarı boş ola bilməz")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifrənin təkrarı")]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifrələr uyğun gəlmir")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
