﻿using FinanceTracker.API.Banking;
using FinanceTracker.API.Data;
using FinanceTracker.API.ML;
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
    public class ExpenseController : ControllerBase
    {
        private readonly FinanceTrackerDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly MLModelHelper _mlHelper;
        private readonly BankingService _bankingService;
        private readonly CurrencyExchangeService _currencyExchangeService;

        public ExpenseController(FinanceTrackerDbContext context, NotificationService notificationService, CurrencyExchangeService currencyExchangeService, BankingService bankingService)
        {
            _context = context;
            _notificationService = notificationService;
            _mlHelper = new MLModelHelper(context);
            _currencyExchangeService = currencyExchangeService;
            _bankingService = bankingService;
        }

        [HttpGet]
        public async Task<IActionResult> GetExpenses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var expenses = await _context.Expenses.Where(e => e.UserId == userId).ToListAsync();
            return Ok(expenses);
        }

        [HttpPost]
        public async Task<IActionResult> AddExpense([FromBody] Expense expense)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            expense.UserId = userId;

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            var budgets = await _context.Budgets.Where(b => b.UserId == userId).ToListAsync();
            foreach (var budget in budgets)
            {
                var spent = _context.Expenses
                    .Where(e => e.UserId == userId && e.Category == budget.Category)
                    .Sum(e => e.Amount);

                if (spent > (decimal)0.8 * budget.Limit)
                {
                    await _notificationService.NotifyUserAsync(userId,
                        $"You have spent more than 80% of your budget for {budget.Category}.");
                }
            }

            return CreatedAtAction(nameof(GetExpenses), new { id = expense.Id }, expense);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] Expense updatedExpense)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            expense.Amount = updatedExpense.Amount;
            expense.Category = updatedExpense.Category;
            expense.Description = updatedExpense.Description;
            expense.Date = updatedExpense.Date;

            _context.Expenses.Update(expense);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("filter")]
        public async Task<IActionResult> GetFilteredExpenses(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string category,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = _context.Expenses.Where(e => e.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(e => e.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.Date <= endDate.Value);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(e => e.Category == category);

            var totalItems = await query.CountAsync();
            var expenses = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                Data = expenses
            });
        }

        [HttpGet("report/category-summary")]
        public async Task<IActionResult> GetCategorySummary(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Expenses.Where(e => e.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(e => e.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.Date <= endDate.Value);

            var categorySummary = await query
                .GroupBy(e => e.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    TotalAmount = g.Sum(e => e.Amount)
                })
                .ToListAsync();

            return Ok(categorySummary);
        }

        [HttpGet("report/monthly-trends")]
        public async Task<IActionResult> GetMonthlyTrends()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var trends = await _context.Expenses
                .Where(e => e.UserId == userId)
                .GroupBy(e => new { e.Date.Year, e.Date.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalAmount = g.Sum(e => e.Amount)
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            return Ok(trends);
        }

        [HttpGet("ai/predict-expenses")]
        public async Task<IActionResult> PredictExpenses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var historicalData = await _mlHelper.PrepareHistoricalData(userId);

            if (historicalData.Count < 3)
                return BadRequest("Insufficient data for predictions. At least 3 months of data is required.");

            var model = _mlHelper.TrainModel(historicalData);

            var userTotalIncome = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.TotalIncome)
                .FirstOrDefaultAsync();

            var futureData = Enumerable.Range(1, 3).Select(i => new ExpenseData
            {
                Income = (float)(userTotalIncome),
                Category = "General",
                IsHolidaySeason = _mlHelper.IsHolidaySeason(DateTime.UtcNow.AddMonths(i)),
                UserSpecificWeight = _mlHelper.CalculateUserSpecificWeight(userId, "General")
            });

            var predictions = _mlHelper.PredictExpenses(model, 3, futureData);

            var result = predictions.Select((amount, index) => new
            {
                Year = DateTime.UtcNow.AddMonths(index + 1).Year,
                Month = DateTime.UtcNow.AddMonths(index + 1).Month,
                PredictedAmount = amount
            });

            return Ok(result);
        }

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertExpense([FromBody] ConversionRequest request)
        {
            var convertedAmount = await _currencyExchangeService.ConvertCurrency(request.FromCurrency, request.ToCurrency, request.Amount);
            return Ok(new { ConvertedAmount = convertedAmount });
        }

        public class ConversionRequest
        {
            public string FromCurrency { get; set; }
            public string ToCurrency { get; set; }
            public decimal Amount { get; set; }
        }

        [HttpPost("sync-transactions")]
        public async Task<IActionResult> SyncTransactions([FromBody] SyncRequest request)
        {
            var transactions = await _bankingService.GetTransactions(request.UserToken);

            foreach (var transaction in transactions)
            {
                var expense = new Expense
                {
                    UserId = request.UserId,
                    Description = transaction.Description,
                    Category = transaction.Category,
                    Amount = transaction.Amount,
                    Date = transaction.Date
                };

                _context.Expenses.Add(expense);
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Transactions synced successfully." });
        }

        public class SyncRequest
        {
            public string UserToken { get; set; }
            public string UserId { get; set; }
        }
        [HttpPost("predict-category")]
        public IActionResult PredictCategory([FromBody] PredictionRequest request)
        {
            var mlHelper = _mlHelper;
            var model = mlHelper.TrainCategoryPredictionModel(_context.Expenses.Select(e => new ExpenseData
            {
                Category = e.Category,
                Description = e.Description
            }));

            var predictedCategory = mlHelper.PredictCategory(model, request.Description);
            return Ok(new { PredictedCategory = predictedCategory });
        }

        public class PredictionRequest
        {
            public string Description { get; set; }
        }

    }
}