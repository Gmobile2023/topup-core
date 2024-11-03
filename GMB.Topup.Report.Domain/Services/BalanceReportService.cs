using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Report.Domain.Connectors;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Domain.Repositories;
using ServiceStack;

using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Report.Model.Dtos.ResponseDto;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Helpers;
using Microsoft.Extensions.Logging;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Discovery.Requests.Balance;
using GMB.Topup.Gw.Model.Events;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Nest;
using ServiceStack.Caching;
using GMB.Topup.Shared.CacheManager;

namespace GMB.Topup.Report.Domain.Services
{
    public partial class BalanceReportService : IBalanceReportService
    {
        private readonly IReportMongoRepository _reportMongoRepository;
        private readonly ILogger<BalanceReportService> _logger;
        private readonly IDateTimeHelper _dateHepper;
        private readonly WebApiConnector _externalServiceConnector;
        private readonly ICacheManager _cacheManager;
        //private readonly IServiceGateway _gateway; gunner
        private readonly IFileUploadRepository _fileUploadsv;
        private readonly IBus _bus;
        private readonly GrpcClientHepper _grpcClient;
        IConfiguration Configuration { get; }
        private readonly string _apiUrl;

        public BalanceReportService(IReportMongoRepository reportMongoRepository,
            IDateTimeHelper dateHepper, ICacheManager cacheManager,
            WebApiConnector externalServiceConnector,
            ILogger<BalanceReportService> logger,
            IFileUploadRepository fileUploadsv,
            IConfiguration configuration,
            IBus bus,
            GrpcClientHepper grpcClient)
        {
            _reportMongoRepository = reportMongoRepository;
            _dateHepper = dateHepper;
            _externalServiceConnector = externalServiceConnector;
            _logger = logger;
            //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
            _fileUploadsv = fileUploadsv;
            _bus = bus;
            _cacheManager = cacheManager;
            _grpcClient = grpcClient;
            Configuration = configuration;
            _apiUrl = Configuration["ServiceUrlConfig:GatewayPrivate"];
        }

        public async Task<MessagePagedResponseBase> ReportDetailGetList(ReportDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportBalanceHistories, bool>> query = p =>
                    p.CurrencyCode == CurrencyCode.VND.ToString("G");
                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportBalanceHistories, bool>> newQuery = p => p.TransCode == request.TransCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.Filter))
                {
                    Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                        p.TransCode == request.Filter || p.TransRef == request.Filter ||
                        p.Description == request.Filter;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AccountCode))
                {
                    Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                        p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ServiceCode))
                {
                    if (request.ServiceCode == ReportServiceCode.TRANSFER)
                    {
                        Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                            p.ServiceCode == ReportServiceCode.TRANSFER && p.SrcAccountCode == request.AccountCode;
                        query = query.And(newQuery);
                    }
                    else if (request.ServiceCode == "RECEIVE_MONEY")
                    {
                        Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                            p.ServiceCode == ReportServiceCode.TRANSFER && p.DesAccountCode == request.AccountCode;
                        query = query.And(newQuery);
                    }
                    else
                    {
                        Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                            p.ServiceCode == request.ServiceCode;
                        query = query.And(newQuery);
                    }
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                        p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                        p.CreatedDate < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<ReportBalanceHistories>(query);

                var listAll = await _reportMongoRepository.GetAllAsync<ReportBalanceHistories>(query);
                var maxDate = listAll.Max(c => c.CreatedDate);
                var minDate = listAll.Min(c => c.CreatedDate);

                var accMax = listAll.Where(c => c.CreatedDate == maxDate).FirstOrDefault();
                var accMin = listAll.Where(c => c.CreatedDate == minDate).FirstOrDefault();
                var sumTotal = new ReportTransactionDetailDto()
                {
                    Increment = Math.Round(listAll.Sum(c => request.AccountCode == c.DesAccountCode ? c.Amount : 0), 0),
                    Decrement = Math.Round(listAll.Sum(c => request.AccountCode == c.SrcAccountCode ? c.Amount : 0), 0),
                    BalanceAfter =
                      Math.Round(
                            accMax.DesAccountCode == request.AccountCode
                                ? accMax.DesAccountBalanceAfterTrans
                                : accMax.SrcAccountBalanceAfterTrans, 0),
                    BalanceBefore =
                       Math.Round(
                            accMin.DesAccountCode == request.AccountCode
                                ? accMin.DesAccountBalanceBeforeTrans
                                : accMin.SrcAccountBalanceBeforeTrans, 0)
                };

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportBalanceHistories, Guid>(query,
                    s => s.CreatedDate, false,
                    request.Offset, request.Limit);

                foreach (var item in lst)
                {
                    item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = lst.ConvertTo<List<ReportTransactionDetailDto>>()
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<RevenueInDayDto> ReportRevenueInDayQuery(RevenueInDayRequest request)
        {
            try
            {
                DateTime fromDate = DateTime.Now.Date;
                DateTime toDate = DateTime.Now.Date.AddDays(1);

                Expression<Func<ReportItemDetail, bool>> query = p =>
                    (p.AccountCode == request.AccountCode
                     || p.PerformAccount == request.AccountCode)
                    && p.CreatedTime >= fromDate.ToUniversalTime()
                    && p.CreatedTime < toDate.ToUniversalTime()
                    && p.Status == ReportStatus.Success
                    && (p.ServiceCode == ReportServiceCode.TOPUP
                        || p.ServiceCode == ReportServiceCode.PIN_CODE
                        || p.ServiceCode == ReportServiceCode.PIN_DATA
                        || p.ServiceCode == ReportServiceCode.PIN_GAME
                        || p.ServiceCode == ReportServiceCode.PAY_BILL
                    ) && p.TransType != ReportServiceCode.REFUND;


                var lst = await _reportMongoRepository.GetAllAsync(query);

                var value = new RevenueInDayDto()
                {
                    Quantity = lst.Count(),
                    Revenue = Convert.ToDouble(lst.Sum(c => c.Amount)),
                    SalePrice = Convert.ToDouble(lst.Sum(c => c.TotalPrice)),
                };

                return value;
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueInDayQuery error: {e}");
                return null;
            }
        }

        public async Task<ReportItemDetail> ReportTransDetailQuery(TransDetailByTransCodeRequest request)
        {
            try
            {
                ReportItemDetail first = null;
                if (!string.IsNullOrEmpty(request.Type))
                {
                    if (request.Type.ToUpper() == "RequestRef".ToUpper())
                    {
                        first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.RequestRef == request.TransCode);
                    }
                    else if (request.Type.ToUpper() == "TransCode".ToUpper())
                    {
                        first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.TransCode == request.TransCode);
                    }
                    else if (request.Type.ToUpper() == "PaidTransCode".ToUpper())
                    {
                        first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.PaidTransCode == request.TransCode);
                    }
                    else if (request.Type.ToUpper() == "REFUND".ToUpper())
                    {
                        first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.RequestTransSouce == request.TransCode);
                    }
                    else
                    {
                        first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.RequestRef == request.TransCode);
                    }
                }
                else
                {

                    first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                        p.RequestRef == request.TransCode);
                    if (first == null)
                    {
                        first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.TransCode == request.TransCode);
                        if (first == null)
                        {
                            first = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                                p.PaidTransCode == request.TransCode);
                        }
                    }
                }


                if (first != null && first.TransType == ReportServiceCode.PAYCOMMISSION)
                {
                    var transOld = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                        p.CommissionPaidCode == first.TransCode);
                    if (transOld == null)
                        transOld = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(p =>
                            p.CommissionPaidCode == first.PaidTransCode);

                    if (transOld != null)
                    {
                        first.ProvidersCode = transOld.ProvidersCode;
                        first.ProvidersInfo = transOld.ProvidersInfo;
                        first.ProductCode = transOld.ProductCode;
                        first.CategoryCode = transOld.CategoryCode;
                        first.CategoryName = transOld.CategoryName;
                        first.ProductCode = transOld.ProductCode;
                        first.ProductName = transOld.ProductName;
                        first.ReceivedAccount = transOld.ReceivedAccount;
                        first.RequestTransSouce = transOld.RequestRef;
                        first.TransTransSouce = transOld.TransCode;

                        first.ServiceCode = transOld.ServiceCode;
                        first.ServiceName = transOld.ServiceName;
                        first.PerformAccount = transOld.AccountCode;
                        first.PerformInfo = transOld.PerformInfo;
                    }
                }

                return first;
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueInDayQuery error: {e}");
                return null;
            }
        }

        public async Task<MessagePagedResponseBase> ReportTransDetailGetList(ReportTransDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true;
                Expression<Func<ReportItemDetail, bool>> newQueryAccount = p =>
                    (p.AccountCode == request.AccountCode
                     || p.PerformAccount == request.AccountCode);

                query = query.And(newQueryAccount);

                if (!string.IsNullOrEmpty(request.Filter))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.RequestRef.ToLower().Contains(request.Filter.ToLower())
                        || p.PaidTransCode.ToLower().Contains(request.Filter.ToLower())
                        || p.TransCode.ToLower().Contains(request.Filter.ToLower())
                        || p.AccountCode.ToLower().Contains(request.Filter.ToLower())
                        || (p.AccountInfo != null &&
                            p.AccountInfo.ToLower().Contains(request.Filter.ToLower()))
                        || p.PerformAccount.ToLower().Contains(request.Filter.ToLower())
                        || (p.PerformInfo != null &&
                            p.PerformInfo.ToLower().Contains(request.Filter.ToLower()))
                        || p.ReceivedAccount.ToLower().Contains(request.Filter.ToLower());

                    query = query.And(newQuery);
                }

                if (request.Type == 1)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        (p.PerformAgentType == 1 || p.PerformAgentType == 2 ||
                            p.PerformAgentType == 3);
                    query = query.And(newQuery);
                }
                else if (request.Type == 2)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                      p.PerformAgentType == 4;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.RequestTransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.RequestRef == request.RequestTransCode
                                                                             || p.TransCode == request.RequestTransCode
                                                                             || p.PaidTransCode ==
                                                                             request.RequestTransCode;
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(request.ReceivedAccount))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.ReceivedAccount == request.ReceivedAccount;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ProviderCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.VenderCode == request.ProviderCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserProcess))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.PerformAccount == request.UserProcess;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ServiceCode))
                {
                    if (request.ServiceCode == ReportServiceCode.REFUND)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.TransType == ReportServiceCode.REFUND;
                        query = query.And(newQuery);
                    }
                    else if (request.ServiceCode == ReportServiceCode.TRANSFER)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.ServiceCode == ReportServiceCode.TRANSFER && p.PerformAccount == request.AccountCode;
                        query = query.And(newQuery);
                    }
                    else if (request.ServiceCode == ReportServiceCode.RECEIVEMONEY)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.ServiceCode == ReportServiceCode.TRANSFER && p.AccountCode == request.AccountCode;
                        query = query.And(newQuery);
                    }
                    else
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.ServiceCode == request.ServiceCode;
                        query = query.And(newQuery);
                    }
                }

                if (!string.IsNullOrEmpty(request.CategoryCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         p.CategoryCode == request.CategoryCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ProductCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.ProductCode == request.ProductCode;
                    query = query.And(newQuery);
                }


                if (request.Status > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.Status == (ReportStatus)request.Status;
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<ReportItemDetail>(query);
                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);
                var sumData = new ReportTransDetailDto()
                {
                    Amount = Convert.ToDecimal(listAll.Sum(c => c.Amount)),
                    Quantity = listAll.Sum(c => c.Quantity == 0 ? 1 : c.Quantity),
                    Discount = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.Discount), 0)),
                    Fee = Convert.ToDecimal(listAll.Sum(c => c.Fee)),
                    TotalPrice =
                       Convert.ToDecimal(Math.Round(
                            listAll.Sum(x =>
                                (x.ServiceCode == ReportServiceCode.TRANSFER && request.AccountCode == x.PerformAccount)
                                    ? -(x.PaidAmount ?? 0)
                                    : (x.PaidAmount ?? 0)), 0)),
                    PriceOut = Convert.ToDecimal(listAll.Sum(x => x.TransType == ReportServiceCode.REFUND
                        ? 0
                        : (x.ServiceCode == ReportServiceCode.TOPUP
                           || x.ServiceCode == ReportServiceCode.TOPUP_DATA
                           || x.ServiceCode == ReportServiceCode.PAY_BILL
                           || x.ServiceCode == ReportServiceCode.PIN_DATA
                           || x.ServiceCode == ReportServiceCode.PIN_GAME
                           || x.ServiceCode == ReportServiceCode.PIN_CODE
                           || x.ServiceCode == ReportServiceCode.CORRECTDOWN
                           || (x.ServiceCode == ReportServiceCode.TRANSFER && request.AccountCode == x.PerformAccount))
                            ? -Math.Abs(x.TotalPrice)
                            : 0)),
                    PriceIn = Convert.ToDecimal(listAll.Sum(x => x.TransType == ReportServiceCode.REFUND
                                               || (x.ServiceCode == ReportServiceCode.TRANSFER &&
                                                   request.AccountCode == x.AccountCode)
                                               || x.ServiceCode == ReportServiceCode.PAYBATCH
                                               || x.ServiceCode == ReportServiceCode.PAYCOMMISSION
                                               || x.ServiceCode == ReportServiceCode.DEPOSIT
                                               || x.ServiceCode == ReportServiceCode.CORRECTUP
                        ? Math.Abs(x.TotalPrice)
                        : 0)),
                };

                var maxDate = listAll.Max(c => c.CreatedTime);
                sumData.Balance = Convert.ToDecimal(listAll.Where(c => c.CreatedTime == maxDate).FirstOrDefault().Balance);

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportItemDetail, Guid>(query,
                    s => s.CreatedTime, false,
                    request.Offset, request.Limit);

                var listView = (from x in lst.ToList()
                                select new ReportTransDetailDto
                                {
                                    StatusName = x.Status == ReportStatus.Success ? "Thành công"
                                        : x.Status == ReportStatus.TimeOut ? "Chưa có KQ"
                                        : x.Status == ReportStatus.Error ? "Lỗi"
                                        : "Chưa có KQ",
                                    Status = x.Status,
                                    CategoryCode = x.CategoryCode,
                                    TransType = string.IsNullOrEmpty(x.TransType) ? x.ServiceCode : x.TransType,
                                    ServiceCode = x.ServiceCode,
                                    TransTypeName = (x.TransType == "REFUND")
                                        ? "Hoàn tiền"
                                        : x.ServiceCode == ReportServiceCode.TOPUP
                                            ? "Nạp tiền điện thoại"
                                            : x.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                ? "Nạp data"
                                                : x.ServiceCode == ReportServiceCode.PAY_BILL
                                                    ? "Thanh toán hóa đơn"
                                                    : x.ServiceCode == ReportServiceCode.PIN_DATA
                                                        ? "Mua thẻ Data"
                                                        : x.ServiceCode == ReportServiceCode.PIN_GAME
                                                            ? "Mua thẻ Game"
                                                            : x.ServiceCode == ReportServiceCode.PIN_CODE
                                                                ? "Mua mã thẻ"
                                                                : x.ServiceCode == ReportServiceCode.PAYBATCH
                                                                    ? "Trả thưởng"
                                                                    : x.ServiceCode == "CORRECT_UP"
                                                                        ? "Điều chỉnh tăng"
                                                                        : x.ServiceCode == "CORRECT_DOWN"
                                                                            ? "Điều chỉnh giảm"
                                                                            : x.ServiceCode == "TRANSFER" &&
                                                                              request.AccountCode == x.AccountCode
                                                                                ? "Nhận tiền đại lý"
                                                                                : x.ServiceCode == "TRANSFER" &&
                                                                                  request.AccountCode == x.PerformAccount
                                                                                    ? "Chuyển tiền đại lý"
                                                                                    : x.ServiceCode ==
                                                                                      ReportServiceCode.DEPOSIT
                                                                                        ? "Nạp tiền"
                                                                                        : "",
                                    TransCode = x.TransType == "REFUND" ? x.PaidTransCode :
                                        string.IsNullOrEmpty(x.RequestRef) ? x.TransCode : x.RequestRef,
                                    AccountRef = x.ServiceCode == "TRANSFER"
                                        ? (request.AccountCode != x.AccountCode
                                            ? (!string.IsNullOrEmpty(x.AccountInfo)
                                                ? x.AccountCode + " - " + x.AccountInfo
                                                : x.AccountCode)
                                            : "")
                                        : (x.ReceivedAccount ?? string.Empty),
                                    Vender = x.VenderName,
                                    Amount = Convert.ToDecimal(x.Price),
                                    Price = Convert.ToDecimal(x.TotalPrice),
                                    PriceIn = Convert.ToDecimal(x.PriceIn),
                                    PriceOut = Convert.ToDecimal(x.PriceOut),
                                    Quantity = x.Quantity,
                                    Discount = Convert.ToDecimal(x.Discount),
                                    Fee = Convert.ToDecimal(x.Fee),
                                    TotalPrice = Convert.ToDecimal((x.ServiceCode == "TRANSFER" && request.AccountCode == x.PerformAccount)
                                        ? -(x.PaidAmount ?? 0)
                                        : (x.PaidAmount ?? 0)),
                                    Balance = Convert.ToDecimal(((x.ServiceCode == "TRANSFER" && request.AccountCode == x.PerformAccount)
                                        ? x.PerformBalance
                                        : x.Balance) ?? 0),
                                    CreatedDate = x.CreatedTime,
                                    UserProcess =
                                        x.ServiceCode == ReportServiceCode.DEPOSIT ? (!string.IsNullOrEmpty(x.PerformInfo)
                                            ? x.PerformAccount + " - " + x.PerformInfo
                                            : !string.IsNullOrEmpty(x.AccountInfo)
                                                ? x.AccountCode + " - " + x.AccountInfo
                                                : string.Empty)
                                        : (x.ServiceCode == "CORRECT_UP" || x.ServiceCode == "CORRECT_DOWN" ||
                                           x.ServiceCode == "REFUND" || x.TransType == "REFUND") ? ""
                                        : (x.ServiceCode == "TRANSFER") ? (request.AccountCode == x.AccountCode
                                            ? (!string.IsNullOrEmpty(x.PerformInfo)
                                                ? x.PerformAccount + " - " + x.PerformInfo
                                                : x.PerformAccount)
                                            : "")
                                        : (!string.IsNullOrEmpty(x.PerformInfo)
                                            ? x.PerformAccount + " - " + x.PerformInfo
                                            : x.PerformAccount),
                                    RequestTransSouce = x.RequestTransSouce ?? string.Empty,
                                    TransTransSouce = x.TransTransSouce ?? string.Empty,
                                    TransNote = x.TransNote ?? string.Empty,
                                }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumData,
                    Payload = listView,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportDebtDetailGetList(ReportDebtDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportStaffDetail, bool>> query = p => p.AccountCode == request.AccountCode;

                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportStaffDetail, bool>> newQuery = p =>
                        p.RequestRef.Contains(request.TransCode);
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ServiceCode))
                {
                    Expression<Func<ReportStaffDetail, bool>> newQuery = p =>
                        p.ServiceCode == request.ServiceCode;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportStaffDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportStaffDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<ReportStaffDetail>(query);
                var listAll = await _reportMongoRepository.GetAllAsync<ReportStaffDetail>(query);
                var sumTotal = new ReportStaffDetail()
                {
                    Price = Math.Round(listAll.Sum(c => c.Price), 0),
                    DebitAmount = Math.Round(listAll.Sum(c => c.DebitAmount), 0),
                    CreditAmount = Math.Round(listAll.Sum(c => c.CreditAmount), 0),
                };

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportStaffDetail, Guid>(query,
                    s => s.CreatedTime, false,
                    request.Offset, request.Limit);

                foreach (var item in lst)
                {
                    item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                    item.TransCode = string.IsNullOrEmpty(item.RequestRef) ? item.TransCode : item.RequestRef;
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = lst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportRefundDetailGetList(ReportRefundDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true
                                                                      && p.TransType == ReportServiceCode.REFUND;
                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.PaidTransCode != null && p.PaidTransCode.ToLower().Contains(request.TransCode.ToLower());
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.TransCodeSouce))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.RequestTransSouce != null &&
                        p.RequestTransSouce.ToLower().Contains(request.TransCodeSouce.ToLower());
                    query = query.And(newQuery);
                }


                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    serviceCode.AddRange(request.ServiceCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    categoryCode.AddRange(request.CategoryCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    productCode.AddRange(request.ProductCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }

                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<ReportItemDetail>(query);
                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);
                var sumTotal = new ReportRefundDetailDto()
                {
                    Price = Convert.ToDecimal(listAll.Sum(c => Math.Abs(c.TotalPrice))),
                    Discount = Convert.ToDecimal(listAll.Sum(c => Math.Abs(c.Discount))),
                    Fee = Convert.ToDecimal(listAll.Sum(c => Math.Abs(c.Fee))),
                };

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportItemDetail, Guid>(query,
                    s => s.CreatedTime, true,
                    request.Offset, request.Limit);


                var list = (from x in lst
                            select new ReportRefundDetailDto()
                            {
                                CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc),
                                AgentCode = x.AccountCode,
                                AgentInfo = !string.IsNullOrEmpty(x.AccountInfo)
                                    ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                                Price = Convert.ToDecimal(Math.Round(Math.Abs(x.TotalPrice), 0)),
                                Discount = Convert.ToDecimal(Math.Round(Math.Abs(x.Discount), 0)),
                                Fee = Convert.ToDecimal(Math.Abs(x.Fee)),
                                ProductCode = x.ProductCode,
                                ProductName = x.ProductName,
                                ServiceCode = x.ServiceCode,
                                ServiceName = x.ServiceName,
                                TransCode = x.PaidTransCode,
                                TransCodeSouce = x.RequestTransSouce,
                                AgentName = x.AccountAgentName,
                                CategoryCode = x.CategoryCode,
                                CategoryName = x.CategoryName,
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = list,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRefundDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportTransferDetailGetList(ReportTransferDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p =>
                    p.ServiceCode == ReportServiceCode.TRANSFER && p.Status == ReportStatus.Success;

                if (!string.IsNullOrEmpty(request.AgentTransferCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.PerformAccount == request.AgentTransferCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentReceiveCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.AccountCode == request.AgentReceiveCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.TransCode == request.TransCode;
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }


                var total = await _reportMongoRepository.CountAsync<ReportItemDetail>(query);
                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);
                var sumTotal = new ReportTransferDetailDto()
                {
                    Price = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.TotalPrice), 0)),
                };

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportItemDetail, Guid>(query,
                    s => s.CreatedTime, false,
                    request.Offset, request.Limit);

                foreach (var item in lst)
                {
                    item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                    item.TransCode = string.IsNullOrEmpty(item.RequestRef) ? item.TransCode : item.RequestRef;
                }

                var list = (from x in lst
                            select new ReportTransferDetailDto()
                            {
                                AgentType = x.AccountAgentType,
                                AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                                AgentReceiveCode = x.AccountCode,
                                AgentReceiveInfo = !string.IsNullOrEmpty(x.AccountInfo)
                                    ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                                AgentTransfer = x.PerformAccount,
                                AgentTransferInfo = !string.IsNullOrEmpty(x.PerformInfo)
                                    ? x.PerformAccount + " - " + x.PerformInfo
                                    : x.PerformAccount,
                                CreatedTime = x.CreatedTime,
                                Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                                ServiceCode = x.ServiceCode,
                                ServiceName = x.ServiceName,
                                TransCode = x.TransCode,
                                Messager = x.TransNote,
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    SumData = sumTotal,
                    Total = (int)total,
                    Payload = list,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportServiceDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportDepositDetailGetList(ReportDepositDetailRequest request)
        {
            try
            {
                Expression<Func<ReportItemDetail, bool>> query = p =>
                    p.ServiceCode == ReportServiceCode.DEPOSIT && p.Status == ReportStatus.Success;

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.TransCode == request.TransCode;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<ReportItemDetail>(query);
                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);
                var sumTotal = new ReportTransferDetailDto()
                {
                    Price = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.TotalPrice), 0)),
                };

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportItemDetail, Guid>(query,
                    s => s.CreatedTime, false,
                    request.Offset, request.Limit);

                foreach (var item in lst)
                {
                    item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                    item.TransCode = string.IsNullOrEmpty(item.RequestRef) ? item.TransCode : item.RequestRef;
                }

                var list = (from x in lst
                            select new ReportDepositDetailDto()
                            {
                                AgentType = x.AccountAgentType,
                                AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                                AgentCode = x.AccountCode,
                                AgentInfo = !string.IsNullOrEmpty(x.AccountInfo)
                                    ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                                CreatedTime = x.CreatedTime,
                                Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                                ServiceCode = x.ServiceCode,
                                ServiceName = x.ServiceName,
                                TransCode = x.TransCode,
                                Messager = x.TransNote,
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    SumData = sumTotal,
                    Total = (int)total,
                    Payload = list,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportDepositDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportServiceDetailGetList(ReportServiceDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true &&
                                                                      (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.TransType != ReportServiceCode.REFUND;
                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(request.AgentCodeParent))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ParentCode == request.AgentCodeParent;
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.TransCode != null && p.TransCode.ToLower().Contains(request.TransCode.ToLower());
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.RequestRef))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.RequestRef != null && p.RequestRef.ToLower().Contains(request.RequestRef.ToLower());
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.PayTransRef))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.PayTransRef != null && p.PayTransRef.ToLower().Contains(request.PayTransRef.ToLower());
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ProviderReceiverType))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ProviderReceiverType == request.ProviderReceiverType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ReceiverType))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ReceiverType == request.ReceiverType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ProviderTransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ProviderTransCode == request.ProviderTransCode;
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(request.ParentProvider))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ParentProvider == request.ParentProvider;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserAgentStaffCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.PerformAccount == request.UserAgentStaffCode || p.AccountCode == request.UserAgentStaffCode;
                    query = query.And(newQuery);
                }

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                var providerCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a);
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    foreach (var a in request.CategoryCode)
                        if (!string.IsNullOrEmpty(a))
                            categoryCode.Add(a);
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    foreach (var a in request.ProductCode)
                        if (!string.IsNullOrEmpty(a))
                            productCode.Add(a);
                }

                if (request.VenderCode != null && request.VenderCode.Count > 0)
                {
                    foreach (var a in request.VenderCode)
                        if (!string.IsNullOrEmpty(a))
                            providerCode.Add(a);
                }


                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }

                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }


                if (providerCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        providerCode.Contains(p.ProvidersCode);
                    query = query.And(newQuery);
                }

                //if (!string.IsNullOrEmpty(request.VenderCode))
                //{
                //    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                //        p.ProvidersCode == request.VenderCode;
                //    query = query.And(newQuery);
                //}

                if (!string.IsNullOrEmpty(request.ReceivedAccount))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.ReceivedAccount != null &&
                        p.ReceivedAccount.ToLower().Contains(request.ReceivedAccount.ToLower());
                    query = query.And(newQuery);
                }


                if (request.AgentType > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountAgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                if (request.Status > 0)
                {
                    var status = ReportStatus.Process;
                    if (request.Status == 1) status = ReportStatus.Success;
                    else if (request.Status == 2 || request.Status == 0) status = ReportStatus.TimeOut;
                    else if (request.Status == 3) status = ReportStatus.Error;
                    if (status == ReportStatus.TimeOut)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.Status == status || p.Status == ReportStatus.Process;
                        query = query.And(newQuery);
                    }
                    else
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.Status == status;
                        query = query.And(newQuery);
                    }
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleCode == request.UserSaleCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleLeaderCode == request.UserSaleLeaderCode;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }


                if (request.CityId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCityId == request.CityId;
                    query = query.And(newQuery);
                }

                if (request.DistrictId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         p.AccountDistrictId == request.DistrictId;
                    query = query.And(newQuery);
                }

                if (request.WardId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountWardId == request.WardId;
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<ReportItemDetail>(query);

                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);


                var sumTotal = new ReportServiceDetailDto()
                {
                    Quantity = listAll.Sum(c => c.Quantity),
                    Discount = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.Discount), 0)),
                    Fee = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.Fee), 0)),
                    Price = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.TotalPrice), 0)),
                    Value = Convert.ToDecimal(Math.Round(listAll.Sum(c => c.Amount), 0)),
                    CommistionAmount = Convert.ToDecimal(Convert.ToDecimal(Math.Round(listAll.Sum(c => c.CommissionAmount ?? 0), 0))),
                };

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportItemDetail, Guid>(query,
                    s => s.CreatedTime, false,
                    request.Offset, request.Limit);

                foreach (var item in lst)
                {
                    item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                }

                var list = (from x in lst
                            select new ReportServiceDetailDto()
                            {
                                AgentType = x.AccountAgentType,
                                AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                                AgentCode = x.AccountCode,
                                AgentInfo = !string.IsNullOrEmpty(x.AccountInfo)
                                    ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                                StaffCode = x.SaleCode,
                                StaffInfo = x.SaleInfo,
                                CreatedTime = x.CreatedTime,
                                Discount = Convert.ToDecimal(Math.Round(x.Discount, 0)),
                                Quantity = x.Quantity,
                                Fee = Convert.ToDecimal(Math.Round(x.Fee, 0)),
                                Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                                Value = Convert.ToDecimal(Math.Round(x.Price, 0)),
                                CommistionAmount = Convert.ToDecimal(x.CommissionAmount ?? 0),
                                AgentParentInfo = x.AccountAgentType == 5 ? x.ParentCode : "",
                                ProductCode = x.ProductCode,
                                ProductName = x.ProductName,
                                ServiceCode = x.ServiceCode,
                                ServiceName = x.ServiceName,
                                Status = x.Status == ReportStatus.Success
                                    ? 1
                                    : x.Status == ReportStatus.TimeOut
                                        ? 2
                                        : 3,
                                StatusName = x.Status == ReportStatus.Success
                                    ? "Thành công"
                                    : (x.Status == ReportStatus.TimeOut || x.Status == ReportStatus.Process)
                                        ? "Chưa có kết quả"
                                        : "Lỗi",
                                TransCode = x.TransCode,
                                RequestRef = x.RequestRef,
                                PayTransRef = x.PayTransRef,
                                CategoryCode = x.CategoryCode,
                                CategoryName = x.CategoryName,
                                UserProcess = !string.IsNullOrEmpty(x.PerformInfo)
                                    ? x.PerformAccount + " - " + x.PerformInfo
                                    : x.PerformAccount,
                                VenderCode = x.ProvidersCode,
                                VenderName = request.AccountType == 0 ? x.ProvidersInfo : "",
                                Channel = x.Channel,
                                ReceivedAccount = x.ReceivedAccount,
                                ReceiverType = x.ReceiverType == "POSTPAID" ? "Trả sau" : x.ReceiverType == "PREPAID" ? "Trả trước" : "",
                                ProviderReceiverType = x.ProviderReceiverType == "TS" ? "Trả sau" : x.ProviderReceiverType == "TT" ? "Trả trước" : "",
                                ProviderTransCode = x.ProviderTransCode,
                                ParentProvider = x.ParentProvider,
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    SumData = sumTotal,
                    Total = (int)total,
                    Payload = list,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportServiceDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportServiceTotalGetList(ReportServiceTotalRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true && p.Status == ReportStatus.Success &&
                                                                      (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.TransType != ReportServiceCode.REFUND;


                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();

                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a);
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    categoryCode.AddRange(request.CategoryCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    productCode.AddRange(request.ProductCode.Where(a => !string.IsNullOrEmpty(a)));
                }




                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }


                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountAgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ProviderReceiverType))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ProviderReceiverType == request.ProviderReceiverType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.ReceiverType))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.ReceiverType == request.ReceiverType;
                    query = query.And(newQuery);
                }

                var listSouces = _reportMongoRepository.GetAll<ReportItemDetail>(query);
                var list = (from g in listSouces
                            select new ReportServiceTotalDto()
                            {
                                ServiceCode = g.ServiceCode,
                                ServiceName = g.ServiceName,
                                CategoryCode = g.CategoryCode,
                                CategoryName = g.CategoryName,
                                ProductCode = g.ProductCode,
                                ProductName = g.ProductName,
                                Quantity = g.Quantity,
                                Discount = Convert.ToDecimal(Math.Round(g.Discount, 0)),
                                Fee = Convert.ToDecimal(Math.Round(g.Fee, 0)),
                                Price = Convert.ToDecimal(Math.Round(g.TotalPrice, 0)),
                            }).OrderBy(c => c.ProductCode).ToList();

                var listGroup = (from g in list
                                 group g by new
                                 { g.ServiceCode, g.ServiceName, g.CategoryCode, g.CategoryName, g.ProductCode, g.ProductName }
                    into g
                                 select new ReportServiceTotalDto()
                                 {
                                     ServiceCode = g.Key.ServiceCode,
                                     ServiceName = g.Key.ServiceName,
                                     CategoryCode = g.Key.CategoryCode,
                                     CategoryName = g.Key.CategoryName,
                                     ProductCode = g.Key.ProductCode,
                                     ProductName = g.Key.ProductName,
                                     Quantity = g.Sum(c => c.Quantity),
                                     Discount = g.Sum(c => c.Discount),
                                     Fee = g.Sum(c => c.Fee),
                                     Price = g.Sum(c => c.Price)
                                 }).OrderBy(c => c.ProductCode).ToList();


                var total = listGroup.Count();
                var sumTotal = new ReportServiceTotalDto()
                {
                    Quantity = listGroup.Sum(c => c.Quantity),
                    Discount = listGroup.Sum(c => c.Discount),
                    Fee = listGroup.Sum(c => c.Fee),
                    Price = listGroup.Sum(c => c.Price),
                };

                var lst = listGroup.Skip(request.Offset).Take(request.Limit);

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = lst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportServiceTotalGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportAgentBalanceGetList(ReportAgentBalanceRequest request)
        {
            try
            {
                if (request.ToDate != null)
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);

                var fromDateLimit = request.FromDate.Value.AddDays(-35);
                Expression<Func<ReportAccountBalanceDay, bool>> query = p => true
                                                                             && p.CurrencyCode == "VND" &&
                                                                             p.AccountType == "CUSTOMER" &&
                                                                             p.CreatedDay >= fromDateLimit.ToUniversalTime() &&
                                                                             p.CreatedDay < request.ToDate.Value.ToUniversalTime();

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p => p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p => p.AgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode) || !string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportAccountDto, bool>> queryUserSale = p => true;
                    if (!string.IsNullOrEmpty(request.UserSaleCode))
                    {
                        Expression<Func<ReportAccountDto, bool>> newSale = p =>
                            p.SaleCode == request.UserSaleCode;
                        queryUserSale = queryUserSale.And(newSale);
                    }

                    if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                    {
                        Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                            p.LeaderCode == request.UserSaleLeaderCode;
                        queryUserSale = queryUserSale.And(newQuery);
                    }

                    var sysLeader = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(queryUserSale);
                    var sysAcountLeader = sysLeader.Select(c => c.AccountCode).Distinct().ToList();
                    Expression<Func<ReportAccountBalanceDay, bool>> newQueryAccount = p =>
                        sysAcountLeader.Contains(p.AccountCode);
                    query = query.And(newQueryAccount);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportAccountDto, bool>> newQuerySale = p =>
                            p.LeaderCode == request.LoginCode;

                        var sysLeader = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(newQuerySale);
                        var sysAcountLeader = sysLeader.Select(c => c.AccountCode).Distinct().ToList();
                        Expression<Func<ReportAccountBalanceDay, bool>> newQueryList = p =>
                            sysAcountLeader.Contains(p.AccountCode);

                        query = query.And(newQueryList);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportAccountDto, bool>> newQuerySale = p =>
                            p.SaleCode == request.LoginCode;

                        var sysLeader = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(newQuerySale);
                        var sysAcountLeader = sysLeader.Select(c => c.AccountCode).Distinct().ToList();
                        Expression<Func<ReportAccountBalanceDay, bool>> newQueryList = p =>
                            sysAcountLeader.Contains(p.AccountCode);
                        query = query.And(newQueryList);
                    }
                }

                var listSouces = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(query);

                #region 1.Đầu kỳ

                var listBefore = listSouces.Where(c => c.CreatedDay < request.FromDate.Value.ToUniversalTime());

                var listGroupBefore = from x in listBefore
                                      group x by new { x.AccountCode } into g
                                      select new ReportAgentBalanceTemp()
                                      {
                                          AgentCode = g.Key.AccountCode,
                                          MaxDate = g.Max(c => c.CreatedDay),
                                      };

                var listViewBefore = from x in listGroupBefore
                                     join yc in listBefore on x.AgentCode equals yc.AccountCode
                                     where x.MaxDate == yc.CreatedDay
                                     select new ReportAgentBalanceTemp()
                                     {
                                         AgentCode = x.AgentCode,
                                         BeforeAmount = yc.BalanceAfter,
                                     };

                #endregion

                #region 2.Cuối kỳ

                var listGroupAfter = from x in listSouces
                                     group x by new { x.AccountCode } into g
                                     select new ReportAgentBalanceTemp()
                                     {
                                         AgentCode = g.Key.AccountCode,
                                         MaxDate = g.Max(c => c.CreatedDay),
                                     };

                var listViewAfter = from x in listGroupAfter
                                    join yc in listSouces on x.AgentCode equals yc.AccountCode
                                    where x.MaxDate == yc.CreatedDay
                                    select new ReportAgentBalanceTemp()
                                    {
                                        AgentCode = x.AgentCode,
                                        AfterAmount = yc.BalanceAfter,
                                    };

                #endregion

                var listKy = listSouces.Where(c => c.CreatedDay >= request.FromDate.Value.ToUniversalTime());


                var listGroupKy = (from x in listKy
                                   group x by x.AccountCode into g
                                   select new ReportAgentBalanceTemp
                                   {
                                       AgentCode = g.Key,
                                       InputAmount = Math.Round(g.Sum(c => c.IncDeposit ?? 0), 0),
                                       AmountUp = Math.Round(g.Sum(c => c.IncOther ?? 0), 0),
                                       SaleAmount = Math.Round(g.Sum(c => c.DecPayment ?? 0), 0),
                                       AmountDown = Math.Round(g.Sum(c => c.DecOther ?? 0), 0)
                                   }).ToList();


                var listView = (from c in listViewAfter
                                join k in listGroupKy on c.AgentCode equals k.AgentCode into gk
                                from ky in gk.DefaultIfEmpty()
                                join d in listViewBefore on c.AgentCode equals d.AgentCode into gd
                                from before in gd.DefaultIfEmpty()
                                select new ReportAgentBalanceTemp()
                                {
                                    AgentType = 1,
                                    AgentTypeName = c.AgentTypeName,
                                    AgentCode = c.AgentCode,
                                    InputAmount = ky?.InputAmount ?? 0,
                                    AmountUp = ky?.AmountUp ?? 0,
                                    SaleAmount = ky?.SaleAmount ?? 0,
                                    AmountDown = ky?.AmountDown ?? 0,
                                    BeforeAmount = before?.BeforeAmount ?? 0,
                                    AfterAmount = c.AfterAmount,
                                }).ToList();


                var total = listView.Count;
                var sumTotal = new ReportAgentBalanceDto()
                {
                    BeforeAmount = Math.Round(listView.Sum(c => c.BeforeAmount), 0),
                    AfterAmount = Math.Round(listView.Sum(c => c.AfterAmount), 0),
                    InputAmount = listView.Sum(c => c.InputAmount),
                    AmountUp = listView.Sum(c => c.AmountUp),
                    SaleAmount = listView.Sum(c => c.SaleAmount),
                    AmountDown = listView.Sum(c => c.AmountDown),
                };

                var lst = listView.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit).ToList();

                var agentCodes = lst.Where(c => !string.IsNullOrEmpty(c.AgentCode)).Select(c => c.AgentCode).Distinct()
                    .ToList();
                Expression<Func<ReportAccountDto, bool>> queryAgent = p => agentCodes.Contains(p.AccountCode);
                var lstSysAgent = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(queryAgent);

                var accountSale = lstSysAgent.Where(c => !string.IsNullOrEmpty(c.SaleCode)).Select(c => c.SaleCode)
                    .Distinct().ToList();
                var accountLeaderSale = lstSysAgent.Where(c => !string.IsNullOrEmpty(c.LeaderCode))
                    .Select(c => c.LeaderCode).Distinct().ToList();
                accountSale.AddRange(accountLeaderSale);

                Expression<Func<ReportAccountDto, bool>> queryLeader = p => accountSale.Contains(p.AccountCode);
                var lstSysLeader = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(queryLeader);

                var list = (from item in lst
                            join a in lstSysAgent on item.AgentCode equals a.AccountCode into ag
                            from account in ag.DefaultIfEmpty()
                            select new ReportAgentBalanceDto()
                            {
                                AgentType = account?.AgentType ?? 1,
                                AgentTypeName = account != null ? GetAgenTypeName(account.AgentType) : string.Empty,
                                AgentCode = item.AgentCode,
                                AgentInfo = account != null
                                    ? account.AccountCode + " - " + account.Mobile + " - " + account.FullName
                                    : "",
                                SaleCode = account != null ? account.SaleCode : string.Empty,
                                SaleInfo = "",
                                SaleLeaderCode = account != null ? account.LeaderCode : string.Empty,
                                SaleLeaderInfo = "",
                                AfterAmount = Math.Round(item.AfterAmount, 0),
                                BeforeAmount = Math.Round(item.BeforeAmount, 0),
                                InputAmount = item.InputAmount,
                                AmountUp = item.AmountUp,
                                SaleAmount = item.SaleAmount,
                                AmountDown = item.AmountDown,
                            }).OrderBy(c => c.AgentCode).ToList();


                var listViewData = (from item in list
                                    join s in lstSysLeader on item.SaleCode equals s.AccountCode into sg
                                    from sale in sg.DefaultIfEmpty()
                                    join l in lstSysLeader on item.SaleLeaderCode equals l.AccountCode into lg
                                    from lead in lg.DefaultIfEmpty()
                                    select new ReportAgentBalanceDto()
                                    {
                                        AgentType = item.AgentType,
                                        AgentTypeName = item.AgentTypeName,
                                        AgentCode = item.AgentCode,
                                        AgentInfo = item.AgentInfo,
                                        SaleCode = item.SaleCode,
                                        SaleInfo = sale != null
                                            ? sale?.UserName + " - " + sale?.Mobile + " - " + sale?.FullName
                                            : string.Empty,
                                        SaleLeaderCode = item.SaleLeaderCode,
                                        SaleLeaderInfo = lead != null
                                            ? lead.UserName + " - " + lead.Mobile + " - " + lead.FullName
                                            : string.Empty,
                                        AfterAmount = Math.Round(item.AfterAmount, 0),
                                        BeforeAmount = Math.Round(item.BeforeAmount, 0),
                                        InputAmount = item.InputAmount,
                                        AmountUp = item.AmountUp,
                                        SaleAmount = item.SaleAmount,
                                        AmountDown = item.AmountDown,
                                    }).OrderBy(c => c.AgentCode).ToList();


                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = listViewData,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportAgentBalanceGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportRevenueAgentGetList(ReportRevenueAgentRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true && p.Status == ReportStatus.Success &&
                                                                      (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.TransType != ReportServiceCode.REFUND;
                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a);
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    foreach (var a in request.CategoryCode)
                        if (!string.IsNullOrEmpty(a))
                            categoryCode.Add(a);
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    foreach (var a in request.ProductCode)
                        if (!string.IsNullOrEmpty(a))
                            productCode.Add(a);
                }


                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }


                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }


                if (request.AgentType > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountAgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                if (request.CityId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         p.AccountCityId == request.CityId;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleCode == request.UserSaleCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleLeaderCode == request.UserSaleLeaderCode;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }


                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                var list = (from g in lstSearch
                            select new ReportRevenueAgentDto
                            {
                                AgentCode = g.AccountCode,
                                AgentInfo = string
                                    .Empty, //g.AccountItem != null ? g.AccountCode + " - " + g.AccountItem.Mobile + " - " + g.AccountItem.FullName : g.AccountCode,
                                AgentName = string.Empty, //g.AccountItem != null ? g.AccountItem.AgentName : "",
                                AgentTypeName = GetAgenTypeName(g.AccountAgentType),
                                CityInfo = g.AccountCityName,
                                DistrictInfo = g.AccountDistrictName,
                                WardInfo = g.AccountWardName,
                                CityId = g.AccountCityId,
                                DistrictId = g.AccountDistrictId,
                                WardId = g.AccountWardId,
                                SaleCode = g.SaleCode,
                                SaleInfo = string
                                    .Empty, //g.SaleItem != null ? g.SaleItem.UserName + " - " + g.SaleItem.Mobile + " - " + g.SaleItem.FullName : "",
                                LeaderCode = g.SaleLeaderCode,
                                SaleLeaderInfo =
                                    string.Empty, //g.SaleLeaderItem != null ? g.SaleLeaderItem.UserName + " - " + g.SaleLeaderItem.Mobile + " - " + g.SaleLeaderItem.FullName : "",
                                Quantity = g.Quantity,
                                Discount = Convert.ToDecimal(g.Discount),
                                Fee = Convert.ToDecimal(g.Fee),
                                Price = Convert.ToDecimal(g.TotalPrice),
                            }).ToList();

                var listGroup = (from x in list
                                 group x by new
                                 {
                                     x.AgentCode,
                                     x.AgentInfo,
                                     x.AgentName,
                                     x.AgentTypeName,
                                     x.CityInfo,
                                     x.DistrictInfo,
                                     x.WardInfo,
                                     x.CityId,
                                     x.DistrictId,
                                     x.WardId,
                                     x.SaleCode,
                                     x.SaleInfo,
                                     x.LeaderCode,
                                     x.SaleLeaderInfo
                                 }
                    into g
                                 select new ReportRevenueAgentDto()
                                 {
                                     AgentCode = g.Key.AgentCode,
                                     AgentInfo = g.Key.AgentInfo,
                                     AgentName = g.Key.AgentName,
                                     AgentTypeName = g.Key.AgentTypeName,
                                     CityInfo = g.Key.CityInfo,
                                     DistrictInfo = g.Key.DistrictInfo,
                                     WardInfo = g.Key.WardInfo,
                                     CityId = g.Key.CityId,
                                     DistrictId = g.Key.DistrictId,
                                     WardId = g.Key.WardId,
                                     SaleCode = g.Key.SaleCode,
                                     SaleInfo = g.Key.SaleInfo,
                                     LeaderCode = g.Key.LeaderCode,
                                     SaleLeaderInfo = g.Key.SaleLeaderInfo,
                                     Quantity = g.Count(),
                                     Discount = g.Sum(c => c.Discount),
                                     Fee = g.Sum(c => c.Fee),
                                     Price = g.Sum(c => c.Price),
                                 });


                var reportRevenueAgentDtos = listGroup as ReportRevenueAgentDto[] ?? listGroup.ToArray();
                var total = reportRevenueAgentDtos.Count();
                var sumtotal = new ReportRevenueAgentDto()
                {
                    Quantity = reportRevenueAgentDtos.Sum(c => c.Quantity),
                    Discount = reportRevenueAgentDtos.Sum(c => c.Discount),
                    Price = reportRevenueAgentDtos.Sum(c => c.Price),
                    Fee = reportRevenueAgentDtos.Sum(c => c.Fee),
                };
                var lst = reportRevenueAgentDtos.Skip(request.Offset).Take(request.Limit).ToList();

                var agentCodes = lst.Where(c => !string.IsNullOrEmpty(c.AgentCode)).Select(c => c.AgentCode).Distinct()
                    .ToList();
                var saleCodes = lst.Where(c => !string.IsNullOrEmpty(c.SaleCode)).Select(c => c.SaleCode).Distinct()
                    .ToList();
                var leadCodes = lst.Where(c => !string.IsNullOrEmpty(c.LeaderCode)).Select(c => c.LeaderCode).Distinct()
                    .ToList();
                agentCodes.AddRange(saleCodes);
                agentCodes.AddRange(leadCodes);
                Expression<Func<ReportAccountDto, bool>> querySales = p => agentCodes.Contains(p.AccountCode);
                var lstSysAccounts = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(querySales);

                var msglst = (from x in lst
                              join a in lstSysAccounts on x.AgentCode equals a.AccountCode into ag
                              from agent in ag.DefaultIfEmpty()
                              join s in lstSysAccounts on x.SaleCode equals s.AccountCode into sg
                              from sale in sg.DefaultIfEmpty()
                              join l in lstSysAccounts on x.LeaderCode equals l.AccountCode into lg
                              from lead in lg.DefaultIfEmpty()
                              select new ReportRevenueAgentDto()
                              {
                                  AgentCode = x.AgentCode,
                                  AgentInfo = agent != null
                                      ? agent?.AccountCode + " - " + agent?.Mobile + " - " + agent?.FullName
                                      : x.AgentCode,
                                  AgentName = agent != null ? agent?.AgentName : string.Empty,
                                  AgentTypeName = x.AgentTypeName,
                                  CityInfo = x.CityInfo,
                                  DistrictInfo = x.DistrictInfo,
                                  WardInfo = x.WardInfo,
                                  CityId = x.CityId,
                                  DistrictId = x.DistrictId,
                                  WardId = x.WardId,
                                  SaleInfo = sale != null
                                      ? sale?.UserName + " - " + sale?.Mobile + " - " + sale?.FullName
                                      : string.Empty,
                                  SaleLeaderInfo = lead != null
                                      ? lead.UserName + " - " + lead.Mobile + " - " + lead.FullName
                                      : string.Empty,
                                  Quantity = x.Quantity,
                                  Discount = x.Discount,
                                  Fee = x.Fee,
                                  Price = x.Price,
                              }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = msglst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueAgentGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportRevenueCityGetList(ReportRevenueCityRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true && p.Status == ReportStatus.Success &&
                                                                      (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.TransType != ReportServiceCode.REFUND;

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    serviceCode.AddRange(request.ServiceCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    categoryCode.AddRange(request.CategoryCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    productCode.AddRange(request.ProductCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }

                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountAgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleCode == request.UserSaleCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleLeaderCode == request.UserSaleLeaderCode;
                    query = query.And(newQuery);
                }

                if (request.CityId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       p.AccountCityId == request.CityId;
                    query = query.And(newQuery);
                }

                if (request.DistrictId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                      p.AccountDistrictId == request.DistrictId;
                    query = query.And(newQuery);
                }

                if (request.WardId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountWardId == request.WardId;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                var detailList = (from x in lstSearch
                                  select new ReportRevenueCityDto
                                  {
                                      AccountCode = x.AccountCode,
                                      CityInfo = x.AccountCityName,
                                      DistrictInfo = x.AccountDistrictName,
                                      WardInfo = x.AccountWardName,
                                      CityId = x.AccountCityId,
                                      DistrictId = x.AccountDistrictId,
                                      WardId = x.AccountWardId,
                                      Price = Convert.ToDecimal(x.TotalPrice),
                                      Discount = Convert.ToDecimal(x.Discount),
                                      Fee = Convert.ToDecimal(x.Fee),
                                      Quantity = 1,
                                  });

                var list = (from x in detailList
                            group x by new
                            { x.AccountCode, x.CityInfo, x.DistrictInfo, x.WardInfo, x.CityId, x.DistrictId, x.WardId }
                    into g
                            select new ReportRevenueCityDto()
                            {
                                AccountCode = g.Key.AccountCode,
                                CityInfo = g.Key.CityInfo,
                                DistrictInfo = g.Key.DistrictInfo,
                                WardInfo = g.Key.WardInfo,
                                CityId = g.Key.CityId,
                                DistrictId = g.Key.DistrictId,
                                WardId = g.Key.WardId,
                                QuantityAgent = 0,
                                Discount = g.Sum(c => c.Discount),
                                Quantity = g.Count(),
                                Price = g.Sum(c => c.Price),
                                Fee = g.Sum(c => c.Fee)
                            });

                var listGroup = (from x in list
                                 group x by new { x.CityInfo, x.CityId, x.DistrictInfo, x.DistrictId, x.WardInfo, x.WardId }
                        into g
                                 select new ReportRevenueCityDto()
                                 {
                                     CityInfo = g.Key.CityInfo,
                                     DistrictInfo = g.Key.DistrictInfo,
                                     WardInfo = g.Key.WardInfo,
                                     CityId = g.Key.CityId,
                                     DistrictId = g.Key.DistrictId,
                                     WardId = g.Key.WardId,
                                     QuantityAgent = g.Count(),
                                     Discount = Math.Round(g.Sum(c => c.Discount), 0),
                                     Quantity = g.Sum(c => c.Quantity),
                                     Price = Math.Round(g.Sum(c => c.Price), 0),
                                     Fee = Math.Round(g.Sum(c => c.Fee), 0)
                                 }).OrderBy(c => c.CityInfo)
                    .ThenBy(c => c.DistrictInfo)
                    .ThenBy(c => c.WardInfo);

                var total = listGroup.Count();
                var sumtotal = new ReportRevenueCityDto()
                {
                    QuantityAgent = listGroup.Sum(c => c.QuantityAgent),
                    Discount = Math.Round(listGroup.Sum(c => c.Discount), 0),
                    Quantity = listGroup.Sum(c => c.Quantity),
                    Price = Math.Round(listGroup.Sum(c => c.Price), 0),
                    Fee = Math.Round(listGroup.Sum(c => c.Fee), 0),
                };
                var lst = listGroup.Skip(request.Offset).Take(request.Limit).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = lst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueCityGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportTotalSaleAgentGetList(ReportTotalSaleAgentRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true && p.Status == ReportStatus.Success &&
                                                                      (p.ServiceCode == ReportServiceCode.PIN_CODE ||
                                                                       p.ServiceCode == ReportServiceCode.PIN_DATA ||
                                                                       p.ServiceCode == ReportServiceCode.PIN_GAME)
                                                                      && p.TransType != ReportServiceCode.REFUND;

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         p.AccountAgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    serviceCode.AddRange(request.ServiceCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    categoryCode.AddRange(request.CategoryCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    productCode.AddRange(request.ProductCode.Where(a => !string.IsNullOrEmpty(a)));
                }


                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }


                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }


                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleCode == request.UserSaleCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleLeaderCode == request.UserSaleLeaderCode;
                    query = query.And(newQuery);
                }

                if (request.CityId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       p.AccountCityId == request.CityId;
                    query = query.And(newQuery);
                }

                if (request.WardId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountWardId == request.WardId;
                    query = query.And(newQuery);
                }

                if (request.DistrictId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       p.AccountDistrictId == request.DistrictId;
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }


                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                var list = (from g in lstSearch
                            select new ReportTotalSaleAgentDto
                            {
                                AgentCode = g.AccountCode,
                                AgentInfo = string
                                    .Empty, //g.AccountItem != null ? g.AccountCode + " - " + g.AccountItem.Mobile + " - " + g.AccountItem.FullName : g.AccountCode,
                                AgentName = string.Empty, //g.AccountItem != null ? g.AccountItem.AgentName : "",
                                AgentTypeName = GetAgenTypeName(g.AccountAgentType),
                                CityInfo = g.AccountCityName,
                                DistrictInfo = g.AccountDistrictName,
                                WardInfo = g.AccountWardName,
                                WardId = g.AccountWardId,
                                DistrictId = g.AccountDistrictId,
                                CityId = g.AccountCityId,
                                SaleCode = g.SaleCode,
                                SaleInfo = string
                                    .Empty, //g.SaleItem != null ? g.SaleItem.UserName + " - " + g.SaleItem.Mobile + " - " + g.SaleItem.FullName : "",
                                LeaderCode = g.SaleLeaderCode,
                                SaleLeaderInfo =
                                    string.Empty, //g.SaleLeaderItem != null ? g.SaleLeaderItem.UserName + " - " + g.SaleLeaderItem.Mobile + " - " + g.SaleLeaderItem.FullName : "",
                                Quantity = g.Quantity,
                                Discount = Convert.ToDecimal(g.Discount),
                                Fee = Convert.ToDecimal(g.Fee),
                                Price = Convert.ToDecimal(g.TotalPrice),
                            });

                var listGroup = (from x in list
                                 group x by new
                                 {
                                     x.AgentCode,
                                     x.AgentInfo,
                                     x.AgentName,
                                     x.AgentTypeName,
                                     x.CityInfo,
                                     x.DistrictInfo,
                                     x.WardInfo,
                                     x.CityId,
                                     x.DistrictId,
                                     x.WardId,
                                     x.SaleCode,
                                     x.SaleInfo,
                                     x.LeaderCode,
                                     x.SaleLeaderInfo
                                 }
                    into g
                                 select new ReportTotalSaleAgentDto
                                 {
                                     AgentCode = g.Key.AgentCode,
                                     AgentInfo = g.Key.AgentInfo,
                                     AgentName = g.Key.AgentName,
                                     AgentTypeName = g.Key.AgentTypeName,
                                     CityInfo = g.Key.CityInfo,
                                     DistrictInfo = g.Key.DistrictInfo,
                                     WardInfo = g.Key.WardInfo,
                                     CityId = g.Key.CityId,
                                     DistrictId = g.Key.DistrictId,
                                     WardId = g.Key.WardId,
                                     SaleCode = g.Key.SaleCode,
                                     SaleInfo = g.Key.SaleInfo,
                                     LeaderCode = g.Key.LeaderCode,
                                     SaleLeaderInfo = g.Key.SaleLeaderInfo,
                                     Quantity = g.Sum(c => c.Quantity),
                                     Discount = Math.Round(g.Sum(c => c.Discount), 0),
                                     Fee = Math.Round(g.Sum(c => c.Fee), 0),
                                     Price = Math.Round(g.Sum(c => c.Price), 0),
                                 }).OrderBy(c => c.AgentCode);

                var total = listGroup.Count();
                var sumtotal = new ReportTotalSaleAgentDto()
                {
                    Quantity = listGroup.Sum(c => c.Quantity),
                    Discount = Math.Round(listGroup.Sum(c => c.Discount), 0),
                    Price = Math.Round(listGroup.Sum(c => c.Price), 0),
                    Fee = Math.Round(listGroup.Sum(c => c.Fee), 0),
                };
                var lst = listGroup.Skip(request.Offset).Take(request.Limit).ToList();
                var agentCodes = lst.Where(c => !string.IsNullOrEmpty(c.AgentCode)).Select(c => c.AgentCode).Distinct()
                    .ToList();
                var saleCodes = lst.Where(c => !string.IsNullOrEmpty(c.SaleCode)).Select(c => c.SaleCode).Distinct()
                    .ToList();
                var leadCodes = lst.Where(c => !string.IsNullOrEmpty(c.LeaderCode)).Select(c => c.LeaderCode).Distinct()
                    .ToList();
                agentCodes.AddRange(saleCodes);
                agentCodes.AddRange(leadCodes);
                Expression<Func<ReportAccountDto, bool>> querySales = p => agentCodes.Contains(p.AccountCode);
                var lstSysAccounts = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(querySales);

                var msglst = (from x in lst
                              join a in lstSysAccounts on x.AgentCode equals a.AccountCode into ag
                              from agent in ag.DefaultIfEmpty()
                              join s in lstSysAccounts on x.SaleCode equals s.AccountCode into sg
                              from sale in sg.DefaultIfEmpty()
                              join l in lstSysAccounts on x.LeaderCode equals l.AccountCode into lg
                              from lead in lg.DefaultIfEmpty()
                              select new ReportTotalSaleAgentDto
                              {
                                  AgentCode = x.AgentCode,
                                  AgentInfo = agent != null
                                      ? agent?.AccountCode + " - " + agent?.Mobile + " - " + agent?.FullName
                                      : string.Empty,
                                  AgentName = agent != null ? agent?.AgentName : string.Empty,
                                  AgentTypeName = x.AgentTypeName,
                                  CityInfo = x.CityInfo,
                                  DistrictInfo = x.DistrictInfo,
                                  WardInfo = x.WardInfo,
                                  CityId = x.CityId,
                                  DistrictId = x.DistrictId,
                                  WardId = x.WardId,
                                  SaleCode = x.SaleCode,
                                  SaleInfo = sale != null
                                      ? sale?.UserName + " - " + sale?.Mobile + " - " + sale?.FullName
                                      : string.Empty,
                                  LeaderCode = x.LeaderCode,
                                  SaleLeaderInfo = lead != null
                                      ? lead.UserName + " - " + lead.Mobile + " - " + lead.FullName
                                      : string.Empty,
                                  Quantity = x.Quantity,
                                  Discount = x.Discount,
                                  Fee = x.Fee,
                                  Price = x.Price,
                              }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = msglst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportTotalSaleAgentGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportRevenueActiveGetList(ReportRevenueActiveRequest request)
        {
            try
            {
                #region 1.Query với thông tin về Account

                Expression<Func<ReportAccountDto, bool>> queryAccount = p => true
                                                                             && (p.AccountType == 1 ||
                                                                                 p.AccountType == 2 ||
                                                                                 p.AccountType == 3);

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.AgentType == request.AgentType;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode))
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.SaleCode == request.UserSaleCode;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.LeaderCode == request.UserSaleLeaderCode;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (request.CityId > 0)
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.CityId == request.CityId;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (request.DistrictId > 0)
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.DistrictId == request.DistrictId;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (request.WardId > 0)
                {
                    Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                        p.WardId == request.WardId;
                    queryAccount = queryAccount.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        queryAccount = queryAccount.And(newQuery);
                    }
                }

                #endregion

                #region 2.Query với thông tin về giao dịch

                Expression<Func<ReportItemDetail, bool>> queryTrans = p => true && p.Status == ReportStatus.Success &&
                                                                           (p.ServiceCode == ReportServiceCode.TOPUP
                                                                            || p.ServiceCode ==
                                                                            ReportServiceCode.TOPUP_DATA
                                                                            || p.ServiceCode ==
                                                                            ReportServiceCode.PIN_CODE
                                                                            || p.ServiceCode ==
                                                                            ReportServiceCode.PIN_DATA
                                                                            || p.ServiceCode ==
                                                                            ReportServiceCode.PIN_GAME
                                                                            || p.ServiceCode ==
                                                                            ReportServiceCode.PAY_BILL
                                                                            || p.ServiceCode ==
                                                                            ReportServiceCode.DEPOSIT)
                                                                           && p.TransType != ReportServiceCode.REFUND;


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    queryTrans = queryTrans.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);

                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    queryTrans = queryTrans.And(newQuery);
                }

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountAgentType == request.AgentType;
                    queryTrans = queryTrans.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    queryTrans = queryTrans.And(newQuery);
                }

                if (request.CityId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCityId == request.CityId;
                    queryTrans = queryTrans.And(newQuery);
                }

                if (request.DistrictId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountDistrictId == request.DistrictId;
                    queryTrans = queryTrans.And(newQuery);
                }

                if (request.WardId > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       p.AccountWardId == request.WardId;
                    queryTrans = queryTrans.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        queryTrans = queryTrans.And(newQuery);
                    }
                }

                if (!string.IsNullOrEmpty(request.UserSaleCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleCode == request.UserSaleCode;
                    queryTrans = queryTrans.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.UserSaleLeaderCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.SaleLeaderCode == request.UserSaleLeaderCode;
                    queryTrans = queryTrans.And(newQuery);
                }

                #endregion

                #region 3.Mapping Query

                #region 3.1.Report

                var listAllTrans = await _reportMongoRepository.GetAllAsync(queryTrans);

                var listMapTrans = (from x in listAllTrans
                                    select new ReportRevenueActiveDto()
                                    {
                                        AgentCode = x.AccountCode,
                                        Deposit = Convert.ToDecimal((x.ServiceCode == ReportServiceCode.DEPOSIT ||
                                                   x.ServiceCode == ReportServiceCode.TRANSFER)
                                            ? x.TotalPrice
                                            : 0),
                                        Sale = Convert.ToDecimal((x.ServiceCode == ReportServiceCode.TOPUP ||
                                                x.ServiceCode == ReportServiceCode.TOPUP_DATA ||
                                                x.ServiceCode == ReportServiceCode.PIN_CODE
                                                || x.ServiceCode == ReportServiceCode.PIN_DATA ||
                                                x.ServiceCode == ReportServiceCode.PIN_GAME
                                                || x.ServiceCode == ReportServiceCode.PAY_BILL)
                                            ? x.TotalPrice
                                            : 0)
                                    }).ToList();


                var listFillTrans = from x in listMapTrans
                                    group x by new
                                    {
                                        x.AgentCode,
                                    }
                    into g
                                    select new ReportRevenueActiveDto
                                    {
                                        AgentCode = g.Key.AgentCode,
                                        Sale = g.Sum(c => c.Sale),
                                        Deposit = g.Sum(c => c.Deposit),
                                    };

                #endregion

                var listAllAccount = await _reportMongoRepository.GetAllAsync(queryAccount);

                var listAllCompare = from c in listAllAccount
                                     join r in listFillTrans on c.AccountCode equals r.AgentCode into gr
                                     from sg in gr.DefaultIfEmpty()
                                     select new ReportRevenueActiveDto()
                                     {
                                         AgentCode = c.AccountCode,
                                         AgentInfo = c.AccountCode + " - " + c.Mobile + " - " + c.FullName,
                                         AgentName = c.AgentName,
                                         AgentTypeName = GetAgenTypeName(c.AgentType),
                                         IdIdentity = c.IdIdentity,
                                         CityInfo = c.CityName,
                                         DistrictInfo = c.DistrictName,
                                         WardInfo = c.WardName,
                                         CityId = c.CityId,
                                         DistrictId = c.DistrictId,
                                         WardId = c.WardId,
                                         SaleInfo = c.SaleCode,
                                         SaleLeaderInfo = c.LeaderCode,
                                         Deposit = sg != null ? sg.Deposit : 0,
                                         Sale = sg != null ? sg.Sale : 0,
                                     };


                //Lọc theo trạng thái
                if (request.Status == 1)
                    listAllCompare = listAllCompare.Where(c => c.Deposit > 0 && c.Sale > 0);
                else if (request.Status == 2)
                    listAllCompare = listAllCompare.Where(c => c.Deposit <= 0 || c.Sale <= 0);


                //Tinh tổng
                var total = listAllCompare.Count();
                var sumData = new ReportRevenueActiveDto()
                {
                    Sale = Math.Round(listAllCompare.Sum(c => c.Sale), 0),
                    Deposit = Math.Round(listAllCompare.Sum(c => c.Deposit), 0)
                };

                //Lấy số bản ghi cần hiển thị
                var lst = listAllCompare.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit).ToList();

                //Lấy ra sale,saleLead
                var saleCodes = lst.Where(c => !string.IsNullOrEmpty(c.SaleInfo)).Select(c => c.SaleInfo).Distinct()
                    .ToList();
                var saleLeads = lst.Where(c => !string.IsNullOrEmpty(c.SaleLeaderInfo)).Select(c => c.SaleLeaderInfo)
                    .Distinct().ToList();
                saleCodes.AddRange(saleLeads);
                Expression<Func<ReportAccountDto, bool>> querySales = p => saleCodes.Contains(p.AccountCode);
                var listAccountSales = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(querySales);

                //Map sale,saleLead để trả về dữ liệu
                var listViewReponse = (from x in lst
                                       join s in listAccountSales on x.SaleInfo equals s.AccountCode into saleg
                                       from sAccountSale in saleg.DefaultIfEmpty()
                                       join l in listAccountSales on x.SaleLeaderInfo equals l.AccountCode into leadg
                                       from sAccountLead in leadg.DefaultIfEmpty()
                                       select new ReportRevenueActiveDto()
                                       {
                                           AgentCode = x.AgentCode,
                                           AgentInfo = x.AgentInfo,
                                           AgentName = x.AgentName,
                                           AgentTypeName = x.AgentTypeName,
                                           IdIdentity = x.IdIdentity,
                                           CityInfo = x.CityInfo,
                                           DistrictInfo = x.DistrictInfo,
                                           WardInfo = x.WardInfo,
                                           CityId = x.CityId,
                                           DistrictId = x.DistrictId,
                                           WardId = x.WardId,
                                           Deposit = x.Deposit,
                                           Sale = x.Sale,
                                           Status = x.Sale > 0 && x.Deposit > 0 ? "Đạt" : "Không đạt",
                                           SaleInfo = sAccountSale != null
                                               ? sAccountSale?.UserName + " - " + sAccountSale?.Mobile + " - " + sAccountSale?.FullName
                                               : string.Empty,
                                           SaleLeaderInfo = sAccountLead != null
                                               ? sAccountLead.UserName + " - " + sAccountLead.Mobile + " - " + sAccountLead.FullName
                                               : string.Empty,
                                       }).ToList();


                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumData,
                    Payload = listViewReponse,
                };

                #endregion
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueActiveGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportSyncAccountFullInfoRequest(ReportSyncAccounMessage message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.AccountCode) && message.UserId <= 0)
                    return new MessagePagedResponseBase
                    {
                        ResponseCode = "00"
                    };

                ReportAccountDto accountInfo = await GetAccountBackend(message.AccountCode);

                var request = await _externalServiceConnector.GetUserInfoDtoAsync(message.AccountCode,
                    userId: Convert.ToInt32(message.UserId));
                if (request != null)
                {
                    if (accountInfo != null)
                    {
                        accountInfo.FullName = request.FullName;
                        accountInfo.UserName = request.UserName;
                        accountInfo.Mobile = request.PhoneNumber;
                        accountInfo.TreePath = request.TreePath;
                        accountInfo.AgentType = request.AgentType;
                        accountInfo.AccountType = request.AccountType;
                        accountInfo.AgentName = request.AgentName;
                        accountInfo.ParentCode = request.ParentCode;
                        accountInfo.UserSaleLeadId = request.UserSaleLeadId;
                        accountInfo.SaleCode = request.SaleCode;
                        accountInfo.LeaderCode = request.LeaderCode;
                        accountInfo.NetworkLevel = request.NetworkLevel;
                        accountInfo.CreationTime = request.CreationTime;
                        accountInfo.LeaderCode = request.LeaderCode;
                        accountInfo.SaleCode = request.SaleCode;
                        if (request.Unit != null)
                        {
                            accountInfo.CityId = request.Unit.CityId;
                            accountInfo.DistrictId = request.Unit.DistrictId;
                            accountInfo.WardId = request.Unit.WardId;
                            accountInfo.CityName = request.Unit.CityName;
                            accountInfo.DistrictName = request.Unit.DistrictName;
                            accountInfo.WardName = request.Unit.WardName;
                            accountInfo.IdIdentity = request.Unit.IdIdentity;
                            accountInfo.ChatId = request.Unit.ChatId;
                        }

                        await _reportMongoRepository.UpdateAccount(accountInfo);
                        if (new[] { 1, 2, 3, 4, 5, 6 }.Contains(accountInfo.AgentType))
                        {
                            var dateNow = DateTime.Now.Date;
                            var date = new DateTime(dateNow.Year, dateNow.Month, 1);
                            await SysAccountBalanceDay(accountInfo.AccountCode, date.AddDays(-60), date);
                        }
                    }
                    else
                    {
                        accountInfo = new ReportAccountDto()
                        {
                            UserId = request.Id,
                            AccountCode = request.AccountCode,
                            UserName = request.UserName,
                            FullName = request.FullName,
                            Mobile = request.PhoneNumber,
                            AccountType = request.AccountType,
                            AgentType = request.AgentType,
                            AgentName = request.AgentName,
                            ParentCode = request.ParentCode,
                            TreePath = request.TreePath,
                            UserSaleLeadId = request.UserSaleLeadId ?? 0,
                            CityId = request.Unit?.CityId ?? 0,
                            DistrictId = request.Unit?.DistrictId ?? 0,
                            WardId = request.Unit?.WardId ?? 0,
                            CityName = request.Unit != null ? request.Unit.CityName : string.Empty,
                            DistrictName = request.Unit != null ? request.Unit.DistrictName : string.Empty,
                            WardName = request.Unit != null ? request.Unit.WardName : string.Empty,
                            IdIdentity = request.Unit != null ? request.Unit.IdIdentity : string.Empty,
                            ChatId = request.Unit != null ? request.Unit.ChatId : string.Empty,
                            SaleCode = request.SaleCode,
                            LeaderCode = request.LeaderCode,
                            NetworkLevel = request.NetworkLevel,
                            CreationTime = request.CreationTime,
                        };
                        await _reportMongoRepository.UpdateAccount(accountInfo);
                        if (new[] { 1, 2, 3, 4, 5, 6 }.Contains(accountInfo.AgentType))
                        {
                            var dateNow = DateTime.Now.Date;
                            var date = new DateTime(dateNow.Year, dateNow.Month, 1);
                            await SysAccountBalanceDay(accountInfo.AccountCode, date.AddDays(-60), date);
                        }
                    }

                    return new MessagePagedResponseBase
                    {
                        ResponseCode = "01",
                        ResponseMessage = "Thành công",
                    };
                }
                else
                {
                    return new MessagePagedResponseBase
                    {
                        ResponseCode = "99"
                    };
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportSyncInfoAccountRequest error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<List<ReportItemDetail>> ReportQueryItemRequest(DateTime date, string transCode)
        {
            try
            {
                var toDate = date.Date.AddDays(1);
                Expression<Func<ReportItemDetail, bool>> query = p =>
                    p.CreatedTime >= date.Date.ToUniversalTime()
                    && p.CreatedTime < toDate.ToUniversalTime();

                if (!string.IsNullOrEmpty(transCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.TransCode == transCode;
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                return lstSearch;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportQueryItemRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<ReportItemDetail>();
            }
        }

        public async Task<List<ReportBalanceHistories>> ReportQueryHistoryRequest(DateTime date, string transCode)
        {
            try
            {
                var toDate = date.Date.AddDays(1);
                Expression<Func<ReportBalanceHistories, bool>> query = p =>
                    p.CreatedDate >= date.Date.ToUniversalTime()
                    && p.CreatedDate < toDate.ToUniversalTime();

                if (!string.IsNullOrEmpty(transCode))
                {
                    Expression<Func<ReportBalanceHistories, bool>> newQuery = p =>
                        p.TransCode == transCode;
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                return lstSearch;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportQueryHistoryRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<ReportBalanceHistories>();
            }
        }

        public async Task<MessagePagedResponseBase> ReportSyncNxtProviderRequest(SyncNXTProviderRequest request)
        {
            try
            {
                Expression<Func<ReportCardStockProviderByDate, bool>> query = p
                    => p.ProviderCode == request.ProviderCode
                       && p.ProductCode == request.ProductCode
                       && p.ShortDate == request.ShortDate;

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                if (lstSearch.Count >= 1)
                {
                    var item = lstSearch.First();
                    item.InventoryBefore = request.InventoryBefore;
                    item.InventoryAfter = request.InventoryAfter;
                    item.Increase = request.Increase;
                    item.Decrease = request.Decrease;
                    item.Sale = request.Sale;
                    item.ExportOther = request.ExportOther;
                    item.IncreaseOther = request.IncreaseOther;
                    item.IncreaseSupplier = request.IncreaseSupplier;
                    item.ModifiedDate = DateTime.Now;
                    await _reportMongoRepository.UpdateOneAsync(item);
                }
                else
                {
                    var itemNew = new ReportCardStockProviderByDate()
                    {
                        ProductCode = request.ProductCode,
                        ProviderCode = request.ProviderCode,
                        InventoryBefore = request.InventoryBefore,
                        InventoryAfter = request.InventoryAfter,
                        Increase = request.Increase,
                        Decrease = request.Decrease,
                        Sale = request.Sale,
                        ExportOther = request.ExportOther,
                        IncreaseOther = request.IncreaseOther,
                        IncreaseSupplier = request.IncreaseSupplier,
                        ServiceCode = request.ServiceCode,
                        ShortDate = request.ShortDate,
                        StockCode = request.StockCode,
                        StockType = request.StockType,
                        CategoryCode = request.CategoryCode,
                        CardValue = request.CardValue,
                        CreatedDate = request.CreatedDate.Date,
                    };

                    await _reportMongoRepository.AddOneAsync(itemNew);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportSyncTransDetailRequest error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00",
                    ResponseMessage = $"{e.Message}|{e.StackTrace}|{e.InnerException}"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportTotalDayGetList(ReportTotalDayRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                    p.CurrencyCode == CurrencyCode.VND.ToString("G");

                if (request.FromDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                Expression<Func<ReportAccountBalanceDay, bool>> newQueryAccount = p =>
                    p.AccountCode == request.AccountCode && p.AccountType == "CUSTOMER";
                query = query.And(newQueryAccount);


                var total = await _reportMongoRepository.CountAsync(query);
                var listAll = await _reportMongoRepository.GetAllAsync(query);
                var maxDate = listAll.Max(c => c.CreatedDay);
                var minDate = listAll.Min(c => c.CreatedDay);

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportAccountBalanceDay, Guid>(query,
                    s => s.CreatedDay, true,
                    request.Offset, request.Limit);

                var sumData = new ReportItemTotalDay()
                {
                    BalanceBefore = listAll.FirstOrDefault(c => c.CreatedDay == minDate).BalanceBefore,
                    BalanceAfter = listAll.Where(c => c.CreatedDay == maxDate).FirstOrDefault()!.BalanceAfter,
                    IncDeposit = listAll.Sum(c => c.IncDeposit ?? 0),
                    IncOther = listAll.Sum(x => x.IncOther ?? 0),
                    DecPayment = listAll.Sum(x => x.DecPayment ?? 0),
                    DecOther = listAll.Sum(x => x.DecOther ?? 0)
                };

                //chỗ này sửa lại map đúng tên trường của a Tiến
                var list = lst.OrderBy(x => x.AccountCode).ThenBy(x => x.CreatedDay);
                var mappingList = (from x in lst
                                   select new ReportItemTotalDay()
                                   {
                                       CreatedDay = _dateHepper.ConvertToUserTime(x.CreatedDay, DateTimeKind.Utc),
                                       BalanceBefore = x.BalanceBefore,
                                       BalanceAfter = x.BalanceAfter,
                                       IncDeposit = x.IncDeposit ?? 0,
                                       IncOther = x.IncOther ?? 0,
                                       DecPayment = x.DecPayment ?? 0,
                                       DecOther = x.DecOther ?? 0,
                                   }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumData,
                    Payload = mappingList,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportTotalDebtGetList(ReportTotalDebtRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                    p.CurrencyCode == CurrencyCode.DEBT.ToString("G");

                if (request.FromDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AccountCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.AccountCode == request.AccountCode;
                    query = query.And(newQuery);
                }

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 5)
                    {
                        Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }


                var list = await _reportMongoRepository.GetAllAsync<ReportAccountBalanceDay>(query);
                var listSearch = (from g in list
                                  select new ReportTotalTempDebt
                                  {
                                      SaleCode = g.AccountCode,
                                      SaleInfo = string
                                          .Empty, //g.AccountItem != null ? g.AccountItem.UserName + " - " + g.AccountItem.Mobile + " - " + g.AccountItem.FullName : g.AccountCode,
                                      DecPayment = g.Debit,
                                      IncDeposit = g.Credite,
                                      CreatedDay = g.CreatedDay,
                                      BalanceAfter = g.BalanceAfter,
                                      BalanceBefore = g.BalanceBefore,
                                      LimitAfter = g.LimitAfter ?? 0,
                                      LimitBefore = g.LimitBefore ?? 0,
                                  }).ToList();


                var listGroup = (from x in listSearch
                                 group x by new { x.SaleCode, x.SaleInfo }
                    into g
                                 select new ReportTotalTempDebt
                                 {
                                     SaleCode = g.Key.SaleCode,
                                     SaleInfo = g.Key.SaleInfo,
                                     DecPayment = g.Sum(c => c.DecPayment),
                                     IncDeposit = g.Sum(c => c.IncDeposit),
                                     MaxDate = g.Max(c => c.CreatedDay),
                                     MinDate = g.Min(c => c.CreatedDay),
                                 }).ToList();

                var listView = (from x in listGroup
                                join mi in listSearch on x.SaleCode equals mi.SaleCode
                                join ma in listSearch on x.SaleCode equals ma.SaleCode
                                where x.MinDate == mi.CreatedDay && x.MaxDate == ma.CreatedDay
                                select new ReportItemTotalDebt()
                                {
                                    SaleCode = x.SaleCode,
                                    SaleInfo = x.SaleInfo,
                                    BalanceBefore = mi.LimitBefore - mi.BalanceBefore,
                                    BalanceAfter = ma.LimitAfter - ma.BalanceAfter,
                                    IncDeposit = x.IncDeposit,
                                    DecPayment = x.DecPayment,
                                }).OrderBy(c => c.SaleInfo).ToList();

                var total = listView.Count;
                var sumtotal = new ReportItemTotalDebt()
                {
                    BalanceBefore = listView.Sum(c => c.BalanceBefore),
                    BalanceAfter = listView.Sum(c => c.BalanceAfter),
                    IncDeposit = listView.Sum(c => c.IncDeposit),
                    DecPayment = listView.Sum(c => c.DecPayment),
                };
                var lst = listView.OrderBy(c => c.SaleInfo).Skip(request.Offset).Take(request.Limit).ToList();
                var saleCodes = lst.Where(c => !string.IsNullOrEmpty(c.SaleCode)).Select(c => c.SaleCode).Distinct()
                    .ToList();

                Expression<Func<ReportAccountDto, bool>> querySales = p => saleCodes.Contains(p.AccountCode);
                var lstSysAccounts = await _reportMongoRepository.GetAllAsync<ReportAccountDto>(querySales);
                var msglst = (from x in lst
                              join y in lstSysAccounts on x.SaleCode equals y.AccountCode into yg
                              from sale in yg.DefaultIfEmpty()
                              select new ReportItemTotalDebt()
                              {
                                  SaleCode = x.SaleCode,
                                  SaleInfo = sale != null ? sale.UserName + " - " + sale.Mobile + " - " + sale.FullName : "",
                                  BalanceBefore = x.BalanceBefore,
                                  BalanceAfter = x.BalanceAfter,
                                  IncDeposit = x.IncDeposit,
                                  DecPayment = x.DecPayment,
                              }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = msglst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportTotalDebtGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportBalanceTotalGetList(BalanceTotalRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                    p.CurrencyCode == CurrencyCode.VND.ToString("G")
                    && p.AccountType == "CUSTOMER";

                if (!string.IsNullOrEmpty(request.AccountCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.AccountCode == request.AccountCode;
                    query = query.And(newQuery);
                }

                if (request.AgentType > 0)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.AgentType == request.AgentType;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 5)
                    {
                        Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                            p.SaleLeaderCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                    else if (request.AccountType == 6)
                    {
                        Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                            p.SaleCode == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }

                var lst = await _reportMongoRepository.GetAllAsync(query);
                var listSelect = from x in lst
                                 select new ReportAccountBalanceDayTemp()
                                 {
                                     AccountCode = x.AccountCode,
                                     AgentType = (x.AgentType == 0 ? 1 : x.AgentType ?? 0),
                                     AccountInfo = x.AccountCode + (!string.IsNullOrEmpty(x.AccountInfo) ? ("-" + x.AccountInfo) : ""),
                                     BalanceBefore = x.BalanceBefore,
                                     BalanceAfter = x.BalanceAfter,
                                     Credited = x.Credite,
                                     Debit = x.Debit,
                                     CreatedDay = x.CreatedDay,
                                 };

                var listGroup = from x in listSelect
                                group x by new { x.AccountCode, x.AccountInfo, x.AgentType }
                    into g
                                select new ReportAccountBalanceDayTemp()
                                {
                                    AccountCode = g.Key.AccountCode,
                                    AgentType = g.Key.AgentType,
                                    AccountInfo = g.Key.AccountInfo,
                                    Credited = g.Sum(c => c.Credited),
                                    Debit = g.Sum(c => c.Debit),
                                    MaxDate = g.Max(c => c.CreatedDay),
                                    MinDate = g.Min(c => c.CreatedDay),
                                };


                var listView = from g in listGroup
                               join minG in listSelect on g.AccountCode equals minG.AccountCode
                               join maxG in listSelect on g.AccountCode equals maxG.AccountCode
                               where g.MinDate == minG.CreatedDay && g.MaxDate == maxG.CreatedDay
                               select new ReportAccountBalanceDayInfo()
                               {
                                   AccountCode = g.AccountCode,
                                   AccountInfo = g.AccountInfo,
                                   AgentType = g.AgentType,
                                   Credited = Math.Round(g.Credited, 0),
                                   Debit = Math.Round(g.Debit, 0),
                                   BalanceBefore = Math.Round(minG.BalanceBefore, 0),
                                   BalanceAfter = Math.Round(maxG.BalanceAfter, 0),
                               };

                var total = listView.Count();
                var sumtotal = new ReportAccountBalanceDayInfo()
                {
                    BalanceBefore = Math.Round(listView.Sum(c => c.BalanceBefore), 0),
                    BalanceAfter = Math.Round(listView.Sum(c => c.BalanceAfter), 0),
                    Credited = Math.Round(listView.Sum(c => c.Credited), 0),
                    Debit = Math.Round(listView.Sum(c => c.Debit), 0),
                };
                listView = listView.OrderBy(x => x.AccountCode).Skip(request.Offset).Take(request.Limit);

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = listView
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportBalanceTotalGetList error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> ReportRevenueDashBoardDayGetList(
            ReportRevenueDashBoardDayRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true && p.Status == ReportStatus.Success &&
                                                                      (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.TransType != ReportServiceCode.REFUND;


                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a);
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    foreach (var a in request.CategoryCode)
                        if (!string.IsNullOrEmpty(a))
                            categoryCode.Add(a);
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    foreach (var a in request.ProductCode)
                        if (!string.IsNullOrEmpty(a))
                            productCode.Add(a);
                }


                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }

                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                            p.AccountCode == request.LoginCode || p.PerformAccount == request.LoginCode;
                        query = query.And(newQuery);
                    }
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                var detailList = from x in lstSearch
                                 select new ReportRevenueDashboardDay
                                 {
                                     CreatedDay = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc).Date,
                                     Revenue = Convert.ToDecimal(x.Amount),
                                     Discount = Convert.ToDecimal(x.Discount)
                                 };

                var list = (from x in detailList
                            group x by x.CreatedDay
                    into g
                            select new ReportRevenueDashboardDay()
                            {
                                CreatedDay = g.Key,
                                DayText = g.Key.ToString("dd-MM-yyyy"),
                                Revenue = g.Sum(c => c.Revenue),
                                Discount = g.Sum(c => c.Discount)
                            }).OrderByDescending(c => c.CreatedDay).ToList();

                var tempDate = request.FromDate.Value.Date;
                var toDate = request.ToDate.Value.Date;

                while (tempDate <= toDate)
                {
                    if (!list.Where(c => c.CreatedDay == tempDate).Any())
                    {
                        list.Add(new ReportRevenueDashboardDay()
                        {
                            CreatedDay = tempDate,
                            DayText = tempDate.ToString("dd-MM-yyyy"),
                            Discount = 0,
                            Revenue = 0,
                        });
                    }

                    tempDate = tempDate.AddDays(1);
                }

                var total = list.Count();
                var sumtotal = new ReportRevenueDashboardDay()
                {
                    Revenue = list.Sum(c => c.Revenue),
                    Discount = list.Sum(c => c.Discount)
                };

                var lst = list.OrderByDescending(c => c.CreatedDay).Skip(request.Offset).Take(request.Limit).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = lst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueDashBoardDayGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<decimal> CheckTopupBalance(string providerCode)
        {
            return await _externalServiceConnector.GetBalanceTopupDtoAsync(providerCode);

        }

        public async Task<MessagePagedResponseBase> ReportBalanceGroupTotalGetList(BalanceGroupTotalRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                    p.CurrencyCode == CurrencyCode.VND.ToString("G");

                if (request.FromDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CreatedDay <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                var lst = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(query);
                var mslist = (from x in lst.ToList()
                              select new BalanceTotalItem
                              {
                                  AccountCode = x.AccountCode,
                                  AccountType = x.AccountType,
                                  CreateDate = x.CreatedDay,
                                  Credited = x.Credite,
                                  Debit = x.Debit,
                                  BalanceBefore = x.BalanceBefore,
                                  BalanceAfter = x.BalanceAfter,
                              }).ToList();


                var revenues = (from x in mslist
                                group x by new { x.AccountCode, x.AccountType }
                    into g
                                select new BalanceTotalItem()
                                {
                                    AccountCode = g.Key.AccountCode,
                                    AccountType = g.Key.AccountType,
                                    Credited = Math.Round(g.Sum(c => c.Credited), 0),
                                    Debit = Math.Round(g.Sum(c => c.Debit), 0),
                                }).ToList();

                var minDate = (from x in mslist
                               group x by new { x.AccountCode, x.AccountType }
                    into g
                               select new BalanceTotalItem()
                               {
                                   AccountCode = g.Key.AccountCode,
                                   AccountType = g.Key.AccountType,
                                   CreateDate = g.Min(c => c.CreateDate),
                               }).ToList();

                var minBalance = (from x in minDate
                                  join y in mslist on x.AccountCode equals y.AccountCode
                                  where x.CreateDate == y.CreateDate && x.AccountType == y.AccountType
                                  select new BalanceTotalItem
                                  {
                                      AccountCode = x.AccountCode,
                                      AccountType = x.AccountType,
                                      BalanceBefore = y.BalanceBefore,
                                  }).ToList();


                var maxDate = (from x in mslist
                               group x by new { x.AccountCode, x.AccountType }
                    into g
                               select new BalanceTotalItem()
                               {
                                   AccountCode = g.Key.AccountCode,
                                   AccountType = g.Key.AccountType,
                                   CreateDate = g.Max(c => c.CreateDate),
                               }).ToList();

                var maxBalance = (from x in maxDate
                                  join y in mslist on x.AccountCode equals y.AccountCode
                                  where x.CreateDate == y.CreateDate && x.AccountType == y.AccountType
                                  select new BalanceTotalItem
                                  {
                                      AccountCode = x.AccountCode,
                                      AccountType = x.AccountType,
                                      BalanceAfter = y.BalanceAfter,
                                  }).ToList();

                var list = (from r in revenues
                            join mx in maxBalance on r.AccountCode equals mx.AccountCode
                            join mi in minBalance on r.AccountCode equals mi.AccountCode
                            select new ReportBalanceTotalDto()
                            {
                                AccountCode = r.AccountCode,
                                AccountType = r.AccountType,
                                BalanceBefore = Math.Round(mi.BalanceBefore, 0),
                                BalanceAfter = Math.Round(mx.BalanceAfter, 0),
                                Credited = Math.Round(r.Credited, 0),
                                Debit = Math.Round(r.Debit, 0),
                            }).ToList();


                var groupList = (from x in list
                                 where x.AccountType == "SYSTEM"
                                 select x).ToList();

                var groupCustomer = new ReportBalanceTotalDto()
                {
                    AccountCode = "CUSTOMER",
                    AccountType = "CUSTOMER",
                    BalanceAfter = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.BalanceAfter), 0),
                    BalanceBefore = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.BalanceBefore),
                        0),
                    Credited = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.Credited), 0),
                    Debit = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.Debit), 0),
                };
                groupList.Add(groupCustomer);

                var total = groupList.Count();
                var sumtotal = new ReportBalanceTotalDto()
                {
                    BalanceBefore = Math.Round(groupList.Sum(c => c.BalanceBefore), 0),
                    BalanceAfter = Math.Round(groupList.Sum(c => c.BalanceAfter), 0),
                    Credited = Math.Round(groupList.Sum(c => c.Credite), 0),
                    Debit = Math.Round(groupList.Sum(c => c.Debit), 0),
                };
                var vList = groupList.Skip(request.Offset).Take(request.Limit).ToList();
                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = vList
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportBalanceGroupTotalGetList error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> SyncDayRequest(SyncTotalDayRequest request)
        {
            try
            {
                var lstAccount = await _externalServiceConnector.GetListAccountCode(request.AccountCode, "VND");
                var list = new List<ReportBalanceHistories>();
                List<DateTime> dateFor = new List<DateTime>();
                DateTime tempDate = request.FromDate.Date;
                while (tempDate <= request.ToDate.Date)
                {
                    dateFor.Add(tempDate);
                    tempDate = tempDate.AddDays(1);
                }

                if (request.SourceType == 1)
                {
                    #region 1.Tổng hợp dữ liệu từ lịch sử ghi nhận                   
                    var listDtos = await GetBalanceHistories(request.FromDate.Date);
                    list = await ConvertBalanceHistoriesDto(listDtos);

                    #endregion
                }

                else if (request.SourceType == 2)
                {
                    #region 2.Loại tổng hợp dựa vào dữ liệu trong report

                    request.ToDate = request.ToDate.AddDays(1).AddSeconds(1);

                    Expression<Func<ReportBalanceHistories, bool>> query = p => true;
                    Expression<Func<ReportBalanceHistories, bool>> newDate =
                        p => p.CreatedDate >= request.FromDate.ToUniversalTime()
                             && p.CreatedDate <= request.ToDate.ToUniversalTime();
                    query = query.And(newDate);

                    if (!string.IsNullOrEmpty(request.AccountCode))
                    {
                        Expression<Func<ReportBalanceHistories, bool>> newQuery =
                            p => p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode;
                        query = query.And(newQuery);
                    }

                    list = _reportMongoRepository.GetAll<ReportBalanceHistories>(query);

                    #endregion
                }

                foreach (var accountCode in lstAccount)
                {
                    #region .Duyệt theo từng tài khoản

                    foreach (var dateItem in dateFor)
                    {
                        #region .Xử lý dữ liệu theo từng ngày

                        var fDate = dateItem;
                        var tDate = dateItem.AddDays(1).AddSeconds(-1);

                        try
                        {
                            var reportDayOld =
                                await _reportMongoRepository.GetReportAccountBalanceDayAsync(accountCode,
                                    request.CurrencyCode, fDate);
                            if (reportDayOld == null || request.IsOverride)
                            {
                                var liTempDay = list.Where(c =>
                                    (c.DesAccountCode == accountCode || c.SrcAccountCode == accountCode)
                                    && c.CreatedDate >= fDate.ToUniversalTime()
                                    && c.CreatedDate <= tDate.ToUniversalTime()).ToList();
                                var reportDay = await GetReportDayByAccount(liTempDay, request.CurrencyCode,
                                    accountCode, fDate);

                                if (reportDayOld == null)
                                    await _reportMongoRepository.AddOneAsync(reportDay);
                                else
                                {
                                    reportDayOld.Credite = reportDay.Credite;
                                    reportDayOld.Debit = reportDay.Debit;
                                    reportDayOld.BalanceAfter = reportDay.BalanceAfter;
                                    reportDayOld.BalanceBefore = reportDay.BalanceBefore;
                                    reportDayOld.CreatedDay = dateItem.AddDays(1).AddHours(-1);
                                    await _reportMongoRepository.UpdateOneAsync(reportDayOld);
                                }
                            }
                        }
                        catch (Exception eitem)
                        {
                            _logger.LogError($"SyncDayRequest Exception Item : {eitem}");
                        }

                        #endregion
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SyncDayRequest Exception: {ex}");
            }

            return null;
        }

        private ReportAccountBalanceDay GeAccountBalanceDayInsert(ReportBalanceHistoriesMessage request,
            string currencyCode, bool IsAccountDes = true)
        {
            var settlement = request.Settlement;

            var account = IsAccountDes
                ? new ReportAccountBalanceDay()
                {
                    AccountCode = settlement.DesAccountCode,
                    AccountType = settlement.DesAccountCode.StartsWith("NT9")
                        ? BalanceAccountTypeConst.CUSTOMER
                        : BalanceAccountTypeConst.SYSTEM,
                    Credite = currencyCode == "DEBT" ? 0 : settlement.Amount,
                    Debit = currencyCode == "DEBT" ? settlement.Amount : 0,
                    IncDeposit = 0,
                    IncOther = 0,
                    DecOther = 0,
                    DecPayment = 0,
                    LimitBefore = 0,
                    LimitAfter = 0,
                    BalanceBefore = settlement.DesAccountBalanceBeforeTrans,
                    BalanceAfter = settlement.DesAccountBalance,
                    CreatedDay = settlement.CreatedDate.Value,
                    CurrencyCode = currencyCode,
                    TextDay = settlement.DesAccountCode + "_" + settlement.CreatedDate.Value.ToString("yyyyMMdd")
                }
                : new ReportAccountBalanceDay()
                {
                    AccountCode = settlement.SrcAccountCode,
                    AccountType = settlement.SrcAccountCode.StartsWith("NT9")
                        ? BalanceAccountTypeConst.CUSTOMER
                        : BalanceAccountTypeConst.SYSTEM,
                    Credite = currencyCode == "DEBT" ? settlement.Amount : 0,
                    Debit = currencyCode == "DEBT" ? 0 : settlement.Amount,
                    IncDeposit = 0,
                    IncOther = 0,
                    DecOther = 0,
                    DecPayment = 0,
                    LimitBefore = 0,
                    LimitAfter = 0,
                    BalanceBefore = settlement.SrcAccountBalanceBeforeTrans,
                    BalanceAfter = settlement.SrcAccountBalance,
                    CreatedDay = settlement.CreatedDate.Value,
                    CurrencyCode = currencyCode,
                    TextDay = settlement.SrcAccountCode + "_" + settlement.CreatedDate.Value.ToString("yyyyMMdd")
                };

            return account;
        }


        private async Task UpdateAccountBalanceDayReport(ReportBalanceHistoriesMessage request)
        {
            try
            {
                DateTime date = request.Settlement.CreatedDate.Value.Date;

                #region 1.Des

                if (!string.IsNullOrEmpty(request.Settlement.DesAccountCode))
                {
                    var infoDes = _reportMongoRepository.GetReportAccountBalanceDayAsync(request.Settlement.DesAccountCode,
                            request.Settlement.CurrencyCode, date).Result;
                    var balanceInfoDes = GeAccountBalanceDayInsert(request, request.Settlement.CurrencyCode, IsAccountDes: true);
                    if (infoDes != null)
                    {
                        var infoAccountDes = infoDes;
                        var dateUser = _dateHepper.ConvertToUserTime(infoAccountDes.CreatedDay).Date;
                        //if (dateUser == date)
                        if (infoAccountDes.TextDay == balanceInfoDes.TextDay)
                        {
                            infoAccountDes.CurrencyCode = request.Settlement.CurrencyCode;
                            infoAccountDes.BalanceAfter = balanceInfoDes.BalanceAfter;
                            if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                request.Settlement.CurrencyCode == "DEBT")
                            {
                                infoAccountDes.Debit = infoAccountDes.Debit + balanceInfoDes.Debit;
                                var limit = await _externalServiceConnector.GetLimitDebtAccount(infoDes.AccountCode);
                                infoAccountDes.DecPayment = (infoAccountDes.DecPayment ?? 0) + balanceInfoDes.Debit;
                                infoAccountDes.LimitAfter = Convert.ToDouble(limit != null ? limit.Limit : 0);
                            }
                            else infoAccountDes.Credite = infoAccountDes.Credite + balanceInfoDes.Credite;

                            if (request.Transaction.TransType == TransactionType.Deposit)
                                infoAccountDes.IncDeposit = (infoAccountDes.IncDeposit ?? 0) + balanceInfoDes.Credite;
                            else if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                     request.Settlement.CurrencyCode == "VND")
                                infoAccountDes.IncDeposit = (infoAccountDes.IncDeposit ?? 0) + balanceInfoDes.Credite;
                            else if (request.Transaction.TransType == TransactionType.Transfer
                                || request.Transaction.TransType == TransactionType.AdjustmentIncrease
                                || request.Transaction.TransType == TransactionType.CancelPayment
                                || request.Transaction.TransType == TransactionType.PayBatch
                                || request.Transaction.TransType == TransactionType.PayCommission)
                                infoAccountDes.IncOther = (infoAccountDes.IncOther ?? 0) + balanceInfoDes.Credite;

                            MappingSaleLeader(ref infoAccountDes);
                            if (infoAccountDes.AgentType == null || infoAccountDes.AgentType == 0)
                                infoAccountDes.AgentType = infoAccountDes.AgentType;

                            if (infoAccountDes.AgentType == 5 && string.IsNullOrEmpty(infoAccountDes.ParentCode))
                            {
                                var account = await GetAccountBackend(infoAccountDes.AccountCode);
                                infoAccountDes.ParentCode = account != null ? account.ParentCode : string.Empty;
                            }

                            await _reportMongoRepository.UpdateOneAsync(infoAccountDes);
                        }
                        else
                        {
                            _logger.LogInformation($"AccountCode={balanceInfoDes.AccountCode} UpdateAccountBalanceDayReport => TextDayBalance={infoAccountDes.TextDay}|TextDayTran= {balanceInfoDes.TextDay}");
                            balanceInfoDes.BalanceBefore = infoAccountDes.BalanceAfter;
                            balanceInfoDes.LimitBefore = infoAccountDes.LimitAfter;
                            balanceInfoDes.CurrencyCode = request.Settlement.CurrencyCode;
                            if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                request.Settlement.CurrencyCode == "DEBT")
                            {
                                var limit = await _externalServiceConnector.GetLimitDebtAccount(balanceInfoDes.AccountCode);
                                balanceInfoDes.DecPayment = balanceInfoDes.Debit;
                                balanceInfoDes.LimitAfter = Convert.ToDouble(limit?.Limit ?? 0);
                                balanceInfoDes.LimitBefore = Convert.ToDouble(limit?.Limit ?? 0);
                                if (limit == null)
                                {
                                    balanceInfoDes.LimitBefore = infoAccountDes.LimitAfter;
                                    balanceInfoDes.LimitAfter = infoAccountDes.LimitAfter;
                                }
                            }
                            else if (request.Transaction.TransType == TransactionType.Deposit)
                                balanceInfoDes.IncDeposit = balanceInfoDes.Credite;
                            else if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                     request.Settlement.CurrencyCode == "VND")
                                infoAccountDes.IncDeposit = balanceInfoDes.Credite;
                            else if (request.Transaction.TransType == TransactionType.Transfer
                                || request.Transaction.TransType == TransactionType.AdjustmentIncrease
                                || request.Transaction.TransType == TransactionType.CancelPayment
                                || request.Transaction.TransType == TransactionType.PayCommission)
                                balanceInfoDes.IncOther = balanceInfoDes.Credite;
                            MappingSaleLeader(ref balanceInfoDes);

                            if (infoAccountDes.AgentType == null || infoAccountDes.AgentType == 0)
                                infoAccountDes.AgentType = infoAccountDes.AgentType;

                            if (infoAccountDes.AgentType == 5 && string.IsNullOrEmpty(infoAccountDes.ParentCode))
                            {
                                var account = await GetAccountBackend(infoAccountDes.AccountCode);
                                infoAccountDes.ParentCode = account != null ? account.ParentCode : string.Empty;
                            }

                            await _reportMongoRepository.AddOneAsync(balanceInfoDes);
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"AccountCode={balanceInfoDes.AccountCode} UpdateAccountBalanceDayReport => TextDayTran={balanceInfoDes.TextDay} => chua_co_ban_ghi");
                        balanceInfoDes.CurrencyCode = request.Settlement.CurrencyCode;
                        if (request.Transaction.TransType == TransactionType.Deposit)
                            balanceInfoDes.IncDeposit = balanceInfoDes.Credite;
                        else if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                 request.Settlement.CurrencyCode == "VND")
                            balanceInfoDes.IncDeposit = balanceInfoDes.Credite;
                        else if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                 request.Settlement.CurrencyCode == "DEBT")
                        {
                            balanceInfoDes.DecPayment = balanceInfoDes.Debit;
                            var limit = await _externalServiceConnector.GetLimitDebtAccount(balanceInfoDes.AccountCode);
                            balanceInfoDes.LimitAfter = Convert.ToDouble(limit?.Limit ?? 0);
                            balanceInfoDes.LimitBefore = Convert.ToDouble(limit?.Limit ?? 0);
                        }
                        else if (request.Transaction.TransType == TransactionType.Transfer
                            || request.Transaction.TransType == TransactionType.AdjustmentIncrease
                            || request.Transaction.TransType == TransactionType.CancelPayment
                            || request.Transaction.TransType == TransactionType.PayBatch
                            || request.Transaction.TransType == TransactionType.PayCommission)
                            balanceInfoDes.IncOther = balanceInfoDes.Credite;

                        MappingSaleLeader(ref balanceInfoDes);

                        if (balanceInfoDes.AgentType == null || balanceInfoDes.AgentType == 0)
                            balanceInfoDes.AgentType = balanceInfoDes.AgentType;

                        if (balanceInfoDes.AgentType == 5 && string.IsNullOrEmpty(balanceInfoDes.ParentCode))
                        {
                            var account = await GetAccountBackend(balanceInfoDes.AccountCode);
                            balanceInfoDes.ParentCode = account != null ? account.ParentCode : string.Empty;
                        }

                        await _reportMongoRepository.AddOneAsync(balanceInfoDes);
                    }
                }

                #endregion

                #region 2.Src

                if (!string.IsNullOrEmpty(request.Settlement.SrcAccountCode))
                {
                    var infoSrc = await _reportMongoRepository.GetReportAccountBalanceDayAsync(request.Settlement.SrcAccountCode, request.Settlement.CurrencyCode, date);
                    var balanceInfoSrc = GeAccountBalanceDayInsert(request, request.Settlement.CurrencyCode, IsAccountDes: false);
                    if (infoSrc != null)
                    {
                        var infoAccountSrc = infoSrc;
                        var dateUser = _dateHepper.ConvertToUserTime(infoAccountSrc.CreatedDay).Date;
                        //if (dateUser == date)
                        if (infoAccountSrc.TextDay == balanceInfoSrc.TextDay)
                        {
                            infoAccountSrc.BalanceAfter = balanceInfoSrc.BalanceAfter;
                            if (request.Transaction.TransType == TransactionType.ClearDebt &&
                                request.Settlement.CurrencyCode == "DEBT")
                            {
                                infoAccountSrc.Credite = infoAccountSrc.Credite + balanceInfoSrc.Credite;
                                infoAccountSrc.IncDeposit = (infoAccountSrc.IncDeposit ?? 0) + balanceInfoSrc.Credite;
                                var limit = await _externalServiceConnector.GetLimitDebtAccount(infoAccountSrc.AccountCode);
                                infoAccountSrc.LimitAfter = Convert.ToDouble(limit != null ? limit.Limit : 0);
                            }
                            else infoAccountSrc.Debit = infoAccountSrc.Debit + balanceInfoSrc.Debit;

                            if (request.Transaction.TransType == TransactionType.Payment)
                                infoAccountSrc.DecPayment = (infoAccountSrc.DecPayment ?? 0) + balanceInfoSrc.Debit;
                            else if (request.Transaction.TransType == TransactionType.Transfer
                                || request.Transaction.TransType == TransactionType.AdjustmentDecrease)
                                infoAccountSrc.DecOther = (infoAccountSrc.DecOther ?? 0) + balanceInfoSrc.Debit;

                            infoAccountSrc.CurrencyCode = request.Settlement.CurrencyCode;
                            MappingSaleLeader(ref infoAccountSrc);
                            if (infoAccountSrc.AgentType == null || infoAccountSrc.AgentType == 0)
                                infoAccountSrc.AgentType = infoAccountSrc.AgentType;

                            if (infoAccountSrc.AgentType == 5 && string.IsNullOrEmpty(infoAccountSrc.ParentCode))
                            {
                                var account = await GetAccountBackend(infoAccountSrc.AccountCode);
                                infoAccountSrc.ParentCode = account != null ? account.ParentCode : string.Empty;
                            }

                            await _reportMongoRepository.UpdateOneAsync(infoAccountSrc);
                        }
                        else
                        {
                            _logger.LogInformation($"AccountCode={balanceInfoSrc.AccountCode} UpdateAccountBalanceDayReport => TextDayBalance={infoAccountSrc.TextDay}|TextDayTran={balanceInfoSrc.TextDay}");
                            balanceInfoSrc.BalanceBefore = infoAccountSrc.BalanceAfter;

                            if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                                request.Settlement.CurrencyCode == "DEBT")
                            {
                                infoAccountSrc.IncDeposit = balanceInfoSrc.Credite;
                                var limit =
                                    await _externalServiceConnector.GetLimitDebtAccount(infoAccountSrc.AccountCode);
                                infoAccountSrc.LimitAfter = Convert.ToDouble(limit != null ? limit.Limit : 0);
                                balanceInfoSrc.LimitBefore = Convert.ToDouble(limit != null ? limit.Limit : 0);
                            }
                            else if (request.Transaction.TransType == TransactionType.Payment)
                                infoAccountSrc.DecPayment = balanceInfoSrc.Debit;
                            else
                                if (request.Transaction.TransType == TransactionType.Transfer
                                || request.Transaction.TransType == TransactionType.AdjustmentDecrease)
                                balanceInfoSrc.DecOther = balanceInfoSrc.Debit;

                            balanceInfoSrc.CurrencyCode = request.Settlement.CurrencyCode;


                            MappingSaleLeader(ref balanceInfoSrc);

                            if (balanceInfoSrc.AgentType == null || balanceInfoSrc.AgentType == 0)
                                balanceInfoSrc.AgentType = balanceInfoSrc.AgentType;

                            if (balanceInfoSrc.AgentType == 5 && string.IsNullOrEmpty(balanceInfoSrc.ParentCode))
                            {
                                var account = await GetAccountBackend(balanceInfoSrc.AccountCode);
                                balanceInfoSrc.ParentCode = account != null ? account.ParentCode : string.Empty;
                            }

                            await _reportMongoRepository.AddOneAsync(balanceInfoSrc);
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"AccountCode={balanceInfoSrc.AccountCode} UpdateAccountBalanceDayReport => TextDayTran={balanceInfoSrc.TextDay} => chua_co_ban_ghi");

                        if (request.Transaction.TransType == TransactionType.SaleDeposit &&
                            request.Settlement.CurrencyCode == "DEBT")
                        {
                            balanceInfoSrc.IncDeposit = balanceInfoSrc.Credite;
                            var limit = await _externalServiceConnector.GetLimitDebtAccount(balanceInfoSrc.AccountCode);
                            balanceInfoSrc.LimitAfter = Convert.ToDouble(limit?.Limit ?? 0);
                            balanceInfoSrc.LimitBefore = Convert.ToDouble(limit?.Limit ?? 0);
                        }
                        else if (request.Transaction.TransType == TransactionType.Payment)
                            balanceInfoSrc.DecPayment = balanceInfoSrc.Debit;
                        else if (request.Transaction.TransType == TransactionType.Transfer
                            || request.Transaction.TransType == TransactionType.AdjustmentDecrease)
                            balanceInfoSrc.DecOther = balanceInfoSrc.Debit;

                        balanceInfoSrc.CurrencyCode = request.Settlement.CurrencyCode;


                        MappingSaleLeader(ref balanceInfoSrc);

                        if (balanceInfoSrc.AgentType == null || balanceInfoSrc.AgentType == 0)
                            balanceInfoSrc.AgentType = balanceInfoSrc.AgentType;

                        if (balanceInfoSrc.AgentType == 5 && string.IsNullOrEmpty(balanceInfoSrc.ParentCode))
                        {
                            var account = await GetAccountBackend(balanceInfoSrc.AccountCode);
                            balanceInfoSrc.ParentCode = account != null ? account.ParentCode : string.Empty;
                        }

                        await _reportMongoRepository.AddOneAsync(balanceInfoSrc);
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateAccountBalanceDayReport Exception: {ex}");
            }
        }

        private async Task<List<BalanceHistories>> GetBalanceHistories(DateTime date)
        {
            _logger.LogInformation($"BalanceHistoriesGetRequest request)");

            try
            {
                var rs = await getBalanceHistoryDataTimeFull(date);
                return rs;
            }
            catch (Exception ex)
            {
                _logger.LogError($"BalanceHistoriesGetRequest error: {ex}");
                return new List<BalanceHistories>();
            }
        }

        private async Task<ReportAccountBalanceDay> GetReportDayByAccount(List<ReportBalanceHistories> lst,
            string accountCode, string currencyCode, DateTime date)
        {
            var list = new List<BalanceTotalItem>();
            foreach (var item in lst)
            {
                if (item.SrcAccountCode == accountCode)
                {
                    list.Add(new BalanceTotalItem()
                    {
                        AccountCode = item.SrcAccountCode,
                        AccountType = item.SrcAccountCode.StartsWith("NT9")
                            ? BalanceAccountTypeConst.CUSTOMER
                            : BalanceAccountTypeConst.SYSTEM,
                        BalanceBefore = item.SrcAccountBalanceBeforeTrans,
                        BalanceAfter = item.SrcAccountBalanceAfterTrans,
                        Debit = item.Amount,
                        Credited = 0,
                        CreateDate = item.CreatedDate,
                    });
                }
                else if (item.DesAccountCode == accountCode)
                {
                    list.Add(new BalanceTotalItem()
                    {
                        AccountCode = item.DesAccountCode,
                        AccountType = item.DesAccountCode.StartsWith("NT9")
                            ? BalanceAccountTypeConst.CUSTOMER
                            : BalanceAccountTypeConst.SYSTEM,
                        BalanceBefore = item.DesAccountBalanceBeforeTrans,
                        BalanceAfter = item.DesAccountBalanceAfterTrans,
                        Debit = 0,
                        Credited = item.Amount,
                        CreateDate = item.CreatedDate,
                    });
                }
            }

            if (list.Count > 0)
            {
                var maxDate = list.Max(c => c.CreateDate);
                var minDate = list.Min(c => c.CreateDate);
                var balanceReport =
                    await _reportMongoRepository.GetReportAccountBalanceDayAsync(accountCode, currencyCode,
                        date.Date.AddDays(-1));
                return new ReportAccountBalanceDay()
                {
                    AccountCode = accountCode,
                    AccountType = accountCode.StartsWith("NT9")
                        ? BalanceAccountTypeConst.CUSTOMER
                        : BalanceAccountTypeConst.SYSTEM,
                    BalanceAfter = list.First(c => c.CreateDate == maxDate).BalanceAfter,
                    BalanceBefore = balanceReport?.BalanceAfter ?? list.First(c => c.CreateDate == minDate).BalanceBefore,
                    CreatedDay = maxDate,
                    Credite = list.Sum(c => c.Credited),
                    Debit = list.Sum(c => c.Debit),
                };
            }
            else
            {
                var balanceReport =
                    await _reportMongoRepository.GetReportAccountBalanceDayAsync(accountCode, currencyCode,
                        date.AddDays(-1));
                if (balanceReport != null)
                {
                    return new ReportAccountBalanceDay()
                    {
                        AccountCode = accountCode,
                        AccountType = accountCode.StartsWith("NT9")
                            ? BalanceAccountTypeConst.CUSTOMER
                            : BalanceAccountTypeConst.SYSTEM,
                        BalanceAfter = balanceReport.BalanceAfter,
                        BalanceBefore = balanceReport.BalanceAfter,
                        CreatedDay = date.Date.AddDays(1).AddHours(-1).ToUniversalTime(),
                        Credite = 0,
                        Debit = 0,
                    };
                }
                else
                {
                    var balanceHistory = await GetAccountBalanceBy(accountCode, date.AddDays(-1));
                    return new ReportAccountBalanceDay()
                    {
                        AccountCode = accountCode,
                        AccountType = accountCode.StartsWith("NT9")
                            ? BalanceAccountTypeConst.CUSTOMER
                            : BalanceAccountTypeConst.SYSTEM,
                        BalanceAfter = balanceHistory,
                        BalanceBefore = balanceHistory,
                        CreatedDay = date.Date.AddDays(1).AddHours(-1).ToUniversalTime(),
                        Credite = 0,
                        Debit = 0,
                    };
                }
            }
        }

        private async Task<double> GetAccountBalanceBy(string accountCode, DateTime date)
        {
            _logger.LogInformation($"GetAccountBalanceBy request)");

            try
            {
                var rs = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync<double>(new BalanceMaxDateRequest
                {
                    AccountCode = accountCode,
                    MaxDate = date
                });
                _logger.LogInformation($"GetAccountBalanceBy return: {rs}");

                return rs;
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"GetAccountBalanceBy error: {ex}");
                return 0;
            }
        }

        private Task<List<ReportBalanceHistories>> ConvertBalanceHistoriesDto(List<BalanceHistories> lst)
        {
            var list = (from x in lst
                        select new ReportBalanceHistories()
                        {
                            Amount = Convert.ToDouble(x.Amount),
                            CreatedDate = x.CreatedDate,
                            CurrencyCode = x.CurrencyCode,
                            DesAccountBalanceAfterTrans = Convert.ToDouble(x.DesAccountBalance),
                            DesAccountBalanceBeforeTrans = Convert.ToDouble(x.DesAccountBalance - x.Amount),
                            SrcAccountBalanceAfterTrans = Convert.ToDouble(x.SrcAccountBalance),
                            SrcAccountBalanceBeforeTrans = Convert.ToDouble(x.SrcAccountBalance + x.Amount),
                            DesAccountCode = x.DesAccountCode,
                            SrcAccountCode = x.SrcAccountCode,
                            TransactionType = x.TransactionType,
                            TransCode = x.TransCode,
                            TransRef = x.TransRef,
                            TransType = x.TransType,
                            TransNote = x.TransNote,
                            Status = (byte)x.Status,
                        }).ToList();

            return Task.FromResult(list);
        }

        private void MappingSaleLeader(ref ReportAccountBalanceDay info)
        {
            if (string.IsNullOrEmpty(info.AccountInfo) || info.AgentType == null)
            {
                var account = GetAccountBackend(info.AccountCode).Result;
                if (account != null)
                {
                    info.AccountInfo = account.Mobile + "-" + account.FullName;
                    info.SaleCode = account.SaleCode;
                    info.SaleLeaderCode = account.LeaderCode;
                    info.AgentType = account.AgentType;
                }
            }
        }

        public async Task SysDayOneProcess(DateTime date)
        {
            try
            {
                DateTime fromDate = date.AddDays(-60);
                DateTime toDate = date.Date.AddDays(1);

                var lstAccount = await _externalServiceConnector.GetListAccountCode("", "VND");
                var lstDebt = await _externalServiceConnector.GetListAccountCode("", "DEBT");

                #region 1.Loại VND

                foreach (var accountCode in lstAccount)
                {
                    var account = await GetAccountBackend(accountCode);
                    if (account != null && new[] { 1, 2, 3, 4, 5, 6 }.Contains(account.AgentType) && new[] { 1, 2, 3, 4 }.Contains(account.AccountType))
                    {
                        _logger.LogInformation($"{accountCode}|VND");
                        await SysAccountBalance(accountCode, "VND", fromDate, date, toDate);
                    }
                }

                #endregion

                #region 2.Loại DEBT

                foreach (var accountCode in lstDebt)
                {
                    var account = await GetAccountBackend(accountCode);
                    if (account != null && new[] { 1, 2, 3, 4, 5, 6 }.Contains(account.AgentType) && account.AccountType > 0)
                    {
                        _logger.LogInformation($"{accountCode}|DEBT");
                        await SysAccountBalance(accountCode, "DEBT", fromDate, date, toDate);
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError($"SysDayOneProcess Exception: {ex}");
            }
        }

        private async Task AddBalanceHistoryReport(ReportBalanceHistoriesMessage request)
        {
            try
            {
                await _reportMongoRepository.AddOneAsync(GetBalanceHistoryInsert(request));
                if (request.Transaction.TransType == TransactionType.MasterTopup)
                {
                    string accountCode = "CONTROL";
                    string dateText = DateTime.Now.ToString("yyyyMMdd");
                    var balance = await getCheckBalance(accountCode);
                    var getData = await GetAccountSystemDayByCode(accountCode, dateText);
                    if (getData == null)
                    {
                        getData = new ReportSystemDay()
                        {
                            AccountCode = accountCode,
                            BalanceAfter = balance,
                            BalanceBefore = balance,
                            UpdateDate = DateTime.Now,
                            TextDay = dateText,
                            CurrencyCode = "VND"
                        };
                    }
                    else
                    {
                        getData.BalanceAfter = balance;
                    }
                    await UpdateAccountSystemDay(getData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{request.Transaction.TransRef}|{request.Transaction.TransactionCode} AddBalanceHistoryReport error: {ex}");
            }
        }

        public Task<MessagePagedResponseBase> ReportTopupRequestLogGetList(ReportTopupRequestLogs request)
        {
            throw new NotImplementedException();
        }
    }
}