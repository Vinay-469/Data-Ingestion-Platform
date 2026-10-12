using CoreModels.DTOs;

namespace ApplicationLayer.Interfaces;

public interface INotificationPublisher
{
    Task PublishToUserAsync(string userId, NotificationMessage notification, Token token = default);
    Task PublishToAdministratorsAsync(NotificationMessage notification, Token token = default);
}
