using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "E-poçt boş ola bilməz")]
        [EmailAddress(ErrorMessage = "Düzgün e-poçt daxil edin")]
        [Display(Name = "E-poçt")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Şifrə boş ola bilməz")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrə")]
        public string Password { get; set; } = null!;

        [Display(Name = "Məni xatırla")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
