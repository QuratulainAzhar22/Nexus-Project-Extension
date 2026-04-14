using System.ComponentModel.DataAnnotations;

namespace Nexus_backend.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // collaboration, message, meeting, payment
        public string ReferenceId { get; set; } = string.Empty; // Related entity ID
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual ApplicationUser? User { get; set; }
    }
}