using System.ComponentModel.DataAnnotations;

namespace Nexus_backend.Models
{
    public class CollaborationRequest
    {
        [Key]
        public int Id { get; set; }

        public string InvestorId { get; set; } = string.Empty;
        public string EntrepreneurId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending, accepted, rejected
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ApplicationUser? Investor { get; set; }
        public virtual ApplicationUser? Entrepreneur { get; set; }
    }
}