using GMB.Topup.Shared.CacheManager;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.TopupGw.Contacts.Dtos;
using System.Linq.Expressions;
using ServiceStack;
using GMB.Topup.Contracts.Commands.Backend;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Contracts.Commands.Commons;
using GMB.Topup.Shared.UniqueIdGenerator;
using Microsoft.Extensions.Configuration;
using System.Threading;
using GMB.Topup.TopupGw.Domains.Entities;
using GMB.Topup.TopupGw.Domains.Repositories;

namespace GMB.Topup.TopupGw.Domains.BusinessServices
{
    public class BackgroundServices
    {
        private readonly IBusControl _bus;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<BackgroundServices> _logger;
        private readonly ITransRepository _transRepository;
        private readonly ITransCodeGenerator _transCodeGenerator;
        private readonly IConfiguration _configuation;

        public BackgroundServices(IBusControl bus, ICacheManager cacheManager, ILogger<BackgroundServices> logger, ITransRepository transRepository, ITransCodeGenerator transCodeGenerator, IConfiguration configuation)
        {
            _bus = bus;
            _cacheManager = cacheManager;
            _logger = logger;
            _transRepository = transRepository;
            _transCodeGenerator = transCodeGenerator;
            _configuation = configuation;
        }

        public async Task StartJob()
        {
            int randomNumber = new Random().Next(0, 100000000);
            int randomTimeout = new Random().Next(0, 20);

            _logger.LogInformation("Start job");
            await _cacheManager.ClearCache("PayGate:BackgroundJob:AutoCloseProvider");
            await _cacheManager.AddEntity<int>("PayGate:BackgroundJob:AutoCloseProvider", randomNumber);

            Thread.Sleep(TimeSpan.FromSeconds(randomTimeout));
           
            //Check chi 1 node chay job
            if (bool.Parse(_configuation["BackgroundJob:AutoCloseProvider:IsEnable"]))
            {
                var cacheNum = await _cacheManager.GetEntity<int>("PayGate:BackgroundJob:AutoCloseProvider");
                int interval = int.Parse(_configuation["BackgroundJob:AutoCloseProvider:Interval"]);
              
                while (true && cacheNum == randomNumber)
                {
                    cacheNum = await _cacheManager.GetEntity<int>("PayGate:BackgroundJob:AutoCloseProvider");
                    CheckAutoLockProviders();
                    Thread.Sleep(TimeSpan.FromSeconds(interval));
                }
            }

        }
        public async Task CheckAutoLockProviders()
        {
            var providers = await _cacheManager.GetAllEntityByPattern<ProviderInfoDto>("PayGate_ProviderInfo:Items:*");

            foreach (var provider in providers)
            {
                _ = Task.Run(() =>
                {
                    CheckAutoClose(provider);
                });

            }

        }

        private async Task CheckAutoClose(ProviderInfoDto provider)
        {
            try
            {
                _logger.LogInformation($"CheckAutoClose {provider.ProviderCode} - TimeScan: {provider.TimeScan} TotalTransScan: {provider.TotalTransScan}  TotalTransError: {provider.TotalTransErrorScan} TotalTimeout: {provider.TotalTransDubious}");

                if (provider.TotalTransErrorScan > 0 || provider.TotalTransDubious > 0)
                {
                    Expression<Func<TopupRequestLog, bool>> query = p => p.ProviderCode == provider.ProviderCode;

                    var items = await _transRepository.GetSortedPaginatedAsync<TopupRequestLog, Guid>(
                                      query,
                                      s => s.AddedAtUtc, false, 0, provider.TotalTransScan);
                    _logger.LogInformation($"CheckAutoClose {provider.ProviderCode} Total Items {items.Count}");

                    items = items.Where(p => p.AddedAtUtc > DateTime.UtcNow.AddMinutes(-1 * provider.TimeScan)).ToList();
                    var lastLock = await _cacheManager.GetEntity<DateTime?>($"PayGate:BackgroundJob:AutoCloseProvider:LastLock:{provider.ProviderCode}");
                    if (lastLock != null)
                    {
                        Console.WriteLine(lastLock);
                        items = items.Where(p => p.AddedAtUtc > lastLock).ToList();
                    }
                    var totalError = items.Count(p => p.Status == GMB.Topup.TopupGw.Contacts.Enums.TransRequestStatus.Fail);
                    var totalTimeout = items.Count(p => p.Status == GMB.Topup.TopupGw.Contacts.Enums.TransRequestStatus.Timeout);

                    _logger.LogInformation($"CheckAutoClose {provider.ProviderCode} ==>{totalError} {totalTimeout}");
                    if ((totalError > provider.TotalTransErrorScan && provider.TotalTransErrorScan>0) || (totalTimeout > provider.TotalTransDubious&& provider.TotalTransDubious>0))
                    {
                        _logger.LogInformation($"SetAutoClose:{provider.ProviderCode}");
                        //Lock provider
                        await _bus.Publish<LockProviderCommand>(new
                        {
                            CorrelationId = Guid.NewGuid(),
                            provider.ProviderCode,
                            provider.TimeClose
                        });
                        //ResetAuto
                        await _transCodeGenerator.ResetAutoCloseIndex(provider.ProviderCode);
                     await _cacheManager.AddEntity<DateTime?>($"PayGate:BackgroundJob:AutoCloseProvider:LastLock:{provider.ProviderCode}", DateTime.UtcNow);
                        await _bus.Publish<SendBotMessage>(new
                        {
                            MessageType = BotMessageType.Wraning,
                            BotType = BotType.Channel,
                            Module = "TopupGw",
                            Title =
                                $"Kênh:{provider.ProviderCode} đóng tự động",
                            Message =
                                $"NCC {provider.ProviderCode} sẽ đóng tự động.\n" +
                                $"Số lượng GD không thành công :{totalError}\n" +
                                  $"Số lượng GD Teimout :{totalTimeout}\n" +
                                $"Thời gian đóng:{provider.TimeClose} phút",
                            TimeStamp = DateTime.Now,
                            CorrelationId = Guid.NewGuid()
                        });
                    }
                }


            }
            catch (Exception e)
            {
                _logger.LogError(e, $"CheckAutoClose {provider.ProviderCode} - {e.Message}");

            }




        }
    }

}
