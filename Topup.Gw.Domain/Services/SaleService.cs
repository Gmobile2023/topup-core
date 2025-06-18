using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Contracts.Commands.Commissions;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Gw.Model;
using Topup.Gw.Model.Dtos;
using Topup.Gw.Model.Events;
using Topup.Gw.Model.RequestDtos;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using Topup.Shared.Helpers;
using Topup.Shared.UniqueIdGenerator;
using MassTransit;
using Microsoft.Extensions.Logging;



using Topup.Discovery.Requests.Backends;
using Topup.Discovery.Requests.Balance;
using Topup.Discovery.Requests.Stocks;
using ServiceStack;
using Topup.Shared.Utils;
using Nest;
using Topup.Gw.Domain.Entities;
using Topup.Gw.Domain.Repositories;

namespace Topup.Gw.Domain.Services
{
    public class SaleService : BusinessServiceBase, ISaleService
    {
        private readonly IBus _bus;
        private readonly ITransCodeGenerator _transCodeGenerator;

        private readonly IDateTimeHelper _dateTimeHelper;

        //private readonly IServiceGateway _gateway; gunner
        private readonly IPaygateMongoRepository _paygateMongoRepository;
        private readonly ILogger<SaleService> _logger;
        private readonly GrpcClientHepper _grpcClient;

        public SaleService(IPaygateMongoRepository paygateMongoRepository,
            IDateTimeHelper dateTimeHelper, IBus bus, ILogger<SaleService> logger,
            ITransCodeGenerator transCodeGenerator, GrpcClientHepper grpcClient)
        {
            _paygateMongoRepository = paygateMongoRepository;
            _dateTimeHelper = dateTimeHelper;
            _bus = bus;
            _logger = logger;
            _transCodeGenerator = transCodeGenerator;
            _grpcClient = grpcClient;
            //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
        }

        public async Task<SaleRequestDto> SaleRequestCreateAsync(SaleRequestDto saleRequestDto)
        {
            try
            {
                saleRequestDto.CreatedTime = DateTime.Now;
                saleRequestDto.Status = saleRequestDto.Status == SaleRequestStatus.Failed ? saleRequestDto.Status : 0;
                if (saleRequestDto.Price == 0)
                    saleRequestDto.Price = saleRequestDto.Amount;
                if (saleRequestDto.Quantity > 1)
                    saleRequestDto.Amount = saleRequestDto.Price * saleRequestDto.Quantity;

                //if (!string.IsNullOrEmpty(saleRequestDto.TransCode))
                saleRequestDto.TransCode =
                    await _transCodeGenerator.SaleTransCodeGeneratorAsync();
                var saleRequest = saleRequestDto.ConvertTo<SaleRequest>();
                if (string.IsNullOrEmpty(saleRequest.Vendor) || string.IsNullOrEmpty(saleRequestDto.Vendor))
                {
                    saleRequest.Vendor =
                        TelcoHepper.GetVendorTrans(saleRequestDto.ServiceCode, saleRequestDto.ProductCode);
                    saleRequestDto.Vendor = saleRequest.Vendor;
                }

                if (string.IsNullOrEmpty(saleRequestDto.ProviderTransCode))
                    saleRequestDto.ProviderTransCode = saleRequestDto.TransCode;
                else
                    saleRequestDto.ProviderTransCode =
                        saleRequestDto.ProviderTransCode + "_" + saleRequestDto.TransCode;
                await _paygateMongoRepository.AddOneAsync(saleRequest);
                return saleRequestDto;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{saleRequestDto.TransRef} Error insert saleRequest: " + ex.Message);
                return null;
            }
        }

        public async Task<BatchRequestDto> BatchLotRequestCreateAsync(BatchRequestDto batchRequestDto)
        {
            try
            {
                //_logger.LogInformation($"Extrainfo: {batchRequestDto.ToJson()}");
                batchRequestDto.CreatedTime = DateTime.Now;
                batchRequestDto.Status = 0;
                batchRequestDto.SaleRequestType = batchRequestDto.BatchType switch
                {
                    "PINCODE" => SaleRequestType.PinCode,
                    "PAYBILL" => SaleRequestType.PayBill,
                    "TOPUP" => SaleRequestType.Topup,
                    _ => batchRequestDto.SaleRequestType
                };

                if (string.IsNullOrEmpty(batchRequestDto.BatchCode))
                    batchRequestDto.BatchCode = Guid.NewGuid().ToString();
                var batchRequestEnt = batchRequestDto.ConvertTo<BatchLotRequest>();
                batchRequestEnt.BatchCode = batchRequestDto.BatchCode;
                batchRequestEnt.EndProcessTime = DateTime.Now;
                batchRequestEnt.Quantity = batchRequestDto.Items.Count();
                batchRequestEnt.PaymentAmount = batchRequestDto.Items.Sum(c => c.PaymentAmount);

                await _paygateMongoRepository.AddOneAsync(batchRequestEnt);
                foreach (var item in batchRequestDto.Items)
                {
                    var detail = item.ConvertTo<BatchDetail>();
                    detail.BatchCode = batchRequestDto.BatchCode;
                    detail.TransRef = Guid.NewGuid().ToString();
                    detail.BatchStatus = BatchLotRequestStatus.Init;
                    detail.Status = SaleRequestStatus.Init;
                    detail.SaleRequestType = batchRequestDto.SaleRequestType;
                    detail.StaffAccount = batchRequestEnt.StaffAccount;
                    item.TransRef = detail.TransRef;
                    item.PartnerCode = batchRequestDto.PartnerCode;
                    await _paygateMongoRepository.AddOneAsync(detail);
                }

                batchRequestDto.Items = batchRequestDto.Items;
                return batchRequestDto;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error insert BatchLotRequestCreateAsync: " + ex.Message);
                return null;
            }
        }

        //public async Task<bool> SaleItemUpdateStatus(string transCode, SaleRequestStatus status)
        //{
        //    var topupItems = await _paygateMongoRepository.GetAllAsync<SaleItem>(x => x.SaleTransCode == transCode);
        //    if (!topupItems.Any()) return false;
        //    foreach (var item in topupItems)
        //    {
        //        item.Status = (byte)status;
        //        await _paygateMongoRepository.UpdateOneAsync(item);
        //    }

        //    return true;
        //}
        public async Task<SaleRequestDto> SaleRequestUpdateAsync(SaleRequestDto saleRequestDto)
        {
            try
            {
                var topup = await _paygateMongoRepository.GetOneAsync<SaleRequest>(x =>
                    x.TransCode == saleRequestDto.TransCode);
                if (topup != null)
                {
                    var topupUpdate = CompareUpdateTopup(topup, saleRequestDto);
                    await _paygateMongoRepository.UpdateOneAsync(topupUpdate);
                    return saleRequestDto;
                }

                _logger.LogError($"{saleRequestDto.TransCode}-{saleRequestDto.TransRef}-TopupRequestUpdateAsync null");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{saleRequestDto.TransCode}-{saleRequestDto.TransRef}-TopupRequestUpdateAsync error: " + ex);
                return null;
            }
        }

        public async Task<bool> SaleItemUpdateStatus(string transCode, SaleRequestStatus status)
        {
            var topupItems = await _paygateMongoRepository.GetAllAsync<SaleItem>(x => x.SaleTransCode == transCode);
            if (!topupItems.Any()) return false;
            foreach (var item in topupItems)
            {
                item.Status = (byte)status;
                await _paygateMongoRepository.UpdateOneAsync(item);
            }

            return true;
        }

        public async Task<bool> SaleItemListCreateAsync(List<SaleItemDto> topupItemDto)
        {
            try
            {
                var topupItem = topupItemDto.ConvertTo<List<SaleItem>>();
                foreach (var item in topupItem) item.TransCode = Guid.NewGuid().ToString();

                await _paygateMongoRepository.AddManyAsync(topupItem);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("TopupItemListCreateAsync error: " + ex.Message);
                return false;
            }
        }

        public async Task<MessageResponseBase> SaleRequestCheckAsync(string transCode, string partnerCode)
        {
            try
            {
                var saleRequest = await _paygateMongoRepository.GetOneAsync<SaleRequest>(p =>
                    p.TransRef == transCode && p.PartnerCode == partnerCode);
                var returnMessage = new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
                if (null != saleRequest)
                {
                    returnMessage.ExtraInfo = saleRequest.TransCode;
                    switch (saleRequest.Status)
                    {
                        case SaleRequestStatus.Success:
                            returnMessage.ResponseCode = ResponseCodeConst.Success;
                            returnMessage.ResponseMessage = "Thành công!";
                            returnMessage.Payload = saleRequest.TransCode + "|" + saleRequest.Amount;
                            break;
                        case SaleRequestStatus.Canceled:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_Cancel;
                            returnMessage.ResponseMessage = "Giao dịch đã bị hủy.";
                            break;
                        case SaleRequestStatus.Failed:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_Failed;
                            returnMessage.ResponseMessage = "Giao dịch thất bại.";
                            break;
                        case SaleRequestStatus.TimeOver:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                            returnMessage.ResponseMessage = "Giao dịch quá thời gian chờ.";
                            break;
                        case SaleRequestStatus.InProcessing:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_InProcessing;
                            returnMessage.ResponseMessage = "Giao dịch đang được xử lý.";
                            break;
                        case SaleRequestStatus.Init:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_InProcessing;
                            returnMessage.ResponseMessage = "Giao dịch đang được xử lý.";
                            break;
                        case SaleRequestStatus.WaitForResult:
                        case SaleRequestStatus.WaitForConfirm:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            returnMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            break;
                        case SaleRequestStatus.Paid:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_InProcessing;
                            returnMessage.ResponseMessage = "Giao dịch đang được xử lý.";
                            break;
                        default:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            returnMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ.";
                            break;
                    }
                }
                else
                {
                    returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_TransactionNotFound;
                    returnMessage.ResponseMessage = "Giao dịch không tồn tại";
                }

                return returnMessage;
            }
            catch (Exception e)
            {
                _logger.LogError($"SaleRequestCheckAsync error:{e.Message}");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ."
                };
            }
        }

        public async Task<SaleRequestDto> SaleRequestGetAsync(string transCode)
        {
            try
            {
                var saleRequest = await _paygateMongoRepository.GetOneAsync<SaleRequest>(p =>
                    p.TransCode == transCode);

                return saleRequest?.ConvertTo<SaleRequestDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"SaleRequestGetAsync error:{transCode}-{ex}");
                return null;
            }
        }

        public async Task<SaleRequestDto> SaleRequestGetTransRefAsync(string transRef, string partnerCode)
        {
            var saleRequest = await _paygateMongoRepository.GetOneAsync<SaleRequest>(p =>
                p.TransRef == transRef && p.ProductCode == partnerCode);

            return saleRequest?.ConvertTo<SaleRequestDto>();
        }

        public async Task<MessagePagedResponseBase> SaleRequestGetListAsync(
            TopupListGetRequest topupListGetRequest)
        {
            try
            {
                Expression<Func<SaleRequest, bool>> query = p => true;

                if (!string.IsNullOrEmpty(topupListGetRequest.PartnerCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.PartnerCode == topupListGetRequest.PartnerCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.CategoryCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.CategoryCode == topupListGetRequest.CategoryCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.ProductCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ProductCode == topupListGetRequest.ProductCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.ServiceCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ServiceCode == topupListGetRequest.ServiceCode;
                    query = query.And(newQuery);
                }

//
                if (!string.IsNullOrEmpty(topupListGetRequest.ReceiverType))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ReceiverType == topupListGetRequest.ReceiverType;
                    query = query.And(newQuery);
                }


                if (topupListGetRequest.CategoryCodes != null && topupListGetRequest.CategoryCodes.Count > 0)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        topupListGetRequest.CategoryCodes.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (topupListGetRequest.ProductCodes != null && topupListGetRequest.ProductCodes.Count > 0)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        topupListGetRequest.ProductCodes.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }

                if (topupListGetRequest.ServiceCodes != null && topupListGetRequest.ServiceCodes.Count > 0)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        topupListGetRequest.ServiceCodes.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }

                if (topupListGetRequest.ProviderCode != null && topupListGetRequest.ProviderCode.Count > 0)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        topupListGetRequest.ProviderCode.Contains(p.Provider);
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(topupListGetRequest.Vendor))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p => p.Vendor == topupListGetRequest.Vendor;
                    query = query.And(newQuery);
                }

                if (topupListGetRequest.Status != null && topupListGetRequest.Status.Any())
                {
                    var status = topupListGetRequest.Status.ToList();
                    if (status.Contains((SaleRequestStatus)99))
                    {
                        Expression<Func<SaleRequest, bool>> newQuery = p =>
                            p.Status != SaleRequestStatus.Canceled && p.Status != SaleRequestStatus.Success &&
                            p.Status != SaleRequestStatus.Failed && p.Status != SaleRequestStatus.Init;
                        query = query.And(newQuery);
                    }
                    else
                    {
                        Expression<Func<SaleRequest, bool>> newQuery = p => status.Contains(p.Status);
                        query = query.And(newQuery);
                    }
                }

                if (topupListGetRequest.AgentType != AgentType.Default)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p => p.AgentType == topupListGetRequest.AgentType;
                    query = query.And(newQuery);
                }

                if (topupListGetRequest.SaleType != SaleType.Default)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p => p.SaleType == topupListGetRequest.SaleType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.MobileNumber))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ReceiverInfo == topupListGetRequest.MobileNumber;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.TransRef))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.TransRef == topupListGetRequest.TransRef;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.TransCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.TransCode == topupListGetRequest.TransCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.ProviderTransCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ProviderTransCode == topupListGetRequest.ProviderTransCode;
                    query = query.And(newQuery);
                }

                //if (!string.IsNullOrEmpty(topupListGetRequest.ProviderCode))
                //{
                //    Expression<Func<SaleRequest, bool>> newQuery = p =>
                //        p.Provider == topupListGetRequest.ProviderCode;
                //    query = query.And(newQuery);
                //}

                if (!string.IsNullOrEmpty(topupListGetRequest.ProviderResponseCode))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ProviderResponseCode == topupListGetRequest.ProviderResponseCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.ReceiverTypeResponse))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ReceiverTypeResponse == topupListGetRequest.ReceiverTypeResponse;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(topupListGetRequest.ParentProvider))
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.ParentProvider == topupListGetRequest.ParentProvider;
                    query = query.And(newQuery);
                }

                if (topupListGetRequest.FromDate != null)
                {
                    var fromdate = $"{topupListGetRequest.FromDate.Value:yMMdd}";

                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.TransCode.CompareTo(fromdate) >= 0;
                    //  string.Compare(p.TransCode, fromdate) > 0;
                    query = query.And(newQuery);

                    if (topupListGetRequest.FromDate.Value.Hour != 0 ||
                        topupListGetRequest.FromDate.Value.Minute != 0 ||
                        topupListGetRequest.FromDate.Value.Second != 0)
                    {
                        var fromDate = (DateTime?)_dateTimeHelper.ConvertToUtcTime(topupListGetRequest.FromDate.Value,
                            _dateTimeHelper.CurrentTimeZone());

                        Expression<Func<SaleRequest, bool>> newQuery2 = p => p.CreatedTime >= fromDate;
                        query = query.And(newQuery2);
                    }
                }

                if (topupListGetRequest.ToDate != null)
                {
                    var todate = $"{topupListGetRequest.ToDate.Value.AddDays(1):yMMdd}";
                    //var toDate = (DateTime?)_dateTimeHelper.ConvertToUtcTime(topupListGetRequest.ToDate.Value,
                    //    _dateTimeHelper.CurrentTimeZone());
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.TransCode.CompareTo(todate) <= 0;
                    //     string.Compare(p.TransCode, todate) < 0;
                    query = query.And(newQuery);


                    if (topupListGetRequest.ToDate.Value.Hour != 23 || topupListGetRequest.ToDate.Value.Minute != 59 ||
                        topupListGetRequest.ToDate.Value.Second != 59)
                    {
                        var toDate = (DateTime?)_dateTimeHelper.ConvertToUtcTime(topupListGetRequest.ToDate.Value,
                            _dateTimeHelper.CurrentTimeZone());

                        Expression<Func<SaleRequest, bool>> newQuery2 = p => p.CreatedTime <= toDate;
                        query = query.And(newQuery2);
                    }
                }

                //User nhân viên vào chỉ lấy gd của nhân viên đấy
                if (!string.IsNullOrEmpty(topupListGetRequest.StaffAccount) &&
                    topupListGetRequest.StaffAccount != topupListGetRequest.PartnerCode)
                {
                    Expression<Func<SaleRequest, bool>> newQuery = p =>
                        p.StaffAccount == topupListGetRequest.StaffAccount;
                    query = query.And(newQuery);
                }


                //    int total = 1000000;

                var total = await _paygateMongoRepository.CountAsync(query);


                var saleRequests = await _paygateMongoRepository.GetSortedPaginatedAsync<SaleRequest, Guid>(
                    query,
                    s => s.TransCode, false, topupListGetRequest.Offset, topupListGetRequest.Limit);

                var lst = saleRequests.ConvertTo<List<SaleRequestDto>>();
                foreach (var item in lst)
                {
                    item.CreatedTime = _dateTimeHelper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                    if (item.ResponseDate != null)
                        item.ResponseDate =
                            _dateTimeHelper.ConvertToUserTime(item.ResponseDate.Value, DateTimeKind.Utc);
                    if (item.RequestDate != null)
                        item.RequestDate =
                            _dateTimeHelper.ConvertToUserTime(item.RequestDate.Value, DateTimeKind.Utc);
                    if (item.AgentType == 0) //Check cho những gd cũ chưa có agentType
                    {
                        item.AgentType = item.Channel == Channel.API ? AgentType.AgentApi : AgentType.Agent;
                    }
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    Payload = lst
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"SaleRequestGetListAsync error:{e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Error",
                    Total = 0,
                    Payload = new List<SaleRequestDto>()
                };
            }
        }

        //todo Gunner. Hàm này xem lại. Update mình status, thành ra update đủ thứ
        public async Task<bool> SaleRequestUpdateStatusAsync(string transCode, string provider,
            SaleRequestStatus status, string transCodeProvider = "", bool isBackend = false,
            string providerResponseTransCode = "", string receiverType = "", string referenceCode = "")
        {
            try
            {
                var saleRequest =
                    await _paygateMongoRepository.GetOneAsync<SaleRequest>(p => p.TransCode == transCode);

                saleRequest.Status = status;
                if (!string.IsNullOrEmpty(provider))
                    saleRequest.Provider = provider;

                if (!string.IsNullOrEmpty(transCodeProvider))
                    saleRequest.ProviderTransCode = transCodeProvider;

                if (!string.IsNullOrEmpty(providerResponseTransCode))
                    saleRequest.ProviderResponseCode = providerResponseTransCode;

                if (!string.IsNullOrEmpty(receiverType))
                    saleRequest.ReceiverTypeResponse = receiverType;

                if (!string.IsNullOrEmpty(referenceCode))
                    saleRequest.ReferenceCode = referenceCode;

                saleRequest.ResponseDate = DateTime.Now;
                try
                {
                    var startTime = saleRequest.RequestDate ?? saleRequest.CreatedTime;
                    var endDate = _dateTimeHelper.ConvertToUtcTime(DateTime.Now, _dateTimeHelper.CurrentTimeZone());
                    var differenceInSeconds = endDate.Subtract(startTime).TotalSeconds;
                    saleRequest.ProcessedTime = Math.Round(differenceInSeconds);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    saleRequest.ProcessedTime = 0;
                }

                await _paygateMongoRepository.UpdateOneAsync(saleRequest);
                if (isBackend)
                {
                    await _bus.Publish(new ReportTransStatusMessage()
                    {
                        PayTransRef = saleRequest.ProviderTransCode,
                        ProviderCode = saleRequest.Provider,
                        TransCode = saleRequest.TransCode,
                        ReceiverTypeResponse = saleRequest.ReceiverTypeResponse,
                        ProviderResponseTransCode = saleRequest.ProviderResponseCode,
                        Status = saleRequest.Status switch
                        {
                            SaleRequestStatus.Success => 1,
                            SaleRequestStatus.Failed => 3,
                            _ => 2
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("TopupRequestUpdateStatusAsync error:" + ex);
                return false;
            }
        }

        // public async Task<LevelDiscountRecordDto> LevelDiscountRecordInsertAsync(
        //     LevelDiscountRecordDto levelDiscountRecordDto)
        // {
        //     try
        //     {
        //         var levelDiscountRecord = levelDiscountRecordDto.ConvertTo<LevelDiscountRecord>();
        //         levelDiscountRecord.TransCode =
        //             await _commonService.TransCodeGenAsync("D");
        //         await _paygateMongoRepository.AddOneAsync(levelDiscountRecord);
        //         return levelDiscountRecordDto;
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError("Error insert levelDiscountRecordDto: " + ex.Message);
        //         return null;
        //     }
        // }
        //todo hàm này xem viêt lại
        private SaleRequest CompareUpdateTopup(SaleRequest sale, SaleRequestDto saleRequestDto)
        {
            if (sale.Amount != saleRequestDto.Amount)
                sale.Amount = saleRequestDto.Amount;

            if (sale.Quantity != saleRequestDto.Quantity)
                sale.Quantity = saleRequestDto.Quantity;

            if (sale.Email != saleRequestDto.Email)
                sale.Email = saleRequestDto.Email;

            if (sale.CategoryCode != saleRequestDto.CategoryCode)
                sale.CategoryCode = saleRequestDto.CategoryCode;

            if (sale.ProductCode != saleRequestDto.ProductCode)
                sale.ProductCode = saleRequestDto.ProductCode;

            if (sale.PaymentTransCode != saleRequestDto.PaymentTransCode)
                sale.PaymentTransCode = saleRequestDto.PaymentTransCode;

            if (sale.PaymentAmount != saleRequestDto.PaymentAmount)
                sale.PaymentAmount = saleRequestDto.PaymentAmount;

            if (sale.Fee != saleRequestDto.Fee)
                sale.Fee = saleRequestDto.Fee;

            if (sale.ReceiverInfo != saleRequestDto.ReceiverInfo)
                sale.ReceiverInfo = saleRequestDto.ReceiverInfo;

            if (saleRequestDto.DiscountRate != null && sale.DiscountRate != saleRequestDto.DiscountRate)
                sale.DiscountRate = saleRequestDto.DiscountRate;

            if (saleRequestDto.FixAmount != null && sale.FixAmount != saleRequestDto.FixAmount)
                sale.FixAmount = saleRequestDto.FixAmount;

            if (saleRequestDto.DiscountAmount != null && sale.DiscountAmount != saleRequestDto.DiscountAmount)
                sale.DiscountAmount = saleRequestDto.DiscountAmount;

            if (sale.Status != saleRequestDto.Status)
                sale.Status = saleRequestDto.Status;

            if (sale.Provider != saleRequestDto.Provider)
                sale.Provider = saleRequestDto.Provider;

            if (sale.ReceiverType != saleRequestDto.ReceiverType)
                sale.ReceiverType = saleRequestDto.ReceiverType;

            if (!string.IsNullOrWhiteSpace(saleRequestDto.ProviderTransCode) &&
                sale.ProviderTransCode != saleRequestDto.ProviderTransCode)
                sale.ProviderTransCode = saleRequestDto.ProviderTransCode;

            if (sale.SaleType != saleRequestDto.SaleType)
                sale.SaleType = saleRequestDto.SaleType;

            if (sale.ParentProvider != saleRequestDto.ParentProvider)
                sale.ParentProvider = saleRequestDto.ParentProvider;

            if (sale.ReferenceCode != saleRequestDto.ReferenceCode)
                sale.ReferenceCode = saleRequestDto.ReferenceCode;

            sale.IsDiscountPaid = saleRequestDto.IsDiscountPaid;
            sale.IsCheckReceiverTypeSuccess = saleRequestDto.IsCheckReceiverTypeSuccess;
            sale.ReceiverTypeResponse = saleRequestDto.ReceiverTypeResponse;

            return sale;
        }

        /// <summary>
        /// Danh sách Lo
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> BatchLotRequestGetListAsync(BatchListGetRequest request)
        {
            try
            {
                if (request.FromDate == null)
                    request.FromDate = DateTime.Now;

                if (request.ToDate == null)
                    request.ToDate = DateTime.Now;

                Expression<Func<BatchLotRequest, bool>> query = p =>
                    p.CreatedTime >= request.FromDate.Value.Date.ToUniversalTime()
                    && p.CreatedTime <= request.ToDate.Value.Date.AddDays(1).AddSeconds(-1).ToUniversalTime();

                if (request.IsStaff)
                {
                    Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                        p.StaffAccount == request.AccountCode;
                    query = query.And(newQuery);
                }
                else
                {
                    Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                        p.PartnerCode == request.AccountCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.BatchCode))
                {
                    Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                        p.BatchCode == request.BatchCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.BatchType))
                {
                    Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                        p.BatchType == request.BatchType;
                    query = query.And(newQuery);
                }

                if (request.Status >= 0)
                {
                    if (request.Status == 0)
                    {
                        Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                            p.Status == BatchLotRequestStatus.Init;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 1)
                    {
                        Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                            p.Status == BatchLotRequestStatus.Completed;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 3)
                    {
                        Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                            p.Status == BatchLotRequestStatus.Stop;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 2)
                    {
                        Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                            p.Status == BatchLotRequestStatus.Process;
                        query = query.And(newQuery);
                    }
                }

                var total = await _paygateMongoRepository.CountAsync(query);
                var list = await _paygateMongoRepository.GetSortedPaginatedAsync<BatchLotRequest, Guid>(query,
                    s => s.CreatedTime, false,
                    request.Offset, request.Limit);

                var lst = list.ConvertTo<List<BatchItemDto>>();
                foreach (var item in lst)
                {
                    item.CreatedTime = _dateTimeHelper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                    item.EndProcessTime = _dateTimeHelper.ConvertToUserTime(item.EndProcessTime, DateTimeKind.Utc);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    Payload = lst
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"BatchLotRequestGetListAsync error:{e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Error",
                    Total = 0,
                    Payload = new List<BatchItemDto>()
                };
            }
        }

        /// <summary>
        /// Chi tiết lô
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> BatchLotRequestGetDetailAsync(BatchDetailGetRequest request)
        {
            try
            {
                Expression<Func<BatchDetail, bool>> query = p => p.BatchCode == request.BatchCode;

                if (request.IsStaff)
                {
                    Expression<Func<BatchDetail, bool>> newQuery = p =>
                        p.StaffAccount == request.AccountCode;
                    query = query.And(newQuery);
                }
                else
                {
                    Expression<Func<BatchDetail, bool>> newQuery = p =>
                        p.PartnerCode == request.AccountCode;
                    query = query.And(newQuery);
                }

                if (request.BatchStatus >= 0)
                {
                    if (request.BatchStatus == 0)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.BatchStatus == BatchLotRequestStatus.Init;
                        query = query.And(newQuery);
                    }
                    else if (request.BatchStatus == 1)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.BatchStatus == BatchLotRequestStatus.Completed;
                        query = query.And(newQuery);
                    }
                    else if (request.BatchStatus == 2)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.BatchStatus == BatchLotRequestStatus.Process;
                        query = query.And(newQuery);
                    }
                    else if (request.BatchStatus == 3)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.BatchStatus == BatchLotRequestStatus.Stop;
                        query = query.And(newQuery);
                    }
                }

                if (request.Status >= 0)
                {
                    if (request.Status == 0)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.Status == SaleRequestStatus.Init;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 1)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.Status == SaleRequestStatus.Success;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 2)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.Status == SaleRequestStatus.InProcessing
                            || p.Status == SaleRequestStatus.TimeOver
                            || p.Status == SaleRequestStatus.ProcessTimeout
                            || p.Status == SaleRequestStatus.WaitForResult
                            || p.Status == SaleRequestStatus.WaitForConfirm
                            || p.Status == SaleRequestStatus.Paid;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 3)
                    {
                        Expression<Func<BatchDetail, bool>> newQuery = p =>
                            p.Status == SaleRequestStatus.Canceled
                            || p.Status == SaleRequestStatus.Failed;
                        query = query.And(newQuery);
                    }
                }


                var total = await _paygateMongoRepository.CountAsync(query);
                var listAll = _paygateMongoRepository.GetAll(query);
                var sumTotat = new BatchDetailDto()
                {
                    Amount = listAll.Sum(c => c.Amount),
                    Fee = listAll.Sum(c => c.Fee),
                    DiscountAmount = listAll.Sum(c => c.DiscountAmount),
                    PaymentAmount = listAll.Sum(c => c.PaymentAmount),
                    Quantity = listAll.Sum(c => c.Quantity),
                };

                var list = await _paygateMongoRepository.GetSortedPaginatedAsync<BatchDetail, Guid>(query,
                    s => s.TransRef, false,
                    request.Offset, request.Limit);

                var lst = list.ConvertTo<List<BatchDetailDto>>();
                foreach (var item in lst)
                    item.CreatedTime = _dateTimeHelper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotat,
                    Payload = lst
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"BatchLotRequestGetDetailAsync error:{e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Error",
                    Total = 0,
                    Payload = new List<BatchDetailDto>()
                };
            }
        }

        /// <summary>
        /// Update dừng lô
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> BatchLotRequestStopAsync(Batch_StopRequest request)
        {
            try
            {
                _logger.LogError($"{request.BatchCode} BatchLotRequestStopAsync Input");
                Expression<Func<BatchLotRequest, bool>> query = p => p.BatchCode == request.BatchCode;
                if (request.IsStaff)
                {
                    Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                        p.StaffAccount == request.AccountCode;
                    query = query.And(newQuery);
                }
                else
                {
                    Expression<Func<BatchLotRequest, bool>> newQuery = p =>
                        p.PartnerCode == request.AccountCode;
                    query = query.And(newQuery);
                }

                var single = await _paygateMongoRepository.GetOneAsync(query);
                if (single != null)
                {
                    single.Status = BatchLotRequestStatus.Stop;
                    single.EndProcessTime = DateTime.Now;
                    await _paygateMongoRepository.UpdateOneAsync(single);
                    Expression<Func<BatchDetail, bool>> queryDetail = p => p.BatchCode == request.BatchCode
                                                                           && p.BatchStatus ==
                                                                           BatchLotRequestStatus.Init;
                    var list = await _paygateMongoRepository.GetAllAsync(queryDetail);
                    foreach (var item in list)
                    {
                        item.BatchStatus = BatchLotRequestStatus.Stop;
                        item.UpdateTime = DateTime.Now;
                        await _paygateMongoRepository.UpdateOneAsync(item);
                    }

                    return new MessagePagedResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Success,
                        ResponseMessage = "Thành công",
                    };
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Không tìm thấy lô.",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{request.BatchCode} BatchLotRequestStopAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Error",
                };
            }
        }

        public async Task<BatchItemDto> BatchRequestSingleGetAsync(string batchCode)
        {
            try
            {
                var request = await _paygateMongoRepository.GetOneAsync<BatchLotRequest>(p =>
                    p.BatchCode == batchCode);

                return request?.ConvertTo<BatchItemDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{batchCode} BatchRequestSingleGetAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return null;
            }
        }

        /// <summary>
        /// Cập nhật lô
        /// </summary>
        /// <param name="batchCode"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public async Task UpdateBatchRequestAsync(string batchCode, BatchLotRequestStatus status)
        {
            try
            {
                var request = await _paygateMongoRepository.GetOneAsync<BatchLotRequest>(p =>
                    p.BatchCode == batchCode);
                if (request != null)
                {
                    request.Status = status;
                    await _paygateMongoRepository.UpdateOneAsync(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"UpdateBatchRequestAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        public async Task<BatchDetailDto> BatchRequestGetDetailSingleAsync(string batchCode, string transRef)
        {
            try
            {
                var request = await _paygateMongoRepository.GetOneAsync<BatchDetail>(p =>
                    p.BatchCode == batchCode && p.TransRef == transRef);

                return request?.ConvertTo<BatchDetailDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"BatchRequestGetDetailSingleAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return null;
            }
        }

        public async Task UpdateBatchDetailStatusAsync(string batchCode, string transRef, BatchLotRequestStatus status)
        {
            try
            {
                var single = await _paygateMongoRepository.GetOneAsync<BatchDetail>(p =>
                    p.BatchCode == batchCode && p.TransRef == transRef);

                if (single != null)
                {
                    single.BatchStatus = status;
                    await _paygateMongoRepository.UpdateOneAsync(single);
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(
                    $"UpdateBatchDetailStatusAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        public async Task UpdateBatchDetailStatusTowAsync(string batchCode, string transRef,
            BatchLotRequestStatus batchStatus, SaleRequestStatus status,
            string vender = "", string provider = "")
        {
            try
            {
                var single = await _paygateMongoRepository.GetOneAsync<BatchDetail>(p =>
                    p.BatchCode == batchCode && p.TransRef == transRef);

                if (single != null)
                {
                    single.BatchStatus = batchStatus;
                    single.Status = status;
                    if (!string.IsNullOrEmpty(vender))
                        single.Vendor = vender;
                    if (!string.IsNullOrEmpty(provider))
                        single.Provider = provider;
                    single.UpdateTime = DateTime.Now;
                    await _paygateMongoRepository.UpdateOneAsync(single);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"UpdateBatchDetailStatusTowAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        private async Task UpdateBatchDetailStatusSingleAsync(string transRef, SaleRequestStatus status,
            string vender = "", string provider = "")
        {
            try
            {
                var single = await _paygateMongoRepository.GetOneAsync<BatchDetail>(p =>
                    p.TransRef == transRef);

                if (single != null)
                {
                    single.Status = status;
                    if (!string.IsNullOrEmpty(vender))
                        single.Vendor = vender;
                    if (!string.IsNullOrEmpty(provider))
                        single.Provider = provider;
                    single.UpdateTime = DateTime.Now;
                    await _paygateMongoRepository.UpdateOneAsync(single);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"UpdateBatchDetailStatusSingleAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }


        public async Task PublishConsumerReport(SaleReponseDto response)
        {
            try
            {
                int status;
                switch (response.Status)
                {
                    case SaleRequestStatus.TimeOver:
                    case SaleRequestStatus.Init:
                    case SaleRequestStatus.InProcessing:
                    case SaleRequestStatus.ProcessTimeout:
                    case SaleRequestStatus.WaitForResult:
                    case SaleRequestStatus.WaitForConfirm:
                    case SaleRequestStatus.Paid:
                    case SaleRequestStatus.Undefined:
                        status = 2;
                        break;
                    case SaleRequestStatus.Canceled:
                    case SaleRequestStatus.Failed:
                        status = 3;
                        break;
                    case SaleRequestStatus.Success:
                        status = 1;
                        break;
                    default:
                        status = 2;
                        break;
                }

                await _bus.Publish(new ReportSaleMessage()
                {
                    CorrelationId = response.Sale.Id,
                    PerformAccount = response.Sale.StaffAccount,
                    AccountCode = response.Sale.PartnerCode,
                    ParentCode = response.Sale.ParentCode,
                    CreatedDate = response.Sale.CreatedTime,
                    ProductCode = response.Sale.ProductCode,
                    Amount = response.Sale.Amount,
                    Price = response.Sale.Price,
                    Quantity = response.Sale.Quantity,
                    PaymentAmount = response.Sale.PaymentAmount,
                    Discount = response.Sale.DiscountAmount ?? 0,
                    Fee = response.Sale.Fee ?? 0,
                    ReceivedAccount = response.Sale.ReceiverInfo,
                    ServiceCode = response.Sale.ServiceCode,
                    TransRef = response.Sale.TransRef,
                    TransCode = response.Sale.TransCode,
                    PaidTransCode = response.Sale.PaymentTransCode,
                    PayTransCode = response.Sale.ProviderTransCode,
                    ExtraInfo = response.Sale.ExtraInfo,
                    Status = status,
                    ProviderCode = response.Sale.Provider,
                    VendorCode = response.Sale.Vendor,
                    Channel = response.Sale.Channel.ToString("G"),
                    Balance = response.Balance,
                    NextStep = response.NextStep,
                    FeeInfo = response.FeeDto,
                    ReceiverType = response.Sale.ReceiverType,
                    ProviderReceiverType = response.Sale.ReceiverTypeResponse,
                    ProviderTransCode = response.Sale.ProviderResponseCode,
                    ParentProvider = response.Sale.ParentProvider,
                });

                if (response.Sale.TransRef.StartsWith("PL"))
                    await UpdateBatchDetailStatusSingleAsync(response.Sale.TransRef, response.Sale.Status,
                        response.Sale.Vendor, response.Sale.Provider);
            }
            catch (Exception ex)
            {
                _logger.LogError($"PublishConsumerReport : {ex}");
            }
        }

        public async Task<NewMessageResponseBase<BalanceResponse>> RefundTransaction(string transCode)
        {
            try
            {
                _logger.LogInformation("RefundTransaction request: " + transCode);
                var response = new MessagePagedResponseBase();
                var saleRequest = await SaleRequestGetAsync(transCode);
                if (saleRequest != null)
                {
                    _logger.LogInformation("RefundTransaction process: " + saleRequest.TransCode);
                    if (saleRequest.Status == SaleRequestStatus.Paid ||
                        saleRequest.Status == SaleRequestStatus.Undefined ||
                        saleRequest.Status == SaleRequestStatus.WaitForResult ||
                        saleRequest.Status == SaleRequestStatus.WaitForConfirm ||
                        saleRequest.Status == SaleRequestStatus.ProcessTimeout ||
                        saleRequest.Status == SaleRequestStatus.InProcessing)
                    {
                        if (saleRequest.PaymentAmount > 0 && saleRequest.RevertAmount <= 0 &&
                            !string.IsNullOrEmpty(saleRequest.PaymentTransCode))
                        {
                            response.ResponseCode = ResponseCodeConst.Success;
                            response.ResponseMessage = "Thành công. Hoàn tiền thành công!";
                            var revertAmount = saleRequest.PaymentAmount;
                            saleRequest.RevertAmount = revertAmount;
                            var refundResponse = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync(
                                new BalanceCancelPaymentRequest
                                {
                                    AccountCode = saleRequest.PartnerCode,
                                    RevertAmount = saleRequest.PaymentAmount,
                                    CurrencyCode = CurrencyCode.VND.ToString("G"),
                                    TransRef = saleRequest.TransCode,
                                    TransactionCode = saleRequest.PaymentTransCode,
                                    TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}"
                                });
                            if (refundResponse.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                                return new NewMessageResponseBase<BalanceResponse>
                                {
                                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                                        refundResponse.ResponseStatus.Message)
                                };
                            var result = refundResponse.Results;
                            if (string.IsNullOrEmpty(result.TransactionCode))
                                result.TransactionCode = result.TransactionCode;
                            saleRequest.Status = SaleRequestStatus.Canceled;
                            await SaleRequestUpdateAsync(saleRequest);
                            await _bus.Publish(new ReportTransStatusMessage()
                            {
                                TransCode = saleRequest.TransCode,
                                Status = saleRequest.Status switch
                                {
                                    SaleRequestStatus.Success => 1,
                                    SaleRequestStatus.Failed => 3,
                                    SaleRequestStatus.Canceled => 3,
                                    _ => 2
                                }
                            });
                            return new NewMessageResponseBase<BalanceResponse>
                            {
                                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success,
                                    "Hoàn tiền thành công"),
                                Results = result
                            };
                        }

                        return new NewMessageResponseBase<BalanceResponse>
                        {
                            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                                "Trạng thái giao dịch không hợp lệ")
                        };
                    }

                    _logger.LogInformation($"Trạng thái giao dịch không hợp lệ: {saleRequest.TransCode}");
                    return new NewMessageResponseBase<BalanceResponse>
                    {
                        ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                            "Trạng thái giao dịch không hợp lệ")
                    };
                }

                _logger.LogInformation($"TopupType invalid or NotFound");
                return new NewMessageResponseBase<BalanceResponse>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Loại giao dịch không thể hoàn hoặc không tồn tại")
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"RefundTransaction error: {e}");
                return new NewMessageResponseBase<BalanceResponse>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Hoàn tiền thất bại")
                };
            }
        }

        public async Task<List<SaleRequestDto>> GetSaleRequestPending(int timePending = 0)
        {
            //Tạm thời k xử lý tự động các gd mua mã thẻ
            var list = await _paygateMongoRepository.GetAllAsync<SaleRequest>(x =>
                (x.Status == SaleRequestStatus.ProcessTimeout ||
                 x.Status == SaleRequestStatus.TimeOver ||
                 x.Status == SaleRequestStatus.WaitForConfirm ||
                 //x.Status == SaleRequestStatus.Init ||
                 //x.Status == SaleRequestStatus.Paid ||
                 //x.Status == SaleRequestStatus.Paid ||
                 x.Status == SaleRequestStatus.WaitForResult) &&
                x.CreatedTime <= DateTime.UtcNow.AddMinutes(-timePending) && timePending > 0 &&
                x.ServiceCode != ServiceCodes.PIN_CODE && x.ServiceCode != ServiceCodes.PIN_DATA &&
                x.ServiceCode != ServiceCodes.PIN_GAME && x.Provider != ProviderConst.HLS &&
                x.Provider != ProviderConst.MOBIFONE && x.Provider != ProviderConst.MOBIFONE_TS);
            return list?.ConvertTo<List<SaleRequestDto>>();
        }

        private async Task<CardBatchRequestDto> CardBatchRequestCreateAsync(CardBatchRequestDto cardBatchRequestDto)
        {
            try
            {
                cardBatchRequestDto.CreatedTime = DateTime.Now;
                cardBatchRequestDto.Status = 0;
                cardBatchRequestDto.Amount = cardBatchRequestDto.Amount;
                cardBatchRequestDto.Price =
                    Math.Round(
                        (100 - cardBatchRequestDto.DiscountRate ?? 0) / 100 * cardBatchRequestDto.Amount *
                        cardBatchRequestDto.Quantity, 0);
                cardBatchRequestDto.TransCode = Guid.NewGuid().ToString();
                var saleRequest = cardBatchRequestDto.ConvertTo<CardBatchRequest>();
                if (string.IsNullOrEmpty(saleRequest.Vendor) || string.IsNullOrEmpty(cardBatchRequestDto.Vendor))
                {
                    saleRequest.Vendor =
                        TelcoHepper.GetVendorTrans(cardBatchRequestDto.ServiceCode, cardBatchRequestDto.ProductCode);
                    cardBatchRequestDto.Vendor = saleRequest.Vendor;
                }

                if (string.IsNullOrEmpty(cardBatchRequestDto.ProviderTransCode))
                    cardBatchRequestDto.ProviderTransCode = cardBatchRequestDto.TransCode;
                else
                    cardBatchRequestDto.ProviderTransCode =
                        cardBatchRequestDto.ProviderTransCode + "_" + cardBatchRequestDto.TransCode;
                await _paygateMongoRepository.AddOneAsync(saleRequest);
                return cardBatchRequestDto;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error insert cardBatchRequest: " + ex.Message);
                return null;
            }
        }

        private async Task<CardBatchRequestDto> CardBatchRequestUpdateAsync(CardBatchRequestDto cardBatchRequestDto)
        {
            try
            {
                var cardBatch = await _paygateMongoRepository.GetOneAsync<CardBatchRequest>(x =>
                    x.TransCode == cardBatchRequestDto.TransCode);
                if (cardBatch == null)
                    return null;

                cardBatch = CompareUpdateCardBatch(cardBatch, cardBatchRequestDto);
                await _paygateMongoRepository.UpdateOneAsync(cardBatch);
                return cardBatchRequestDto;
            }
            catch (Exception ex)
            {
                _logger.LogError("CardBatchRequestUpdateAsync : " + ex);
                return null;
            }
        }

        private CardBatchRequest CompareUpdateCardBatch(CardBatchRequest sale, CardBatchRequestDto saleRequestDto)
        {
            if (sale.Amount != saleRequestDto.Amount)
                sale.Amount = saleRequestDto.Amount;

            if (sale.Quantity != saleRequestDto.Quantity)
                sale.Quantity = saleRequestDto.Quantity;

            if (sale.CategoryCode != saleRequestDto.CategoryCode)
                sale.CategoryCode = saleRequestDto.CategoryCode;

            if (sale.ProductCode != saleRequestDto.ProductCode)
                sale.ProductCode = saleRequestDto.ProductCode;

            if (saleRequestDto.DiscountRate != null && sale.DiscountRate != saleRequestDto.DiscountRate)
                sale.DiscountRate = saleRequestDto.DiscountRate;

            if (saleRequestDto.DiscountAmount != null && sale.DiscountAmount != saleRequestDto.DiscountAmount)
                sale.DiscountAmount = saleRequestDto.DiscountAmount;

            if (sale.Status != saleRequestDto.Status)
                sale.Status = saleRequestDto.Status;

            if (sale.Provider != saleRequestDto.Provider)
                sale.Provider = saleRequestDto.Provider;

            if (sale.ProviderTransCode != saleRequestDto.ProviderTransCode)
                sale.ProviderTransCode = saleRequestDto.ProviderTransCode;

            return sale;
        }


        /// <summary>
        /// Lấy thẻ theo APi
        /// </summary>
        /// <param name="cardSaleRequest"></param>
        /// <returns></returns>
        public async Task<NewMessageResponseBase<string>> CardImportProvider(CardImportProviderRequest cardSaleRequest)
        {
            _logger.LogInformation("CardImportProvider {Request}", cardSaleRequest.ToJson());
            var reponse = new NewMessageResponseBase<string>();

            if (string.IsNullOrEmpty(cardSaleRequest.PartnerCode))
            {
                reponse.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Chưa có tài khoản thực hiện.");

                return reponse;
            }

            if (string.IsNullOrEmpty(cardSaleRequest.ProviderCode))
            {
                reponse.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Chưa có nhà cung cấp.");

                return reponse;
            }

            if (cardSaleRequest.CardItems == null || cardSaleRequest.CardItems.Count == 0)
            {
                reponse = new NewMessageResponseBase<string>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Danh sách sản phẩm không hợp lệ."),
                };
                return reponse;
            }

            DateTime dateNow = DateTime.Now;

            var itemBatchs = (from x in cardSaleRequest.CardItems
                group x by new { x.ServiceCode, x.CategoryCode, x.ProductCode, x.CardValue }
                into g
                select new CardBatchRequestDto()
                {
                    UserProcess = cardSaleRequest.PartnerCode,
                    Provider = cardSaleRequest.ProviderCode,
                    Channel = cardSaleRequest.Channel,
                    Quantity = g.Sum(c => c.Quantity),
                    DiscountRate = g.First().Discount,
                    Amount = g.Key.CardValue,
                    ServiceCode = g.Key.ServiceCode,
                    CategoryCode = g.Key.CategoryCode,
                    ProductCode = g.Key.ProductCode,
                    RequestDate = dateNow,                   
                }).ToList();

            var lstBatchs = new List<CardBatchRequestDto>();
            foreach (var item in itemBatchs)
            {
                var batch = await CardBatchRequestCreateAsync(item);
                if (batch != null)
                    lstBatchs.Add(batch);
            }

            var sendCard = await _grpcClient.GetClientCluster(GrpcServiceName.Stock).SendAsync(
                new StockCardImportApiRequest
                {
                    Provider = cardSaleRequest.ProviderCode,
                    Description = cardSaleRequest.Description,
                    ExpiredDate= cardSaleRequest.ExpiredDate,
                    CardItems = lstBatchs.Select(item => new CardImportApiItemRequest()
                    {
                        CategoryCode = item.CategoryCode,
                        ProductCode = item.ProductCode,
                        Quantity = item.Quantity,
                        ServiceCode = item.ServiceCode,
                        TransCode = item.TransCode,
                        TransCodeProvider = item.ProviderTransCode,
                        Discount = float.Parse((item.DiscountRate ?? 0).ToString(CultureInfo.InvariantCulture)),
                        CardValue = item.Amount,
                    }).ToList()
                });

            _logger.LogInformation("CardBatchRequest return {Response}", sendCard.ToJson());

            if (sendCard.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
            {
                var endDate = DateTime.Now;
                var items = sendCard.Results.ConvertTo<List<NewMessageResponseBase<string>>>();
                foreach (var item in lstBatchs)
                {
                    var fItem = items.Where(c => c.Results == item.ProviderTransCode).FirstOrDefault();
                    if (fItem != null)
                    {
                        if (fItem.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                            item.Status = CardBatchRequestStatus.Success;
                        else if (fItem.ResponseStatus.ErrorCode == ResponseCodeConst.ResponseCode_WaitForResult)
                            item.Status = CardBatchRequestStatus.WaitForResult;
                        else
                            item.Status = CardBatchRequestStatus.Failed;
                    }
                    else
                        item.Status = CardBatchRequestStatus.Failed;

                    item.EndDate = endDate;

                    await CardBatchRequestUpdateAsync(item);
                }
            }

            reponse = new NewMessageResponseBase<string>()
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success,
                    "Nhập kho thành công."),
            };

            return reponse;
        }


        public async Task CommissionRequest(SaleRequestDto request)
        {
            try
            {
                _logger.LogInformation(
                    $"Commission push:{request.TransCode}-{request.TransRef}-{request.AgentType}-{request.ParentCode}");
                if (!string.IsNullOrEmpty(request.ParentCode) && request.AgentType == AgentType.SubAgent)
                    await _bus.Publish<CommissionTransactionCommand>(new
                    {
                        request.ReceiverInfo,
                        request.SaleType,
                        request.Channel,
                        request.PartnerCode,
                        TransRef = request.TransCode,
                        request.ServiceCode,
                        request.PaymentAmount,
                        request.ProductCode,
                        request.CategoryCode,
                        request.DiscountAmount,
                        request.Quantity,
                        request.Amount,
                        request.ParentCode,
                        request.AgentType,
                        TimeStamp = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid()
                    });
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{request.TransRef}-{request.TransCode}-CommissionRequest error : {ex}");
            }
        }

        public async Task TransactionCallBackCorrect(CallBackCorrectTransRequest request)
        {
            try
            {
                var saleRequest =
                    await _paygateMongoRepository.GetOneAsync<SaleRequest>(x => x.TransCode == request.TransCode);
                if (saleRequest != null)
                {
                    if (saleRequest.Status == SaleRequestStatus.Success &&
                        request.ResponseCode == ResponseCodeConst.Error)
                    {
                        _logger.LogInformation(
                            $"TransactionCorrect process:{request.TransCode}-{request.ResponseCode}-{request.ResponseMessage}");
                        //Tiến hành hoàn tiền
                        saleRequest.Status = SaleRequestStatus.Paid;
                        var update =
                            await SaleRequestUpdateStatusAsync(saleRequest.TransCode, null, SaleRequestStatus.Paid);
                        if (update)
                        {
                            _logger.LogInformation($"TransactionCorrect UpdateStatus success:{request.TransCode}");
                            var refund = await RefundTransaction(request.TransCode);
                            _logger.LogInformation($"TransactionCorrect Refund:{request.TransCode}-{refund.ToJson()}");
                            try
                            {
                                var text = refund.ResponseStatus.ErrorCode == ResponseCodeConst.Success
                                    ? ($"Hoàn tiền thành công: {refund.Results.TransactionCode}")
                                    : ($"Không thể hoàn tiền cho KH. Vui lòng check lại");
                                await _bus.Publish<SendBotMessage>(new
                                {
                                    MessageType = BotMessageType.Message,
                                    BotType = BotType.Sale,
                                    Module = "Backend",
                                    Title = $"Giao dịch Update sau đối soát từ NCC: {saleRequest.Provider}",
                                    Message =
                                        $"Trạng thái trước: THÀNH CÔNG" +
                                        $"\nTrạng thái sau: LỖI - Hoàn Tiền" +
                                        $"\nMessage: {request.ResponseMessage}" +
                                        $"\nMã GD {saleRequest.TransCode}" +
                                        $"\nRef: {saleRequest.TransRef}" +
                                        $"\nMã TK: {saleRequest.PartnerCode}" +
                                        $"\nMã NCC: {saleRequest.Provider}" +
                                        $"\nSố tiền: {saleRequest.Amount.ToFormat("đ")}" +
                                        $"\nSĐT: {saleRequest.ReceiverInfo}" +
                                        $"\nLoại giao dịch: {saleRequest.SaleType:G}" +
                                        $"\n{text}",
                                    TimeStamp = DateTime.Now,
                                    CorrelationId = Guid.NewGuid()
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"SendTeleMessage : {ex}");
                            }
                        }
                    }
                    else if (request.ResponseCode == ResponseCodeConst.Success)
                    {
                        _logger.LogInformation(
                            $"TransactionCorrect auto retopup process:{request.TransCode}-{request.ResponseCode}-{request.ResponseMessage}");
                        await _bus.Publish<SendBotMessage>(new
                        {
                            MessageType = BotMessageType.Message,
                            BotType = BotType.Sale,
                            Module = "Backend",
                            Title = $"Giao dịch đã được tự động nạp bù từ NCC: {saleRequest.Provider}",
                            Message =
                                $"Trạng thái trước: THÀNH CÔNG" +
                                $"\nTrạng thái sau: LỖI - Đã được tự động nạp bù" +
                                $"\nMessage: {request.ResponseMessage}" +
                                $"\nMã GD {saleRequest.TransCode}" +
                                $"\nRef: {saleRequest.TransRef}" +
                                $"\nMã TK: {saleRequest.PartnerCode}" +
                                $"\nMã NCC: {saleRequest.Provider}" +
                                $"\nSố tiền: {saleRequest.Amount.ToFormat("đ")}" +
                                $"\nSĐT: {saleRequest.ReceiverInfo}" +
                                $"\nLoại giao dịch: {saleRequest.SaleType:G}",
                            TimeStamp = DateTime.Now,
                            CorrelationId = Guid.NewGuid()
                        });
                    }
                    else if (request.ResponseCode == "11")
                    {
                        _logger.LogInformation(
                            $"TransactionCorrect auto retopup process:{request.TransCode}-{request.ResponseCode}-{request.ResponseMessage}");
                        await _bus.Publish<SendBotMessage>(new
                        {
                            MessageType = BotMessageType.Message,
                            BotType = BotType.Sale,
                            Module = "Backend",
                            Title = $"Giao dịch callBack từ NCC: {saleRequest.Provider}",
                            Message =
                                $"Trạng thái trước: LỖI" +
                                $"\nTrạng thái sau: THÀNH CÔNG" +
                                $"\nMessage: {request.ResponseMessage}" +
                                $"\nMã GD {saleRequest.TransCode}" +
                                $"\nRef: {saleRequest.TransRef}" +
                                $"\nMã TK: {saleRequest.PartnerCode}" +
                                $"\nMã NCC: {saleRequest.Provider}" +
                                $"\nSố tiền: {saleRequest.Amount.ToFormat("đ")}" +
                                $"\nSĐT: {saleRequest.ReceiverInfo}" +
                                $"\nLoại giao dịch: {saleRequest.SaleType:G}",
                            TimeStamp = DateTime.Now,
                            CorrelationId = Guid.NewGuid()
                        });
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"TransactionCallBackCorrect error:{e.Message}");
            }
        }

        public async Task<SaleOffsetRequestDto> SaleOffsetOriginGetAsync(string transCode, bool isProcess)
        {
            if (isProcess)
            {
                var saleRequest = await _paygateMongoRepository.GetOneAsync<SaleOffsetRequest>(p =>
                    p.OriginTransCode == transCode && p.Status == SaleRequestStatus.InProcessing);
                return saleRequest?.ConvertTo<SaleOffsetRequestDto>();
            }
            else
            {
                var saleRequest =
                    await _paygateMongoRepository.GetOneAsync<SaleOffsetRequest>(p => p.OriginTransCode == transCode);
                return saleRequest?.ConvertTo<SaleOffsetRequestDto>();
            }
        }

        public async Task<SaleOffsetRequestDto> SaleOffsetGetAsync(string transCode)
        {
            var saleRequest =
                await _paygateMongoRepository.GetOneAsync<SaleOffsetRequest>(p => p.TransRef == transCode);
            return saleRequest?.ConvertTo<SaleOffsetRequestDto>();
        }

        public async Task<SaleOffsetRequestDto> SaleOffsetCreateAsync(SaleRequestDto saleRequestDto, string partnerCode)
        {
            try
            {
                var saleOffsetDto = new SaleOffsetRequestDto()
                {
                    Amount = saleRequestDto.Amount,
                    OriginPartnerCode = saleRequestDto.PartnerCode,
                    OriginTransCode = saleRequestDto.TransCode,
                    OriginTransRef = saleRequestDto.TransRef,
                    OriginCreatedTime = saleRequestDto.CreatedTime,
                    OriginProviderCode = saleRequestDto.Provider,
                    ReceiverInfo = saleRequestDto.ReceiverInfo,
                    ProductCode = saleRequestDto.ProductCode,
                    Status = SaleRequestStatus.InProcessing,
                    Vendor = saleRequestDto.Vendor,
                    CreatedTime = DateTime.Now,
                    ServiceCode = saleRequestDto.ServiceCode,
                    PartnerCode = partnerCode,
                };
                saleOffsetDto.TransRef = Guid.NewGuid().ToString();
                var saleOffsetRequest = saleOffsetDto.ConvertTo<SaleOffsetRequest>();
                saleOffsetRequest.PartnerCode = partnerCode;
                await _paygateMongoRepository.AddOneAsync(saleOffsetRequest);
                return saleOffsetDto;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error insert SaleOffsetCreateAsync: " + ex.Message);
                return null;
            }
        }

        public async Task<SaleOffsetRequestDto> SaleOffsetUpdateAsync(SaleOffsetRequestDto saleOffsetDto)
        {
            try
            {
                var sale = await _paygateMongoRepository.GetOneAsync<SaleOffsetRequest>(x =>
                    x.TransRef == saleOffsetDto.TransRef);
                if (sale == null)
                    return null;

                if (sale.Amount != saleOffsetDto.Amount)
                    sale.Amount = saleOffsetDto.Amount;

                if (sale.ProductCode != saleOffsetDto.ProductCode)
                    sale.ProductCode = saleOffsetDto.ProductCode;

                if (sale.PartnerCode != saleOffsetDto.PartnerCode)
                    sale.PartnerCode = saleOffsetDto.PartnerCode;

                if (sale.TransCode != saleOffsetDto.TransCode)
                    sale.TransCode = saleOffsetDto.TransCode;

                if (sale.Status != saleOffsetDto.Status)
                    sale.Status = saleOffsetDto.Status;

                if (sale.ProviderCode != saleOffsetDto.ProviderCode)
                    sale.ProviderCode = saleOffsetDto.ProviderCode;
                if (sale.Status == SaleRequestStatus.Success)
                {
                    var saleBu = await _paygateMongoRepository.GetOneAsync<SaleRequest>(x =>
                        x.TransRef == saleOffsetDto.TransRef);

                    if (saleBu != null)
                    {
                        sale.ProviderCode = saleBu.Provider;
                        sale.TransCode = saleBu.TransCode;
                    }
                }

                await _paygateMongoRepository.UpdateOneAsync(sale);
                return sale.ConvertTo<SaleOffsetRequestDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError("SaleOffsetUpdateAsync: " + ex);
                return null;
            }
        }

        public async Task<SaleRequestDto> GetLastSaleRequest(string partnercode)
        {
            var item = await _paygateMongoRepository.GetByMaxAsync<SaleRequest>(x => x.PartnerCode == partnercode,
                x => x.AddedAtUtc);
            return item.ConvertTo<SaleRequestDto>();
        }

        public async Task<ResponseMesssageObject<CheckTransResult>> SaleRequestPartnerChecktransAsync(string transCode,
            string partnerCode, string secrectkey)
        {
            try
            {
                var saleRequest = await _paygateMongoRepository.GetOneAsync<SaleRequest>(p =>
                    p.TransRef == transCode && p.PartnerCode == partnerCode);
                var returnMessage = new ResponseMesssageObject<CheckTransResult>
                {
                    ResponseCode = ResponseCodeConst.Error
                };
                if (null != saleRequest)
                {
                    returnMessage.ExtraInfo = saleRequest.TransRef;
                    switch (saleRequest.Status)
                    {
                        case SaleRequestStatus.Success:

                            var saleResponse = new CheckTransResult()
                            {
                                TransCode = (!string.IsNullOrEmpty(saleRequest.ReferenceCode) &&
                                             saleRequest.Status == SaleRequestStatus.Success)
                                    ? saleRequest.ReferenceCode
                                    : saleRequest.TransCode, //bổ sung thêm mã ASIM
                                ReferenceCode = saleRequest.TransRef,
                                Amount = saleRequest.Amount,
                                PaymentAmount = saleRequest.PaymentAmount,
                                Discount = Convert.ToDecimal(saleRequest.DiscountAmount),
                                ReceiverType = saleRequest.ReceiverType switch
                                {
                                    ReceiverType.PostPaid => "TS",
                                    ReceiverType.PrePaid or ReceiverType.Default => "TT",
                                    _ => saleRequest.ReceiverType
                                },
                                ServiceCode = saleRequest.ServiceCode,
                            };

                            if (saleRequest.ServiceCode.StartsWith("PIN_"))
                            {
                                saleResponse.cards = await GetPartnerPinCodeHistoriesAsync(saleRequest, secrectkey);
                            }

                            returnMessage.ResponseCode = ResponseCodeConst.Success;
                            returnMessage.ResponseMessage = "Thành công!";
                            returnMessage.Payload = saleResponse;
                            break;
                        case SaleRequestStatus.Canceled:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_Cancel;
                            returnMessage.ResponseMessage = "Giao dịch đã bị hủy.";
                            break;
                        case SaleRequestStatus.Failed:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_Failed;
                            returnMessage.ResponseMessage = "Giao dịch thất bại.";
                            break;
                        case SaleRequestStatus.TimeOver:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                            returnMessage.ResponseMessage = "Giao dịch quá thời gian chờ.";
                            break;
                        case SaleRequestStatus.InProcessing:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_InProcessing;
                            returnMessage.ResponseMessage = "Giao dịch đang được xử lý.";
                            break;
                        case SaleRequestStatus.Init:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_RequestReceived;
                            returnMessage.ResponseMessage = "Đã tiếp nhận giao dịch thành công";
                            break;
                        case SaleRequestStatus.WaitForResult:
                        case SaleRequestStatus.WaitForConfirm:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            returnMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            break;
                        case SaleRequestStatus.Paid:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_Paid;
                            returnMessage.ResponseMessage = "Giao dịch đã trừ tiền khách hàng - Đang xử lý";
                            break;
                        default:
                            returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            returnMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ.";
                            break;
                    }
                }
                else
                {
                    returnMessage.ResponseCode = ResponseCodeConst.ResponseCode_TransactionNotFound;
                    returnMessage.ResponseMessage = "Giao dịch không tồn tại";
                }

                return returnMessage;
            }
            catch (Exception e)
            {
                _logger.LogError($"SaleRequestChecktransAsync error:{e.Message}");
                return new ResponseMesssageObject<CheckTransResult>
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ."
                };
            }
        }

        private async Task<List<CardResponsePartnerDto>> GetPartnerPinCodeHistoriesAsync(SaleRequest saleRequest,
            string secretKey)
        {
            try
            {
                var cardItems = new List<CardResponsePartnerDto>();
                if (saleRequest == null)
                    return cardItems;

                Expression<Func<SaleItem, bool>> query = p => p.SaleTransCode == saleRequest.TransCode;
                var lst = await _paygateMongoRepository.GetAllAsync(query);

                cardItems = lst.Select(x =>
                {
                    var item = x.ConvertTo<CardResponsePartnerDto>();
                    var date = _dateTimeHelper.ConvertToUserTime(x.CardExpiredDate, DateTimeKind.Utc);
                    item.Serial = x.Serial;
                    item.CardCode =
                        Cryptography.DefaultTripleEncrypt(x.CardCode.DecryptTripleDes(), secretKey);
                    item.CardValue = x.CardValue.ToString();
                    item.ExpireDate = date.ToString("dd/MM/yyyy");
                    return item;
                }).ToList();

                return cardItems;
            }
            catch (Exception e)
            {
                _logger.LogError("TransactionReportsGetAsync error " + e);
                return new List<CardResponsePartnerDto>();
            }
        }

        public async Task<bool> CheckSaleItemAsync(string transCode)
        {
            try
            {
                var check = await _paygateMongoRepository.GetOneAsync<SaleItem>(c => c.SaleTransCode == transCode);
                return check != null;
            }
            catch (Exception ex)
            {
                _logger.LogError("CheckSaleItemAsync error: " + ex.Message);
                return true;
            }
        }

        public async Task<SaleGateRequestDto> SaleGateRequestGetAsync(string transCode)
        {
            try
            {
                var saleRequest = await _paygateMongoRepository.GetOneAsync<SaleGateRequest>(p =>
                    p.TransCode == transCode);

                return saleRequest?.ConvertTo<SaleGateRequestDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"SaleGateRequestGetAsync error:{transCode}-{ex}");
                return null;
            }
        }

        public async Task<SaleGateRequestDto> SaleGateRequestUpdateAsync(SaleGateRequestDto saleRequestDto)
        {
            try
            {
                var topup = await _paygateMongoRepository.GetOneAsync<SaleGateRequest>(x =>
                    x.TransCode == saleRequestDto.TransCode);
                if (topup != null)
                {
                    topup.TopupAmount = saleRequestDto.TopupAmount;
                    topup.TopupProvider = saleRequestDto.TopupProvider;
                    topup.Status = saleRequestDto.Status;
                    topup.EndDate = saleRequestDto.EndDate;
                    await _paygateMongoRepository.UpdateOneAsync(topup);
                    return saleRequestDto;
                }

                _logger.LogError($"{saleRequestDto.TransCode}-SaleGateRequestUpdateAsync null");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{saleRequestDto.TransCode}-SaleGateRequestUpdateAsync error: " + ex);
                return null;
            }
        }

        public async Task<SaleGateRequestDto> SaleGateCreateAsync(SaleGateRequestDto saleRequestDto)
        {
            try
            {
                var saleGateRequest = saleRequestDto.ConvertTo<SaleGateRequest>();
                await _paygateMongoRepository.AddOneAsync(saleGateRequest);
                return saleRequestDto;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{saleRequestDto.TransCode}| Error insert SaleGateCreateAsync: " + ex.Message);
                return null;
            }
        }


        public async Task<List<SaleGateRequestDto>> GetSaleGateRequestPending(int timePending = 0)
        {
            var list = await _paygateMongoRepository.GetAllAsync<SaleGateRequest>(x =>
                x.Status == SaleRequestStatus.WaitForResult &&
                x.CreatedDate <= DateTime.UtcNow.AddMinutes(-timePending) && timePending > 0);
            return list?.ConvertTo<List<SaleGateRequestDto>>();
        }
    }
}