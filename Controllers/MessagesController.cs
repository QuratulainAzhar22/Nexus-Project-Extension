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
    public class MessagesController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public MessagesController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
                return Unauthorized();

            var conversations = await _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            var conversationDtos = new List<ConversationDto>();

            foreach (var otherUserId in conversations)
            {
                var otherUser = await _userManager.FindByIdAsync(otherUserId);
                if (otherUser == null) continue;

                var messages = await _context.Messages
                    .Where(m => (m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                                (m.SenderId == otherUserId && m.ReceiverId == currentUserId))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var lastMessage = messages.LastOrDefault();
                var otherUserDto = await GetUserDto(otherUser);

                conversationDtos.Add(new ConversationDto
                {
                    Id = $"conv-{currentUserId}-{otherUserId}",
                    Participants = new List<string> { currentUserId, otherUserId },
                    LastMessage = lastMessage != null ? await MapToDto(lastMessage) : null,
                    UpdatedAt = lastMessage?.Timestamp ?? DateTime.UtcNow,
                    OtherParticipant = otherUserDto
                });
            }

            return Ok(conversationDtos.OrderByDescending(c => c.UpdatedAt));
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMessagesWithUser(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
                return Unauthorized();

            var otherUser = await _userManager.FindByIdAsync(userId);
            if (otherUser == null)
                return NotFound(new { message = "User not found" });

            var messages = await _context.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                            (m.SenderId == userId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            var unreadMessages = messages.Where(m => m.ReceiverId == currentUserId && !m.IsRead).ToList();
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();

            var messageDtos = new List<MessageDto>();
            foreach (var message in messages)
            {
                messageDtos.Add(await MapToDto(message));
            }

            return Ok(messageDtos);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto model)
        {
            var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (senderId == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.Content))
                return BadRequest(new { message = "Message content cannot be empty" });

            var receiver = await _userManager.FindByIdAsync(model.ReceiverId);
            if (receiver == null)
                return NotFound(new { message = "Receiver not found" });

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = model.ReceiverId,
                Content = model.Content,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Create notification for receiver
            var sender = await _userManager.FindByIdAsync(senderId);
            var notificationsController = new NotificationsController(_userManager, _context);
            await notificationsController.CreateNotification(
                model.ReceiverId,
                "New Message",
                $"{sender?.Name} sent you a message",
                "message",
                message.Id.ToString()
            );

            var messageDto = await MapToDto(message);
            return Ok(messageDto);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
                return Unauthorized();

            var message = await _context.Messages.FindAsync(id);
            if (message == null)
                return NotFound(new { message = "Message not found" });

            if (message.ReceiverId != currentUserId)
                return BadRequest(new { message = "You can only mark messages sent to you as read" });

            message.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Message marked as read" });
        }

        [HttpGet("unread/count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
                return Unauthorized();

            var unreadCount = await _context.Messages
                .Where(m => m.ReceiverId == currentUserId && !m.IsRead)
                .CountAsync();

            return Ok(new { unreadCount });
        }

        private async Task<MessageDto> MapToDto(Message message)
        {
            var sender = await _userManager.FindByIdAsync(message.SenderId);
            var receiver = await _userManager.FindByIdAsync(message.ReceiverId);

            return new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                Timestamp = message.Timestamp,
                IsRead = message.IsRead,
                Sender = sender != null ? await GetUserDto(sender) : null,
                Receiver = receiver != null ? await GetUserDto(receiver) : null
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