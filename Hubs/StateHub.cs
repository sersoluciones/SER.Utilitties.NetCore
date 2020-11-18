using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Hubs
{
    [Authorize]
    public class StateHub : Hub
    {
        #region Fields
        private readonly ILogger _logger;

        #endregion

        public StateHub(
           ILogger<StateHub> logger)
        {
            _logger = logger;
        }

        public Task SendMessageToGroup(string groupName, string message)
        {
            return Clients.Group(groupName).SendAsync("Send", $"{Context.ConnectionId}: {message}");
        }

        public async Task AddToGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            await Clients.Group(groupName).SendAsync("Send", $"{Context.ConnectionId} has joined the group {groupName}.");
        }

        public async Task RemoveFromGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            await Clients.Group(groupName).SendAsync("Send", $"{Context.ConnectionId} has left the group {groupName}.");
        }

        #region HubMethods
        public Task SendMessage(string user, string message)
        {
            return Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public Task SendToOtherMessage(string user, string message)
        {
            return Clients.Others.SendAsync("ReceiveMessage", user, message);
        }

        public Task SendMessageToCaller(string message)
        {
            return Clients.Caller.SendAsync("ReceiveMessage", message);
        }

        #endregion

        #region HubMethodName
        [HubMethodName("SendMessageToUser")]
        public Task DirectMessage(string user, string message)
        {
            return Clients.User(user).SendAsync("ReceiveMessage", message);
        }
        #endregion

        #region OnConnectedAsync
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"OnConnectedAsync USER: {Context.User.Identity.Name} {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }
        #endregion

        #region OnDisconnectedAsync
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            Console.WriteLine($"OnDisconnectedAsync USER: {Context.User.Identity.Name} {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
        #endregion
    }
}
