using System.ComponentModel.DataAnnotations;

namespace Nexus_backend.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // deposit, withdraw, transfer
        public string Status { get; set; } = string.Empty; // pending, completed, failed
        public string Reference { get; set; } = string.Empty; // Stripe payment intent ID or transfer reference
        public string? ToUserId { get; set; } // For transfers
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ApplicationUser? User { get; set; }
        public virtual ApplicationUser? ToUser { get; set; }
    }
}