using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Ad Soyad boş ola bilməz")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "E-poçt boş ola bilməz")]
        [EmailAddress(ErrorMessage = "Düzgün e-poçt daxil edin")]
        [Display(Name = "E-poçt")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Şifrə boş ola bilməz")]
        [StringLength(100, ErrorMessage = "Şifrə ən azı {2} simvol olmalıdır", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrə")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Şifrəni təsdiqləyin")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrəni təsdiqlə")]
        [Compare("Password", ErrorMessage = "Şifrələr uyğun gəlmir")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
