using System;
using Topup.Common.Model.Dtos;
using Microsoft.Extensions.Configuration;
using Topup.Common.Model.Dtos;

namespace Topup.Common.Interface.Configs;

public static class SendMailStockMinInventoryConfig
{
    public static SendMailStockMinInventoryDto GetSendMailStockMinInventoryConfig(this IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var config = new SendMailStockMinInventoryDto
        {
            EmailReceive = configuration.GetValue<string>("EmailConfig:SendEmailLimitMinInventory:EmailReceive"),
            IsSendMail = configuration.GetValue<bool>("EmailConfig:SendEmailLimitMinInventory:IsSendMail"),
            SendCount = configuration.GetValue<int>("EmailConfig:SendEmailLimitMinInventory:SendCount"),
            TimeReSend = configuration.GetValue<int>("EmailConfig:SendEmailLimitMinInventory:TimeReSend"),
            IsBotMessage = configuration.GetValue<bool>("EmailConfig:SendEmailLimitMinInventory:IsBotMessage")
        };

        return config;
    }
}