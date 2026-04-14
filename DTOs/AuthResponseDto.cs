namespace Nexus_backend.DTOs
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; } 
        public string? Token { get; set; } 
        public string? RefreshToken { get; set; } 
        public UserDto? User { get; set; }
    }
}