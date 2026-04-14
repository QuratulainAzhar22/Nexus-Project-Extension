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
    public class NotificationsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public NotificationsController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: api/notifications
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var notificationDtos = new List<NotificationDto>();
            foreach (var notification in notifications)
            {
                notificationDtos.Add(await MapToDto(notification));
            }

            return Ok(notificationDtos);
        }

        // GET: api/notifications/unread/count
        [HttpGet("unread/count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var unreadCount = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();

            return Ok(new { unreadCount });
        }

        // PUT: api/notifications/{id}/read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
                return NotFound(new { message = "Notification not found" });

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // PUT: api/notifications/mark-all-read
        [HttpPut("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // Helper method to create notification
        public async Task CreateNotification(string userId, string title, string message, string type, string referenceId = "")
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        private async Task<NotificationDto> MapToDto(Notification notification)
        {
            var user = await _userManager.FindByIdAsync(notification.UserId);

            return new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                ReferenceId = notification.ReferenceId,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                User = user != null ? await GetUserDto(user) : null,
                UserName = user?.Name,
                UserAvatar = user?.AvatarUrl
            };
        }

        private async Task<UserDto> GetUserDto(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "";

            return new UserDto
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
        }
    }
}