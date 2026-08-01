using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "E-poçt boş ola bilməz")]
        [EmailAddress(ErrorMessage = "Düzgün e-poçt daxil edin")]
        [Display(Name = "E-poçt")]
        public string Email { get; set; } = null!;
    }
}
