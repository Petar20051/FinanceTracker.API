using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using FinanceTracker.API.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinanceTracker.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BudgetController : ControllerBase
    {
        private readonly FinanceTrackerDbContext _context;
        private  readonly NotificationService _notificationService;

        public BudgetController(FinanceTrackerDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var budgets = await _context.Budgets.Where(b => b.UserId == userId).ToListAsync();
            return Ok(budgets);
        }

        [HttpPost]
        public async Task<IActionResult> AddBudget([FromBody] Budget budget)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            budget.UserId = userId;
            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetBudgets), new { id = budget.Id }, budget);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudget(int id, [FromBody] Budget updatedBudget)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (budget == null)
                return NotFound();

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("filter")]
        public async Task<IActionResult> GetFilteredBudgets(
    [FromQuery] string category,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = _context.Budgets.Where(b => b.UserId == userId);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(b => b.Category == category);

            var totalItems = await query.CountAsync();
            var budgets = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                Data = budgets
            });
        }

        [HttpGet("report/budget-summary")]
        public async Task<IActionResult> GetBudgetSummary()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId)
                .Select(b => new
                {
                    b.Category,
                    b.Limit,
                    Spent = _context.Expenses
                        .Where(e => e.UserId == userId && e.Category == b.Category)
                        .Sum(e => e.Amount)
                })
                .ToListAsync();

            return Ok(budgets);
        }

        [HttpGet("report/budget-performance")]
        public async Task<IActionResult> GetBudgetPerformance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var performance = await _context.Budgets
                .Where(b => b.UserId == userId)
                .Select(b => new
                {
                    b.Category,
                    b.Limit,
                    Spent = _context.Expenses
                        .Where(e => e.UserId == userId && e.Category == b.Category)
                        .Sum(e => e.Amount),
                    Remaining = b.Limit - _context.Expenses
                        .Where(e => e.UserId == userId && e.Category == b.Category)
                        .Sum(e => e.Amount)
                })
                .ToListAsync();

            return Ok(performance);
        }


    }
}