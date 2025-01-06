using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker.API.SignalR
{
    public class NotificationService
    {
        private readonly FinanceTrackerDbContext _context;
        private readonly IHubContext<NotificationsHub> _hubContext;

        public NotificationService(FinanceTrackerDbContext context, IHubContext<NotificationsHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task NotifyUserAsync(string userId, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", message);
        }
    }
}
