using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Common.Model.Dtos.ResponseDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Common.Domain.Services;

public interface INotificationSevice
{
    Task<ResponseMessageApi<bool>> SendNotification(SendNotificationRequest request);
    Task<MessagePagedResponseBase> GetUserNotifications(GetUserNotificationRequest request);
    Task<ResponseMessageApi<NotificationAppOutDto>> GetNotification(GetNotificationRequest request);
    Task<ResponseMessageApi<ShowNotificationDto>> GetLastNotificationRequest(GetLastNotificationRequest request);
    Task<ResponseMessageApi<bool>> SetAllNotificationsAsRead(SetAllNotificationsAsReadRequest request);
    Task<ResponseMessageApi<bool>> SetNotificationAsRead(SetNotificationAsReadRequest request);
    Task<ResponseMessageApi<bool>> DeleteNotification(DeleteNotificationRequest request);
    Task<ResponseMessageApi<bool>> SubscribeNotification(SubscribeNotificationRequest request);
    Task<ResponseMessageApi<bool>> UnSubscribeNotification(UnSubscribeNotificationRequest request);
}