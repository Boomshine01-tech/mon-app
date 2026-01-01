using Microsoft.AspNetCore.SignalR;

namespace SmartNest.Server.Hubs
{
    public class RealtimeHub : Hub
    {
        private readonly ILogger<RealtimeHub> _logger;

        public RealtimeHub(ILogger<RealtimeHub> logger)
        {
            _logger = logger;
        }

        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation($"✅ Client {Context.ConnectionId} joined group {userId}");
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"✅ Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"❌ Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}