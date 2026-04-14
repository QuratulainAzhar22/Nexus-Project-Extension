using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nexus_backend.Models
{
    public class Investor
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public List<string> InvestmentInterests { get; set; } = new();
        public List<string> InvestmentStage { get; set; } = new();
        public List<string> PortfolioCompanies { get; set; } = new();
        public int TotalInvestments { get; set; }
        public string MinimumInvestment { get; set; } = string.Empty;
        public string MaximumInvestment { get; set; } = string.Empty;

        [JsonIgnore]
        public virtual ApplicationUser? User { get; set; }
    }
}