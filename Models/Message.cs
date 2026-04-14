using System.ComponentModel.DataAnnotations;

namespace Nexus_backend.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        public string SenderId { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

        // Navigation properties
        public virtual ApplicationUser? Sender { get; set; }
        public virtual ApplicationUser? Receiver { get; set; }
    }
}