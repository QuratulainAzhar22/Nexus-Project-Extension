using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus_backend.Data;
using Nexus_backend.DTOs;
using Nexus_backend.Helpers;
using Nexus_backend.Models;
using Nexus_backend.Services;
using System.Security.Claims;

namespace Nexus_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly JwtHelper _jwtHelper;
        private readonly IEmailService _emailService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            JwtHelper jwtHelper,
            IEmailService emailService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _jwtHelper = jwtHelper;
            _emailService = emailService;
        }

      
        // LOGIN
        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid model" });

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password" });

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!isPasswordValid)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password" });

            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault() ?? "";

            // Check if role matches the selected role in login
            if (userRole.ToLower() != model.Role.ToLower())
                return Unauthorized(new AuthResponseDto { Success = false, Message = $"You are registered as {userRole}, not as {model.Role}" });

            // Update online status
            user.IsOnline = true;
            await _userManager.UpdateAsync(user);

            var token = _jwtHelper.GenerateToken(user.Id, user.Email!, userRole);
            var userDto = await GetUserDto(user);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = userDto
            });
        }

        // ==============================================
        // REGISTER
        // ==============================================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid model" });

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Email already exists" });

            // Create user
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name,
                Bio = "",
                AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(model.Name)}&background=random",
                IsOnline = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new AuthResponseDto { Success = false, Message = errors });
            }

            // Ensure role exists
            var roleName = model.Role.ToLower();
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));

            await _userManager.AddToRoleAsync(user, roleName);

            // Create role-specific profile
            if (roleName == "entrepreneur")
            {
                var entrepreneur = new Entrepreneur
                {
                    UserId = user.Id,
                    StartupName = "",
                    PitchSummary = "",
                    FundingNeeded = "",
                    Industry = "",
                    Location = "",
                    FoundedYear = DateTime.UtcNow.Year,
                    TeamSize = 1
                };
                _context.Entrepreneurs.Add(entrepreneur);
            }
            else if (roleName == "investor")
            {
                var investor = new Investor
                {
                    UserId = user.Id,
                    InvestmentInterests = new List<string>(),
                    InvestmentStage = new List<string>(),
                    PortfolioCompanies = new List<string>(),
                    TotalInvestments = 0,
                    MinimumInvestment = "",
                    MaximumInvestment = ""
                };
                _context.Investors.Add(investor);
            }

            await _context.SaveChangesAsync();

            var token = _jwtHelper.GenerateToken(user.Id, user.Email!, roleName);
            var userDto = await GetUserDto(user);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = "Registration successful",
                Token = token,
                User = userDto
            });
        }

        // ==============================================
        // LOGOUT
        // ==============================================
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsOnline = false;
                await _userManager.UpdateAsync(user);
            }
            return Ok(new { Success = true, Message = "Logged out successfully" });
        }

        // ==============================================
        // GET USER BY ID
        // ==============================================
        [HttpGet("user/{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { Success = false, Message = "User not found" });

            var userDto = await GetUserDto(user);
            return Ok(userDto);
        }

        // ==============================================
        // GET CURRENT USER
        // ==============================================
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { Success = false, Message = "User not found" });

            var userDto = await GetUserDto(user);
            return Ok(userDto);
        }

        // ==============================================
        // SEND OTP (For Password Reset / 2FA)
        // ==============================================
        [HttpPost("send-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> SendOtp([FromBody] ForgotPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that user doesn't exist
                return Ok(new { success = true, message = "If your email is registered, you will receive an OTP" });
            }

            var otp = await _emailService.GenerateOtpAsync(user.Id);
            var subject = "Your Nexus Platform OTP";
            var body = $@"
                <h2>Nexus Platform - One Time Password</h2>
                <p>Your OTP for password reset is: <strong>{otp}</strong></p>
                <p>This OTP will expire in 10 minutes.</p>
                <p>If you did not request this, please ignore this email.</p>
            ";

            await _emailService.SendEmailAsync(user.Email!, subject, body);

            return Ok(new { success = true, message = "OTP sent to your email" });
        }

        // ==============================================
        // VERIFY OTP
        // ==============================================
        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return BadRequest(new { success = false, message = "Invalid request" });
            }

            var isValid = _emailService.VerifyOtp(user.Id, model.Otp);

            if (!isValid)
            {
                return BadRequest(new { success = false, message = "Invalid or expired OTP" });
            }

            // Generate a temporary token for password reset
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            return Ok(new
            {
                success = true,
                message = "OTP verified successfully",
                resetToken = resetToken,
                email = user.Email
            });
        }

        // ==============================================
        // FORGOT PASSWORD (Legacy - use send-otp instead)
        // ==============================================
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return Ok(new { success = true, message = "If your email is registered, you will receive reset instructions" });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // In production, send email with token
            return Ok(new { success = true, message = "Reset token generated", token = token, email = user.Email });
        }

        // ==============================================
        // RESET PASSWORD (With Token)
        // ==============================================
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Token) || string.IsNullOrEmpty(model.NewPassword))
            {
                return BadRequest(new { success = false, message = "Email, token, and new password are required" });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return BadRequest(new { success = false, message = "Invalid request" });
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Ok(new { success = true, message = "Password reset successfully" });
        }

        // ==============================================
        // CHANGE PASSWORD (While Logged In)
        // ==============================================
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found" });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Ok(new { success = true, message = "Password changed successfully" });
        }

        // ==============================================
        // ENABLE/DISABLE 2FA
        // ==============================================
        [HttpPost("enable-2fa")]
        [Authorize]
        public async Task<IActionResult> Enable2FA([FromBody] Enable2FADto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found" });

            var result = await _userManager.SetTwoFactorEnabledAsync(user, model.Enable);

            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, message = "Failed to update 2FA setting" });
            }

            return Ok(new { success = true, message = model.Enable ? "2FA enabled" : "2FA disabled" });
        }

        // ==============================================
        // HELPER METHOD: Get User DTO
        // ==============================================
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