using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ApplicationLayer;

[Authorize]
public sealed class NotificationHub : Hub
{
    public const string UserGroupPrefix = "user:";
    public const string AdministratorsGroup = "role:Admin";

    public override async Task OnConnectedAsync()
    {
        // UploadPublisher targets customer groups by the authenticated
        // username. Keep the SignalR group key consistent with the API's
        // UserId resolution instead of using the internal JWT subject ID.
        var userId = Context.User?.Identity?.Name ?? Context.User?.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(userId)) await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupPrefix + userId);
        if (Context.User?.IsInRole("Admin") == true) await Groups.AddToGroupAsync(Context.ConnectionId, AdministratorsGroup);
        await base.OnConnectedAsync();
    }
}
