using FinanceTracker.API.Banking;
using FinanceTracker.API.Data;
using FinanceTracker.API.ML;
using FinanceTracker.API.Models;
using FinanceTracker.API.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;

namespace FinanceTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExpenseController : ControllerBase
    {
        private readonly FinanceTrackerDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly MLModelHelper _mlHelper;
        private readonly BankingService _bankingService;
        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly HttpClient _httpClient;
        private readonly SaltEdgeService _saltEdgeService;

        public ExpenseController(
            FinanceTrackerDbContext context,
            NotificationService notificationService,
            CurrencyExchangeService currencyExchangeService,
            BankingService bankingService,
            HttpClient httpClient,
            SaltEdgeService saltEdgeService)
        {
            _context = context;
            _notificationService = notificationService;
            _mlHelper = new MLModelHelper(context);
            _currencyExchangeService = currencyExchangeService;
            _bankingService = bankingService;
            _httpClient = httpClient;
            _saltEdgeService = saltEdgeService;
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
        public async Task<IActionResult> GetExpenses()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                Console.WriteLine("Unauthorized access - UserId is null.");
                return Unauthorized(new { Message = "User not authenticated." });
            }

            Console.WriteLine($"Fetching expenses for UserId: {userId}");

            var expenses = await _context.Expenses
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            if (!expenses.Any())
            {
                Console.WriteLine("No expenses found for this user.");
                return Ok(new { Message = "No expenses available." });
            }

            Console.WriteLine($"Found {expenses.Count} expenses for UserId: {userId}");
            return Ok(expenses);
        }



        // Add a new expense
        [HttpPost]
        public async Task<IActionResult> AddExpense([FromBody] Expense expense)
        {
            // Validate the incoming model
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            // Extract UserId from the token
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            // Assign user ID and default the date if not provided
            expense.UserId = userId;
            expense.Date = expense.Date == DateTime.MinValue ? DateTime.UtcNow : expense.Date;

            // Add the expense to the database
            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            // Budget notifications
            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in budget notifications: {ex.Message}");
            }

            // Return the created expense
            return CreatedAtAction(nameof(GetExpenses), new { id = expense.Id }, expense);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] Expense updatedExpense)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound(new { Message = "Expense not found." });

            // Update the expense fields
            expense.Category = updatedExpense.Category;
            expense.Amount = updatedExpense.Amount;
            expense.Description = updatedExpense.Description;
            expense.Date = updatedExpense.Date == DateTime.MinValue
                ? DateTime.UtcNow
                : updatedExpense.Date;

            _context.Expenses.Update(expense);
            await _context.SaveChangesAsync();

            return Ok(expense); // Return the updated expense
        }


        // Delete an expense
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();

            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (expense == null) return NotFound();

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        [HttpGet("report/category-summary")]
        public async Task<IActionResult> GetCategorySummary(
     [FromQuery] DateTime? startDate,
     [FromQuery] DateTime? endDate)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            try
            {
               
                var query = _context.Expenses.Where(e => e.UserId == userId);

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
            catch (Exception ex)
            {
                
                Console.Error.WriteLine($"Error in GetCategorySummary: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }


        [HttpGet("report/monthly-trends")]
        public async Task<IActionResult> GetMonthlyExpenseTrends()
        {
            Console.WriteLine("GetMonthlyExpenseTrends called");

            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            try
            {
                var trends = await _context.Expenses
                    .Where(e => e.UserId == userId && e.Date != null)
                    .GroupBy(e => new { e.Date.Year, e.Date.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Amount = g.Sum(e => e.Amount)
                    })
                    .OrderBy(g => g.Year)
                    .ThenBy(g => g.Month)
                    .ToListAsync();

                return Ok(trends.Select(t => new
                {
                    Month = $"{t.Year}-{t.Month:00}",
                    Amount = t.Amount
                }));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetMonthlyExpenseTrends: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }






        [HttpPost("convert")]
        public async Task<IActionResult> ConvertCurrency([FromBody] ConversionRequest request)
        {
            try
            {
                // Validate the request to ensure all required fields are provided
                if (string.IsNullOrWhiteSpace(request.FromCurrency) ||
                    string.IsNullOrWhiteSpace(request.ToCurrency) ||
                    request.Amount <= 0)
                {
                    return BadRequest(new { Message = "Invalid input. Please provide valid currencies and a positive amount." });
                }

                // Perform the currency conversion
                var convertedAmount = await _currencyExchangeService.ConvertCurrency(request.FromCurrency, request.ToCurrency, request.Amount);

                // Return the converted amount in a structured response
                return Ok(new { ConvertedAmount = convertedAmount });
            }
            catch (Exception ex)
            {
                // Log the error details for debugging purposes
                Console.WriteLine($"Error in ConvertCurrency: {ex.Message}");
                return BadRequest(new { Message = $"Currency conversion failed: {ex.Message}" });
            }
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
            if (request == null || string.IsNullOrWhiteSpace(request.UserToken))
            {
                return BadRequest(new { Message = "Connection ID must be provided to sync transactions." });
            }

            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();

            try
            {
                
                var transactions = await _bankingService.GetTransactions(request.UserToken);

                foreach (var transaction in transactions)
                {
                    
                    if (!_context.Expenses.Any(e => e.UserId == userId && e.Description == transaction.Description && e.Date == transaction.Date && e.Amount == transaction.Amount))
                    {
                        var expense = new Expense
                        {
                            UserId = userId,
                            Description = transaction.Description,
                            Category = transaction.Category,
                            Amount = transaction.Amount,
                            Date = transaction.Date
                        };

                        _context.Expenses.Add(expense);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Transactions synced successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing transactions: {ex.Message}");
                return StatusCode(500, new { Message = $"Error syncing transactions: {ex.Message}" });
            }
        }



        [HttpGet("initiate-connect")]
        public async Task<IActionResult> InitiateConnect()
        {
            try
            {
                var connectUrl = await _saltEdgeService.GenerateConnectUrlAsync();
                return Ok(new { connectUrl });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating Salt Edge Connect URL: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }



        [HttpPost("predict-category")]
        public IActionResult PredictCategory([FromBody] PredictionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { Message = "Description is required." });
            }

            try
            {
                var userId = GetUserIdFromToken();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Message = "User is not authorized." });
                }

                // Load user-specific expense data
                var expenseData = _context.Expenses
                    .Where(e => e.UserId == userId)
                    .Select(e => new ExpenseData
                    {
                        Category = e.Category,
                        Description = e.Description,
                        TotalAmount = (float)e.Amount,
                        UserSpecificWeight = 1.0f // Default weight
                    }).ToList();

                if (!expenseData.Any())
                {
                    return NotFound(new { Message = "No training data available for this user." });
                }

                // Train the model and predict
                var model = _mlHelper.TrainCategoryPredictionModel(expenseData);
                var predictedCategory = _mlHelper.PredictCategory(model, request.Description);

                return Ok(new { PredictedCategory = predictedCategory });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Internal server error: {ex.Message} - {ex.StackTrace}");
                return StatusCode(500, new { Message = "Internal server error.", Details = ex.Message });
            }
        }
        [HttpPost("predict-next-month-expense")]
        public async Task<IActionResult> PredictNextMonthExpense([FromBody] CategoryRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Category))
            {
                return BadRequest(new { message = "The category field is required." });
            }

            try
            {
                var userId = GetUserIdFromToken();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "User is not authorized." });
                }

                Console.WriteLine($"Received category: {request.Category}");

                // Call the async method to predict the expense
                var predictedAmount = await _mlHelper.PredictNextMonthExpenseForCategoryAsync(request.Category, userId);

                Console.WriteLine($"Predicted amount: {predictedAmount}");

                return Ok(new { predictedAmount });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PredictNextMonthExpense: {ex.Message}");
                return StatusCode(500);
            }
        }





        // Nested classes for request bodies

        public class QueryDetails
        {
            public string From { get; set; }
            public string To { get; set; }
            public decimal Amount { get; set; }
        }

        public class CategoryRequest
        {
            public string Category { get; set; }
        }
        public class RateInfo
        {
            public decimal Rate { get; set; }
        }


        public class SyncRequest
        {
            public string UserToken { get; set; }
        }

        public class PredictionRequest
        {
            public string Description { get; set; } // Already exists
            //public string Category { get; set; } // For predicting the next month's expense for a specific category
            //public string UserId { get; set; } // Add this property
        }

    }
}
