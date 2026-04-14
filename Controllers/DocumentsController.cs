using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus_backend.Data;
using Nexus_backend.DTOs;
using Nexus_backend.Models;
using System.Security.Claims;

namespace Nexus_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DocumentsController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _context = context;
            _environment = environment;
        }

        // GET: api/documents
        [HttpGet]
        public async Task<IActionResult> GetUserDocuments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var documents = await _context.Documents
                .Where(d => d.OwnerId == userId)
                .OrderByDescending(d => d.LastModified)
                .ToListAsync();

            var documentDtos = new List<DocumentDto>();
            foreach (var doc in documents)
            {
                documentDtos.Add(await MapToDto(doc));
            }

            return Ok(documentDtos);
        }

        // GET: api/documents/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

            if (document == null)
                return NotFound(new { message = "Document not found" });

            var documentDto = await MapToDto(document);
            return Ok(documentDto);
        }

        // POST: api/documents/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument([FromBody] UploadDocumentDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            if (string.IsNullOrEmpty(model.Base64Content))
                return BadRequest(new { message = "File content is required" });

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Generate unique filename
                var fileExtension = Path.GetExtension(model.Name);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Convert base64 to bytes and save
                var bytes = Convert.FromBase64String(model.Base64Content);
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);

                // Generate URL for the file
                var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{uniqueFileName}";

                // Save document metadata to database
                var document = new Document
                {
                    Name = model.Name,
                    Type = model.Type,
                    Size = model.Size,
                    LastModified = DateTime.UtcNow,
                    Shared = model.Shared,
                    Url = fileUrl,
                    OwnerId = userId,
                    IsSigned = false,
                    SignatureImageUrl = null,
                    SignedAt = null,
                    SignedByUserId = null
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                var documentDto = await MapToDto(document);
                return Ok(new { success = true, message = "Document uploaded successfully", document = documentDto });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error uploading file: {ex.Message}" });
            }
        }

        // PUT: api/documents/{id}/share
        [HttpPut("{id}/share")]
        public async Task<IActionResult> ShareDocument(int id, [FromBody] UpdateDocumentDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

            if (document == null)
                return NotFound(new { message = "Document not found" });

            document.Shared = model.Shared;
            document.LastModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Document {(model.Shared ? "shared" : "unshared")} successfully" });
        }

        // DELETE: api/documents/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

            if (document == null)
                return NotFound(new { message = "Document not found" });

            // Delete physical file
            try
            {
                var filePath = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", Path.GetFileName(document.Url));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                // Log error but continue with database deletion
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }

            // Delete database record
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Document deleted successfully" });
        }

        // GET: api/documents/download/{id}
        [HttpGet("download/{id}")]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

            if (document == null)
                return NotFound(new { message = "Document not found" });

            var filePath = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", Path.GetFileName(document.Url));

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "File not found on server" });

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(document.Name);

            return File(fileBytes, contentType, document.Name);
        }

        // ==============================================
        // E-SIGNATURE ENDPOINT
        // ==============================================

        // POST: api/documents/{id}/sign
        [HttpPost("{id}/sign")]
        public async Task<IActionResult> SignDocument(int id, [FromBody] SignDocumentDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

            if (document == null)
                return NotFound(new { message = "Document not found" });

            if (document.IsSigned)
                return BadRequest(new { message = "Document is already signed" });

            try
            {
                // Create signatures directory if it doesn't exist
                var signaturesFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "signatures");
                if (!Directory.Exists(signaturesFolder))
                    Directory.CreateDirectory(signaturesFolder);

                // Generate unique filename for signature
                var signatureFileName = $"signature_{id}_{DateTime.Now.Ticks}.png";
                var filePath = Path.Combine(signaturesFolder, signatureFileName);

                // Remove data:image/png;base64, prefix if present
                var base64Data = model.SignatureImageUrl;
                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }

                // Convert base64 to bytes and save
                var bytes = Convert.FromBase64String(base64Data);
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);

                // Generate URL for the signature
                var signatureUrl = $"{Request.Scheme}://{Request.Host}/signatures/{signatureFileName}";

                // Get the user who signed
                var signedBy = await _userManager.FindByIdAsync(userId);

                // Update document with signature information
                document.SignatureImageUrl = signatureUrl;
                document.IsSigned = true;
                document.SignedAt = DateTime.UtcNow;
                document.SignedByUserId = userId;

                await _context.SaveChangesAsync();

                var documentDto = await MapToDto(document);
                return Ok(new { success = true, message = "Document signed successfully", document = documentDto });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error saving signature: {ex.Message}" });
            }
        }

        private async Task<DocumentDto> MapToDto(Document document)
        {
            var owner = await _userManager.FindByIdAsync(document.OwnerId);
            var signedBy = document.SignedByUserId != null ? await _userManager.FindByIdAsync(document.SignedByUserId) : null;

            return new DocumentDto
            {
                Id = document.Id,
                Name = document.Name,
                Type = document.Type,
                Size = document.Size,
                LastModified = document.LastModified,
                Shared = document.Shared,
                Url = document.Url,
                OwnerId = document.OwnerId,
                Owner = owner != null ? await GetUserDto(owner) : null,
                SignatureImageUrl = document.SignatureImageUrl,
                IsSigned = document.IsSigned,
                SignedAt = document.SignedAt,
                SignedByUserId = document.SignedByUserId,
                SignedBy = signedBy != null ? await GetUserDto(signedBy) : null
            };
        }

        private async Task<UserDto> GetUserDto(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "";

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? "",
                Role = role,
                AvatarUrl = user.AvatarUrl,
                Bio = user.Bio,
                IsOnline = user.IsOnline,
                CreatedAt = user.CreatedAt
            };

            if (role == "entrepreneur")
            {
                var entrepreneur = await _context.Entrepreneurs.FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (entrepreneur != null)
                {
                    userDto.StartupName = entrepreneur.StartupName;
                    userDto.PitchSummary = entrepreneur.PitchSummary;
                    userDto.FundingNeeded = entrepreneur.FundingNeeded;
                    userDto.Industry = entrepreneur.Industry;
                    userDto.Location = entrepreneur.Location;
                    userDto.FoundedYear = entrepreneur.FoundedYear;
                    userDto.TeamSize = entrepreneur.TeamSize;
                }
            }
            else if (role == "investor")
            {
                var investor = await _context.Investors.FirstOrDefaultAsync(i => i.UserId == user.Id);
                if (investor != null)
                {
                    userDto.InvestmentInterests = investor.InvestmentInterests;
                    userDto.InvestmentStage = investor.InvestmentStage;
                    userDto.PortfolioCompanies = investor.PortfolioCompanies;
                    userDto.TotalInvestments = investor.TotalInvestments;
                    userDto.MinimumInvestment = investor.MinimumInvestment;
                    userDto.MaximumInvestment = investor.MaximumInvestment;
                }
            }

            return userDto;
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}