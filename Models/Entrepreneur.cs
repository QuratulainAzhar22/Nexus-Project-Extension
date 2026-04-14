using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nexus_backend.Models
{
    public class Entrepreneur
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string StartupName { get; set; } = string.Empty;
        public string PitchSummary { get; set; } = string.Empty;
        public string FundingNeeded { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int FoundedYear { get; set; }
        public int TeamSize { get; set; }

        [JsonIgnore]
        public virtual ApplicationUser? User { get; set; }
    }
}