using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus_backend.Data;
using Nexus_backend.DTOs;
using Nexus_backend.Models;
using Stripe;
using System.Security.Claims;

namespace Nexus_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public PaymentsController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        [HttpGet("config")]
        public IActionResult GetPaymentConfig()
        {
            return Ok(new
            {
                publishableKey = _configuration["Stripe:PublishableKey"],
                currency = "usd"
            });
        }

        [HttpPost("create-payment-intent")]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] DepositDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            if (model.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0" });

            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(model.Amount * 100),
                    Currency = "usd",
                    PaymentMethodTypes = new List<string> { "card" },
                    Description = $"Deposit to Nexus Platform - User: {userId}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "user_id", userId },
                        { "type", "deposit" }
                    }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                var transaction = new Transaction
                {
                    UserId = userId,
                    Amount = model.Amount,
                    Type = "deposit",
                    Status = "pending",
                    Reference = paymentIntent.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                return Ok(new PaymentIntentResponseDto
                {
                    ClientSecret = paymentIntent.ClientSecret,
                    PaymentIntentId = paymentIntent.Id
                });
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("confirm-deposit")]
        public async Task<IActionResult> ConfirmDeposit([FromBody] string paymentIntentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Reference == paymentIntentId && t.UserId == userId);

            if (transaction == null)
                return NotFound(new { message = "Transaction not found" });

            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                if (paymentIntent.Status == "succeeded")
                {
                    transaction.Status = "completed";
                    await _context.SaveChangesAsync();

                    return Ok(new { success = true, message = "Deposit confirmed successfully", amount = transaction.Amount });
                }
                else
                {
                    transaction.Status = "failed";
                    await _context.SaveChangesAsync();

                    return BadRequest(new { message = $"Payment status: {paymentIntent.Status}" });
                }
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            if (model.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0" });

            var balance = await GetUserBalance(userId);

            if (balance < model.Amount)
                return BadRequest(new { message = "Insufficient balance" });

            var transaction = new Transaction
            {
                UserId = userId,
                Amount = -model.Amount,
                Type = "withdraw",
                Status = "completed",
                Reference = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Withdrawal processed successfully", amount = model.Amount, newBalance = balance - model.Amount });
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferDto model)
        {
            var fromUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (fromUserId == null)
                return Unauthorized();

            if (model.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0" });

            var toUser = await _userManager.FindByIdAsync(model.ToUserId);
            if (toUser == null)
                return NotFound(new { message = "Recipient not found" });

            if (fromUserId == model.ToUserId)
                return BadRequest(new { message = "Cannot transfer to yourself" });

            var balance = await GetUserBalance(fromUserId);

            if (balance < model.Amount)
                return BadRequest(new { message = "Insufficient balance" });

            var fromTransaction = new Transaction
            {
                UserId = fromUserId,
                Amount = -model.Amount,
                Type = "transfer",
                Status = "completed",
                Reference = Guid.NewGuid().ToString(),
                ToUserId = model.ToUserId,
                CreatedAt = DateTime.UtcNow
            };

            var toTransaction = new Transaction
            {
                UserId = model.ToUserId,
                Amount = model.Amount,
                Type = "transfer",
                Status = "completed",
                Reference = fromTransaction.Reference,
                ToUserId = fromUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(fromTransaction);
            _context.Transactions.Add(toTransaction);
            await _context.SaveChangesAsync();

            // Create notification for recipient
            var fromUser = await _userManager.FindByIdAsync(fromUserId);
            var notificationsController = new NotificationsController(_userManager, _context);
            await notificationsController.CreateNotification(
                model.ToUserId,
                "Payment Received",
                $"You received ${model.Amount} from {fromUser?.Name}",
                "payment",
                toTransaction.Id.ToString()
            );

            return Ok(new { success = true, message = $"Transferred ${model.Amount} to {toUser.Name}", newBalance = balance - model.Amount });
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var balance = await GetUserBalance(userId);
            return Ok(new { balance = balance });
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactionHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var transactions = await _context.Transactions
                .Include(t => t.User)
                .Include(t => t.ToUser)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var transactionDtos = new List<PaymentDto>();
            foreach (var transaction in transactions)
            {
                transactionDtos.Add(await MapToDto(transaction));
            }

            return Ok(transactionDtos);
        }

        private async Task<decimal> GetUserBalance(string userId)
        {
            var completedTransactions = await _context.Transactions
                .Where(t => t.UserId == userId && t.Status == "completed")
                .ToListAsync();

            return completedTransactions.Sum(t => t.Amount);
        }

        private async Task<PaymentDto> MapToDto(Transaction transaction)
        {
            var user = await _userManager.FindByIdAsync(transaction.UserId);
            var toUser = transaction.ToUserId != null ? await _userManager.FindByIdAsync(transaction.ToUserId) : null;

            return new PaymentDto
            {
                Id = transaction.Id,
                UserId = transaction.UserId,
                Amount = transaction.Amount,
                Type = transaction.Type,
                Status = transaction.Status,
                Reference = transaction.Reference,
                ToUserId = transaction.ToUserId,
                CreatedAt = transaction.CreatedAt,
                User = user != null ? await GetUserDto(user) : null,
                ToUser = toUser != null ? await GetUserDto(toUser) : null
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