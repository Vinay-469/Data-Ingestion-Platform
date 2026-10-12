using ApplicationLayer;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BusinessLayer.Services;

public sealed class NotificationPublisher(IHubContext<NotificationHub> hub) : INotificationPublisher
{
    public Task PublishToUserAsync(string userId, NotificationMessage notification, Token token = default) => hub.Clients.Group(NotificationHub.UserGroupPrefix + userId).SendAsync("notification", notification, token);
    public Task PublishToAdministratorsAsync(NotificationMessage notification, Token token = default) => hub.Clients.Group(NotificationHub.AdministratorsGroup).SendAsync("notification", notification, token);
}
