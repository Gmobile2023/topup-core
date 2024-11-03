
using GMB.Topup.Report.Model.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack;
using GMB.Topup.Shared;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using Microsoft.Extensions.Logging;
using GMB.Topup.Contracts.Commands.Commissions;
using System.Linq.Expressions;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Contracts.Commands.Commons;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Gw.Model.Events;

namespace GMB.Topup.Report.Domain.Services
{
    public partial class BalanceReportService
    {
        /// <summary>
        /// Xử lý giao dịch thanh toán
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task ReportSaleIntMessage(ReportSaleMessage message)
        {
            try
            {
                Console.WriteLine(
                    $"Input_Message: {message.TransCode} => Next|Status: {message.NextStep}|{message.Status}");
                var item = await _reportMongoRepository.GetReportItemByTransCode(message.TransCode);
                if (item == null)
                {
                    item = await ConvertInfoSale(message);
                    if (item == null)
                        return;
                    await AddItemReport(item);
                    if (item.Status == ReportStatus.Error)
                        await ReportRefundInfoError(item);
                }
                else
                    await UpdateReportQueue(message, item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportSaleIntMessage {message.TransCode} => Error: {ex}");
                //await _bus.Publish(message);
                await SendAlarm(message.TransCode, ex.Message);
                // await _reportMongoRepository.InsertWaringInfo(new ReportItemWarning()
                // {
                //     TransType = ReportServiceCode.TOPUP,
                //     TransCode = message.TransCode,
                //     Message = message.ToJson()
                // });
            }
        }

        private async Task AddItemReport(ReportItemDetail item)
        {
            try
            {
                await _reportMongoRepository.AddOneAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{item.TransCode} AddItemReport_Item => Error: {ex}");
                await SendAlarm(item.TransCode, ex.Message);
            }
        }

        /// <summary>
        /// Update trạng thái và nhà cung cấp
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task ReportStatusMessage(ReportTransStatusMessage request)
        {
            try
            {
                var item = await _reportMongoRepository.GetReportItemByTransCode(request.TransCode);
                if (item != null)
                {
                    item.Status = request.Status == 1 ? ReportStatus.Success :
                        request.Status == 3 ? ReportStatus.Error : ReportStatus.TimeOut;

                    if (!string.IsNullOrEmpty(request.PayTransRef) && item.PayTransRef != request.PayTransRef)
                        item.PayTransRef = request.PayTransRef;
                    
                    if (!string.IsNullOrEmpty(request.ReceiverTypeResponse) && item.ProviderReceiverType != request.ReceiverTypeResponse)
                        item.ProviderReceiverType = request.ReceiverTypeResponse;

                    if (!string.IsNullOrEmpty(request.ProviderResponseTransCode) && item.ProviderTransCode != request.ProviderResponseTransCode)
                        item.ProviderTransCode = request.ProviderResponseTransCode;

                    if (!string.IsNullOrEmpty(request.ProviderCode) && request.ProviderCode != item.ProvidersCode)
                    {
                        item.ProvidersCode = request.ProviderCode;
                        var provider = await GetProviderBackend(request.ProviderCode);
                        item.ProvidersInfo = provider?.ProviderName;
                    }

                    await _reportMongoRepository.UpdateOneAsync(item);
                }
                else
                {
                    _logger.LogWarning($"ReportStatusMessage {request.TransCode} is null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportStatusMessage {request.TransCode} error: {ex}");
            }
        }

        private async Task ReportItemHistoryMessage(ReportBalanceHistoriesMessage request)
        {
            try
            {
                if (request.Transaction.TransType == TransactionType.Deposit
                    || request.Transaction.TransType == TransactionType.PayBatch
                    || request.Transaction.TransType == TransactionType.PayCommission
                    || request.Transaction.TransType == TransactionType.AdjustmentIncrease
                    || request.Transaction.TransType == TransactionType.AdjustmentDecrease
                    || request.Transaction.TransType == TransactionType.CancelPayment)
                {
                    await ReportDepositMessage(new ReportDepositMessage()
                    {
                        AccountCode = request.Transaction.TransType == TransactionType.AdjustmentDecrease
                            ? request.Settlement.SrcAccountCode
                            : request.Settlement.DesAccountCode,
                        Balance = request.Transaction.TransType == TransactionType.AdjustmentDecrease
                            ? request.Settlement.SrcAccountBalance
                            : request.Settlement.DesAccountBalance,
                        CreatedDate = request.Settlement.CreatedDate ?? DateTime.Now,
                        ServiceCode = request.Transaction.TransType switch
                        {
                            TransactionType.Deposit => ReportServiceCode.DEPOSIT,
                            TransactionType.PayBatch => ReportServiceCode.PAYBATCH,
                            TransactionType.AdjustmentIncrease => ReportServiceCode.CORRECTUP,
                            TransactionType.AdjustmentDecrease => ReportServiceCode.CORRECTDOWN,
                            TransactionType.CancelPayment => ReportServiceCode.REFUND,
                            TransactionType.PayCommission => ReportServiceCode.PAYCOMMISSION,
                            _ => ""
                        },
                        Amount = request.Settlement.Amount,
                        TransRef = request.Transaction.TransRef,
                        TransCode = request.Transaction.TransactionCode,
                        Price = request.Settlement.Amount,
                        SaleProcess = request.Settlement.Description,
                        TransNote = request.Transaction.TransNote,
                        Description = request.Transaction.Description,
                        ExtraInfo = request.ExtraInfo,
                    }, request.Transaction.TransType);

                    if (request.Transaction.TransType == TransactionType.PayCommission)
                    {
                        _logger.LogInformation(
                            $"{request.Transaction.TransRef} PayCommission Input : {request.Transaction.TransNote}");
                        var transCodeRef = request.Transaction.TransNote.Split(':').Length >= 2
                            ? request.Transaction.TransNote.Split(':')[1].Trim(' ').TrimEnd(' ').TrimStart(' ')
                            : string.Empty;
                        if (!string.IsNullOrEmpty(transCodeRef))
                        {
                            _logger.LogInformation($"{request.Transaction.TransRef} PayCommission {transCodeRef}");
                            var transOld = await _reportMongoRepository.GetReportItemByTransCode(transCodeRef);
                            if (transOld != null)
                            {
                                _logger.LogInformation(
                                    $"{request.Transaction.TransRef} PayCommission {transCodeRef} update");
                                transOld.CommissionPaidCode = request.Transaction.TransRef;
                                transOld.CommissionStatus = 1;
                                transOld.CommissionAmount = Convert.ToDouble(request.Settlement.Amount);
                                transOld.CommissionDate = request.Settlement.CreatedDate ?? DateTime.Now;
                                await _reportMongoRepository.UpdateOneAsync(transOld);
                            }
                            else
                            {
                                await _bus.Publish<CommissionReportCommand>(new
                                {
                                    Type = 1,
                                    TransCode = transCodeRef,
                                    ParentCode = string.Empty,
                                    CommissionCode = request.Transaction.TransRef,
                                    Commission = Convert.ToDouble(request.Settlement.Amount),
                                    CommissionDate = request.Settlement.CreatedDate ?? DateTime.Now,
                                    Status = 1,
                                    CorrelationId = Guid.NewGuid()
                                });
                                _logger.LogInformation(
                                    $"{request.Transaction.TransRef} PayCommission {transCodeRef} Not_Check_Item");
                            }
                        }
                    }
                }
                else if (request.Transaction.TransType == TransactionType.Transfer)
                {
                    await ReportTransferMessage(new ReportTransferMessage()
                    {
                        AccountCode = request.Settlement.SrcAccountCode,
                        Balance = request.Settlement.SrcAccountBalance,
                        ReceivedAccountCode = request.Settlement.DesAccountCode,
                        ReceivedBalance = request.Settlement.DesAccountBalance,
                        CreatedDate = request.Settlement.CreatedDate ?? DateTime.Now,
                        ServiceCode = ReportServiceCode.TRANSFER,
                        Amount = request.Settlement.Amount,
                        TransRef = request.Transaction.TransRef,
                        TransCode = request.Transaction.TransactionCode,
                        Price = request.Settlement.Amount,
                        TransNote = request.Transaction.TransNote,
                        Description = request.Transaction.Description,
                    });
                }
                else if (request.Transaction.TransType == TransactionType.ClearDebt
                         || request.Transaction.TransType == TransactionType.SaleDeposit)
                {
                    await ReportStaffMessage(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportProcessPublish error: {ex}");
            }
        }

        private async Task ReportTransferMessage(ReportTransferMessage message)
        {
            try
            {
                var item = await ConvertInfoTransfer(message);
                await _reportMongoRepository.AddOneAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{message.TransCode} ReportTransferMessage error: {ex}");
            }
        }

        private async Task ReportDepositMessage(ReportDepositMessage message, TransactionType transType)
        {
            try
            {
                var item = await ConvertInfoDeposit(message, transType);
                await _reportMongoRepository.AddOneAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{message.TransCode} ReportDepositMessage error: {ex}");
            }
        }

        private async Task ReportStaffMessage(ReportBalanceHistoriesMessage message)
        {
            if (message.Transaction.TransType == TransactionType.SaleDeposit)
            {
                if (message.Settlement.CurrencyCode == "VND")
                {
                    //Nap tien vao tai khoan dai ly
                    await ReportDepositMessage(new ReportDepositMessage()
                    {
                        AccountCode = message.Settlement.DesAccountCode,
                        Balance = message.Settlement.DesAccountBalance,
                        CreatedDate = message.Transaction.CreatedDate,
                        ServiceCode = ReportServiceCode.DEPOSIT,
                        Amount = message.Settlement.Amount,
                        Price = message.Settlement.Amount,
                        TransRef = message.Transaction.TransRef,
                        TransCode = message.Transaction.TransactionCode,
                        Description = message.Settlement.Description,
                    }, message.Transaction.TransType);
                }
                else if (message.Settlement.CurrencyCode == "DEBT")
                {
                    //Ghi nhan cong no
                    try
                    {
                        _logger.LogInformation($"ReportStaffMessage SaleDeposit Input : {message.ToJson()}");
                        var item = await ConvertInfoStaff(message.Transaction, message.Settlement);
                        await _reportMongoRepository.AddOneAsync(item);
                        _logger.LogInformation(
                            $"ReportStaffMessage SaleDeposit Save : {message.Transaction.TransRef} success.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"ReportStaffMessage SaleDeposit error: {ex}");
                    }
                }
            }
            else if (message.Transaction.TransType == TransactionType.ClearDebt)
            {
                //Giảm tru cong no
                try
                {
                    _logger.LogInformation($"ReportStaffMessage ClearDebt Input : {message.ToJson()}");
                    var item = await ConvertInfoStaff(message.Transaction, message.Settlement);
                    await _reportMongoRepository.AddOneAsync(item);
                    _logger.LogInformation(
                        $"ReportStaffMessage ClearDebt Save : {message.Transaction.TransRef} success.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"ReportStaffMessage ClearDebt error: {ex}");
                }
            }
        }

        public async Task<MessageResponseBase> CreateReportTransDetail(ReportBalanceHistoriesMessage request)
        {
            try
            {
                await AddBalanceHistoryReport(request);
                await ReportItemHistoryMessage(request);
                await UpdateAccountBalanceDayReport(request);

                return new MessageResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{request.Transaction.TransRef}|{request.Transaction.TransactionCode} BalanceHistoryCreateAsync error: {ex}");
                return new MessageResponseBase
                {
                    ResponseCode = "00",
                    ResponseMessage = "BalanceHistoryCreateAsync error: " + ex
                };
            }
        }

        public async Task<ReportItemDetail> ConvertInfoSale(ReportSaleMessage message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.PaidTransCode))
                    return null;

                var perform = await GetAccountBackend(message.PerformAccount);
                var account = await GetAccountBackend(message.AccountCode);
                var provider = await GetProviderBackend(message.ProviderCode);
                var product = await GetProductBackend(message.ProductCode);
                var service = await GetServiceBackend(message.ServiceCode);
                var vendor = await GetVenderBackend(message.VendorCode);

                var item = new ReportItemDetail()
                {
                    Id = message.CorrelationId,

                    #region //**.Thông tin đại lý

                    PerformAccount = message.PerformAccount,
                    ParentCode = message.ParentCode,
                    AccountCode = message.AccountCode,
                    AccountInfo = account.Mobile + "-" + account.FullName,
                    AccountAccountType = account.AccountType,
                    AccountAgentName = account.AgentName,
                    AccountAgentType = account.AgentType,
                    AccountCityId = account.CityId,
                    AccountDistrictId = account.DistrictId,
                    AccountWardId = account.WardId,
                    AccountCityName = account.CityName,
                    AccountDistrictName = account.CityName,
                    AccountWardName = account.WardName,
                    SaleCode = account.SaleCode,
                    SaleLeaderCode = account.LeaderCode,

                    #endregion

                    ProductCode = message.ProductCode,
                    ProductName = product.ProductName,
                    CategoryCode = product.CategoryCode,
                    CategoryName = product.CategoryName,
                    ServiceCode = service.ServiceCode,
                    ServiceName = service.ServiceName,
                    TransType = service.ServiceCode,
                    VenderCode = message.VendorCode,
                    VenderName = vendor?.VenderName,
                    ProvidersCode = message.ProviderCode,
                    ProvidersInfo = provider.ProviderName,
                    RequestRef = message.TransRef,
                    TransCode = message.TransCode,
                    PaidTransCode = message.PaidTransCode,
                    PayTransRef = message.PayTransCode,
                    ReceivedAccount = message.ReceivedAccount,
                    CreatedTime = message.CreatedDate,
                    Discount = Convert.ToDouble(message.Discount),
                    Amount = Convert.ToDouble(message.Price * message.Quantity),
                    Price = Convert.ToDouble(message.Price),
                    Quantity = message.Quantity,
                    PriceIn = 0,
                    PriceOut = Convert.ToDouble(message.PaymentAmount),
                    TotalPrice = Convert.ToDouble(message.PaymentAmount),
                    PaidAmount = -Convert.ToDouble(message.PaymentAmount),
                    Fee = Convert.ToDouble(message.Fee),
                    Channel = message.Channel,
                    ExtraInfo = message.ExtraInfo,
                    Balance = Convert.ToDouble(message.Balance),
                    FeeText = string.Empty,
                    CommissionStatus = 0,
                    CommissionAmount = 0,
                    CommissionPaidCode = string.Empty,
                    ParentName = string.Empty,
                    ReceiverType = string.Empty,
                    TransTransSouce = string.Empty,
                    RequestTransSouce = string.Empty,
                    SaleInfo = string.Empty,
                    SaleLeaderInfo = string.Empty,
                    ProviderReceiverType = message.ProviderReceiverType,
                    ProviderTransCode = message.ProviderTransCode,
                    ParentProvider = message.ParentProvider,
                    TextDay = DateTime.Now.ToString("yyyyMMdd"),
                };
                if (new[] {"PREPAID", "POSTPAID"}.Contains(message.ReceiverType))
                    item.ReceiverType = message.ReceiverType;

                if (item.AccountAgentType != 5)
                {
                    item.ParentCode = string.Empty;
                    item.ParentName = string.Empty;
                }

                if (perform != null)
                {
                    item.PerformInfo = perform.Mobile + "-" + perform.FullName;
                    item.PerformAgentType = perform.AccountType;
                }

                if (account.AgentType != (int) AgentType.AgentApi)
                {
                    if (!string.IsNullOrEmpty(account.SaleCode))
                    {
                        var accountSale = await GetAccountBackend(account.SaleCode);
                        item.SaleCode = account.SaleCode;
                        item.SaleInfo = accountSale.UserName + "-" + accountSale.Mobile + "-" + account.FullName;
                    }

                    if (!string.IsNullOrEmpty(account.LeaderCode))
                    {
                        var accountLeader = await GetAccountBackend(account.LeaderCode);
                        item.SaleLeaderCode = account.LeaderCode;
                        item.SaleLeaderInfo = accountLeader.UserName + "-" + accountLeader.Mobile + "-" +
                                              accountLeader.FullName;
                    }
                }

                if (!string.IsNullOrEmpty(item.ParentCode))
                {
                    var parentAccount = await GetAccountBackend(item.ParentCode);
                    item.ParentName = parentAccount != null ? parentAccount.FullName : string.Empty;
                }

                if (message.ServiceCode == ReportServiceCode.PAY_BILL && !string.IsNullOrEmpty(message.FeeInfo))
                    item.FeeText = GetFeeTextInfo(message.FeeInfo);

                if (message.Status == 1)
                    item.Status = ReportStatus.Success;
                else if (message.Status == 3)
                    item.Status = ReportStatus.Error;
                else item.Status = ReportStatus.TimeOut;

                item.PayTransRef = !string.IsNullOrEmpty(message.PayTransCode) ? message.PayTransCode : item.TransCode;

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    $"{message.TransRef}|{message.TransCode}|{message.PayTransCode} ConvertInfoSale Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                await _reportMongoRepository.InsertWaringInfo(new ReportItemWarning()
                {
                    TransType = ReportServiceCode.TOPUP,
                    Type = ReportWarningType.Type_ErrorConvertInfo,
                    TransCode = message.TransCode,
                    Message = message.ToJson()
                });
                return null;
            }
        }

        private async Task<ReportItemDetail> ConvertInfoDeposit(ReportDepositMessage message, TransactionType transType)
        {
            var account = await GetAccountBackend(message.AccountCode);
            var service = await GetServiceBackend(message.ServiceCode);
            var item = new ReportItemDetail()
            {
                Id = message.Id,
                AccountCode = message.AccountCode,
                AccountInfo = account.Mobile + "-" + account.FullName,
                AccountAccountType = account.AccountType,
                AccountAgentType = account.AgentType,
                AccountAgentName = account.AgentName,
                AccountCityId = account.CityId,
                AccountDistrictId = account.DistrictId,
                AccountWardId = account.WardId,
                AccountCityName = account.CityName,
                AccountDistrictName = account.CityName,
                AccountWardName = account.WardName,
                SaleCode = account.SaleCode,
                SaleLeaderCode = account.LeaderCode,
                ParentCode = account.ParentCode,
                ServiceCode = message.ServiceCode,
                ServiceName = service.ServiceName,
                ProductCode = string.Empty,
                ProductName = string.Empty,
                ProvidersCode = string.Empty,
                ProvidersInfo = string.Empty,
                RequestRef = string.Empty,
                PayTransRef = string.Empty,
                CategoryCode = string.Empty,
                CategoryName = string.Empty,
                CommissionPaidCode = string.Empty,
                ParentName = string.Empty,
                TransType = service.ServiceCode,
                TransCode = message.TransRef,
                PaidTransCode = message.TransCode,
                CreatedTime = message.CreatedDate,
                Amount = Convert.ToDouble(message.Price),
                Quantity = 1,
                Price = Convert.ToDouble(message.Price),
                TotalPrice = Convert.ToDouble(message.Price),
                PriceIn = Convert.ToDouble(transType == TransactionType.AdjustmentDecrease ? 0 : message.Price),
                PriceOut = Convert.ToDouble(transType == TransactionType.AdjustmentDecrease ? message.Price : 0),
                Balance = Convert.ToDouble(message.Balance),
                PaidAmount =
                    Convert.ToDouble(transType == TransactionType.AdjustmentDecrease ? -message.Price : message.Price),
                TransNote = message.TransNote,
                ExtraInfo = message.ExtraInfo,
                Status = ReportStatus.Success,
                TextDay = message.CreatedDate.Date.ToString("yyyyMMdd"),
                ReceiverType = string.Empty,
            };

            if (string.IsNullOrEmpty(message.Description) && transType == TransactionType.SaleDeposit)
            {
                var accountDebt = await GetAccountBackend(message.SaleProcess);

                if (accountDebt != null)
                {
                    item.PerformAccount = accountDebt.AccountCode;
                    item.PerformAgentType = accountDebt.AgentType;
                }
            }

            if (transType == TransactionType.CancelPayment)
            {
                item.RequestRef = string.Empty;
                item.TransCode = string.Empty;
                item.TransType = ReportServiceCode.REFUND;
                item.TransTransSouce = message.TransRef;
                var itemRef =
                    await _reportMongoRepository.GetOneAsync<ReportItemDetail>(x => x.TransCode == message.TransRef);
                if (itemRef != null)
                {
                    item = await ConvertInfoSouceRefund(item, itemRef);
                    itemRef.Status = ReportStatus.Error;
                    await _reportMongoRepository.UpdateOneAsync(itemRef);
                }
                else
                {
                    await _bus.Publish(new ReportRefundMessage()
                    {
                        TransCode = message.TransRef,
                        PaidTransCode = item.PaidTransCode
                    });
                }
            }

            return item;
        }

        private async Task<ReportItemDetail> ConvertInfoTransfer(ReportTransferMessage message)
        {
            var account = await GetAccountBackend(message.AccountCode);
            var received = await GetAccountBackend(message.ReceivedAccountCode);
            var service = await GetServiceBackend(message.ServiceCode);

            var item = new ReportItemDetail
            {
                Id = message.Id,
                PerformAccount = message.AccountCode,
                PerformInfo = account.Mobile + "-" + account.FullName,
                PerformAgentType = account.AgentType,
                AccountCode = message.ReceivedAccountCode,
                AccountInfo = received.Mobile + "-" + received.FullName,
                AccountAccountType = received.AccountType,
                AccountAgentType = received.AgentType,
                AccountAgentName = received.AgentName,
                ParentCode = received.ParentCode,
                AccountCityId = received.CityId,
                AccountDistrictId = received.DistrictId,
                AccountWardId = received.WardId,
                AccountCityName = received.CityName,
                AccountDistrictName = received.DistrictName,
                AccountWardName = received.WardName,
                SaleCode = received.SaleCode,
                SaleLeaderCode = received.LeaderCode,
                ProductCode = string.Empty,
                ProductName = string.Empty,
                TransType = message.ServiceCode,
                ServiceCode = message.ServiceCode,
                ServiceName = service?.ServiceName,
                ProvidersCode = string.Empty,
                ProvidersInfo = string.Empty,
                TransCode = message.TransRef,
                PaidTransCode = message.TransCode,
                CreatedTime = message.CreatedDate,
                Price = Convert.ToDouble(message.Price),
                PriceIn = Convert.ToDouble(message.Price),
                PriceOut = 0,
                TotalPrice = Convert.ToDouble(message.Price),
                PaidAmount = Convert.ToDouble(message.Price),
                Amount = Convert.ToDouble(message.Price),
                Quantity = 1,
                PerformBalance = Convert.ToDouble(message.Balance),
                Balance = Convert.ToDouble(message.ReceivedBalance),
                TransNote = message.TransNote,
                Status = ReportStatus.Success,
                TextDay = DateTime.Now.ToString("yyyyMMdd"),
                ReceiverType = "",
            };

            return item;
        }

        private async Task<ReportStaffDetail> ConvertInfoStaff(TransactionReportDto message,
            SettlementReportDto settlement)
        {
            //1.Cấp tiền cho đại lý
            if (message.TransType == TransactionType.SaleDeposit)
            {
                var account = await GetAccountBackend(settlement.DesAccountCode);
                var service = await GetServiceBackend("SaleDeposit");
                var limit = await GetLimitDebtAccount(settlement.DesAccountCode);
                var item = new ReportStaffDetail
                {
                    Id = message.Id,
                    AccountCode = settlement.DesAccountCode,
                    AccountInfo = account.UserName + "-" + account.Mobile + "-" + account.FullName,
                    ReceivedCode = settlement.Description,
                    ServiceCode = service.ServiceCode,
                    DebitAmount = settlement.Amount,
                    CreditAmount = 0,
                    LimitBalance =Convert.ToDouble(limit?.Limit ?? 0),
                    Price = settlement.Amount,
                    ServiceName = service.ServiceName,
                    RequestRef = message.TransRef,
                    TransCode = settlement.TransCode,
                    CreatedTime = message.CreatedDate,
                    Balance = Convert.ToDouble(limit?.Limit ?? 0) - settlement.DesAccountBalance,
                    TextDay = DateTime.Now.ToString("yyyyMMdd"),
                    Status = ReportStatus.Success,
                    IsView = true,
                };

                if (!string.IsNullOrEmpty(item.ReceivedCode))
                {
                    var accountDes = await GetAccountBackend(item.ReceivedCode);
                    if (accountDes != null)
                        item.Description = $"Nạp tiền đại lý {item.ReceivedCode}";
                }

                return item;
            }
            //2.Nhân viên nộp tiền vào tài khoản
            else if (message.TransType == TransactionType.ClearDebt)
            {
                var account = await GetAccountBackend(settlement.SrcAccountCode);
                var service = await GetServiceBackend("ClearDebt");
                var limit = await GetLimitDebtAccount(settlement.SrcAccountCode);

                var item = new ReportStaffDetail
                {
                    Id = message.Id,
                    AccountCode = message.SrcAccountCode,
                    AccountInfo = account.UserName + "-" + account.Mobile + "-" + account.FullName,
                    ServiceCode = service.ServiceCode,
                    DebitAmount = 0,
                    CreditAmount = settlement.Amount,
                    IsView = true,
                    LimitBalance = Convert.ToDouble(limit?.Limit ?? 0),
                    Price = settlement.Amount,
                    ServiceName = service.ServiceCode,
                    RequestRef = message.TransRef,
                    TransCode = settlement.TransCode,
                    CreatedTime = message.CreatedDate,
                    Balance = Convert.ToDouble(limit?.Limit ?? 0) - settlement.SrcAccountBalance,
                    Description = $"Thanh toán công nợ ngày {DateTime.Now:dd/MM/yyyy}",
                    TextDay = DateTime.Now.ToString("yyyyMMdd"),
                    Status = ReportStatus.Success,
                };

                return item;
            }

            return null;
        }

        private Task<ReportItemDetail> ConvertInfoSouceRefund(ReportItemDetail item, ReportItemDetail itemRef)
        {
            item.PerformAccount = itemRef.PerformAccount;
            item.PerformInfo = itemRef.PerformInfo;
            item.PerformAgentType = itemRef.PerformAgentType;
            item.ParentCode = itemRef.ParentCode;
            item.ParentName = itemRef.ParentName;
            item.AccountInfo = itemRef.AccountInfo;
            item.ServiceCode = itemRef.ServiceCode;
            item.ServiceName = itemRef.ServiceName;
            item.ProductCode = itemRef.ProductCode;
            item.ProductName = itemRef.ProductName;
            item.CategoryCode = itemRef.CategoryCode;
            item.CategoryName = itemRef.CategoryName;
            item.ProvidersCode = itemRef.ProvidersCode;
            item.ProvidersInfo = itemRef.ProvidersInfo;
            item.VenderCode = itemRef.VenderCode;
            item.VenderName = itemRef.VenderName;
            item.SaleCode = itemRef.SaleCode;
            item.SaleInfo = itemRef.SaleInfo;
            item.SaleLeaderCode = itemRef.SaleLeaderCode;
            item.SaleLeaderInfo = itemRef.SaleLeaderInfo;
            item.AccountCityId = itemRef.AccountCityId;
            item.AccountCityName = itemRef.AccountCityName;
            item.AccountDistrictId = itemRef.AccountDistrictId;
            item.AccountDistrictName = itemRef.AccountDistrictName;
            item.AccountWardId = itemRef.AccountWardId;
            item.AccountWardName = itemRef.AccountWardName;
            item.Quantity = -itemRef.Quantity;
            item.Amount = -itemRef.Amount;
            item.Price = -itemRef.Price;
            item.TotalPrice = -itemRef.TotalPrice;
            item.Discount = -itemRef.Discount;
            item.Fee = -itemRef.Fee;
            item.PriceIn = Math.Abs(itemRef.PaidAmount ?? 0);
            item.PriceOut = 0;
            item.ReceivedAccount = itemRef.ReceivedAccount;
            item.RequestTransSouce = itemRef.RequestRef;
            item.TransTransSouce = itemRef.TransCode;
            item.ExtraInfo = itemRef.ExtraInfo;
            item.ReceiverType = "";
            if (item.AccountAgentType != 5)
            {
                item.ParentCode = string.Empty;
                item.ParentName = string.Empty;
            }

            return Task.FromResult(item);
        }

        private async Task UpdateReportQueue(ReportSaleMessage message, ReportItemDetail item)
        {
            try
            {
                if (message.NextStep == 0)
                {
                    return;
                }
                else if (item == null)
                {
                    if (message.Retry < 5)
                    {
                        message.Retry += 1;
                        if (message.Retry == 5)
                            await Task.Delay(10000);

                        message.CorrelationId = Guid.NewGuid();
                        await _bus.Publish(message);
                    }
                    else
                    {
                        await _reportMongoRepository.InsertWaringInfo(new ReportItemWarning()
                        {
                            TransType = ReportServiceCode.TOPUP,
                            Type = ReportWarningType.Type_Status,
                            TransCode = message.TransCode,
                            Message = message.ToJson()
                        });
                    }

                    return;
                }

                _logger.LogInformation(
                    $"UpdateReportQueue_Input: {message.TransRef}-{message.TransCode}-{message.PaidTransCode}-{message.PayTransCode}-{message.ProviderCode} =>Next|Status: {message.NextStep}|{message.Status} =>Status_After: {item.Status}");
                if (!string.IsNullOrEmpty(message.PayTransCode) && item.PayTransRef != message.PayTransCode)
                    item.PayTransRef = message.PayTransCode;

                if (item.Status == ReportStatus.TimeOut)
                {
                    if (message.Status == 1)
                        item.Status = ReportStatus.Success;
                    else if (message.Status == 3)
                        item.Status = ReportStatus.Error;
                    else item.Status = ReportStatus.TimeOut;
                }

                if (string.IsNullOrEmpty(item.ExtraInfo) && !string.IsNullOrEmpty(message.ExtraInfo))
                    item.ExtraInfo = message.ExtraInfo;

                if (!string.IsNullOrEmpty(message.ProviderCode) && message.ProviderCode != item.ProvidersCode)
                {
                    item.ProvidersCode = message.ProviderCode;
                    var provider = await GetProviderBackend(message.ProviderCode);
                    item.ProvidersInfo = provider?.ProviderName;
                }

                await _reportMongoRepository.UpdateOneAsync(item);
                _logger.LogInformation(
                    $"UpdateReportQueue: {message.TransRef}-{message.TransCode}-{message.PaidTransCode}-{message.PayTransCode} => Next|Status: {message.NextStep}|{message.Status} => Update success");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"UpdateReportQueue: {message.TransRef}-{message.TransCode} Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                await _reportMongoRepository.InsertWaringInfo(new ReportItemWarning()
                {
                    TransType = ReportServiceCode.TOPUP,
                    Type = ReportWarningType.Type_Status,
                    TransCode = message.TransCode,
                    Message = message.ToJson()
                });
            }
        }

        public async Task ReportCompensationHistoryMessage(ReportCompensationHistoryMessage message)
        {
            try
            {
                var history = await _reportMongoRepository.GetReportBalanceHistoriesByTransCode(message.PaidTransCode);
                if (history != null && string.IsNullOrEmpty(history.ServiceCode))
                {
                    var sv = await GetServiceBackend(history.ServiceCode);
                    history.ServiceCode = message.ServiceCode;
                    history.ServiceName = sv != null ? sv.ServiceName : string.Empty;
                    await _reportMongoRepository.UpdateOneAsync(history);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"ReportCompensationHistoryMessage: {message.PaidTransCode} Exception : {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        public async Task ReportRefundInfoMessage(ReportRefundMessage message)
        {
            try
            {
                var item = await _reportMongoRepository.GetReportItemByPaidTransCode(message.PaidTransCode);
                if (item != null)
                {
                    var itemRef = await _reportMongoRepository.GetReportItemByTransCode(message.TransCode);

                    if (itemRef != null)
                    {
                        item.PerformAccount = itemRef.PerformAccount;
                        item.PerformInfo = itemRef.PerformInfo;
                        item.ParentCode = itemRef.ParentCode;
                        item.ParentName = itemRef.ParentName;
                        item.AccountInfo = itemRef.AccountInfo;
                        item.ServiceCode = itemRef.ServiceCode;
                        item.ServiceName = itemRef.ServiceName;
                        item.ProductCode = itemRef.ProductCode;
                        item.ProductName = itemRef.ProductName;
                        item.CategoryCode = itemRef.CategoryCode;
                        item.CategoryName = itemRef.CategoryName;
                        item.ProvidersCode = itemRef.ProvidersCode;
                        item.ProvidersInfo = itemRef.ProvidersInfo;
                        item.SaleCode = itemRef.SaleCode;
                        item.SaleInfo = itemRef.SaleInfo;
                        item.SaleLeaderCode = itemRef.SaleLeaderCode;
                        item.SaleLeaderInfo = itemRef.SaleLeaderInfo;
                        item.Quantity = -itemRef.Quantity;
                        item.Amount = -itemRef.Amount;
                        item.Price = -itemRef.Price;
                        item.TotalPrice = -itemRef.TotalPrice;
                        item.Discount = -itemRef.Discount;
                        item.Fee = -itemRef.Fee;
                        item.ReceivedAccount = itemRef.ReceivedAccount;
                        item.RequestTransSouce = itemRef.RequestRef;
                        item.TransTransSouce = itemRef.TransCode;
                        item.ExtraInfo = itemRef.ExtraInfo;
                        await _reportMongoRepository.UpdateOneAsync(item);

                        return;
                    }
                }

                if (message.Retry <= 3)
                {
                    message.Retry += 1;
                    await _bus.Publish(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"ReportRefundInfoMessage: {message.PaidTransCode} Exception : {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        public async Task ReportRefundInfoError(ReportItemDetail itemRef)
        {
            try
            {
                var item = await _reportMongoRepository.GetReportItemByTransSouce(itemRef.TransCode);
                if (item != null)
                {
                    item.PerformAccount = itemRef.PerformAccount;
                    item.PerformInfo = itemRef.PerformInfo;
                    item.PerformAgentType = itemRef.PerformAgentType;
                    item.ParentCode = itemRef.ParentCode;
                    item.ParentName = itemRef.ParentName;
                    item.AccountInfo = itemRef.AccountInfo;
                    item.ServiceCode = itemRef.ServiceCode;
                    item.ServiceName = itemRef.ServiceName;
                    item.ProductCode = itemRef.ProductCode;
                    item.ProductName = itemRef.ProductName;
                    item.CategoryCode = itemRef.CategoryCode;
                    item.CategoryName = itemRef.CategoryName;
                    item.ProvidersCode = itemRef.ProvidersCode;
                    item.ProvidersInfo = itemRef.ProvidersInfo;
                    item.SaleCode = itemRef.SaleCode;
                    item.SaleInfo = itemRef.SaleInfo;
                    item.SaleLeaderCode = itemRef.SaleLeaderCode;
                    item.SaleLeaderInfo = itemRef.SaleLeaderInfo;
                    item.Quantity = -itemRef.Quantity;
                    item.Amount = -itemRef.Amount;
                    item.Price = -itemRef.Price;
                    item.TotalPrice = -itemRef.TotalPrice;
                    item.Discount = -itemRef.Discount;
                    item.Fee = -itemRef.Fee;
                    item.ReceivedAccount = itemRef.ReceivedAccount;
                    item.RequestTransSouce = itemRef.RequestRef;
                    item.TransTransSouce = itemRef.TransCode;
                    item.ExtraInfo = itemRef.ExtraInfo;
                    await _reportMongoRepository.UpdateOneAsync(item);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"ReportRefundInfoError: {itemRef.TransCode} Exception : {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        private string GetFeeTextInfo(string feeDto)
        {
            string str;
            try
            {
                var row = feeDto.FromJson<ProductFeeDto>();
                const string txt =
                    "Phí {0} áp dụng cho các GD {1}.Các GD có giá trị {2} phụ phí {3} cho mỗi block {4} tăng thêm";
                if (row.AmountMinFee != null && row.SubFee != null && row.AmountIncrease != null && row.MinFee != null)
                {
                    str = string.Format(txt, (row.MinFee ?? 0).ToString("N0") + "đ",
                        "<= " + (row.AmountMinFee ?? 0).ToString("N0") + "đ",
                        ">= " + (row.AmountMinFee ?? 0).ToString("N0") + "đ",
                        (row.SubFee ?? 0).ToString("N0") + "đ",
                        (row.AmountIncrease ?? 0).ToString("N0") + "đ");
                }
                else if (row.MinFee != null)
                {
                    str = (row.MinFee ?? 0).ToString("N0") + " đ";
                }
                else str = "";
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"GetFeeTextInfo Exception:  {ex.Message}");
                str = string.Empty;
            }

            return str;
        }

        public async Task SysAccountBalanceDay(string accountCode, DateTime fromDate, DateTime date)
        {
            try
            {
                Expression<Func<ReportAccountBalanceDay, bool>> query = p => p.CurrencyCode == "VND" &&
                                                                             p.AccountType == "CUSTOMER" &&
                                                                             p.AccountCode == accountCode &&
                                                                             p.CreatedDay >=
                                                                             fromDate.ToUniversalTime() &&
                                                                             p.CreatedDay <= date.ToUniversalTime();

                var check = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(query)
                    .OrderByDescending(c => c.CreatedDay).FirstOrDefault();
                if (check == null)
                {
                    var account = await GetAccountBackend(accountCode);
                    if (account != null)
                    {
                        var itemNew = new ReportAccountBalanceDay()
                        {
                            AccountCode = account.AccountCode,
                            AccountType = BalanceAccountTypeConst.CUSTOMER,
                            Credite = 0,
                            Debit = 0,
                            BalanceBefore = 0,
                            BalanceAfter = 0,
                            CreatedDay = date,
                            CurrencyCode = "VND",
                            DecPayment = 0,
                            DecOther = 0,
                            IncDeposit = 0,
                            IncOther = 0,
                            ParentCode = string.Empty,
                            SaleCode = account.SaleCode,
                            SaleLeaderCode = account.LeaderCode,
                            TextDay = date.ToString("yyyyMMdd"),
                        };

                        if (account.AgentType == 5)
                            itemNew.ParentCode = account.ParentCode;

                        MappingSaleLeader(ref itemNew);
                        await _reportMongoRepository.AddOneAsync(itemNew);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SysAccountBalanceDay error: {ex}");
            }
        }

        public async Task<bool> InsertSmsMessage(SmsMessageRequest request)
        {
            try
            {
                var item = request.ConvertTo<SmsMessage>();
                item.CreatedDate = DateTime.UtcNow;
                await _reportMongoRepository.AddOneAsync(item);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"InsertSmsMessage error: {ex}");
                return false;
            }
        }

        private async Task SendAlarm(string transCode, string msg)
        {
            try
            {
                await _bus.Publish<SendBotMessage>(new
                {
                    MessageType = BotMessageType.Wraning,
                    BotType = BotType.Dev,
                    Module = "Report",
                    Title = "Ghi nhận báo cáo không thành công. Vui lòng check dữ liệu",
                    Message = $"Mã GD: {transCode}\n" +
                              $"Nội dung: {msg}",
                    TimeStamp = DateTime.Now,
                    CorrelationId = Guid.NewGuid()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"WaitTelegram error: {ex}");
            }
        }
    }
}