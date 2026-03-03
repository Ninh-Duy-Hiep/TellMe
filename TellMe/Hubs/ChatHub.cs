using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace TellMe.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _connectedUsers = new();

        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            _connectedUsers.TryAdd(connectionId, connectionId);

            await Clients.Others.SendAsync("UserConnected", connectionId);
            
            Console.WriteLine($"[SignalR] Client connected: {connectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            _connectedUsers.TryRemove(connectionId, out _);

            await Clients.Others.SendAsync("UserDisconnected", connectionId);
            
            Console.WriteLine($"[SignalR] Client disconnected: {connectionId}");
            await base.OnDisconnectedAsync(exception);
        }

    }
}
