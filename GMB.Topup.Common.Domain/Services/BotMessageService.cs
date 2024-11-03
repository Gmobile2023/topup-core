using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GMB.Topup.Common.Model.Dtos;
using GMB.Topup.Shared.ConfigDtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GMB.Topup.Contracts.Requests.Commons;
using ServiceStack;

namespace GMB.Topup.Common.Domain.Services
{
    public class BotMessageService : IBotMessageService
    {
        private readonly IConfiguration _configuration;

        private readonly BotConfig _botConfig;
        private readonly ILogger<BotMessageService> _logger;

        public BotMessageService(IConfiguration configuration, ILogger<BotMessageService> logger)
        {
            _configuration = configuration;
            _botConfig = new BotConfig();
            configuration.GetSection("BotConfig").Bind(_botConfig);
            _logger = logger;
        }

        public async Task<bool> SendAlarmMessage(SendAlarmMessageInput input)
        {
            try
            {
                //K gửi cảnh báo này
                if (input.Title.Contains("Thông báo biến động số dư do hoàn tiền lỗi giao dịch"))
                    return true;
                _logger.LogInformation($"SendAlarmMessage request: {input.ToJson()}");
                var chatId = _botConfig.DefaultChatId;
                if (input.ChatId < 0)
                    chatId = input.ChatId;
                else
                {
                    if (_botConfig.ChatIds != null && _botConfig.ChatIds.Any())
                    {
                        var getChatId = _botConfig.ChatIds.Find(x => x.BotType == input.BotType.ToString("G"))
                            ?.ChatId;
                        if (getChatId != null)
                            chatId = (int)getChatId;
                    }
                }

                var message = GetAlarmMessage(input);
                var response = await SendMessage(message, chatId, _botConfig.Token);
                _logger.LogInformation($"SendAlarmMessage return:{response}");
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"SendAlarmMessage error:{e}");
                return false;
            }
        }

        private string GetAlarmMessage(SendAlarmMessageInput request)
        {
            //var info = $"[{request.Module}]-[{(request.MessageType == BotMessageType.Wraning ? "WARNING" : request.MessageType.ToString("G").ToUpper())}]";
            var level =
                $"[{(request.MessageType == BotMessageType.Wraning ? "WARNING" : request.MessageType.ToString("G").ToUpper())}]";
            var info = $"[{request.Module}] - {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            var icon = request.MessageType switch
            {
                BotMessageType.Message => '✅',
                BotMessageType.Wraning => '⚠',
                _ => '❌'
            };
            var newSms = $"{icon} {request.Title}\n{info}\n{request.Message}";
            return newSms;
        }


        private async Task<bool> CallApi(string message, long chatId, string token)
        {
            try
            {
                _logger.LogInformation($"SendMessage: {chatId}-{token}-{message}");
                var response = await $"https://api.telegram.org/{token}/sendMessage"
                    .PostToUrlAsync($"chat_id={chatId}&text={message}").ConfigureAwait(false);
                _logger.LogInformation($"SendMessage:{response}");
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"SendMessage_Error:{e}");
                return false;
            }
        }

        private async Task<bool> SendMessage(string message, long chatId, string token)
        {
            try
            {
                _logger.LogInformation($"SendMessage request:{chatId}");
                var retry = 0;
                var response = await CallApi(message, chatId, token);
                while (!response && retry < 3)
                {
                    retry++;
                    response = await CallApi(message, chatId, token);
                    _logger.LogInformation($"SendMessage return:{response}-Retry:{retry}");
                    if (response)
                        break;
                }

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"SendMessage_Error:{e}");
                return false;
            }
        }
    }
}