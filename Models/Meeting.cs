using System.ComponentModel.DataAnnotations;

namespace Nexus_backend.Models
{
    public class Meeting
    {
        [Key]
        public int Id { get; set; }

        public string OrganizerId { get; set; } = string.Empty;
        public string ParticipantId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = "pending"; // pending, accepted, rejected, completed
        public string MeetingUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ApplicationUser? Organizer { get; set; }
        public virtual ApplicationUser? Participant { get; set; }
    }
}