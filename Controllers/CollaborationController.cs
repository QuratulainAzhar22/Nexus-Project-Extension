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
    public class CollaborationController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CollaborationController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("requests")]
        public async Task<IActionResult> CreateRequest([FromBody] CreateCollaborationRequestDto model)
        {
            var investorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (investorId == null)
                return Unauthorized();

            var investor = await _userManager.FindByIdAsync(investorId);
            if (investor == null || !await _userManager.IsInRoleAsync(investor, "investor"))
                return BadRequest(new { message = "Only investors can send collaboration requests" });

            var entrepreneur = await _userManager.FindByIdAsync(model.EntrepreneurId);
            if (entrepreneur == null || !await _userManager.IsInRoleAsync(entrepreneur, "entrepreneur"))
                return BadRequest(new { message = "Entrepreneur not found" });

            var existingRequest = await _context.CollaborationRequests
                .FirstOrDefaultAsync(r => r.InvestorId == investorId && r.EntrepreneurId == model.EntrepreneurId);

            if (existingRequest != null)
                return BadRequest(new { message = "Collaboration request already sent" });

            var request = new CollaborationRequest
            {
                InvestorId = investorId,
                EntrepreneurId = model.EntrepreneurId,
                Message = model.Message,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.CollaborationRequests.Add(request);
            await _context.SaveChangesAsync();

            // Create notification for entrepreneur
            var notificationsController = new NotificationsController(_userManager, _context);
            await notificationsController.CreateNotification(
                model.EntrepreneurId,
                "New Collaboration Request",
                $"{investor.Name} wants to collaborate with you",
                "collaboration",
                request.Id.ToString()
            );

            return Ok(new { success = true, message = "Collaboration request sent", requestId = request.Id });
        }

        [HttpGet("requests/entrepreneur")]
        public async Task<IActionResult> GetRequestsForEntrepreneur()
        {
            var entrepreneurId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (entrepreneurId == null)
                return Unauthorized();

            var requests = await _context.CollaborationRequests
                .Include(r => r.Investor)
                .Where(r => r.EntrepreneurId == entrepreneurId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var requestDtos = new List<CollaborationRequestDto>();
            foreach (var request in requests)
            {
                requestDtos.Add(await MapToDto(request));
            }

            return Ok(requestDtos);
        }

        [HttpGet("requests/investor")]
        public async Task<IActionResult> GetRequestsFromInvestor()
        {
            var investorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (investorId == null)
                return Unauthorized();

            var requests = await _context.CollaborationRequests
                .Include(r => r.Entrepreneur)
                .Where(r => r.InvestorId == investorId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var requestDtos = new List<CollaborationRequestDto>();
            foreach (var request in requests)
            {
                requestDtos.Add(await MapToDto(request));
            }

            return Ok(requestDtos);
        }

        [HttpPut("requests/{id}/status")]
        public async Task<IActionResult> UpdateRequestStatus(int id, [FromBody] UpdateRequestStatusDto model)
        {
            var entrepreneurId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (entrepreneurId == null)
                return Unauthorized();

            var request = await _context.CollaborationRequests
                .Include(r => r.Investor)
                .FirstOrDefaultAsync(r => r.Id == id && r.EntrepreneurId == entrepreneurId);

            if (request == null)
                return NotFound(new { message = "Request not found" });

            if (model.Status != "accepted" && model.Status != "rejected")
                return BadRequest(new { message = "Status must be 'accepted' or 'rejected'" });

            request.Status = model.Status;
            await _context.SaveChangesAsync();

            // Create notification for investor when accepted
            if (model.Status == "accepted")
            {
                var entrepreneur = await _userManager.FindByIdAsync(entrepreneurId);
                var notificationsController = new NotificationsController(_userManager, _context);
                await notificationsController.CreateNotification(
                    request.InvestorId,
                    "Collaboration Request Accepted",
                    $"{entrepreneur?.Name} accepted your collaboration request",
                    "collaboration",
                    request.Id.ToString()
                );
            }

            return Ok(new { success = true, message = $"Request {model.Status}", status = request.Status });
        }

        private async Task<CollaborationRequestDto> MapToDto(CollaborationRequest request)
        {
            var investor = await _userManager.FindByIdAsync(request.InvestorId);
            var entrepreneur = await _userManager.FindByIdAsync(request.EntrepreneurId);

            var investorDto = investor != null ? await GetUserDto(investor) : null;
            var entrepreneurDto = entrepreneur != null ? await GetUserDto(entrepreneur) : null;

            return new CollaborationRequestDto
            {
                Id = request.Id.ToString(),
                InvestorId = request.InvestorId,
                EntrepreneurId = request.EntrepreneurId,
                Message = request.Message,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                Investor = investorDto,
                Entrepreneur = entrepreneurDto
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
    }
}