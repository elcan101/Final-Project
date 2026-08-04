using Microsoft.AspNetCore.Identity;

namespace E_Commerce.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = null!;
    }
}
