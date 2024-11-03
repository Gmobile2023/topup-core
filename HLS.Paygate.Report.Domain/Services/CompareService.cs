using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Domain.Exporting;
using HLS.Paygate.Report.Domain.Repositories;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Emailing;
using HLS.Paygate.Shared.Helpers;
using MassTransit;
using MassTransit.Middleware;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;
using Paygate.Discovery.Requests.Backends;
using ServiceStack;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HLS.Paygate.Report.Domain.Services;

public class CompareService : ICompareService
{
    private readonly IBalanceReportService _balanceReportSvc;
    private readonly IBus _bus;
    private readonly ICacheManager _cacheManager;
    private readonly IExportDataExcel _dataExcel;
    private readonly IDateTimeHelper _dateHepper;
    private readonly IEmailSender _emailSender;
    //private readonly IServiceGateway _gateway; gunner
    private readonly ILogger<CompareService> _logger;
    private readonly IReportMongoRepository _reportMongoRepository;
    private readonly GrpcClientHepper _grpcClient;
    public CompareService(IReportMongoRepository reportMongoRepository,
        IDateTimeHelper dateHepper,
        IBalanceReportService balanceReportSvc,
        IExportDataExcel dataExcel,
        IEmailSender emailSender,
        ICacheManager cacheManager,
        ILogger<CompareService> logger, IBus bus, GrpcClientHepper grpcClient)
    {
        _logger = logger;
        _bus = bus;
        _dateHepper = dateHepper;
        _emailSender = emailSender;
        _reportMongoRepository = reportMongoRepository;
        _balanceReportSvc = balanceReportSvc;
        _cacheManager = cacheManager;
        _dataExcel = dataExcel;
        _grpcClient = grpcClient;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task<MessagePagedResponseBase> ReportCompareGetList(ReportCompareListRequest request)
    {
        try
        {
            #region .MappingDate

            request.FromCompareDate ??= DateTime.Now.Date;

            request.ToCompareDate = request.ToCompareDate == null
                ? DateTime.Now.Date.AddDays(1).AddSeconds(-1)
                : request.ToCompareDate.Value.AddDays(1).AddSeconds(-1);

            request.FromTransDate ??= DateTime.Now.Date;

            request.ToTransDate = request.ToTransDate == null
                ? DateTime.Now.Date.AddDays(1).AddSeconds(-1)
                : request.ToTransDate.Value.AddDays(1).AddSeconds(-1);

            #endregion

            Expression<Func<CompareHistory, bool>> query = p => p.Isenabled == true;

            if (!string.IsNullOrEmpty(request.ProviderCode))
            {
                Expression<Func<CompareHistory, bool>> newQuery = p =>
                    p.ProviderCode == request.ProviderCode;
                query = query.And(newQuery);
            }

            Expression<Func<CompareHistory, bool>> newQueryTime = p =>
                p.CompareDate >= request.FromCompareDate.Value.ToUniversalTime() &&
                p.CompareDate <= request.ToCompareDate.Value.ToUniversalTime()
                || p.TransDate >= request.FromTransDate.Value.ToUniversalTime() &&
                p.TransDate <= request.ToTransDate.Value.ToUniversalTime();
            query = query.And(newQueryTime);

            var total = await _reportMongoRepository.CountAsync(query);
            var listAll = await _reportMongoRepository.GetAllAsync(query);

            var sumTotal = new CompareHistory
            {
                SysQuantity = listAll.Sum(c => c.SysQuantity),
                SysAmount = listAll.Sum(c => c.SysAmount),
                SameQuantity = listAll.Sum(c => c.SameQuantity),
                SameAmount = listAll.Sum(c => c.SameAmount),
                SysOnlyQuantity = listAll.Sum(c => c.SysOnlyQuantity),
                SysOnlyAmount = listAll.Sum(c => c.SysOnlyAmount),
                NotSameQuantity = listAll.Sum(c => c.NotSameQuantity),
                NotSameSysAmount = listAll.Sum(c => c.NotSameSysAmount),
                NotSameProviderAmount = listAll.Sum(c => c.NotSameProviderAmount),
                ProviderAmount = listAll.Sum(c => c.ProviderAmount),
                ProviderOnlyQuantity = listAll.Sum(c => c.ProviderOnlyQuantity),
                ProviderQuantity = listAll.Sum(c => c.ProviderQuantity),
                ProviderOnlyAmount = listAll.Sum(c => c.ProviderOnlyAmount),
                RefundQuantity = listAll.Sum(c => c.RefundQuantity),
                RefundAmount = listAll.Sum(c => c.RefundAmount),
                RefundWaitQuantity = listAll.Sum(c => c.RefundWaitQuantity),
                RefundWaitAmount = listAll.Sum(c => c.RefundWaitAmount)
            };


            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<CompareHistory, Guid>(query,
                s => s.CompareDate, false,
                request.Offset, request.Limit);

            foreach (var item in lst)
            {
                item.TransDate = _dateHepper.ConvertToUserTime(item.TransDate, DateTimeKind.Utc);
                item.CompareDate = _dateHepper.ConvertToUserTime(item.CompareDate, DateTimeKind.Utc);
            }

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = (int)total,
                SumData = sumTotal,
                Payload = lst
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareGetList error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> ReportCompareDetailReonseList(
        ReportCompareDetailReonseRequest request)
    {
        try
        {
            Expression<Func<CompareTime, bool>> query = p => p.ProviderCode == request.ProviderCode
                                                             && p.KeyCode == request.KeyCode
                                                             && p.ProviderCode == request.ProviderCode;

            if (request.CompareType > 0)
            {
                if (request.CompareType == 1)
                {
                    Expression<Func<CompareTime, bool>> newQuery = p =>
                        p.Status == 1 && p.Result == 1;
                    query = query.And(newQuery);
                }
                else if (request.CompareType == 2)
                {
                    Expression<Func<CompareTime, bool>> newQuery = p =>
                        p.Status == 1 && p.Result == 3;
                    query = query.And(newQuery);
                }
                else if (request.CompareType == 3)
                {
                    Expression<Func<CompareTime, bool>> newQuery = p =>
                        p.Status == 1 && p.Result == 2;
                    query = query.And(newQuery);
                }
                else if (request.CompareType == 4)
                {
                    Expression<Func<CompareTime, bool>> newQuery = p =>
                        p.Status == 1 && p.Result == 0;
                    query = query.And(newQuery);
                }
            }

            var total = await _reportMongoRepository.CountAsync(query);

            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<CompareTime, Guid>(query,
                s => s.TransDate, true,
                request.Offset, request.Limit);

            var listView = (from item in lst
                            select new CompareReponseDetailDto
                            {
                                TransDate = _dateHepper.ConvertToUserTime(item.TransDate, DateTimeKind.Utc),
                                AgentCode = item.AccountCode,
                                TransCode = item.TransCode,
                                TransPay = item.TransCodePay,
                                ProductValue = item.Status == 1 && item.Result == 2 ? item.ProviderValue : item.SysValue,
                                ReceivedAccount = item.ReceivedAccount,
                                ProductCode = item.ProductCode,
                                ProductName = item.ProductName
                            }).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = listView
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareGetDetail error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public Task<MessagePagedResponseBase> ReportCompareReonseList(ReportCompareReonseRequest request)
    {
        try
        {
            Expression<Func<CompareHistory, bool>> query = p => p.ProviderCode == request.ProviderCode
                                                                && p.TransDate >= request.TransDate.Date
                                                                    .ToUniversalTime()
                                                                && p.TransDate <= request.TransDate.Date.AddDays(1)
                                                                    .AddSeconds(-1).ToUniversalTime()
                                                                && p.ProviderCode == request.ProviderCode;

            var lst = _reportMongoRepository.GetAll(query);
            var list = new List<CompareReponseDto>();
            list.Add(new CompareReponseDto
            {
                CompareType = "Khớp",
                Quantity = lst.Sum(c => c.SameQuantity),
                AmountSys = lst.Sum(c => c.SameAmount),
                AmountProvider = lst.Sum(c => c.SameAmount),
                Deviation = 0
            });

            list.Add(new CompareReponseDto
            {
                CompareType = "Lệch",
                Quantity = lst.Sum(c => c.NotSameQuantity),
                AmountSys = lst.Sum(c => c.NotSameSysAmount),
                AmountProvider = lst.Sum(c => c.NotSameProviderAmount),
                Deviation = 0
            });

            list.Add(new CompareReponseDto
            {
                CompareType = "NCC có, Nhất Trần không có",
                Quantity = lst.Sum(c => c.ProviderOnlyQuantity),
                AmountSys = 0,
                AmountProvider = lst.Sum(c => c.ProviderOnlyAmount),
                Deviation = lst.Sum(c => c.ProviderOnlyAmount)
            });

            list.Add(new CompareReponseDto
            {
                CompareType = "Nhất Trần có, NCC không có",
                Quantity = lst.Sum(c => c.SysOnlyQuantity),
                AmountSys = lst.Sum(c => c.SysOnlyAmount),
                AmountProvider = 0,
                Deviation = lst.Sum(c => c.SysOnlyAmount)
            });

            return Task.FromResult(new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = 4,
                Payload = list
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareReonseList error {ex}");
            return Task.FromResult(new MessagePagedResponseBase
            {
                ResponseCode = "00"
            });
        }
    }

    public async Task<MessagePagedResponseBase> ReportCompareRefundDetail(ReportCompareRefundDetailRequest request)
    {
        try
        {
            Expression<Func<CompareTime, bool>> query = p => p.ProviderCode == request.ProviderCode
                                                             && p.KeyCode == request.KeyCode
                                                             && p.Result == 0 && p.Status == 1;

            if (request.RefundInt == 1)
            {
                Expression<Func<CompareTime, bool>> newQuery = p =>
                    p.IsRefund == true;
                query = query.And(newQuery);
            }
            else if (request.RefundInt == 2)
            {
                Expression<Func<CompareTime, bool>> newQuery = p =>
                    p.IsRefund == false || p.IsRefund == null;
                query = query.And(newQuery);
            }

            var listAll = _reportMongoRepository.GetAll(query);
            var total = listAll.Count();

            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<CompareTime, Guid>(query,
                s => s.CompareDate, false,
                request.Offset, request.Limit);


            var mlist = (from item in lst
                         select new CompareRefunDetailDto
                         {
                             TransDate = _dateHepper.ConvertToUserTime(item.TransDate, DateTimeKind.Utc),
                             AgentCode = item.AccountCode,
                             TransCode = item.TransCode,
                             TransPay = item.TransCodePay,
                             ProductValue = item.SysValue,
                             Amount = item.Amount,
                             ReceivedAccount = item.ReceivedAccount,
                             ProductCode = item.ProductCode,
                             ProductName = item.ProductName,
                             Status = item.IsRefund ?? false ? 1 : 0,
                             StatusName = item.IsRefund ?? false ? "Đã hoàn" : "Chưa hoàn",
                             TransCodeRefund = item.TranCodeRefund
                         }).OrderBy(c => c.TransDate).ToList();


            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                Payload = mlist
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareRefundDetail error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> ReportCompareRefundList(ReportCompareRefundRequest request)
    {
        try
        {
            Expression<Func<CompareHistory, bool>> query = p => p.Isenabled == true
                                                                && p.SysOnlyQuantity > 0;

            if (!string.IsNullOrEmpty(request.ProviderCode))
            {
                Expression<Func<CompareHistory, bool>> newQuery = p =>
                    p.ProviderCode == request.ProviderCode;
                query = query.And(newQuery);
            }


            if (request.FromDateTrans != null)
            {
                Expression<Func<CompareHistory, bool>> newQuery = p =>
                    p.TransDate >= request.FromDateTrans.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (request.ToDateTrans != null)
            {
                Expression<Func<CompareHistory, bool>> newQuery = p =>
                    p.TransDate <= request.ToDateTrans.AddDays(1).AddSeconds(-1).ToUniversalTime();
                query = query.And(newQuery);
            }


            var listAll = _reportMongoRepository.GetAll(query);

            var total = listAll.Count();
            var sumTotal = new CompareRefunDto
            {
                Quantity = listAll.Sum(c => c.SysOnlyQuantity),
                Amount = listAll.Sum(c => c.SysOnlyPrice),
                RefundQuantity = listAll.Sum(c => c.RefundQuantity ?? 0),
                RefundAmount = listAll.Sum(c => c.RefundPrice ?? 0),
                RefundWaitQuantity = listAll.Sum(c => c.RefundWaitQuantity ?? 0),
                RefundWaitAmount = listAll.Sum(c => c.RefundWaitPrice ?? 0)
            };

            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<CompareHistory, Guid>(query,
                s => s.TransDate, false,
                request.Offset, request.Limit);

            var list = (from item in lst
                        select new CompareRefunDto
                        {
                            TransDate = _dateHepper.ConvertToUserTime(item.TransDate, DateTimeKind.Utc),
                            Provider = item.ProviderCode,
                            Quantity = item.SysOnlyQuantity,
                            Amount = item.SysOnlyPrice,
                            RefundQuantity = item.RefundQuantity ?? 0,
                            RefundAmount = item.RefundPrice ?? 0,
                            RefundWaitQuantity = item.RefundWaitQuantity ?? 0,
                            RefundWaitAmount = item.RefundWaitPrice ?? 0,
                            KeyCode = item.KeyCode
                        }).OrderByDescending(c => c.TransDate).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = list
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareGetList error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public Task<MessageResponseBase> ReportCompareRefundSingle(ReportCompareRefundSingleRequest request)
    {
        try
        {
            //Expression<Func<CompareHistory, bool>> query = p => p.Isenabled == true
            //&& p.TransDateSoft == request.TransDate
            //&& p.ProviderCode == request.ProviderCode;

            //var single = _reportMongoRepository.GetAll<CompareHistory>(query).FirstOrDefault();
            //if (single == null)
            //    return new MessageResponseBase
            //    {
            //        ResponseCode = "00",
            //        ResponseMessage = "Không có dữ liệu",
            //        Payload = null
            //    };

            //var viewData = new CompareRefunDto()
            //{
            //    Quantity = single.SysQuantity,
            //    Amount = single.SysAmount,
            //    RefundQuantity = single.RefundQuantity ?? 0,
            //    RefundAmount = single.RefundAmount ?? 0,
            //    RefundWaitQuantity = single.RefundWaitQuantity ?? 0,
            //    RefundWaitAmount = single.RefundWaitAmount ?? 0,
            //};

            //return new MessageResponseBase
            //{
            //    ResponseCode = "01",
            //    ResponseMessage = "Thành công",
            //    Payload = viewData,
            //    ExtraInfo = viewData.ToJson(),
            //};


            Expression<Func<CompareTime, bool>> query = p =>
                p.KeyCode == request.KeyCode
                && p.Result == 0 && p.Status == 1
                && p.ProviderCode == request.ProviderCode;

            var list = _reportMongoRepository.GetAll(query);
            if (list == null)
                return Task.FromResult(new MessageResponseBase
                {
                    ResponseCode = "00",
                    ResponseMessage = "Không có dữ liệu",
                    Payload = null
                });

            var viewData = new CompareRefunDto
            {
                Quantity = list.Count(i => i.Result == 0),
                Amount = list.Where(i => i.Result == 0).Sum(i => i.Amount),
                RefundQuantity = list.Count(i => i.IsRefund == true),
                RefundAmount = list.Where(i => i.IsRefund == true).Sum(c => c.Amount),
                RefundWaitQuantity = list.Count(i => i.IsRefund == false || i.IsRefund == null),
                RefundWaitAmount = list.Where(i => i.IsRefund == false || i.IsRefund == null).Sum(c => c.Amount)
            };

            return Task.FromResult(new MessageResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Payload = viewData,
                ExtraInfo = viewData.ToJson()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareRefundSingle error {ex}");
            return Task.FromResult(new MessageResponseBase
            {
                ResponseCode = "00"
            });
        }
    }

    public async Task<MessagePagedResponseBase> ReportCheckCompareGet(ReportCheckCompareRequest request)
    {
        try
        {
            Expression<Func<CompareHistory, bool>> query = p
                => p.ProviderCode == request.ProviderCode
                   && p.TransDateSoft == request.TransDate;

            var total = await _reportMongoRepository.CountAsync(query);
            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = total.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCheckCompareGet error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> CompareProviderData(CompareProviderRequest request)
    {
        try
        {
            var checkCompare = await ReportCheckCompareGet(new ReportCheckCompareRequest
            {
                TransDate = request.TransDate.ToString("yyyyMMdd"),
                ProviderCode = request.ProviderCode
            });

            if (checkCompare.ResponseCode == "01")
            {
                if (Convert.ToInt32(checkCompare.Payload) > 0)
                    return new MessagePagedResponseBase
                    {
                        ResponseCode = "00",
                        ResponseMessage =
                            $"Nhà cung cấp này đã được đối soát của ngày {request.TransDate:dd/MM/yyyy}"
                    };

                var history = request.ConvertTo<CompareHistory>();
                var list = request.Items.ConvertTo<List<CompareTime>>();
                history.SysOnlyPrice = list.Where(c => c.Status == 1 && c.Result == 0).Sum(c => c.Amount);

                history.RefundQuantity = list.Count(c => c.Status == 1 && c.Result == 2 && c.IsRefund == true);
                history.RefundAmount = list.Where(c => c.Status == 1 && c.Result == 0 && c.IsRefund == true)
                    .Sum(c => c.SysValue);
                history.RefundPrice = list.Where(c => c.Status == 1 && c.Result == 0 && c.IsRefund == true)
                    .Sum(c => c.Amount);

                history.RefundWaitQuantity = history.SysOnlyQuantity - history.RefundQuantity;
                history.RefundWaitAmount = history.SysOnlyAmount - history.RefundAmount;
                history.RefundWaitPrice = history.SysOnlyPrice - history.RefundPrice;

                history.CompareDateSoft = history.CompareDate.ToString("yyyyMMdd");
                history.TransDateSoft = history.TransDate.ToString("yyyyMMdd");
                history.KeyCode = history.ProviderCode + "_" + history.TransDate.ToString("yyyyMMdd") + "_" +
                                  history.CompareDate.ToString("yyyyMMdd");
                await _reportMongoRepository.AddOneAsync(history);
                foreach (var item in list)
                {
                    item.CompareDateSoft = item.CompareDate.ToString("yyyyMMdd");
                    item.TransDateSoft = item.TransDate.ToString("yyyyMMdd");
                    item.KeyCode = history.KeyCode;
                    await _reportMongoRepository.AddOneAsync(item);
                }

                //await ConfirmSyncStatus(list);

                var register = await _balanceReportSvc.GetRegisterInfo("COMPARE_FILE");
                _logger.LogInformation($"COMPARE_FILE {(register != null ? register.ToJson() : "")}");
                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
                {
                    var listMail = register.EmailSend.Split(',', ';').ToList();
                    var listAddTach = new List<string>();
                    var inputDtos = new List<CompareReponseDto>();

                    #region Bảng

                    inputDtos.Add(new CompareReponseDto
                    {
                        CompareType = "Khớp",
                        Quantity = history.SameQuantity,
                        AmountSys = history.SameAmount,
                        AmountProvider = history.SameAmount,
                        Deviation = 0
                    });

                    inputDtos.Add(new CompareReponseDto
                    {
                        CompareType = "Lệch",
                        Quantity = history.NotSameQuantity,
                        AmountSys = history.NotSameSysAmount,
                        AmountProvider = history.NotSameProviderAmount,
                        Deviation = Math.Abs(history.NotSameSysAmount - history.NotSameProviderAmount)
                    });

                    inputDtos.Add(new CompareReponseDto
                    {
                        CompareType = "NCC có, Nhất Trần không có",
                        Quantity = history.ProviderOnlyQuantity,
                        AmountSys = 0,
                        AmountProvider = history.ProviderOnlyAmount,
                        Deviation = history.ProviderOnlyAmount
                    });

                    inputDtos.Add(new CompareReponseDto
                    {
                        CompareType = "Nhất Trần có,NCC không có",
                        Quantity = history.SysOnlyQuantity,
                        AmountSys = history.SysOnlyAmount,
                        AmountProvider = 0,
                        Deviation = history.SysOnlyAmount
                    });

                    #endregion
                    string sendFile = "";
                    var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.COMPARE);
                    var fileXName = $"{sourcePath.PathName}/Doisoat_x_{DateTime.Now.ToString("ddMMyyyyHHmmssfff")}.xlsx";                  
                    var excel = _dataExcel.ReportCompareToFile(inputDtos, fileXName);
                    if (excel != null)
                    {
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        if (fileBytes != null)
                        {                           
                            string fileName = $"Doisoat_{DateTime.Now.ToString("ddMMyyyyHHmmssfff")}.xlsx";
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            await File.WriteAllBytesAsync(pathSave, fileBytes);
                            sendFile = await _balanceReportSvc.ZipForderCreate(sourcePath);
                        }
                    }

                    var strBody = FillBodyByTableCompare(inputDtos);
                    var content = string.Format(register.Content, history.ProviderCode,
                        history.TransDate.ToString("dd-MM-yyyy"));
                    _emailSender.SendEmailReportAuto(listMail, content, strBody, sendFile);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Lưu thông tin đối soát thành công."
                };
            }

            return new MessagePagedResponseBase
            {
                ResponseCode = "00",
                ResponseMessage = "Lưu thông tin đối soát không thành công."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCompareGetList error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> RefundCompareData(CompareRefundCompareRequest request)
    {
        try
        {
            var items = new List<string>();
            if (request.Items != null && request.Items.Count > 0)
                items.AddRange(request.Items);

            if (!string.IsNullOrEmpty(request.ProviderCode) && !string.IsNullOrEmpty(request.KeyCode))
            {
                var reponseSerach = await ReportCompareRefundDetail(new ReportCompareRefundDetailRequest
                {
                    KeyCode = request.KeyCode,
                    ProviderCode = request.ProviderCode,
                    RefundInt = 2,
                    Offset = 0,
                    Limit = int.MaxValue
                });

                if (reponseSerach.ResponseCode == "01")
                {
                    var list = reponseSerach.Payload.ConvertTo<List<CompareRefunDetailDto>>();
                    items.AddRange(list.Select(c => c.TransCode).Distinct().ToList());
                }
            }

            if (items.Count > 0)
            {
                var dtoWait = new CompareReponseDto
                {
                    CompareType = "Giao dịch phải hoàn",
                    Quantity = 0
                };

                var dtoRefund = new CompareReponseDto
                {
                    CompareType = "Giao dịch đã hoàn",
                    Quantity = 0
                };

                var dtoPending = new CompareReponseDto
                {
                    CompareType = "Giao dịch còn phải hoàn",
                    Quantity = 0
                };

                var providerCode = "";
                var compareDate = "";
                foreach (var transCode in items)
                {
                    var saleRequest = await _grpcClient.GetClientCluster(GrpcServiceName.Backend).SendAsync(new GetSaleRequest
                    {
                        Filter = transCode
                    });
                    if (saleRequest != null)
                    {
                        compareDate = saleRequest.CreatedTime.ToString("dd-MM-yyyy");
                        providerCode = saleRequest.Provider;
                        dtoWait.Quantity = dtoWait.Quantity + 1;
                        dtoWait.AmountSys = dtoWait.AmountSys + saleRequest.Amount;
                        dtoWait.Amount = dtoWait.Amount + saleRequest.PaymentAmount;

                        if (saleRequest.Status == SaleRequestStatus.InProcessing
                            || saleRequest.Status == SaleRequestStatus.Paid
                            || saleRequest.Status == SaleRequestStatus.TimeOver
                            || saleRequest.Status == SaleRequestStatus.WaitForResult
                            || saleRequest.Status == SaleRequestStatus.ProcessTimeout
                           )
                        {
                            try
                            {
                                //Gunner => Gọi sang backend để check và hoàn tiền, sau đấy tự động update sale , chứ k phải gọi sang ví hoàn , rồi lại gọi sale update status
                                var refundResponse = await _grpcClient.GetClientCluster(GrpcServiceName.Backend).SendAsync(new TransactionRefundRequest
                                {
                                    TransCode = saleRequest.TransCode
                                });
                                _logger.LogInformation(
                                    $"PaymenyRefund return: {refundResponse.ToJson()} {saleRequest.TransCode}-{saleRequest.TransRef}");

                                if (refundResponse.ResponseStatus.ErrorCode == "01")
                                {
                                    dtoRefund.Quantity += 1;
                                    dtoRefund.Amount += saleRequest.PaymentAmount;
                                    dtoRefund.AmountSys += saleRequest.Amount;
                                    await _reportMongoRepository.UpdateReportStatus(saleRequest.TransCode,
                                        ReportStatus.Error);
                                    await RefundSetCompareData(new RefundSetCompareRequest
                                    {
                                        TransCode = saleRequest.TransCode,
                                        TransCodeRefund = refundResponse.Results.TransactionCode,
                                        KeyCode = request.KeyCode
                                    });
                                    // await Task.Run(async () =>
                                    // {
                                    //     try
                                    //     {
                                    //         //Noti
                                    //         var balanceRs = refundResponse.Results;
                                    //         var userAccount =
                                    //             await _reportMongoRepository.GetReportAccountByAccountCode(
                                    //                 saleRequest
                                    //                     .PartnerCode);
                                    //         if (userAccount != null && !string.IsNullOrEmpty(userAccount.ChatId) &&
                                    //             balanceRs != null)
                                    //         {
                                    //             var message =
                                    //                 $"Nhất Trần xin thông báo .Tài khoản {userAccount.AccountCode}-{userAccount.FullName} " +
                                    //                 $"vừa được hoàn tiền thành công cho giao dịch lỗi, số tiền {saleRequest.PaymentAmount.ToFormat("đ")}. " +
                                    //                 $"Số dư: {balanceRs.DesBalance.ToFormat("đ")}, lúc {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}." +
                                    //                 $"%0ANội dung: {$"Hoàn tiền cho giao dịch lỗi. Mã giao dịch:{saleRequest.TransRef}"}";
                                    //             await _bus.Publish<SendBotMessageToGroup>(new
                                    //             {
                                    //                 Title = "Thông báo biến động số dư do hoàn tiền lỗi giao dịch",
                                    //                 Message = message,
                                    //                 Module = "Balance",
                                    //                 BotType = BotType.Sale,
                                    //                 MessageType = BotMessageType.Message,
                                    //                 userAccount.ChatId,
                                    //                 TimeStamp = DateTime.Now,
                                    //                 CorrelationId = Guid.NewGuid()
                                    //             });
                                    //         }
                                    //     }
                                    //     catch (Exception e)
                                    //     {
                                    //         _logger.LogError($"SendNotifiRefundError:{e}");
                                    //     }
                                    // }).ConfigureAwait(false);
                                }
                                else
                                {
                                    dtoPending.Quantity = dtoPending.Quantity + 1;
                                    dtoPending.AmountSys = dtoPending.AmountSys + saleRequest.Amount;
                                    dtoPending.Amount = dtoPending.Amount + saleRequest.PaymentAmount;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    $"PaymenyRefund_Exception return: {saleRequest.TransCode}-{saleRequest.TransRef} => {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                                dtoPending.Quantity += 1;
                                dtoPending.AmountSys += saleRequest.Amount;
                                dtoPending.Amount += saleRequest.PaymentAmount;
                            }
                        }
                        else
                        {
                            dtoPending.Quantity += 1;
                            dtoPending.AmountSys += saleRequest.Amount;
                            dtoPending.Amount += saleRequest.PaymentAmount;
                        }
                    }
                }

                var register = await _balanceReportSvc.GetRegisterInfo("COMPARE_REFUND");
                _logger.LogInformation($"COMPARE_REFUND {(register != null ? register.ToJson() : "")}");
                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
                {
                    var listMail = register.EmailSend.Split(',', ';').ToList();
                    var sendFile = string.Empty;
                    var fileName = $"HoanTien_{DateTime.Now.ToString("ddMMyyyyHHmmssfff")}.xls";
                    var inputDtos = new List<CompareReponseDto> { dtoWait, dtoRefund, dtoPending };
                    var excel = _dataExcel.ReportRefundToFile(inputDtos, fileName);
                    if (excel != null)
                    {
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        if (fileBytes != null)
                        {
                            var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.COMPARE);
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            await File.WriteAllBytesAsync(pathSave, fileBytes);
                            sendFile = await _balanceReportSvc.ZipForderCreate(sourcePath);
                        }
                    }

                    var content = string.Format(register.Content, providerCode, compareDate);
                    var strBody = FillBodyByTableRefund(inputDtos);
                    _emailSender.SendEmailReportAuto(listMail, content, strBody, sendFile);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Lưu thông tin đối soát thành công."
                };
            }

            return new MessagePagedResponseBase
            {
                ResponseCode = "00",
                ResponseMessage = "Không có dữ liệu hoàn tiền."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"RefundCompareData error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    private async Task<MessagePagedResponseBase> RefundSetCompareData(RefundSetCompareRequest request)
    {
        try
        {
            Expression<Func<CompareTime, bool>> query = p
                => p.TransCode == request.TransCode && p.KeyCode == request.KeyCode;

            var dateCompare = "";
            var single = _reportMongoRepository.GetAll(query).FirstOrDefault();
            if (single != null && single.IsRefund == false)
            {
                Expression<Func<CompareHistory, bool>> queryHis = p => p.TransDateSoft == single.TransDateSoft
                                                                       && p.ProviderCode == single.ProviderCode
                                                                       && p.KeyCode == single.KeyCode;

                single.TranCodeRefund = request.TransCodeRefund;
                single.IsRefund = true;
                single.RefunDate = DateTime.Now;
                await _reportMongoRepository.UpdateOneAsync(single);

                var singleHis = _reportMongoRepository.GetAll(queryHis).FirstOrDefault();
                if (singleHis != null)
                {
                    singleHis.RefundQuantity = (singleHis.RefundQuantity ?? 0) + 1;
                    singleHis.RefundAmount = (singleHis.RefundAmount ?? 0) + single.SysValue;
                    singleHis.RefundPrice = (singleHis.RefundPrice ?? 0) + single.Amount;
                    singleHis.RefundWaitQuantity = (singleHis.RefundWaitQuantity ?? 0) - 1;
                    singleHis.RefundWaitAmount = (singleHis.RefundWaitAmount ?? 0) - single.SysValue;
                    singleHis.RefundWaitPrice = (singleHis.RefundWaitPrice ?? 0) - single.Amount;
                    dateCompare = singleHis.CompareDate.ToString("dd-MM-yyyy");
                    await _reportMongoRepository.UpdateOneAsync(singleHis);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    ExtraInfo = dateCompare
                };
            }

            return new MessagePagedResponseBase
            {
                ResponseCode = "00",
                ResponseMessage = "Cập nhật thất bại"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"RefundSetCompareData error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    private async Task ConfirmSyncStatus(List<CompareTime> list)
    {
        try
        {
            var lst = list.Where(c => c.Status == 1 && c.Result == 1).ToList();
            foreach (var item in lst)
            {
                await _grpcClient.GetClientCluster(GrpcServiceName.Backend).SendAsync<object>(new TopupUpdateStatusRequest
                {
                    Status = SaleRequestStatus.Success,
                    TransCode = item.TransCode
                });
                await _reportMongoRepository.UpdateReportStatus(item.TransCode, ReportStatus.Success);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"ConfirmSyncStatus Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }
    }

    private string FillBodyByTableCompare(List<CompareReponseDto> listResult)
    {
        try
        {
            var strBuilder = new StringBuilder();
            strBuilder.Append(
                "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
            strBuilder.Append("<th class='align_center' scope='col'>Loại kết quả</th>");
            strBuilder.Append("<th class='align_center' scope='col'>SL giao dịch</th>");
            strBuilder.Append("<th class='align_center' scope='col'>Số tiền BF Nhất Trần</th>");
            strBuilder.Append("<th class='align_center' scope='col'>Số tiền BF NCC</th>");
            strBuilder.Append("<th class='align_center' scope='col'>Số tiền lệch</th>");
            strBuilder.Append("</tr>");

            foreach (var rpt in listResult)
            {
                strBuilder.Append($"<tr><td class='align_left' style='width:200px;'><b>{rpt.CompareType}</b></td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.Quantity).ToString("N0")}</td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.AmountSys).ToString("N0")}</td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.AmountProvider).ToString("N0")}</td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.Deviation).ToString("N0")}</td>");
                strBuilder.Append("</tr>");
            }

            strBuilder.Append("</table>");
            return strBuilder.ToString();
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private string FillBodyByTableRefund(List<CompareReponseDto> listResult)
    {
        try
        {
            var strBuilder = new StringBuilder();
            strBuilder.Append(
                "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
            strBuilder.Append("<th class='align_center' scope='col'>Loại kết quả</th>");
            strBuilder.Append("<th class='align_center' scope='col'>Số lượng</th>");
            strBuilder.Append("<th class='align_center' scope='col'>Mệnh giá</th>");
            strBuilder.Append("<th class='align_center' scope='col'>Số tiền hoàn</th>");
            strBuilder.Append("</tr>");

            foreach (var rpt in listResult)
            {
                strBuilder.Append($"<tr><td class='align_left' style='width:200px;'><b>{rpt.CompareType}</b></td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.Quantity).ToString("N0")}</td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.AmountSys).ToString("N0")}</td>");
                strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.Amount).ToString("N0")}</td>");
                strBuilder.Append("</tr>");
            }

            strBuilder.Append("</table>");
            return strBuilder.ToString();
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}