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
    public class MeetingsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public MeetingsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingDto model)
        {
            try
            {
                var organizerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (organizerId == null)
                    return Unauthorized(new { message = "User not authenticated" });

                if (string.IsNullOrEmpty(model.ParticipantId))
                    return BadRequest(new { message = "Participant is required" });

                if (string.IsNullOrEmpty(model.Title))
                    return BadRequest(new { message = "Title is required" });

                if (model.EndTime <= model.StartTime)
                    return BadRequest(new { message = "End time must be after start time" });

                var startTimeUtc = model.StartTime.ToUniversalTime();
                var endTimeUtc = model.EndTime.ToUniversalTime();

                var hasConflict = await CheckConflict(organizerId, startTimeUtc, endTimeUtc);
                if (hasConflict)
                    return BadRequest(new { message = "You already have a meeting scheduled during this time" });

                var participant = await _userManager.FindByIdAsync(model.ParticipantId);
                if (participant == null)
                    return NotFound(new { message = "Participant not found" });

                var meetingUrl = $"https://meet.jit.si/meeting_{Guid.NewGuid().ToString().Substring(0, 8)}";

                var meeting = new Meeting
                {
                    OrganizerId = organizerId,
                    ParticipantId = model.ParticipantId,
                    Title = model.Title,
                    Description = model.Description ?? "",
                    StartTime = startTimeUtc,
                    EndTime = endTimeUtc,
                    Status = "pending",
                    MeetingUrl = meetingUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Meetings.Add(meeting);
                await _context.SaveChangesAsync();

                // Create notification for participant
                var organizer = await _userManager.FindByIdAsync(organizerId);
                var notificationsController = new NotificationsController(_userManager, _context);
                await notificationsController.CreateNotification(
                    model.ParticipantId,
                    "New Meeting Invitation",
                    $"{organizer?.Name} scheduled a meeting with you",
                    "meeting",
                    meeting.Id.ToString()
                );

                var meetingDto = await MapToDto(meeting);
                return Ok(new { success = true, message = "Meeting scheduled successfully", meeting = meetingDto });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating meeting: {ex.Message}");
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserMeetings()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                    return Unauthorized();

                var meetings = await _context.Meetings
                    .Include(m => m.Organizer)
                    .Include(m => m.Participant)
                    .Where(m => m.OrganizerId == userId || m.ParticipantId == userId)
                    .OrderBy(m => m.StartTime)
                    .ToListAsync();

                var meetingDtos = new List<MeetingDto>();
                foreach (var meeting in meetings)
                {
                    meetingDtos.Add(await MapToDto(meeting));
                }

                return Ok(meetingDtos);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting meetings: {ex.Message}");
                return StatusCode(500, new { message = "Failed to retrieve meetings" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMeeting(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var meeting = await _context.Meetings
                .Include(m => m.Organizer)
                .Include(m => m.Participant)
                .FirstOrDefaultAsync(m => m.Id == id && (m.OrganizerId == userId || m.ParticipantId == userId));

            if (meeting == null)
                return NotFound(new { message = "Meeting not found" });

            var meetingDto = await MapToDto(meeting);
            return Ok(meetingDto);
        }

        [HttpPut("{id}/accept")]
        public async Task<IActionResult> AcceptMeeting(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var meeting = await _context.Meetings
                .Include(m => m.Organizer)
                .FirstOrDefaultAsync(m => m.Id == id && m.ParticipantId == userId);

            if (meeting == null)
                return NotFound(new { message = "Meeting not found or you are not the participant" });

            if (meeting.Status != "pending")
                return BadRequest(new { message = $"Meeting already {meeting.Status}" });

            var hasConflict = await CheckConflict(userId, meeting.StartTime, meeting.EndTime, id);
            if (hasConflict)
                return BadRequest(new { message = "You already have another meeting scheduled during this time" });

            meeting.Status = "accepted";
            await _context.SaveChangesAsync();

            // Create notification for organizer
            var participant = await _userManager.FindByIdAsync(userId);
            var notificationsController = new NotificationsController(_userManager, _context);
            await notificationsController.CreateNotification(
                meeting.OrganizerId,
                "Meeting Accepted",
                $"{participant?.Name} accepted your meeting invitation",
                "meeting",
                meeting.Id.ToString()
            );

            return Ok(new { success = true, message = "Meeting accepted", status = meeting.Status });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectMeeting(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.Id == id && m.ParticipantId == userId);

            if (meeting == null)
                return NotFound(new { message = "Meeting not found or you are not the participant" });

            if (meeting.Status != "pending")
                return BadRequest(new { message = $"Meeting already {meeting.Status}" });

            meeting.Status = "rejected";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Meeting rejected", status = meeting.Status });
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteMeeting(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.Id == id && m.OrganizerId == userId);

            if (meeting == null)
                return NotFound(new { message = "Meeting not found or you are not the organizer" });

            meeting.Status = "completed";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Meeting marked as completed" });
        }

        [HttpPost("check-conflict")]
        public async Task<IActionResult> CheckConflict([FromBody] CheckConflictDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var startTimeUtc = model.StartTime.ToUniversalTime();
            var endTimeUtc = model.EndTime.ToUniversalTime();
            var hasConflict = await CheckConflict(userId, startTimeUtc, endTimeUtc);
            return Ok(new { hasConflict = hasConflict });
        }

        private async Task<bool> CheckConflict(string userId, DateTime startTime, DateTime endTime, int? excludeMeetingId = null)
        {
            var query = _context.Meetings
                .Where(m => (m.OrganizerId == userId || m.ParticipantId == userId)
                    && m.Status != "rejected"
                    && m.Status != "completed"
                    && ((startTime >= m.StartTime && startTime < m.EndTime)
                        || (endTime > m.StartTime && endTime <= m.EndTime)
                        || (startTime <= m.StartTime && endTime >= m.EndTime)));

            if (excludeMeetingId.HasValue)
                query = query.Where(m => m.Id != excludeMeetingId.Value);

            return await query.AnyAsync();
        }

        private async Task<MeetingDto> MapToDto(Meeting meeting)
        {
            var organizer = await _userManager.FindByIdAsync(meeting.OrganizerId);
            var participant = await _userManager.FindByIdAsync(meeting.ParticipantId);

            return new MeetingDto
            {
                Id = meeting.Id,
                OrganizerId = meeting.OrganizerId,
                ParticipantId = meeting.ParticipantId,
                Title = meeting.Title,
                Description = meeting.Description,
                StartTime = meeting.StartTime,
                EndTime = meeting.EndTime,
                Status = meeting.Status,
                MeetingUrl = meeting.MeetingUrl,
                CreatedAt = meeting.CreatedAt,
                Organizer = organizer != null ? await GetUserDto(organizer) : null,
                Participant = participant != null ? await GetUserDto(participant) : null
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