using Microsoft.AspNetCore.Identity;

namespace Nexus_backend.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Entrepreneur? Entrepreneur { get; set; }
        public virtual Investor? Investor { get; set; }
    }
}