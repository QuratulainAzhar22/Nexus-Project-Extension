namespace Nexus_backend.DTOs
{
    public class MessageDto
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public UserDto? Sender { get; set; }
        public UserDto? Receiver { get; set; }
    }

    public class SendMessageDto
    {
        public string ReceiverId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ConversationDto
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new List<string>();
        public MessageDto? LastMessage { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserDto? OtherParticipant { get; set; }
    }
}