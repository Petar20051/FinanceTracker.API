namespace FinanceTracker.API.Models
{
    public class Goal
    {
            public int Id { get; set; }
            public string UserId { get; set; }
            public ApplicationUser User { get; set; }
            public string Title { get; set; }
            public string Category { get; set; }
            public decimal TargetAmount { get; set; }
            public decimal CurrentProgress { get; set; }
            public DateTime Deadline { get; set; }
            public bool IsAchieved { get; set; }
        

    }
}
