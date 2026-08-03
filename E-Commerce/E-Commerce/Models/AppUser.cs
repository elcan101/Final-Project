using Microsoft.AspNetCore.Identity;

namespace E_Commerce.Models
{
    // ASP.NET Core Identity-nin standart istifadəçisini genişləndiririk
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = null!;
   