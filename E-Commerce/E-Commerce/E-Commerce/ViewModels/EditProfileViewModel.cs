using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Ad Soyad boş ola bilməz")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; } = null!;

        [Display(Name = "Əlaqə nömrəsi")]
        [Phone(ErrorMessage = "Düzgün əlaqə nömrəsi daxil edin")]
        public string? PhoneNumber { get; set; }

        // Şifrəni dəyişmək istəməyən istifadəçi bu sahələri boş buraxa bilər
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifrə (istəyə bağlı)")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifrəni təsdiqlə")]
        [Compare("NewPassword", ErrorMessage = "Şifrələr uyğun gəlmir")]
        public string? ConfirmNewPassword { get; set; }
    }
}
