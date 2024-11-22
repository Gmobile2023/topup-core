using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Backend.Interface.Connectors;
using MassTransit;
using MongoDB.Bson;
using NLog;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class LevelDiscountConsumer : IConsumer<TopupCommandDone>
    {
        private readonly ISaleService _saleService;
        private readonly ExternalServiceConnector _externalServiceConnector;
        private readonly Logger _logger = LogManager.GetLogger("LevelDiscountConsumer");

        public LevelDiscountConsumer(ISaleService saleService, ExternalServiceConnector externalServiceConnector)
        {
            _saleService = saleService;
            _externalServiceConnector = externalServiceConnector;
        }

        public async Task Consume(ConsumeContext<TopupCommandDone> context)
        {
            try
            {
                var saleRequest = context.Message.SaleRequest;

                _logger.LogInformation("Received saleRequestDone: " + saleRequest.ToJson());
                //Chỗ này xem phương án gọi sang api bên tính trả về danh sách những thằng dc cộng lãi chiết khấu. Bên này chỉ thực hiện payment theo list đó

                var transAccountInfos =
                    await _externalServiceConnector.AccountGetInfoByCodeAsync(saleRequest.PartnerCode);

                if (transAccountInfos != null && transAccountInfos.AccountType == SystemAccountType.Agent)
                {
                    var treePath = transAccountInfos.TreePath;
                    _logger.LogInformation($"Account {saleRequest.PartnerCode}, treePath: {treePath}");
                    var treePathSeparated = treePath.Split('-');
                    if (treePathSeparated.Length > 0)
                    {
                        var levelToCheckDiscount = await _externalServiceConnector.LevelToCheckDiscountGetAsync();

                        var previousDiscountRate = saleRequest.DiscountRate;

                        _logger.LogInformation($"TransAccount {saleRequest.PartnerCode} discountRate {previousDiscountRate}");

                        //Bỏ thằng công ty và chính nó.
                        var listPay = treePathSeparated.Where(x => x != transAccountInfos.AccountCode).ToList();
                        if (listPay.Count > 0)
                            listPay.RemoveAt(0);
                        if (listPay.Count > 0)
                        {
                            //Duyệt cây
                            var countPay = levelToCheckDiscount > 0 && listPay.Count > levelToCheckDiscount
                                ? levelToCheckDiscount
                                : listPay.Count;
                            listPay.Reverse();
                            for (var i = 0; i < countPay; i++)
                            {
                                var parentCode = listPay[i];
                                var parentAccountInfos =
                                    await _externalServiceConnector.AccountGetInfoByCodeAsync(parentCode);

                                if (parentAccountInfos == null)
                                    continue;

                                var levelDiscountPolicy =
                                    await _externalServiceConnector.DiscountPolicyGetAsync(parentCode,
                                        saleRequest.CategoryCode,
                                        saleRequest.ProcessedAmount); //lấy theo số tiền ghép được

                                if (levelDiscountPolicy != null)
                                {
                                    var levelDiscount = levelDiscountPolicy.DiscountValue - previousDiscountRate;

                                    //Gán phần CK trước đó lại để tính cho level tiếp theo.
                                    previousDiscountRate = levelDiscountPolicy.DiscountValue;
                                    _logger.LogInformation($"TransAccount {parentCode} discountRate {previousDiscountRate}");
                                    var levelDiscountRecord = new LevelDiscountRecordDto
                                    {
                                        Level = parentAccountInfos.NetworkLevel,
                                        AccountCode = parentCode,
                                        TransAccount = saleRequest.PartnerCode,
                                        CreatedDate = DateTime.Now,
                                        TransAmount = saleRequest.Amount,
                                        TransRef = saleRequest.TransCode,
                                        PartnerTransCode = saleRequest.TransRef,
                                        TransDate = saleRequest.CreatedTime,
                                        LevelDiscountRate = levelDiscount,
                                        LevelDiscountAmount = levelDiscount * saleRequest.Amount / 100,
                                        TransDiscountAmount = saleRequest.Amount * saleRequest.DiscountRate / 100,
                                        TransDiscountRate = saleRequest.DiscountRate,
                                        LevelDiscountPolicyId = levelDiscountPolicy.DiscountId,
                                        Status = LevelDiscountStatus.Init,
                                        RefUserName = transAccountInfos.UserName,
                                        RefPhone = transAccountInfos.PhoneNumber
                                    };

                                    await _saleService.LevelDiscountRecordInsertAsync(levelDiscountRecord);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation("LevelDiscountConsumer error: " + e);
            }
        }
    }
}
