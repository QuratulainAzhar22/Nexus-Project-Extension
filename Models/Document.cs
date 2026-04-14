using System.ComponentModel.DataAnnotations;

namespace Nexus_backend.Models
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public bool Shared { get; set; } = false;
        public string Url { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;

        // E-signature fields
        public string? SignatureImageUrl { get; set; }
        public bool IsSigned { get; set; } = false;
        public DateTime? SignedAt { get; set; }
        public string? SignedByUserId { get; set; }

        // Navigation properties
        public virtual ApplicationUser? Owner { get; set; }
        public virtual ApplicationUser? SignedBy { get; set; }
    }
}