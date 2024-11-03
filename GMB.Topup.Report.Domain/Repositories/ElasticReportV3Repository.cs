using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Report.Model.Dtos.ResponseDto;
using GMB.Topup.Shared;
using GMB.Topup.Shared.EsIndexs;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Nest;
using ServiceStack;

namespace GMB.Topup.Report.Domain.Repositories;

public partial class ElasticReportRepository
{
    public async Task<MessagePagedResponseBase> ReportComparePartnerGetList(ReportComparePartnerRequest request)
    {
        try
        {
            var services = new List<string>
            {
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
                ReportServiceCode.PAY_BILL.ToLower(),
                ReportServiceCode.PIN_CODE.ToLower(),
                ReportServiceCode.PIN_DATA.ToLower(),
                ReportServiceCode.PIN_GAME.ToLower(),
                ReportServiceCode.REFUND.ToLower(),
                ReportServiceCode.PAYBATCH.ToLower(),
                ReportServiceCode.CORRECTDOWN.ToLower(),
                ReportServiceCode.CORRECTUP.ToLower(),
                ReportServiceCode.TRANSFER.ToLower(),
                ReportServiceCode.DEPOSIT.ToLower()
            };

            string[] status = { "1", "2", "0", "3" };
            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.ToUniversalTime();
            var toDate = request.ToDate.AddDays(1).ToUniversalTime();

            if (request.Type == ReportServiceCode.PIN_CODE.Replace("_", ""))
            {
                status = new[] { "1" };
                if (request.ServiceCode == ReportServiceCode.PIN_GAME)
                {
                    services = new List<string>
                    {
                      ReportServiceCode.PIN_GAME.ToLower()
                    };
                }
                else if (request.ServiceCode == ReportServiceCode.PIN_CODE)
                {
                    services = new List<string>
                    {
                       ReportServiceCode.PIN_CODE.ToLower(),
                    };
                }
                else
                {
                    services = new List<string>
                    {
                       ReportServiceCode.PIN_CODE.ToLower(),
                       ReportServiceCode.PIN_GAME.ToLower()
                    };
                }
            }
            else if (request.Type == ReportServiceCode.TOPUP)
            {
                status = new[] { "1" };
                services = new List<string>
                {
                    ReportServiceCode.TOPUP.ToLower(),
                };
            }
            else if (request.Type == "DATA")
            {
                status = new[] { "1" };
                services = new List<string>
                {
                    ReportServiceCode.PIN_DATA.ToLower(),
                    ReportServiceCode.TOPUP_DATA.ToLower()
                };
            }
            else if (request.Type == ReportServiceCode.PAY_BILL.Replace("_", ""))
            {
                status = new[] { "1" };
                services = new List<string>
                {
                    ReportServiceCode.PAY_BILL.ToLower()
                };
            }
            else if (request.Type == "EXPORT" || request.Type == "SENDMAIL")
            {
                status = new[] { "1" };
                services = new List<string>
                {
                    ReportServiceCode.TOPUP.ToLower(),
                    ReportServiceCode.TOPUP_DATA.ToLower(),
                    ReportServiceCode.PIN_CODE.ToLower(),
                    ReportServiceCode.PIN_DATA.ToLower(),
                    ReportServiceCode.PIN_GAME.ToLower(),
                    ReportServiceCode.PAY_BILL.ToLower()
                };
            }

            if (request.ChangerType == ReceiverType.PostPaid || request.ChangerType == ReceiverType.PrePaid)
            {
                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                         , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.PerformAccount).Field(c => c.AccountCode)).Query(request.AgentCode))
                         , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                         , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                         , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.ReceiverType.Suffix("keyword")).Query(request.ChangerType))
                    )));               
            }
            else
            {
                // string[] sub = new string[] { "POSTPAID" };
                if (request.Type == ReportServiceCode.TOPUP)
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                     , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.PerformAccount).Field(c => c.AccountCode)).Query(request.AgentCode))
                     , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                     , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                     , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                   ).MustNot(v => v.MatchPhrase(i => i.Field(p => p.CategoryCode.Suffix("keyword")).Query("VTE_TOPUP")))));
                }
                else
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                   , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.PerformAccount).Field(c => c.AccountCode)).Query(request.AgentCode))
                   , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                   , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                   , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                   )));
                }                     
            }

            query.From(0).Size(10000).Scroll("5m");
            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            ScrollReportDetail(scanResults, int.MaxValue, ref searchData);

            if (request.Type == "BALANCE")
            {
                var balance = await GetBalanceAgent(request.AgentCode, request.FromDate, request.ToDate);
                var requestSouce = searchData
                    .Where(c => c.Status == ReportStatus.Error && c.TransType != ReportServiceCode.REFUND)
                    .Select(c => c.TransCode).ToList();
                var requestSouceRefund = searchData
                    .Where(c => c.Status == ReportStatus.Success && c.TransType == ReportServiceCode.REFUND &&
                               !string.IsNullOrEmpty(c.TransTransSouce)).Select(c => c.TransTransSouce).ToList();

                var balanceItems = new List<ReportBalancePartnerDto>();
                //1.Dư đầu kỳ
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 1,
                    Name = "Dư đầu kỳ",
                    Value = balance.BalanceBefore
                });

                //2.Nạp trong kỳ
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 2,
                    Name = "Nạp trong kỳ",
                    Value = searchData
                        .Where(c => c.TransType == ReportServiceCode.DEPOSIT && c.Status == ReportStatus.Success)
                        .Sum(c => c.TotalPrice)
                });

                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 3,
                    Name = "Số tiền tạm ứng trong kỳ",
                    Value = 0
                });

                //3.Bán trong kỳ
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 4,
                    Name = "Bán trong kỳ",
                    Value = searchData.Where(p => (p.ServiceCode == ReportServiceCode.TOPUP
                                                                     || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                     || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                     || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                     || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                     || p.ServiceCode == ReportServiceCode.PAY_BILL) &&
                                                                      p.TransType != ReportServiceCode.REFUND
                                                                      && p.Status == ReportStatus.Success)
                    .Sum(c => c.TotalPrice)
                });

                //4.Lỗi kỳ trước, hoàn trong kỳ
                var excpetSouceRefund = requestSouceRefund.Except(requestSouce).ToList();
                var amountRefund = searchData
                    .Where(c => c.RequestTransSouce != null && excpetSouceRefund.Contains(c.TransTransSouce))
                    .Sum(c => Math.Abs(c.TotalPrice));
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 5,
                    Name = "Lỗi kỳ trước, hoàn trong kỳ",
                    Value = amountRefund
                });

                //5.Lỗi trong kỳ, hoàn kỳ sau
                var excpetSouce = requestSouce.Except(requestSouceRefund).ToList();
                var amountSouce = searchData.Where(c => c.TransCode != null && excpetSouce.Contains(c.TransCode))
                    .Sum(c => c.TotalPrice);
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 6,
                    Name = "Lỗi trong kỳ, hoàn kỳ sau",
                    Value = amountSouce
                });

                //6.Chưa có kết quả
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 7,
                    Name = "Chưa có kết quả",
                    Value = searchData.Where(c =>
                        c.Status == ReportStatus.TimeOut || c.Status == ReportStatus.Process).Sum(c => c.TotalPrice),
                    TransCodes = searchData.Where(c =>
                       c.Status == ReportStatus.TimeOut || c.Status == ReportStatus.Process).Select(p => p.TransCode).ToList()

                });

                //7.Phát sinh tăng khác
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 8,
                    Name = "Phát sinh tăng khác trong kỳ",
                    Value = searchData.Where(c =>
                        (c.TransType == ReportServiceCode.CORRECTUP
                        || c.TransType == ReportServiceCode.PAYBATCH
                        || (c.TransType == ReportServiceCode.TRANSFER && c.AccountCode == request.AgentCode)) && c.Status == ReportStatus.Success).Sum(c => c.TotalPrice)
                });

                //8.Giảm trừ trong kỳ
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 9,
                    Name = "Giảm trừ khác trong kỳ",
                    Value = searchData.Where(c =>
                    (c.TransType == ReportServiceCode.CORRECTDOWN || (c.TransType == ReportServiceCode.TRANSFER && c.AccountCode != request.AgentCode))
                                                                    && c.Status == ReportStatus.Success)
                        .Sum(c => c.TotalPrice)
                });

                //9.Dư cuối kỳ
                balanceItems.Add(new ReportBalancePartnerDto
                {
                    Index = 10,
                    Name = "Dư cuối kỳ",
                    Value = balance.BalanceAfter
                });


                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = 1,
                    Payload = balanceItems
                };
            }

            var list = (from g in searchData
                        select new ReportComparePartnerDto
                        {
                            ServiceCode = g.ServiceCode,
                            ServiceName = g.ServiceName,
                            CategoryCode = g.CategoryCode,
                            CategoryName = g.CategoryName,
                            ProductCode = g.ProductCode,
                            ProductName = g.ProductName,
                            Quantity = g.Quantity,
                            Value = Convert.ToDecimal(g.Amount),
                            ProductValue = Convert.ToDecimal(g.ServiceCode == ReportServiceCode.PAY_BILL ? 1 : g.Price),
                            Discount = Convert.ToDecimal(Math.Round(g.Discount, 0)),
                            DiscountRate = Convert.ToDecimal(g.ServiceCode == ReportServiceCode.PAY_BILL
                                ? 0
                                : Math.Round(g.Price != 0 && g.Quantity != 0 ? g.Discount / g.Price / g.Quantity * 100 : 0, 2)),
                            Fee = Convert.ToDecimal(Math.Round(g.Fee, 0)),
                            FeeText = g.FeeText ?? string.Empty,
                            Price = Convert.ToDecimal(Math.Round(g.TotalPrice, 0)),
                            ReceiverType = g.ReceiverType ?? string.Empty,
                            Note = g.ReceiverType ?? string.Empty,
                        }).OrderBy(c => c.ProductCode).ToList();

            var listGroup = (from g in list
                             group g by new
                             {
                                 g.ServiceCode,
                                 g.ServiceName,
                                 g.CategoryCode,
                                 g.CategoryName,
                                 g.ProductCode,
                                 g.ProductName,
                                 g.ProductValue,
                                 g.DiscountRate,
                                 g.FeeText,
                                 g.ReceiverType,
                                 g.Note,
                             }
                into g
                             select new ReportComparePartnerDto
                             {
                                 ServiceCode = g.Key.ServiceCode,
                                 ServiceName = g.Key.ServiceName,
                                 CategoryCode = g.Key.CategoryCode,
                                 CategoryName = g.Key.CategoryName,
                                 ProductCode = g.Key.ProductCode,
                                 ProductName = g.Key.ProductName,
                                 ProductValue = g.Key.ProductValue,
                                 DiscountRate = g.Key.DiscountRate,
                                 FeeText = g.Key.FeeText,
                                 Note = g.Key.Note == ReceiverType.PostPaid ? "Trả sau" : g.Key.Note == ReceiverType.PrePaid ? "Trả trước" : "",
                                 ReceiverType = g.Key.ReceiverType,
                                 Quantity = g.Sum(c => c.Quantity),
                                 Discount = g.Sum(c => c.Discount),
                                 Fee = g.Sum(c => c.Fee),
                                 Price = g.Sum(c => c.Price),
                                 Value = g.Sum(c => c.Value),

                             }).OrderBy(c => c.ProductCode).ToList();

            var total = listGroup.Count();
            var sumTotal = new ReportComparePartnerDto
            {
                Quantity = listGroup.Sum(c => c.Quantity),
                Discount = listGroup.Sum(c => c.Discount),
                Value = listGroup.Sum(c => c.Value),
                Fee = listGroup.Sum(c => c.Fee),
                Price = listGroup.Sum(c => c.Price)
            };

            var serviceNames = listGroup.Select(c => c.ServiceName).Distinct().ToList();
            var listGroupOrder = new List<ReportComparePartnerDto>();

            foreach (var service in serviceNames.OrderBy(c => c))
            {
                var litCates = listGroup.Where(c => c.ServiceName == service).ToList();
                var cates = litCates.Select(c => c.CategoryName).Distinct().ToList();
                foreach (var cate in cates.OrderBy(c => c))
                {
                    var products = litCates.Where(c => c.CategoryName == cate && c.ServiceName == service)
                        .OrderBy(c => c.ProductValue).ToList();
                    listGroupOrder.AddRange(products);
                }
            }

            var lst = listGroupOrder.Skip(request.Offset).Take(request.Limit);

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = lst
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportComparePartnerGetList error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }
    private async Task<List<ReportStaffDetail>> RptDebtDetail_DateTime(ReportDebtDetailRequest request,
       DateTime dateSearch, string keyCode)
    {
        try
        {
            var dateStart = DateTime.Now;
            var query = new SearchDescriptor<ReportStaffDetail>();
            var f = dateSearch;
            var fromDate = dateSearch.ToUniversalTime();
            var toDate = f.AddDays(1).ToUniversalTime();

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData {dateSearch.ToString("dd/MM/yyyy")}");

            query.Index(ReportIndex.ReportStaffdetailsIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AccountCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                )
            ));

            query.From(0).Size(10000).Scroll("3m");

            var searchData = new List<ReportStaffDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportStaffDetail>(query);
            ScrollStaffDetail(scanResults, int.MaxValue, ref searchData);

            _logger.LogInformation(
                $"KeyCode= {keyCode} [{dateSearch.ToString("dd/MM/yyyy")}] Lay du lieu xong Total: {searchData.Count()} => TotalSeconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return searchData;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"KeyCode= {keyCode} [{dateSearch.ToString("dd/MM/yyyy")}] Lay du lieu xong Total: Exception : {ex}");
            return new List<ReportStaffDetail>();
        }
    }
    private async Task<MessagePagedResponseBase> RptDebtDetailGrid(ReportDebtDetailRequest request)
    {
        var keyCode = "DebtDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;
        try
        {
            var query = new SearchDescriptor<ReportStaffDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");

            query.Index(ReportIndex.ReportStaffdetailsIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AccountCode))
                    , mu => mu.DateRange(
                      r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.TransCode).Query(request.TransCode))
                    , mu => mu.Match(m => m.Field(f => f.ServiceCode).Query(request.ServiceCode))
                )
            ));

            var totalQuery = query;
            query.Aggregations(agg => agg
                .Sum("Price", s => s.Field(p => p.Price))
                .Sum("DebitAmount", s => s.Field(p => p.DebitAmount))
                .Sum("CreditAmount", s => s.Field(p => p.CreditAmount))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Limit + request.Offset <= 10000)
                query.From(0).Size(request.Limit + request.Offset).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");

            var searchData = new List<ReportStaffDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportStaffDetail>(query);
            var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
            var fDebitAmount = scanResults.Aggregations.GetValueOrDefault("DebitAmount");
            var fCreditAmount = scanResults.Aggregations.GetValueOrDefault("CreditAmount");
            var price = fPrice.ConvertTo<ValueTeam>();
            var debitAmount = fDebitAmount.ConvertTo<ValueTeam>();
            var creditAmount = fCreditAmount.ConvertTo<ValueTeam>();
            var sumData = new ReportStaffDetail
            {
                Price = Convert.ToInt32(price.Value),
                DebitAmount = debitAmount != null ? debitAmount.Value : 0,
                CreditAmount = creditAmount != null ? creditAmount.Value : 0,
            };
            ScrollStaffDetail(scanResults, request.Offset + request.Limit, ref searchData);
            totalQuery.From(0).Size(1000).Scroll("5m");
            var total = int.Parse((await _elasticClient.SearchAsync<ReportStaffDetail>(totalQuery)).Total.ToString());

            _logger.LogInformation($"KeyCode= {keyCode} .Lay xong du lieu Total: {total} => Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            var listView = searchData.Skip(request.Offset).Take(request.Limit).ToList();

            foreach (var item in listView)
            {
                item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                item.TransCode = string.IsNullOrEmpty(item.RequestRef) ? item.TransCode : item.RequestRef;
            }

            _logger.LogInformation($"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumData,
                Total = total,
                Payload = listView
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"RptDebtDetailGrid error: {e}");
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }
    private async Task<MessagePagedResponseBase> RptDebtDetail_Export(ReportDebtDetailRequest request)
    {
        var dateStart = DateTime.Now;
        var arrayDates = getArrayDate(request.FromDate.Value.Date, request.ToDate.Value.Date);
        var keyCode = "DebtDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateTemp = DateTime.Now;
        var listView = new List<ReportStaffDetail>();
        Parallel.ForEach(arrayDates, date =>
        {
            var item = RptDebtDetail_DateTime(request, date, keyCode).Result;
            listView.AddRange(item);
        });

        _logger.LogInformation($"KeyCode= {keyCode} [{request.FromDate.Value.ToString("dd/MM/yyyy")} - {request.ToDate.Value.ToString("dd/MM/yyyy")}].Lay xong du lieu SumTotal: {listView.Count}. Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
        dateTemp = DateTime.Now;

        var list = listView.OrderBy(c => c.CreatedTime).ToList();

        _logger.LogInformation($"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
        dateTemp = DateTime.Now;
        if (list.Count >= 3000)
        {
            if (request.File == "EXCEL")
            {
                #region .xls

                var excel = _exportDataExcel.ReportStaffDetailToFile(list);
                _logger.LogInformation($"ReportServiceDetailGetList : {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                var fileBytes = await _cacheManager.GetFile(excel.FileToken);

                _logger.LogInformation($"KeyCode= {keyCode} Write file .xlsx Seconds : {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;

                if (excel.FileToken != null)
                {
                    var fileName = "DebtDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xlsx";
                    var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);
                    _logger.LogInformation($"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    dateTemp = DateTime.Now;

                    if (!string.IsNullOrEmpty(linkFile))
                    {
                        await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                        {
                            TextDay = DateTime.Now.ToString("yyyyMMdd"),
                            AddedAtUtc = DateTime.Now,
                            Type = "Báo cáo chi tiết bán hàng BE",
                            FileName = linkFile
                        });
                        return new MessagePagedResponseBase
                        {
                            ResponseCode = "01",
                            ResponseMessage = linkFile,
                            Payload = null,
                            ExtraInfo = "Downloadlink"
                        };
                    }
                }

                #endregion
            }
            else
            {
                #region .csv

                var sourcePath = Path.Combine("", "ReportFiles");
                if (!Directory.Exists(sourcePath)) Directory.CreateDirectory(sourcePath);

                var fileName = "ServiceDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".csv";
                var pathSave = $"{sourcePath}/{fileName}";
                var strReadFile = Directory.GetCurrentDirectory() + "/" + pathSave;
                _exportDataExcel.ReportStaffDetailToFileCsv(pathSave, list);

                _logger.LogInformation($"KeyCode= {keyCode} .Write file csv Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;
                byte[] fileBytes;
                var fs = new FileStream(strReadFile, FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fs);
                var numBytes = new FileInfo(strReadFile).Length;
                fileBytes = br.ReadBytes((int)numBytes);
                var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                _logger.LogInformation($"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                fs.Close();
                await fs.DisposeAsync();
                File.Delete(strReadFile);
                await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                {
                    TextDay = DateTime.Now.ToString("yyyyMMdd"),
                    AddedAtUtc = DateTime.Now,
                    Type = "Báo cáo chi tiết bán hàng BE",
                    FileName = linkFile
                });

                _logger.LogInformation($"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = linkFile,
                    Payload = null,
                    ExtraInfo = "Downloadlink"
                };

                #endregion
            }
        }

        _logger.LogInformation($"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
        return new MessagePagedResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "",
            Payload = list,
            ExtraInfo = ""
        };
    }
    public async Task<MessagePagedResponseBase> ReportDebtDetailGetList(ReportDebtDetailRequest request)
    {
        if (request.SearchType == SearchType.Export)
            return await RptDebtDetail_Export(request);
        return await RptDebtDetailGrid(request);
    }
    public async Task<ReportItemDetail> ReportTransDetailQuery(TransDetailByTransCodeRequest request)
    {
        try
        {
            var query = new SearchDescriptor<ReportItemDetail>();
            ReportItemDetail first = null;
            if (!string.IsNullOrEmpty(request.Type))
            {
                if (request.Type.ToUpper() == "RequestRef".ToUpper())
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.RequestRef).Query(request.TransCode)))));
                }
                else if (request.Type.ToUpper() == "TransCode".ToUpper())
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.TransCode).Query(request.TransCode)))));
                }
                else if (request.Type.ToUpper() == "PaidTransCode".ToUpper())
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.PaidTransCode).Query(request.TransCode)))));
                }
                else if (request.Type.ToUpper() == "REFUND".ToUpper())
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.RequestTransSouce).Query(request.TransCode)))));
                }
                else
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.RequestRef).Query(request.TransCode)))));
                }

                query.From(0).Size(10000).Scroll("3m");
                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                if (scanResults.Documents != null && scanResults.Documents.Count > 0)
                    first = scanResults.Documents.FirstOrDefault();
            }
            else
            {

                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                       b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.RequestRef).Query(request.TransCode)))));
                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);

                if (scanResults.Documents == null || scanResults.Documents.Count == 0)
                {
                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                      b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.TransCode).Query(request.TransCode)))));

                    scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);

                    if (scanResults.Documents == null || scanResults.Documents.Count == 0)
                    {
                        query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                        b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.PaidTransCode).Query(request.TransCode)))));
                        scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                    }
                }

                if (scanResults.Documents != null && scanResults.Documents.Count > 0)
                    first = scanResults.Documents.FirstOrDefault();
            }


            if (first != null && first.TransType == ReportServiceCode.PAYCOMMISSION)
            {
                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                       b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CommissionPaidCode).Query(first.TransCode)))));
                var scanResults2 = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                if (scanResults2.Documents == null || scanResults2.Documents.Count() == 0)
                {

                    query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                      b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CommissionPaidCode).Query(first.PaidTransCode)))));
                    scanResults2 = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                }

                if (scanResults2.Documents != null && scanResults2.Documents.Count() > 0)
                {
                    var transOld = scanResults2.Documents.FirstOrDefault();
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
    public async Task<ReportAccountBalanceDay> getSaleByAccount(string accountCode, DateTime date)
    {
        try
        {
            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = date.Date.ToUniversalTime();
            var toDate = date.Date.AddDays(1).ToUniversalTime();
            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.PerformAccount)).Query(accountCode))
                )
            ));

            query.From(0).Size(10000).Scroll("5m");
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var searchData = new List<ReportItemDetail>();
            ScrollReportDetail(scanResults, int.MaxValue, ref searchData);

            double deposit = searchData.Where(c => c.TransType == ReportServiceCode.DEPOSIT).Sum(c => Math.Abs(c.TotalPrice));

            double incOther = searchData.Where(c =>
            (c.TransType == ReportServiceCode.TRANSFER && accountCode == c.AccountCode)
            || c.TransType == ReportServiceCode.REFUND
            || c.TransType == ReportServiceCode.CORRECTUP
            || c.TransType == ReportServiceCode.PAYBATCH
            || c.TransType == ReportServiceCode.PAYCOMMISSION).Sum(c => Math.Abs(c.TotalPrice));

            double decPayment = searchData.Where(c => (c.ServiceCode == ReportServiceCode.TOPUP
            || c.ServiceCode == ReportServiceCode.TOPUP_DATA || c.ServiceCode == ReportServiceCode.PAY_BILL
            || c.ServiceCode == ReportServiceCode.PIN_DATA || c.ServiceCode == ReportServiceCode.PIN_GAME
            || c.ServiceCode == ReportServiceCode.PIN_CODE) && c.TransType != ReportServiceCode.REFUND
            ).Sum(c => Math.Abs(c.TotalPrice));

            double decOther = searchData.Where(c => (c.TransType == ReportServiceCode.TRANSFER && c.AccountCode != accountCode)
            || (c.TransType == ReportServiceCode.CORRECTDOWN)).Sum(c => Math.Abs(c.TotalPrice));

            var dto = new ReportAccountBalanceDay()
            {
                AccountCode = accountCode,
                IncDeposit = deposit,
                IncOther = incOther,
                DecPayment = decPayment,
                DecOther = decOther,
                Credite = deposit + incOther,
                Debit = decPayment + decOther
            };

            return dto;
        }
        catch (Exception e)
        {
            _logger.LogError($"{accountCode} getSaleByAccount error: {e}");
            return new ReportAccountBalanceDay();
        }
    }
    public async Task<List<ReportItemDetail>> getSaleReportItem(DateTime date)
    {
        try
        {
            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = date.Date.ToUniversalTime();
            var toDate = date.Date.AddDays(1).ToUniversalTime();
            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate)))
            ));

            query.From(0).Size(10000).Scroll("5m");
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var searchData = new List<ReportItemDetail>();
            ScrollReportDetail(scanResults, int.MaxValue, ref searchData);
            return searchData;
        }
        catch (Exception e)
        {
            _logger.LogError($"getSaleReportItem error: {e}");
            return new List<ReportItemDetail>();
        }
    }

    public async Task<List<ReportBalanceHistories>> getHistoryReportItem(DateTime date)
    {
        try
        {
            var query = new SearchDescriptor<ReportBalanceHistories>();
            var fromDate = date.Date.ToUniversalTime();
            var toDate = date.Date.AddDays(1).ToUniversalTime();
            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate)))
            ));

            query.From(0).Size(10000).Scroll("5m");
            var scanResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(query);
            var searchData = new List<ReportBalanceHistories>();
            ScrollBalanceHistories(scanResults, int.MaxValue, ref searchData);
            return searchData;
        }
        catch (Exception e)
        {
            _logger.LogError($"getHistoryReportItem error: {e}");
            return new List<ReportBalanceHistories>();
        }
    }

    public async Task<List<ReportAgentBalanceDto>> getAccountBalanceItem(DateTime date)
    {
        try
        {
            var query = new SearchDescriptor<ReportAccountBalanceDay>();
            var fromDate = date.Date.ToUniversalTime();
            var toDate = date.Date.AddDays(1).ToUniversalTime();
            query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER")))
            ));
            query.From(0).Size(10000).Scroll("5m");
            var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
            var searchData = new List<ReportAccountBalanceDay>();
            ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);

            var list = (from x in searchData
                        select new ReportAgentBalanceDto()
                        {
                            AgentCode = x.AccountCode,
                            AgentType = x.AgentType ?? 0,
                            AgentTypeName = GetAgenTypeName(x.AgentType ?? 0),
                            AfterAmount = x.BalanceAfter,
                            BeforeAmount = x.BalanceBefore,
                            InputAmount = x.IncDeposit ?? 0,
                            SaleAmount = x.DecPayment ?? 0,
                            AmountDown = x.Debit,
                            AmountUp = x.Credite,
                            AgentInfo = x.AccountInfo,
                            SaleCode = x.SaleCode,
                            SaleLeaderCode = x.SaleLeaderCode,

                        }).ToList();

            return list;
        }
        catch (Exception e)
        {
            _logger.LogError($"getAccountBalanceItem error: {e}");
            return new List<ReportAgentBalanceDto>();
        }
    }
}