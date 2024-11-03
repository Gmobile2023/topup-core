using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using HLS.Paygate.Common.Domain.Entities;
using HLS.Paygate.Common.Domain.Repositories;
using HLS.Paygate.Common.Model.Dtos;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Common.Model.Dtos.ResponseDto;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.Common.Domain.Services;

public class NotificationSevice : INotificationSevice
{
    private readonly IConfiguration _configuration;
    private readonly IDateTimeHelper _dateHepper;
    private readonly ILogger<NotificationSevice> _logger;
    private readonly ICommonMongoRepository _reportMongoRepository;

    public NotificationSevice(ICommonMongoRepository reportMongoRepository, ILogger<NotificationSevice> logger,
        IConfiguration configuration, IDateTimeHelper dateHepper)
    {
        _reportMongoRepository = reportMongoRepository;
        _logger = logger;
        _configuration = configuration;
        _dateHepper = dateHepper;
    }

    public async Task<ResponseMessageApi<bool>> SendNotification(SendNotificationRequest request)
    {
        var response = new ResponseMessageApi<bool>();
        try
        {
            Expression<Func<NotificationSubscriptions, bool>> query = p =>
                p.AccountCode == request.AccountCode && p.IsReceive;
            var listtoken = await _reportMongoRepository.GetAllAsync(query);
            var android = new List<string>();
            var ios = new List<string>();
            var web = new List<string>();
            if (string.IsNullOrEmpty(request.IconNotifi)) request.IconNotifi = "";

            var info = await InsertSysNotificaion(request);
            if (info != null)
            {
                if (listtoken != null && listtoken.Any())
                {
                    request.NotificationId = info.NotificationId;
                    request.NotifiTypeCode = info.NotifiTypeCode;
                    request.TotalUnRead = await GetTotalUnRead(request.AccountCode);
                    foreach (var item in listtoken)
                        if (item.Channel == ChannelRequest.App)
                        {
                            if (item.AppType == NotifiAppDeivceType.Android)
                                android.Add(item.Token);
                            if (item.AppType == NotifiAppDeivceType.IOS)
                                ios.Add(item.Token);
                        }
                        else if (item.Channel == ChannelRequest.Web)
                        {
                            web.Add(item.Token);
                        }

                    PushNotificationDto notifiCation;
                    if (android.Any())
                    {
                        notifiCation = getNotificationMessage(request, null, android, NotifiAppDeivceType.Android);
                        await PuslishNotification(notifiCation);
                    }

                    if (ios.Any())
                    {
                        notifiCation = getNotificationMessage(request, null, ios, NotifiAppDeivceType.IOS);
                        await PuslishNotification(notifiCation);
                    }

                    if (web.Any())
                    {
                        notifiCation = getNotificationMessage(request, null, web, NotifiAppDeivceType.Web);
                        await PuslishNotification(notifiCation);
                    }

                    response.Success = true;
                    return response;
                }

                response.Error.Message = "Chưa đăng ký thiết bị nhận thông báo";
                _logger.LogError("Device not found");
                return response;
            }

            response.Error.Message = "Tạo thông báo không thành công";
            _logger.LogError("Create notification item error");
            return response;
        }
        catch (Exception ex)
        {
            response.Error.Message = "SendNotification error";
            _logger.LogError("SendNotification error:" + ex);
            return response;
        }
    }

    public async Task<MessagePagedResponseBase> GetUserNotifications(GetUserNotificationRequest request)
    {
        Expression<Func<NotificationMesssage, bool>> query = p => p.AccountCode == request.AccountCode;

        if (request.State != null)
        {
            Expression<Func<NotificationMesssage, bool>> newQuery = p =>
                p.State == request.State;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.NotificationType))
        {
            Expression<Func<NotificationMesssage, bool>> newQuery = p =>
                p.NotificationType == request.NotificationType;
            query = query.And(newQuery);
        }

        var total = await _reportMongoRepository.CountAsync(query);
        var lst = await _reportMongoRepository.GetSortedPaginatedAsync<NotificationMesssage, Guid>(query,
            s => s.CreatedDate, false,
            request.Offset, request.Limit);
        foreach (var item in lst) item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

        return new MessagePagedResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Thành công",
            Total = (int) total,
            Payload = lst.ConvertTo<List<NotificationAppOutDto>>()
        };
    }

    public async Task<ResponseMessageApi<NotificationAppOutDto>> GetNotification(GetNotificationRequest request)
    {
        var rs = await _reportMongoRepository.GetOneAsync<NotificationMesssage>(p =>
            p.Id == request.Id && p.AccountCode == request.AccountCode);
        return new ResponseMessageApi<NotificationAppOutDto>
        {
            Success = true,
            Result = rs.ConvertTo<NotificationAppOutDto>()
        };
    }

    public async Task<ResponseMessageApi<ShowNotificationDto>> GetLastNotificationRequest(
        GetLastNotificationRequest request)
    {
        Expression<Func<NotificationMesssage, bool>> query = p =>
            p.AccountCode == request.AccountCode && p.State == (byte) UserNotificationState.Unread;
        var notifications = new List<NotificationAppOutDto>();
        var totalCount = 0;
        if (request.IsTotalOnly == null || request.IsTotalOnly == false)
        {
            if (request.State != null)
            {
                Expression<Func<NotificationMesssage, bool>> newQuery = p =>
                    p.State == request.State;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.NotificationType))
            {
                Expression<Func<NotificationMesssage, bool>> newQuery = p =>
                    p.NotificationType == request.NotificationType;
                query = query.And(newQuery);
            }

            totalCount = (int) await _reportMongoRepository.CountAsync(query);
            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<NotificationMesssage, Guid>(query,
                s => s.CreatedDate, false,
                request.Offset, request.Limit);
            notifications = lst.ConvertAll(p => p.ConvertTo<NotificationAppOutDto>());
            foreach (var item in notifications)
                item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);
        }

        return new ResponseMessageApi<ShowNotificationDto>
        {
            Success = true,
            Result = new ShowNotificationDto
            {
                LastNotification = notifications,
                Total = totalCount,
                TotalUnRead = (int) await _reportMongoRepository.CountAsync(query)
            }
        };
    }

    public async Task<ResponseMessageApi<bool>> SetAllNotificationsAsRead(SetAllNotificationsAsReadRequest request)
    {
        var resonse = new ResponseMessageApi<bool>();
        var list = await _reportMongoRepository.GetAllAsync<NotificationMesssage>(p =>
            p.AccountCode == request.AccountCode && p.State == (byte) UserNotificationState.Unread);
        foreach (var item in list)
        {
            item.State = (byte) UserNotificationState.Read;
            await _reportMongoRepository.UpdateOneAsync(item);
        }

        resonse.Success = true;
        return resonse;
    }

    public async Task<ResponseMessageApi<bool>> SetNotificationAsRead(SetNotificationAsReadRequest request)
    {
        var resonse = new ResponseMessageApi<bool>();
        var notifi = await _reportMongoRepository.GetOneAsync<NotificationMesssage>(p =>
            p.Id == request.Id && p.AccountCode == request.AccountCode);
        if (notifi == null) return resonse;
        notifi.State = (byte) UserNotificationState.Read;
        await _reportMongoRepository.UpdateOneAsync(notifi);
        resonse.Success = true;
        return resonse;
    }

    public async Task<ResponseMessageApi<bool>> DeleteNotification(DeleteNotificationRequest request)
    {
        var resonse = new ResponseMessageApi<bool>();
        var notifi = await _reportMongoRepository.GetOneAsync<NotificationMesssage>(p =>
            p.Id == request.Id && p.AccountCode == request.AccountCode);
        if (notifi == null)
            return resonse;
        notifi.State = 1;
        await _reportMongoRepository.DeleteOneAsync(notifi);
        resonse.Success = true;
        return resonse;
    }

    public async Task<ResponseMessageApi<bool>> SubscribeNotification(SubscribeNotificationRequest request)
    {
        var resonse = new ResponseMessageApi<bool>();
        try
        {
            // if (string.IsNullOrEmpty(request.Channel) ||
            //     (request.Channel != ChannelRequest.App && request.Channel != ChannelRequest.Web))
            // {
            //     resonse.Error.Message = "Chưa truyền thông tinh Channel";
            //     return resonse;
            // }

            // var deviceType = "";
            // if (request.Channel == Channel.APP)
            // {
            //     deviceType = request.AppVersion.Split('-')[0];
            //     if (deviceType != NotifiAppDeivceType.Android && deviceType != NotifiAppDeivceType.IOS)
            //     {
            //         resonse.Error.Message = "Thiết bị không hỗ trợ";
            //         return resonse;
            //     }
            // }

            var checkExsit = await IsSubcriptionByToken(request.Token, request.AccountCode);
            if (checkExsit)
            {
                resonse.Error.Message = "Token đã tồn tại";
                return resonse;
            }

            //Clear hết token cũ theo channel
            var checkSub = await GetTokenAccountChannel(request.AccountCode, request.Channel);
            if (checkSub != null) await _reportMongoRepository.DeleteManyAsync(checkSub);

            var tokenItem = new NotificationSubscriptions
            {
                CreatedDate = DateTime.Now,
                DeviceName = request.DeviceName,
                AppVersion = request.AppVersion,
                DeviceVersion = request.DeviceVersion,
                ScreenSize = request.ScreenSize,
                Location = request.Location,
                Token = request.Token,
                AccountCode = request.AccountCode,
                TenantId = request.TenantId,
                AppType = request.DeviceType,
                Channel = request.Channel.ToString("G"),
                IsReceive = true
            };
            //await AddTopic(request.Token);
            await RegisterDevice(tokenItem);
            resonse.Success = true;
            return resonse;
        }
        catch (Exception ex)
        {
            return resonse;
        }
    }

    public async Task<ResponseMessageApi<bool>> UnSubscribeNotification(UnSubscribeNotificationRequest request)
    {
        var resonse = new ResponseMessageApi<bool>();
        var notifi = await _reportMongoRepository.GetOneAsync<NotificationSubscriptions>(p =>p.Token == request.Token);
        if (notifi == null) resonse.Error.Message = "Device not found";

        await _reportMongoRepository.DeleteOneAsync(notifi);
        resonse.Success = true;
        return resonse;
    }

    public Task<bool> UnSubscribeNotificationAccount(
        UnSubscribeAcountNotificationRequest request)
    {
        throw new NotImplementedException();
    }

    private async Task<int> GetTotalUnRead(string accountcode)
    {
        Expression<Func<NotificationMesssage, bool>> query = p =>
            p.AccountCode == accountcode && p.State == (byte) UserNotificationState.Unread;
        return (int) await _reportMongoRepository.CountAsync(query);
    }

    private async Task<CreateNotifiOutDto> InsertSysNotificaion(SendNotificationRequest request)
    {
        try
        {
            var notifiItem = new Notifications
            {
                NotificationType = request.NotifiTypeCode,
                AccountCode = request.AccountCode,
                NotificationName = request.NotifiTypeCode,
                CreatedDate = DateTime.UtcNow,
                TenantId = request.TenanId,
                AppNotificationName = request.AppNotificationName
            };
            await _reportMongoRepository.AddOneAsync(notifiItem);
            var notifiCationUser = new NotificationMesssage
            {
                NotificationType = request.NotifiTypeCode,
                Body = request.Body,
                Data = request.Data,
                Title = request.Title,
                State = (byte) UserNotificationState.Unread,
                TenantId = request.TenanId,
                AccountCode = request.AccountCode,
                Severity = request.Severity,
                CreatedDate = DateTime.UtcNow,
                AppNotificationName = request.AppNotificationName
            };
            await _reportMongoRepository.AddOneAsync(notifiCationUser);
            return new CreateNotifiOutDto
            {
                NotificationId = notifiItem.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("SaveNotification to FCM error:" + ex);
            return null;
        }
    }

    private PushNotificationDto getNotificationMessage(SendNotificationRequest data, string regId,
        List<string> registrationIds,
        string appType)
    {
        var dataNotifi = data.Data.FromJson<object>();
        var notifi = new PushNotificationDto
        {
            notification = new NotificationMessageDto
            {
                title = data.Title,
                body = data.Body,
                sound = "sampleaudio"
            },
            data = dataNotifi
        };
        if (regId != null)
            notifi.to = regId;
        if (registrationIds != null)
            notifi.registration_ids = registrationIds;

        if (appType == NotifiAppDeivceType.Android)
        {
            //to do something
        }

        if (appType == NotifiAppDeivceType.IOS)
        {
            //to do something
        }

        return notifi;
    }

    private async Task PuslishNotification(PushNotificationDto notification)
    {
        try
        {
            _logger.LogInformation($"PuslishNotification :{notification}");
            var server = _configuration["FcmConfig:ServerUrl"];
            var key = _configuration["FcmConfig:ServerKey"];
            var client = new HttpClient
            {
                BaseAddress = new Uri(server)
            };
            var request = new StringContent(notification.ToJson(), Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "key=" + key);
            var response = await client.PostAsync("/fcm/send", request);
            _logger.LogInformation($"PuslishNotification response:{response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var rs = result.FromJson<FcmResultDto>();
                _logger.LogInformation($"PuslishNotification return:{rs.ToJson()}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("PuslishNotification to FCM error:" + ex);
        }
    }

    private async Task<bool> IsSubcriptionByToken(string token, string accountcode)
    {
        var check = await _reportMongoRepository.GetOneAsync<NotificationSubscriptions>(p =>
            p.Token == token && p.AccountCode == accountcode);
        if (check != null)
            return true;
        return false;
    }

    private async Task<bool> RegisterDevice(NotificationSubscriptions token)
    {
        try
        {
            await _reportMongoRepository.AddOneAsync(token);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private async Task<List<NotificationSubscriptions>> GetTokenAccountChannel(string accountCode, Channel channel)
    {
        var subscription = await _reportMongoRepository.GetAllAsync<NotificationSubscriptions>(p =>
            p.Channel == channel.ToString("G") && p.AccountCode == accountCode);
        return subscription;
    }
}