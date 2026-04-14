namespace Nexus_backend.DTOs
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string? ToUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto? User { get; set; }
        public UserDto? ToUser { get; set; }
    }

    public class DepositDto
    {
        public decimal Amount { get; set; }
        public string PaymentMethodId { get; set; } = string.Empty;
    }

    public class WithdrawDto
    {
        public decimal Amount { get; set; }
    }

    public class TransferDto
    {
        public string ToUserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class PaymentIntentResponseDto
    {
        public string ClientSecret { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; } = string.Empty;
    }
}