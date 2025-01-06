using FinanceTracker.API.SignalR;
using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

public class MonthlySummaryService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer _timer;

    public MonthlySummaryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(async _ => await ProcessMonthlySummary(), null, TimeSpan.Zero, TimeSpan.FromDays(1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async Task ProcessMonthlySummary()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var users = await dbContext.Users.ToListAsync();
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            foreach (var user in users)
            {
                var totalExpenses = dbContext.Expenses
                    .Where(e => e.UserId == user.Id && e.Date >= startOfMonth)
                    .Sum(e => e.Amount);

                var notification = new Notification
                {
                    UserId = user.Id,
                    Message = $"You have spent a total of {totalExpenses:C} in {DateTime.UtcNow:MMMM yyyy}.",
                    CreatedAt = DateTime.UtcNow
                    
                };

                dbContext.Notifications.Add(notification);

                
                await notificationService.NotifyUserAsync(user.Id, notification.Message);
            }

            await dbContext.SaveChangesAsync();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
