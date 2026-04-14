namespace Nexus_backend.DTOs
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto? User { get; set; }
        public string? UserName { get; set; }
        public string? UserAvatar { get; set; }
    }

    public class MarkAsReadDto
    {
        public int NotificationId { get; set; }
    }
}