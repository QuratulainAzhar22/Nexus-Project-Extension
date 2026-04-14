namespace Nexus_backend.DTOs
{
    public class MeetingDto
    {
        public int Id { get; set; }
        public string OrganizerId { get; set; } = string.Empty;
        public string ParticipantId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string MeetingUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public UserDto? Organizer { get; set; }
        public UserDto? Participant { get; set; }
    }

    public class CreateMeetingDto
    {
        public string ParticipantId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class UpdateMeetingStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class CheckConflictDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}