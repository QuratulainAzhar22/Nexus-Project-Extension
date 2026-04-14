namespace Nexus_backend.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime CreatedAt { get; set; }

        // Entrepreneur specific fields
        public string? StartupName { get; set; }
        public string? PitchSummary { get; set; }
        public string? FundingNeeded { get; set; }
        public string? Industry { get; set; }
        public string? Location { get; set; }
        public int? FoundedYear { get; set; }
        public int? TeamSize { get; set; }

        // Investor specific fields
        public List<string>? InvestmentInterests { get; set; }
        public List<string>? InvestmentStage { get; set; }
        public List<string>? PortfolioCompanies { get; set; }
        public int? TotalInvestments { get; set; }
        public string? MinimumInvestment { get; set; }
        public string? MaximumInvestment { get; set; }
    }
}