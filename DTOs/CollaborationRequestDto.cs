namespace Nexus_backend.DTOs
{
    public class CollaborationRequestDto
    {
        public string Id { get; set; } = string.Empty;
        public string InvestorId { get; set; } = string.Empty;
        public string EntrepreneurId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending, accepted, rejected
        public DateTime CreatedAt { get; set; }
        public UserDto? Investor { get; set; }
        public UserDto? Entrepreneur { get; set; }
    }

    public class CreateCollaborationRequestDto
    {
        public string EntrepreneurId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class UpdateRequestStatusDto
    {
        public string Status { get; set; } = string.Empty; // accepted or rejected
    }
}