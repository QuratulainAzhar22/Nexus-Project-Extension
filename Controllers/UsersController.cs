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
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: api/users/me
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var userDto = await GetUserDto(user);
            return Ok(userDto);
        }

        // GET: api/users/entrepreneurs
        [HttpGet("entrepreneurs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllEntrepreneurs()
        {
            var entrepreneurs = await _userManager.GetUsersInRoleAsync("entrepreneur");
            var entrepreneurDtos = new List<UserDto>();

            foreach (var user in entrepreneurs)
            {
                entrepreneurDtos.Add(await GetUserDto(user));
            }

            return Ok(entrepreneurDtos);
        }

        // GET: api/users/investors
        [HttpGet("investors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllInvestors()
        {
            var investors = await _userManager.GetUsersInRoleAsync("investor");
            var investorDtos = new List<UserDto>();

            foreach (var user in investors)
            {
                investorDtos.Add(await GetUserDto(user));
            }

            return Ok(investorDtos);
        }

        // GET: api/users/entrepreneurs/{id}
        [HttpGet("entrepreneurs/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEntrepreneurById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await _userManager.IsInRoleAsync(user, "entrepreneur"))
                return NotFound(new { message = "Entrepreneur not found" });

            var userDto = await GetUserDto(user);
            return Ok(userDto);
        }

        // GET: api/users/investors/{id}
        [HttpGet("investors/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInvestorById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await _userManager.IsInRoleAsync(user, "investor"))
                return NotFound(new { message = "Investor not found" });

            var userDto = await GetUserDto(user);
            return Ok(userDto);
        }

        // PUT: api/users/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Update basic info
            user.Name = model.Name;
            user.Bio = model.Bio;
            if (!string.IsNullOrEmpty(model.AvatarUrl))
                user.AvatarUrl = model.AvatarUrl;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(updateResult.Errors);

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == "entrepreneur")
            {
                var entrepreneur = await _context.Entrepreneurs.FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (entrepreneur != null)
                {
                    if (!string.IsNullOrEmpty(model.StartupName)) entrepreneur.StartupName = model.StartupName;
                    if (!string.IsNullOrEmpty(model.PitchSummary)) entrepreneur.PitchSummary = model.PitchSummary;
                    if (!string.IsNullOrEmpty(model.FundingNeeded)) entrepreneur.FundingNeeded = model.FundingNeeded;
                    if (!string.IsNullOrEmpty(model.Industry)) entrepreneur.Industry = model.Industry;
                    if (!string.IsNullOrEmpty(model.Location)) entrepreneur.Location = model.Location;
                    if (model.FoundedYear.HasValue) entrepreneur.FoundedYear = model.FoundedYear.Value;
                    if (model.TeamSize.HasValue) entrepreneur.TeamSize = model.TeamSize.Value;

                    _context.Entrepreneurs.Update(entrepreneur);
                }
            }
            else if (role == "investor")
            {
                var investor = await _context.Investors.FirstOrDefaultAsync(i => i.UserId == user.Id);
                if (investor != null)
                {
                    if (model.InvestmentInterests != null) investor.InvestmentInterests = model.InvestmentInterests;
                    if (model.InvestmentStage != null) investor.InvestmentStage = model.InvestmentStage;
                    if (model.PortfolioCompanies != null) investor.PortfolioCompanies = model.PortfolioCompanies;
                    if (model.TotalInvestments.HasValue) investor.TotalInvestments = model.TotalInvestments.Value;
                    if (!string.IsNullOrEmpty(model.MinimumInvestment)) investor.MinimumInvestment = model.MinimumInvestment;
                    if (!string.IsNullOrEmpty(model.MaximumInvestment)) investor.MaximumInvestment = model.MaximumInvestment;

                    _context.Investors.Update(investor);
                }
            }

            await _context.SaveChangesAsync();

            var userDto = await GetUserDto(user);
            return Ok(userDto);
        }

        // GET: api/users/search?q=keyword
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchUsers([FromQuery] string q)
        {
            if (string.IsNullOrEmpty(q))
                return Ok(new List<UserDto>());

            var users = await _userManager.Users
                .Where(u => u.Name.Contains(q) || (u.Email != null && u.Email.Contains(q)))
                .ToListAsync();

            var userDtos = new List<UserDto>();
            foreach (var user in users)
            {
                userDtos.Add(await GetUserDto(user));
            }

            return Ok(userDtos);
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