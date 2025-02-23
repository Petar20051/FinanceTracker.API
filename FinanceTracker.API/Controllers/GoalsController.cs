using FinanceTracker.API.Data;
using FinanceTracker.API.ML;
using FinanceTracker.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FinanceTracker.API.Controllers
{
   
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController : ControllerBase
    {
        private readonly FinanceTrackerDbContext _context;

        public GoalsController(FinanceTrackerDbContext context)
        {
            _context = context;
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
        public async Task<IActionResult> GetGoals()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var now = DateTime.UtcNow;
            var goals = await _context.Goals
                .Where(g => g.UserId == userId && g.Deadline >= now.AddDays(-3)) 
                .Select(g => new {
                    g.Id,
                    g.Title,
                    g.Category,
                    g.TargetAmount,
                    g.Deadline,
                    Progress = _context.Expenses
                        .Where(e => e.UserId == userId
                                    && e.Category.ToLower() == g.Category.ToLower()
                                    && e.Date >= now
                                    && e.Date <= g.Deadline)
                        .Sum(e => e.Amount)
                })
                .ToListAsync();

            return Ok(goals);
        }





        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] Goal goal)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            goal.UserId = userId;
            goal.CurrentProgress = 0;
            goal.IsAchieved = false;

            _context.Goals.Add(goal);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetGoals), new { id = goal.Id }, goal);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGoal(int id, [FromBody] Goal updatedGoal)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null)
                return NotFound();

            goal.Title = updatedGoal.Title;
            goal.Category = updatedGoal.Category;
            goal.TargetAmount = updatedGoal.TargetAmount;
            goal.Deadline = updatedGoal.Deadline;

            _context.Goals.Update(goal);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null)
                return NotFound();

            _context.Goals.Remove(goal);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetGoalSuggestions()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { Message = "User not authenticated." });

            try
            {
                var expenses = await _context.Expenses
                    .Where(e => e.UserId == userId)
                    .GroupBy(e => e.Category)
                    .Select(g => new PredictionData
                    {
                        Category = g.Key,
                        TotalAmount = (float)g.Sum(e => e.Amount),
                        IsRecurring = g.Count() > 3 ? 1.0f : 0.0f
                    })
                    .ToListAsync();

                if (!expenses.Any())
                    return Ok(new List<Goal>()); 

                var goalHelper = new GoalSuggestionHelper();
                var model = goalHelper.TrainGoalSuggestionModel(expenses);

                var futureData = expenses.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Category));
                if (futureData == null)
                    return Ok(new List<Goal>()); 

                var predictedCategory = goalHelper.PredictGoalCategory(model, futureData);

                var suggestedGoal = new Goal
                {
                    Title = $"Save for {predictedCategory} expenses",
                    Category = predictedCategory,
                    TargetAmount = (decimal)(futureData.TotalAmount * 1.2f),
                    Deadline = DateTime.UtcNow.AddMonths(1)
                };

                
                return Ok(new List<Goal> { suggestedGoal });
            }
            catch (Exception ex)
            {
               
                Console.Error.WriteLine($"Error in GetGoalSuggestions: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }


    }
}
