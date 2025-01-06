using FinanceTracker.API.Data;
using FinanceTracker.API.ML;
using FinanceTracker.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinanceTracker.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController : ControllerBase
    {
        private readonly FinanceTrackerDbContext _context;

        public GoalsController(FinanceTrackerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetGoals()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var goals = await _context.Goals.Where(g => g.UserId == userId).ToListAsync();
            return Ok(goals);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] Goal goal)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            
            var expenses = await _context.Expenses
                .Where(e => e.UserId == userId)
                .GroupBy(e => e.Category)
                .Select(g => new PredictionData
                {
                    Category = g.Key,
                    TotalAmount = (float)g.Sum(e => e.Amount),
                    IsRecurring = g.Count() > 3 
                })
                .ToListAsync();

            
            var goalHelper = new GoalSuggestionHelper();
            var model = goalHelper.TrainGoalSuggestionModel(expenses);

            
            var futureData = expenses.Select(e => new PredictionData
            {
                Category = e.Category,
                TotalAmount = e.TotalAmount * 1.1f, 
                IsRecurring = e.IsRecurring
            });

            
            var predictedCategories = goalHelper.PredictGoalCategories(model, futureData);

            
            var suggestedGoals = predictedCategories.Select(category => new
            {
                Title = $"Save for {category} expenses",
                Category = category,
                TargetAmount = expenses.FirstOrDefault(e => e.Category == category)?.TotalAmount * 1.2f ?? 1000,
                Deadline = DateTime.UtcNow.AddMonths(6)
            });

            return Ok(suggestedGoals);
        }

    }


}
