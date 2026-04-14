namespace Nexus_backend.DTOs
{
    public class DocumentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public bool Shared { get; set; }
        public string Url { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public UserDto? Owner { get; set; }

        // E-signature fields
        public string? SignatureImageUrl { get; set; }
        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? SignedByUserId { get; set; }
        public UserDto? SignedBy { get; set; }
    }

    public class UploadDocumentDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Base64Content { get; set; } = string.Empty;
        public bool Shared { get; set; } = false;
    }

    public class UpdateDocumentDto
    {
        public bool Shared { get; set; }
    }

    public class SignDocumentDto
    {
        public string SignatureImageUrl { get; set; } = string.Empty;
    }
}