using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using FinanceTracker.API.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BudgetController : ControllerBase
    {
        private readonly FinanceTrackerDbContext _context;
        private readonly NotificationService _notificationService;

        public BudgetController(FinanceTrackerDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        
        private string GetUserIdFromToken()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", string.Empty);
            if (string.IsNullOrEmpty(token)) return null;

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            return jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId)
                .Select(b => new
                {
                    b.Id,
                    b.Category,
                    b.Limit,
                    b.Spent
                })
                .ToListAsync();

            if (!budgets.Any())
                return Ok(new { Message = "No budgets available." });

            return Ok(new { Data = budgets });
        }



        [HttpPost]
        public async Task<IActionResult> AddBudget([FromBody] Budget budget)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserIdFromToken(); 
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            budget.UserId = userId; 

            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBudgets), new { id = budget.Id }, budget);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudget(int id, [FromBody] Budget updatedBudget)
        {
            var userId = GetUserIdFromToken(); 
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null)
                return NotFound();

            budget.Category = updatedBudget.Category;
            budget.Limit = updatedBudget.Limit;
            budget.Spent = updatedBudget.Spent;

            _context.Budgets.Update(budget);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var userId = GetUserIdFromToken(); 
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null)
                return NotFound();

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("filter")]
        public async Task<IActionResult> GetFilteredBudgets([FromQuery] string category)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { Message = "User not authenticated." });

            try
            {
                var query = _context.Budgets.Where(b => b.UserId == userId);

                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(b => b.Category.ToLower().Contains(category.ToLower()));
                }

                var budgets = await query
                    .Select(b => new
                    {
                        b.Id,
                        b.Category,
                        b.Limit,
                        b.Spent
                    })
                    .ToListAsync();

                return Ok(budgets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFilteredBudgets: {ex.Message}");
                return StatusCode(500, new { Message = "An internal server error occurred.", Details = ex.Message });
            }
        }

        [HttpGet("report/budget-summary")]
        public async Task<IActionResult> GetBudgetSummary()
        {
            var userId = GetUserIdFromToken(); 
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId)
                .Select(b => new
                {
                    b.Id,
                    b.Category,
                    b.Limit,
                    b.Spent  
                })
                .ToListAsync();

            return Ok(budgets);
        }

        [HttpGet("report/budget-performance")]
        public async Task<IActionResult> GetBudgetPerformance()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var performance = await _context.Budgets
                .Where(b => b.UserId == userId)
                .Select(b => new
                {
                    b.Id,
                    b.Category,
                    b.Limit,
                    Spent = _context.Expenses
                        .Where(e => e.UserId == userId && e.Category == b.Category)
                        .Sum(e => e.Amount),
                    Remaining = b.Limit - b.Spent
                })
                .ToListAsync();

            return Ok(performance);
        }
    }
}
