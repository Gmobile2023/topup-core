using System.Threading.Tasks;
using Topup.Common.Model.Dtos.RequestDto;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.Common.Interface.Services;

public partial class CommonService
{
    public async Task<object> PostAsync(SendNotificationRequest request)
    {
        _logger.LogInformation($"SendNotificationRequest:{request.ToJson()}");
        return await _notification.SendNotification(request);
    }

    public async Task<object> GetAsync(GetUserNotificationRequest request)
    {
        return await _notification.GetUserNotifications(request);
    }

    public async Task<object> GetAsync(GetNotificationRequest request)
    {
        return await _notification.GetNotification(request);
    }

    public async Task<object> GetAsync(GetLastNotificationRequest request)
    {
        return await _notification.GetLastNotificationRequest(request);
    }

    public async Task<object> PostAsync(SetAllNotificationsAsReadRequest request)
    {
        return await _notification.SetAllNotificationsAsRead(request);
    }

    public async Task<object> PostAsync(SetNotificationAsReadRequest request)
    {
        return await _notification.SetNotificationAsRead(request);
    }

    public async Task<object> DeleteAsync(DeleteNotificationRequest request)
    {
        return await _notification.DeleteNotification(request);
    }

    public async Task<object> PostAsync(SubscribeNotificationRequest request)
    {
        return await _notification.SubscribeNotification(request);
    }

    public async Task<object> PostAsync(UnSubscribeNotificationRequest request)
    {
        return await _notification.UnSubscribeNotification(request);
    }
}