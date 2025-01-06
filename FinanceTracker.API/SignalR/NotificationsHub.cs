using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker.API.SignalR
{
    public class NotificationsHub: Hub
    {
        public async Task SendNotification(string userId, string message)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", message);
        }
    }
}
