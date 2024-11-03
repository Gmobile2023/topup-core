using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Domain.Exporting;
using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Report.Model.Dtos.ResponseDto;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.EsIndexs;
using GMB.Topup.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Nest;
using ServiceStack;

namespace GMB.Topup.Report.Domain.Repositories;

public partial class ElasticReportRepository : IElasticReportRepository
{
    private readonly ICacheManager _cacheManager;
    private readonly IDateTimeHelper _dateHepper;
    private readonly ElasticClient _elasticClient;
    private readonly IExportDataExcel _exportDataExcel;
    private readonly ILogger<ElasticReportRepository> _logger;
    private readonly IReportMongoRepository _reportMongoRepository;
    private readonly IFileUploadRepository _uploadFile;

    public ElasticReportRepository(ElasticClient elasticClient,
        IDateTimeHelper dateHepper,
        IReportMongoRepository reportMongoRepository,
        IExportDataExcel exportDataExcel,
        ICacheManager cacheManager,
        IFileUploadRepository uploadFile,
        ILogger<ElasticReportRepository> logger)
    {
        _elasticClient = elasticClient;
        _dateHepper = dateHepper;
        _reportMongoRepository = reportMongoRepository;
        _exportDataExcel = exportDataExcel;
        _cacheManager = cacheManager;
        _uploadFile = uploadFile;
        _logger = logger;
    }

    #region A.=>Báo cáo

    /// <summary>
    ///     1.Báo cáo chi tiết giao dịch
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportServiceDetailGetList(ReportServiceDetailRequest request)
    {
        if (request.SearchType == SearchType.Export)
            return await RptServiceDetail_Export(request);
        return await RptServiceDetailGrid(request);
    }

    private async Task<MessagePagedResponseBase> RptServiceDetailGrid(ReportServiceDetailRequest request)
    {
        var keyCode = "ServiceDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;
        try
        {
            var services = new List<string>
            {
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
                ReportServiceCode.PAY_BILL.ToLower(),
                ReportServiceCode.PIN_CODE.ToLower(),
                ReportServiceCode.PIN_DATA.ToLower(),
                ReportServiceCode.PIN_GAME.ToLower()
            };

            var status = new string[0];

            if (request.Status == 1)
                status = new[] { "1" };
            else if (request.Status == 3)
                status = new[] { "3" };
            else if (request.Status == 2)
                status = new[] { "2", "0" };
            else status = new[] { "1", "2", "0", "3" };

            var city = "";
            var distrinct = "";
            var ward = "";
            var loginCode = "";
            var agentType = "";
            var userAgentStaffCode = "";

            if (request.AccountType > 0)
                loginCode = request.LoginCode;
            if (request.CityId > 0)
                city = request.CityId.ToString();
            if (request.DistrictId > 0)
                distrinct = request.DistrictId.ToString();
            if (request.WardId > 0)
                ward = request.WardId.ToString();
            if (request.AgentType > 0)
                agentType = request.AgentType.ToString();

            if (!string.IsNullOrEmpty(request.UserAgentStaffCode))
                userAgentStaffCode = request.UserAgentStaffCode;

            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            var providerCode = new List<string>();

            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());

            if (request.VenderCode != null && request.VenderCode.Count > 0)
                foreach (var a in request.VenderCode)
                    if (!string.IsNullOrEmpty(a))
                        providerCode.Add(a);

            //providerCode.Add("FAKE-BACKUP_02");
            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.RequestRef).Query(request.RequestRef))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.PayTransRef).Query(request.PayTransRef))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Match(m => m.Field(f => f.ReceivedAccount).Query(request.ReceivedAccount))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(request.UserSaleCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(request.UserSaleLeaderCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(request.AgentCodeParent))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountDistrictId).Query(distrinct))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountWardId).Query(ward))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode))
                            .Query(loginCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.PerformAccount).Field(c => c.AccountCode))
                            .Query(userAgentStaffCode))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.ProvidersCode.Suffix("keyword")).Terms(providerCode.ToArray()))
                    //, mu => mu.MatchPhrase(m =>m.Field(f => f.ProvidersCode.Suffix("keyword")).Query(request.VenderCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ReceiverType).Query(request.ReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderReceiverType).Query(request.ProviderReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderTransCode).Query(request.ProviderTransCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentProvider).Query(request.ParentProvider))
                )
            ));


            var totalQuery = query;
            query.Aggregations(agg => agg
                .Sum("Quantity", s => s.Field(p => p.Quantity))
                .Sum("Value", s => s.Field(p => p.Amount))
                .Sum("Discount", s => s.Field(p => p.Discount))
                .Sum("Fee", s => s.Field(p => p.Fee))
                .Sum("Price", s => s.Field(p => p.TotalPrice))
                .Sum("Commission", s => s.Field(p => p.CommissionAmount))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Offset + request.Limit < 10000)
                query.From(0).Size(request.Offset + request.Limit).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var fQuantity = scanResults.Aggregations.GetValueOrDefault("Quantity");
            var fValue = scanResults.Aggregations.GetValueOrDefault("Value");
            var fDiscount = scanResults.Aggregations.GetValueOrDefault("Discount");
            var fFee = scanResults.Aggregations.GetValueOrDefault("Fee");
            var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
            var fCommission = scanResults.Aggregations.GetValueOrDefault("Commission");
            var quantity = fQuantity.ConvertTo<ValueTeam>();
            var value = fValue.ConvertTo<ValueTeam>();
            var discount = fDiscount.ConvertTo<ValueTeam>();
            var fee = fFee.ConvertTo<ValueTeam>();
            var price = fPrice.ConvertTo<ValueTeam>();
            var commission = fCommission.ConvertTo<ValueTeam>();

            var sumData = new ReportServiceDetailDto
            {
                Quantity = Convert.ToDecimal(quantity.Value),
                Value = Convert.ToDecimal(value != null ? value.Value : 0),
                Discount = Convert.ToDecimal(discount != null ? discount.Value : 0),
                Fee = Convert.ToDecimal(fee != null ? fee.Value : 0),
                Price = Convert.ToDecimal(price != null ? price.Value : 0),
                CommistionAmount = Convert.ToDecimal(commission != null ? commission.Value : 0)
            };

            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);
            totalQuery.From(0).Size(1).Scroll("5m");
            var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(totalQuery)).Total.ToString());

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu Total: {total} => Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            var listView = searchData.Skip(request.Offset).Take(request.Limit);

            var list = (from x in listView.ToList()
                        select new ReportServiceDetailDto
                        {
                            AgentType = x.AccountAgentType,
                            AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                            AgentCode = x.AccountCode,
                            AgentInfo = !string.IsNullOrEmpty(x.AccountInfo) ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                            StaffCode = x.SaleCode,
                            StaffInfo = !string.IsNullOrEmpty(x.SaleInfo) ? x.SaleInfo : x.SaleCode,
                            CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc),
                            Discount = Convert.ToDecimal(Math.Round(x.Discount, 0)),
                            Quantity = x.Quantity,
                            Fee = Convert.ToDecimal(Math.Round(x.Fee, 0)),
                            Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                            Value = Convert.ToDecimal(Math.Round(x.Price, 0)),
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
                                : x.Status == ReportStatus.TimeOut || x.Status == ReportStatus.Process
                                    ? "Chưa có kết quả"
                                    : "Lỗi",
                            TransCode = x.TransCode,
                            RequestRef = x.RequestRef,
                            PayTransRef = x.PayTransRef,
                            CategoryCode = x.CategoryCode,
                            CategoryName = x.CategoryName,
                            UserProcess = !string.IsNullOrEmpty(x.PerformInfo) ? x.PerformAccount + " - " + x.PerformInfo : x.PerformAccount,
                            VenderCode = x.ProvidersCode,
                            CommistionAmount = Convert.ToDecimal(x.CommissionAmount ?? 0),
                            AgentParentInfo = x.AccountAgentType == 5 ? x.ParentCode : String.Empty,
                            VenderName = request.AccountType == 0 ? x.ProvidersInfo : "",
                            Channel = x.Channel,
                            ReceivedAccount = x.ReceivedAccount,
                            ReceiverType = x.ReceiverType == "POSTPAID" ? "Trả sau" :
                                x.ReceiverType == "PREPAID" ? "Trả trước" : "",
                            ProviderReceiverType = x.ProviderReceiverType == "TT"
                            ? "Trả trước" : x.ProviderReceiverType == "TS"
                            ? "Trả sau" : "",
                            ProviderTransCode = x.ProviderTransCode,
                            ParentProvider = x.ParentProvider,
                            ReceiverTypeNote = x.ReceiverType,
                        }).ToList();

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumData,
                Total = total,
                Payload = list
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportServiceDetailGetList error: {e}");
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    private async Task<MessagePagedResponseBase> RptServiceDetail_Export(ReportServiceDetailRequest request)
    {
        var dateStart = DateTime.Now;
        var arrayDates = getArrayDate(request.FromDate.Value.Date, request.ToDate.Value.Date);
        var keyCode = "ServiceDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateTemp = DateTime.Now;
        var listView = new List<ReportItemDetail>();
        Parallel.ForEach(arrayDates, date =>
        {
            var item = RptServiceDetail_DateTime(request, date, keyCode).Result;
            listView.AddRange(item);
        });

        _logger.LogInformation(
            $"KeyCode= {keyCode} [{request.FromDate.Value.ToString("dd/MM/yyyy")} - {request.ToDate.Value.ToString("dd/MM/yyyy")}].Lay xong du lieu SumTotal: {listView.Count}. Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
        dateTemp = DateTime.Now;

        var list = (from x in listView.OrderBy(c => c.CreatedTime).ToList()
                    select new ReportServiceDetailDto
                    {
                        AgentType = x.AccountAgentType,
                        AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                        AgentCode = x.AccountCode,
                        AgentInfo = !string.IsNullOrEmpty(x.AccountInfo) ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                        StaffCode = x.SaleCode,
                        StaffInfo = x.SaleInfo,
                        CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc),
                        Discount = Convert.ToDecimal(Math.Round(x.Discount, 0)),
                        Quantity = x.Quantity,
                        Fee = Convert.ToDecimal(Math.Round(x.Fee, 0)),
                        Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                        Value = Convert.ToDecimal(Math.Round(x.Price, 0)),
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
                            : x.Status == ReportStatus.TimeOut || x.Status == ReportStatus.Process
                                ? "Chưa có kết quả"
                                : "Lỗi",
                        TransCode = x.TransCode,
                        RequestRef = x.RequestRef,
                        PayTransRef = x.PayTransRef,
                        CategoryCode = x.CategoryCode,
                        CategoryName = x.CategoryName,
                        UserProcess = !string.IsNullOrEmpty(x.PerformInfo) ? x.PerformAccount + " - " + x.PerformInfo : x.PerformAccount,
                        VenderCode = x.ProvidersCode,
                        CommistionAmount = Convert.ToDecimal(x.CommissionAmount ?? 0),
                        AgentParentInfo = x.AccountAgentType == 5 ? x.ParentCode : "",
                        VenderName = request.AccountType == 0 ? x.ProvidersInfo : "",
                        Channel = x.Channel,
                        ReceivedAccount = x.ReceivedAccount,
                        ReceiverType = x.ReceiverType == "POSTPAID" ? "Trả sau" : x.ReceiverType == "PREPAID" ? "Trả trước" : "",
                        ProviderReceiverType = x.ProviderReceiverType == "TS" ? "Trả sau" : x.ProviderReceiverType == "TT" ? "Trả trước" : "",
                        ProviderTransCode = x.ProviderTransCode,
                        ParentProvider = x.ParentProvider,
                        ReceiverTypeNote = x.ReceiverType,
                    }).ToList();

        _logger.LogInformation(
            $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
        dateTemp = DateTime.Now;
        if (list.Count >= 3000)
        {
            if (request.File == "EXCEL")
            {
                #region .xls

                var excel = _exportDataExcel.ReportServiceDetailToFile(list);
                _logger.LogInformation(
                    $"ReportServiceDetailGetList : {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                var fileBytes = await _cacheManager.GetFile(excel.FileToken);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} Write file .xlsx Seconds : {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;

                if (excel.FileToken != null)
                {
                    var fileName = "ServiceDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xlsx";
                    var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);
                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
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
                _exportDataExcel.ReportServiceDetailToFileCsv(pathSave, list);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} .Write file csv Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;
                byte[] fileBytes;
                var fs = new FileStream(strReadFile, FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fs);
                var numBytes = new FileInfo(strReadFile).Length;
                fileBytes = br.ReadBytes((int)numBytes);
                var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
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

                _logger.LogInformation(
                    $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
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

        _logger.LogInformation(
            $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
        return new MessagePagedResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "",
            Payload = list,
            ExtraInfo = ""
        };
    }

    private async Task<List<ReportItemDetail>> RptServiceDetail_DateTime(ReportServiceDetailRequest request,
        DateTime dateSearch, string keyCode)
    {
        try
        {
            var dateStart = DateTime.Now;

            var services = new List<string>
            {
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
                ReportServiceCode.PAY_BILL.ToLower(),
                ReportServiceCode.PIN_CODE.ToLower(),
                ReportServiceCode.PIN_DATA.ToLower(),
                ReportServiceCode.PIN_GAME.ToLower()
            };

            var status = new string[0];

            if (request.Status == 1)
                status = new[] { "1" };
            else if (request.Status == 3)
                status = new[] { "3" };
            else if (request.Status == 2)
                status = new[] { "2", "0" };
            else status = new[] { "1", "2", "0", "3" };

            var city = "";
            var distrinct = "";
            var ward = "";
            var loginCode = "";
            var agentType = "";
            var userAgentStaffCode = "";

            if (request.AccountType > 0)
                loginCode = request.LoginCode;
            if (request.CityId > 0)
                city = request.CityId.ToString();
            if (request.DistrictId > 0)
                distrinct = request.DistrictId.ToString();
            if (request.WardId > 0)
                ward = request.WardId.ToString();
            if (request.AgentType > 0)
                agentType = request.AgentType.ToString();

            if (!string.IsNullOrEmpty(request.UserAgentStaffCode))
                userAgentStaffCode = request.UserAgentStaffCode;

            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            var providerCode = new List<string>();
            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());

            if (request.VenderCode != null && request.VenderCode.Count > 0)
                foreach (var a in request.VenderCode)
                    if (!string.IsNullOrEmpty(a))
                        providerCode.Add(a);

            var query = new SearchDescriptor<ReportItemDetail>();
            var f = dateSearch;
            var fromDate = dateSearch.ToUniversalTime();
            var toDate = f.AddDays(1).ToUniversalTime();

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData {dateSearch.ToString("dd/MM/yyyy")}");

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.RequestRef).Query(request.RequestRef))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.PayTransRef).Query(request.PayTransRef))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Match(m => m.Field(f => f.ReceivedAccount).Query(request.ReceivedAccount))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(request.UserSaleCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(request.UserSaleLeaderCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(request.AgentCodeParent))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountDistrictId).Query(distrinct))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountWardId).Query(ward))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode))
                            .Query(loginCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.PerformAccount).Field(c => c.AccountCode))
                            .Query(userAgentStaffCode))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.ProvidersCode.Suffix("keyword")).Terms(providerCode))
                    //, mu => mu.MatchPhrase(m =>m.Field(f => f.ProvidersCode.Suffix("keyword")).Query(request.VenderCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ReceiverType).Query(request.ReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderReceiverType).Query(request.ProviderReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderTransCode).Query(request.ProviderTransCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentProvider).Query(request.ParentProvider))
                )
            ));

            query.From(0).Size(10000).Scroll("3m");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);
            _logger.LogInformation(
                $"KeyCode= {keyCode} [{dateSearch.ToString("dd/MM/yyyy")}] Lay du lieu xong Total: {searchData.Count()} => TotalSeconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return searchData;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"KeyCode= {keyCode} [{dateSearch.ToString("dd/MM/yyyy")}] Lay du lieu xong Total: Exception : {ex}");
            return new List<ReportItemDetail>();
        }
    }


    /// <summary>
    ///     2.Báo cáo hoàn tiền
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportRefundDetailGetList(ReportRefundDetailRequest request)
    {
        var keyCode = "RefundDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;
        try
        {
            var transTypes = new List<string>
            {
                ReportServiceCode.REFUND.ToLower()
            };

            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AgentCode)), mu => mu.DateRange(
                        r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Match(m => m.Field(f => f.PaidTransCode).Query(request.TransCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.TransTransSouce).Query(request.TransCodeSouce))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(transTypes))
                )
            ));

            var totalQuery = query;
            query.Aggregations(agg => agg
                .Sum("Discount", s => s.Field(p => p.Discount))
                .Sum("Fee", s => s.Field(p => p.Fee))
                .Sum("Price", s => s.Field(p => p.PaidAmount))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Offset + request.Limit < 10000)
                query.From(0).Size(request.Offset + request.Limit).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var fDiscount = scanResults.Aggregations.GetValueOrDefault("Discount");
            var fFee = scanResults.Aggregations.GetValueOrDefault("Fee");
            var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
            var discount = fDiscount.ConvertTo<ValueTeam>();
            var fee = fFee.ConvertTo<ValueTeam>();
            var price = fPrice.ConvertTo<ValueTeam>();
            var sumData = new ReportRefundDetailDto
            {
                Discount = Convert.ToDecimal(Math.Abs(discount.Value)),
                Fee = Convert.ToDecimal(Math.Abs(fee.Value)),
                Price = Convert.ToDecimal(Math.Abs(price.Value))
            };

            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);
            totalQuery.From(0).Size(10000).Scroll("3m");
            var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(totalQuery)).Total.ToString());

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            var listView = searchData.Skip(request.Offset).Take(request.Limit);

            var list = (from x in listView
                        select new ReportRefundDetailDto
                        {
                            CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc),
                            AgentCode = x.AccountCode,
                            AgentInfo = !string.IsNullOrEmpty(x.AccountInfo) ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                            Price = Convert.ToDecimal(Math.Round(Math.Abs(x.TotalPrice), 0)),
                            Discount = Convert.ToDecimal(Math.Round(Math.Abs(x.Discount), 0)),
                            Fee = Convert.ToDecimal(Math.Abs(x.Fee)),
                            ProductCode = x.ProductCode,
                            ProductName = x.ProductName,
                            ServiceCode = x.ServiceCode,
                            ServiceName = x.ServiceName,
                            TransCode = x.PaidTransCode,
                            TransCodeSouce = x.TransTransSouce,
                            AgentName = x.AccountAgentName,
                            CategoryCode = x.CategoryCode,
                            CategoryName = x.CategoryName
                        }).ToList();


            _logger.LogInformation(
                $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            if (request.SearchType == SearchType.Export && list.Count >= 3000)
            {
                var excel = _exportDataExcel.ReportRefundDetailToFile(list);
                _logger.LogInformation(
                    $"ReportRefundDetailGetList : {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                var fileBytes = await _cacheManager.GetFile(excel.FileToken);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} Write file .xlsx Seconds : {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;

                if (excel.FileToken != null)
                {
                    var fileName = "Refund_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xlsx";
                    var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

                    if (!string.IsNullOrEmpty(linkFile))
                    {
                        await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                        {
                            TextDay = DateTime.Now.ToString("yyyyMMdd"),
                            AddedAtUtc = DateTime.Now,
                            Type = "Báo cáo hoàn tiền BE",
                            FileName = linkFile
                        });

                        _logger.LogInformation(
                            $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

                        return new MessagePagedResponseBase
                        {
                            ResponseCode = "01",
                            ResponseMessage = linkFile,
                            Total = total,
                            SumData = sumData,
                            Payload = null,
                            ExtraInfo = "Downloadlink"
                        };
                    }
                }
            }

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumData,
                Total = total,
                Payload = list
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

    /// <summary>
    ///     3.Báo cáo lịch sử giao dịch
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportDetailGetList(ReportDetailRequest request)
    {
        var keyCode = "HistoryDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;

        try
        {
            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");

            var query = detailQuery(request, null);
            var totalQuery = detailQuery(request, null);
            var totalIncrement = detailTotalQuery(request, "Increment");
            var totalDecrement = detailTotalQuery(request, "Decrement");

            query.Aggregations(agg => agg
                .Max("MaxDate", s => s.Field(p => p.CreatedDate))
                .Min("MinDate", s => s.Field(p => p.CreatedDate))
            ).Sort(c => c.Descending(i => i.CreatedDate));

            if (request.Limit + request.Offset <= 10000)
                query.From(0).Size(request.Limit + request.Offset).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");

            totalQuery.From(0).Size(1).Scroll("5m");


            var total = int.Parse((await _elasticClient.SearchAsync<ReportBalanceHistories>(totalQuery)).Total.ToString());
            var searchData = new List<ReportBalanceHistories>();
            var scanResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(query);

            var scanIncrementResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(totalIncrement);
            var scanDecrementResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(totalDecrement);
            var fMaxDate = scanResults.Aggregations.GetValueOrDefault("MaxDate");
            var fMinDate = scanResults.Aggregations.GetValueOrDefault("MinDate");
            var fIncrement = scanIncrementResults.Aggregations.GetValueOrDefault("Increment");
            var fDecrement = scanDecrementResults.Aggregations.GetValueOrDefault("Decrement");
            var maxDate = fMaxDate.ConvertTo<ValueTeamDate>();
            var minDate = fMinDate.ConvertTo<ValueTeamDate>();
            var increment = fIncrement.ConvertTo<ValueTeam>();
            var decrement = fDecrement.ConvertTo<ValueTeam>();

            var queryMaxDate = detailQuery(request, maxDate.ValueAsString).From(0).Size(3);
            var queryMinDate = detailQuery(request, minDate.ValueAsString).From(0).Size(3);
            var searchMaxDateResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(queryMaxDate);
            var searchMinDateResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(queryMinDate);
            var itemMaxDate = searchMaxDateResults.Documents.OrderByDescending(c => c.CreatedDate).FirstNonDefault();
            var itemMinDate = searchMinDateResults.Documents.OrderBy(c => c.CreatedDate).FirstNonDefault();
            ScrollBalanceHistories(scanResults, request.Offset + request.Limit, ref searchData);
            _logger.LogInformation($"KeyCode= {keyCode} .Lay xong du lieu Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            var sumTotal = new ReportTransactionDetailDto
            {
                Increment = Math.Round(increment.Value, 0),
                Decrement = Math.Round(decrement.Value, 0),
                BalanceAfter = itemMaxDate != null
                    ? Math.Round(
                        itemMaxDate.DesAccountCode == request.AccountCode
                            ? itemMaxDate.DesAccountBalanceAfterTrans
                            : itemMaxDate.SrcAccountBalanceAfterTrans, 0)
                    : 0,
                BalanceBefore = itemMinDate != null
                    ? Math.Round(
                        itemMinDate.DesAccountCode == request.AccountCode
                            ? itemMinDate.DesAccountBalanceBeforeTrans
                            : itemMinDate.SrcAccountBalanceBeforeTrans, 0)
                    : 0
            };

            var listData = searchData.Skip(request.Offset).Take(request.Limit).ToList();

            #region .Fill Data

            foreach (var item in listData)
                item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

            var listHistory = listData.ConvertTo<List<ReportDetailDto>>();
            // var listDetails = await searchItemDetailList(request);
            var listView = (from x in listHistory
                                //join y in listDetails on x.TransCode equals y.PaidTransCode into g
                                //from i in g.DefaultIfEmpty()
                            select new ReportDetailDto()
                            {
                                CreatedDate = x.CreatedDate,
                                Decrement = request.AccountCode == x.SrcAccountCode ? x.Amount : 0,
                                Increment = request.AccountCode == x.DesAccountCode ? x.Amount : 0,
                                Amount = x.Amount,
                                BalanceAfter = request.AccountCode == x.SrcAccountCode
                                ? x.SrcAccountBalanceAfterTrans : request.AccountCode == x.DesAccountCode
                                ? x.DesAccountBalanceAfterTrans : 0,
                                BalanceBefore = request.AccountCode == x.SrcAccountCode
                                ? x.SrcAccountBalanceBeforeTrans : request.AccountCode == x.DesAccountCode
                                ? x.DesAccountBalanceBeforeTrans : 0,
                                TransCode = x.TransCode,
                                TransNote = x.ServiceCode == ReportServiceCode.TRANSFER
                                ? request.AccountCode == x.SrcAccountCode
                                ? $"Chuyển tiền tới tài khoản {x.DesAccountCode}. Nội dung: {x.Description}"
                                : request.AccountCode == x.DesAccountCode ? string.Format($"Nhận tiền từ tài khoản {x.SrcAccountCode}. Nội dung: {x.Description}")
                                : "" : x.TransNote,
                                ServiceCode = x.ServiceCode,
                                ServiceName = getServiceNameDetail(x.ServiceCode, request.AccountCode, x.DesAccountCode, x.SrcAccountCode),

                            }).ToList();

            #endregion

            _logger.LogInformation($"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            if (request.SearchType == SearchType.Export && listView.Count >= 3000)
            {
                if (request.File == "EXCEL")
                {
                    var excel = _exportDataExcel.ReportDetailToFile(listView);
                    _logger.LogInformation(
                        $"{request.AccountCode} ReportDetailGetList : {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                    var fileBytes = await _cacheManager.GetFile(excel.FileToken);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} Write file .xlsx Seconds : {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    dateTemp = DateTime.Now;

                    if (excel.FileToken != null)
                    {
                        var fileName = "Detail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xlsx";
                        var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                        _logger.LogInformation(
                            $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                        dateTemp = DateTime.Now;

                        if (!string.IsNullOrEmpty(linkFile))
                        {
                            await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                            {
                                TextDay = DateTime.Now.ToString("yyyyMMdd"),
                                AddedAtUtc = DateTime.Now,
                                Type = "Báo cáo lịch sử số dư BE",
                                FileName = linkFile
                            });

                            _logger.LogInformation(
                                $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                            return new MessagePagedResponseBase
                            {
                                ResponseCode = "01",
                                ResponseMessage = linkFile,
                                Total = total,
                                SumData = sumTotal,
                                Payload = null,
                                ExtraInfo = "Downloadlink"
                            };
                        }
                    }
                }

                else
                {
                    #region .csv

                    var sourcePath = Path.Combine("", "ReportFiles");
                    if (!Directory.Exists(sourcePath)) Directory.CreateDirectory(sourcePath);

                    var fileName = "Detail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".csv";
                    var pathSave = $"{sourcePath}/{fileName}";
                    var strReadFile = Directory.GetCurrentDirectory() + "/" + pathSave;
                    _exportDataExcel.ReportDetailToFileCsv(pathSave, listView);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Write file csv Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    dateTemp = DateTime.Now;
                    byte[] fileBytes;
                    var fs = new FileStream(strReadFile, FileMode.Open, FileAccess.Read);
                    var br = new BinaryReader(fs);
                    var numBytes = new FileInfo(strReadFile).Length;
                    fileBytes = br.ReadBytes((int)numBytes);
                    var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    fs.Close();
                    await fs.DisposeAsync();
                    File.Delete(strReadFile);
                    await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                    {
                        TextDay = DateTime.Now.ToString("yyyyMMdd"),
                        AddedAtUtc = DateTime.Now,
                        Type = "Báo cáo chi lịch sử số dư BE",
                        FileName = linkFile
                    });

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                    return new MessagePagedResponseBase
                    {
                        ResponseCode = "01",
                        ResponseMessage = linkFile,
                        Total = total,
                        SumData = sumTotal,
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
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = listView
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportDetailGetList error: {e}");
            _logger.LogInformation($"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    #region QueryHistoty

    private SearchDescriptor<ReportBalanceHistories> detailQuery(ReportDetailRequest request, DateTime? datePoint)
    {
        var query = new SearchDescriptor<ReportBalanceHistories>();
        var fromDate = request.FromDate.Value.ToUniversalTime();
        var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

        var serviceCode = string.Empty;
        var srcAccountCode = string.Empty;
        var desAccountCode = string.Empty;
        var accountCode = string.Empty;
        var filter = string.Empty;

        if (!string.IsNullOrEmpty(request.ServiceCode) && request.ServiceCode != "[]")
        {
            if (request.ServiceCode == ReportServiceCode.TRANSFER)
            {
                serviceCode = ReportServiceCode.TRANSFER;
                srcAccountCode = request.AccountCode;
            }
            else if (request.ServiceCode == ReportServiceCode.RECEIVEMONEY)
            {
                serviceCode = ReportServiceCode.TRANSFER;
                desAccountCode = request.AccountCode;
            }
            else
            {
                accountCode = request.AccountCode;
                serviceCode = request.ServiceCode;
            }
        }
        else
        {
            accountCode = request.AccountCode;
        }

        if (!string.IsNullOrEmpty(request.Filter))
            filter = request.Filter;

        if (datePoint != null)
            query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                    , mu => mu.DateRange(r =>
                        r.Field(f => f.CreatedDate).GreaterThanOrEquals(datePoint).LessThanOrEquals(datePoint))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(serviceCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.SrcAccountCode).Field(c => c.DesAccountCode)).Query(accountCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.TransCode).Field(c => c.TransRef).Field(c => c.TransNote))
                            .Query(filter))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.DesAccountCode).Query(desAccountCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SrcAccountCode).Query(srcAccountCode))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                )));
        else
            query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(serviceCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.SrcAccountCode).Field(c => c.DesAccountCode)).Query(accountCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.TransCode).Field(c => c.TransRef).Field(c => c.TransNote))
                            .Query(filter))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.DesAccountCode).Query(desAccountCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SrcAccountCode).Query(srcAccountCode))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                )));

        return query;
    }

    private SearchDescriptor<ReportBalanceHistories> detailTotalQuery(ReportDetailRequest request, string type)
    {
        var query = new SearchDescriptor<ReportBalanceHistories>();
        var fromDate = request.FromDate.Value.ToUniversalTime();
        var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
        var serviceCode = string.Empty;
        var filter = string.Empty;

        if (!string.IsNullOrEmpty(request.ServiceCode) && request.ServiceCode != "[]")
        {
            if (request.ServiceCode == ReportServiceCode.TRANSFER)
                serviceCode = ReportServiceCode.TRANSFER;
            else if (request.ServiceCode == ReportServiceCode.RECEIVEMONEY)
                serviceCode = ReportServiceCode.TRANSFER;
            else
                serviceCode = request.ServiceCode;
        }

        if (!string.IsNullOrEmpty(request.Filter))
            filter = request.Filter;

        if (type == "Increment")
        {
            query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.DesAccountCode).Query(request.AccountCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.TransCode).Field(c => c.TransRef).Field(c => c.TransNote))
                            .Query(filter))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(serviceCode))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                )));

            query.Aggregations(agg => agg
                .Sum("Increment", s => s.Field(p => p.Amount))).Size(1);
        }
        else
        {
            query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.SrcAccountCode).Query(request.AccountCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.TransCode).Field(c => c.TransRef).Field(c => c.TransNote))
                            .Query(filter))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(serviceCode))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                )));

            query.Aggregations(agg => agg
                .Sum("Decrement", s => s.Field(p => p.Amount))).Size(1);
        }

        return query;
    }

    #endregion

    /// <summary>
    ///     4.Báo cáo sao kê. lịch sử ở fontend
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportTransDetailGetList(ReportTransDetailRequest request)
    {
        if (request.FromDate == null)
            request.FromDate = DateTime.Now.AddDays(-30);
        if (request.ToDate == null)
            request.ToDate = DateTime.Now;

        if (request.SearchType == SearchType.Export)
            return await RptTransDetail_Export(request);
        return await RptTransDetailGet_Grid(request);
    }

    private async Task<MessagePagedResponseBase> RptTransDetailGet_Grid(ReportTransDetailRequest request)
    {
        var keyCode = "DetailFE_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;
        try
        {
            if (request.ToDate != null)
                request.ToDate = request.ToDate.Value.Date.AddDays(1);
            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");

            var queryGroup = queryTransDetail(request, request.FromDate, request.ToDate);
            queryGroup.Size(0).Aggregations(cs => cs.MultiTerms("Accounts",
                s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                    .Terms(t => t.Field(c => c.AccountCode.Suffix("keyword")),
                        t => t.Field(c => c.TransType.Suffix("keyword"))
                    ).Size(10000).Aggregations(c => c
                    .Sum("TotalPrice", i => i.Field(v => v.TotalPrice))
                    //.Sum("PriceIn", i => i.Field(v => v.PriceIn))
                    //.Sum("PriceOut",i => i.Field(v => v.PriceOut))
                    )));

            var scanGroupResults = await _elasticClient.SearchAsync<ReportItemDetail>(queryGroup);
            var searchDataSum = new List<ReportItemDetail>();
            var states = scanGroupResults.Aggregations.MultiTerms("Accounts");
            foreach (var bucket in states.Buckets)
            {
                var tempTotalPrice = bucket.GetValueOrDefault("TotalPrice");
                var _price = tempTotalPrice.ConvertTo<ValueTeam>();
                var key = bucket.Key.ToList();
                searchDataSum.Add(new ReportItemDetail
                {
                    AccountCode = key[0],
                    TransType = key[1],
                    TotalPrice = Math.Abs(_price.ConvertTo<ValueTeam>().Value)
                });
            }

            var query = queryTransDetail(request, request.FromDate, request.ToDate);
            query.Aggregations(agg => agg
                .Max("MaxDate", s => s.Field(p => p.CreatedTime))
                .Sum("Discount", s => s.Field(p => p.Discount))
                .Sum("Quantity", s => s.Field(p => p.Quantity))
                .Sum("Fee", s => s.Field(p => p.Fee))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Limit + request.Offset <= 10000)
                query.From(0).Size(request.Limit + request.Offset).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");
            var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(query)).Total.ToString());
            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);

            var fMaxDate = scanResults.Aggregations.GetValueOrDefault("MaxDate");
            var fQuantity = scanResults.Aggregations.GetValueOrDefault("Quantity");
            var fDiscount = scanResults.Aggregations.GetValueOrDefault("Discount");
            var fFee = scanResults.Aggregations.GetValueOrDefault("Fee");


            var maxDate = fMaxDate.ConvertTo<ValueTeamDate>();

            var priceOut1 = searchDataSum.Where(c => c.TransType == ReportServiceCode.TOPUP
                                                     || c.TransType == ReportServiceCode.TOPUP_DATA
                                                     || c.TransType == ReportServiceCode.PIN_CODE
                                                     || c.TransType == ReportServiceCode.PIN_GAME
                                                     || c.TransType == ReportServiceCode.PIN_DATA
                                                     || c.TransType == ReportServiceCode.PAY_BILL
                                                     || c.TransType == ReportServiceCode.CORRECTDOWN).Sum(c => c.TotalPrice);

            var priceOut2 = searchDataSum.Where(c => c.AccountCode != request.AccountCode && c.TransType == ReportServiceCode.TRANSFER).Sum(c => c.TotalPrice);
            var priceOut = priceOut1 + priceOut2;

            var priceIn1 = searchDataSum.Where(c => c.TransType == ReportServiceCode.DEPOSIT
                                                     || c.TransType == ReportServiceCode.PAYBATCH
                                                     || c.TransType == ReportServiceCode.PAYCOMMISSION
                                                     || c.TransType == ReportServiceCode.CORRECTUP
                                                     || c.TransType == ReportServiceCode.REFUND).Sum(c => c.TotalPrice);

            var priceIn2 = searchDataSum.Where(c => c.AccountCode == request.AccountCode && c.TransType == ReportServiceCode.TRANSFER).Sum(c => c.TotalPrice);
            var priceIn = priceIn1 + priceIn2;

            var sumData = new ReportTransDetailDto
            {
                Quantity = Convert.ToInt32(fQuantity.ConvertTo<ValueTeam>().Value),
                Discount = Convert.ToDecimal(fDiscount.ConvertTo<ValueTeam>().Value),
                Fee = Convert.ToDecimal(fFee.ConvertTo<ValueTeam>().Value),
                PriceOut = -Convert.ToDecimal(priceOut),
                PriceIn = Convert.ToDecimal(priceIn)
            };

            var queryMaxDate = new SearchDescriptor<ReportItemDetail>();
            queryMaxDate.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r =>
                        r.Field(f => f.CreatedTime).GreaterThanOrEquals(maxDate.ValueAsString)
                            .LessThanOrEquals(maxDate.ValueAsString))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(p => p.AccountCode).Field(p => p.PerformAccount))
                            .Query(request.AccountCode)))
            )).From(0).Size(3);

            var searchMaxDateResults = await _elasticClient.SearchAsync<ReportItemDetail>(queryMaxDate);
            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;


            var listData = searchData.Skip(request.Offset).Take(request.Limit);

            #region Fill Object

            var listView = listData.ConvertTo<List<ReportItemDetail>>();
            foreach (var item in listView)
            {
                item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                item.Quantity = item.Quantity == 0 ? 1 : item.Quantity;

                if (item.ServiceCode == ReportServiceCode.TRANSFER)
                {
                    if (item.AccountCode != request.AccountCode)
                    {
                        item.PriceOut = -Math.Abs(item.TotalPrice);
                        item.PriceIn = 0;
                    }
                    else
                    {
                        item.PriceIn = Math.Abs(item.TotalPrice);
                        item.PriceOut = 0;
                    }
                }
                else if (item.TransType == ReportServiceCode.TOPUP
                         || item.TransType == ReportServiceCode.TOPUP_DATA
                         || item.TransType == ReportServiceCode.PIN_CODE
                         || item.TransType == ReportServiceCode.PIN_GAME
                         || item.TransType == ReportServiceCode.PIN_DATA
                         || item.TransType == ReportServiceCode.PAY_BILL)
                {
                    item.PriceOut = -Math.Abs(item.TotalPrice);
                    item.PriceIn = 0;
                }
                else
                {
                    item.PriceIn = Math.Abs(item.TotalPrice);
                    item.PriceOut = 0;
                }
            }

            if (searchMaxDateResults.Documents.Count > 0)
            {
                var balance = searchMaxDateResults.Documents.OrderByDescending(c => c.CreatedTime).FirstOrDefault();
                sumData.Balance = Convert.ToDecimal(balance.Balance ?? 0);
            }

            var msgList = (from x in listView.ToList()
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

            #endregion

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumData,
                Payload = msgList
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportDetailGetList error: {e}");
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    private async Task<MessagePagedResponseBase> RptTransDetail_Export(ReportTransDetailRequest request)
    {
        var keyCode = "DetailFE_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;
        try
        {
            var dateTemp = DateTime.Now;
            var arraysDate = getArrayDate(request.FromDate.Value, request.ToDate.Value);
            var listData = new List<ReportItemDetail>();
            Parallel.ForEach(arraysDate, date =>
            {
                var item = RptTransDetail_Date(request, date, keyCode).Result;
                listData.AddRange(item);
            });


            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu SumTotal:{listData.Count} => Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");




            dateTemp = DateTime.Now;


            #region Fill Object

            foreach (var item in listData.OrderBy(c => c.CreatedTime))
            {
                item.CreatedTime = _dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                item.Quantity = item.Quantity == 0 ? 1 : item.Quantity;

                if (item.ServiceCode == ReportServiceCode.TRANSFER)
                {
                    if (item.AccountCode != request.AccountCode)
                    {
                        item.PriceOut = -Math.Abs(item.TotalPrice);
                        item.PriceIn = 0;
                    }
                    else
                    {
                        item.PriceIn = Math.Abs(item.TotalPrice);
                        item.PriceOut = 0;
                    }
                }
                else if (item.TransType == ReportServiceCode.TOPUP
                         || item.TransType == ReportServiceCode.TOPUP_DATA
                         || item.TransType == ReportServiceCode.PIN_CODE
                         || item.TransType == ReportServiceCode.PIN_GAME
                         || item.TransType == ReportServiceCode.PIN_DATA
                         || item.TransType == ReportServiceCode.PAY_BILL)
                {
                    item.PriceOut = -Math.Abs(item.TotalPrice);
                    item.PriceIn = 0;
                }
                else
                {
                    item.PriceIn = Math.Abs(item.TotalPrice);
                    item.PriceOut = 0;
                }
            }

            var msgList = (from x in listData.ToList()
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

            #endregion

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            if (msgList.Count <= 3000)
            {
                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Payload = msgList
                };
            }

            if (msgList.Count >= 0)
            {
                if (request.File == "EXCEL")
                {
                    var excel = _exportDataExcel.ReportTransDetailToFile(msgList);
                    _logger.LogInformation(
                        $"{request.AccountCode} ReportTransDetailGetList : {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                    var fileBytes = await _cacheManager.GetFile(excel.FileToken);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} Write file .xlsx Seconds : {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    dateTemp = DateTime.Now;

                    if (excel.FileToken != null)
                    {
                        var fileName = "Detail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xlsx";
                        var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                        _logger.LogInformation(
                            $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                        dateTemp = DateTime.Now;


                        if (!string.IsNullOrEmpty(linkFile))
                        {
                            await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                            {
                                TextDay = DateTime.Now.ToString("yyyyMMdd"),
                                AddedAtUtc = DateTime.Now,
                                Type = "Báo cáo lịch sử số dư FE",
                                FileName = linkFile
                            });

                            _logger.LogInformation(
                                $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                            return new MessagePagedResponseBase
                            {
                                ResponseCode = "01",
                                ResponseMessage = linkFile,

                                Payload = new object(),
                                ExtraInfo = "Downloadlink"
                            };
                        }
                    }

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                }
                else
                {
                    #region .csv

                    var sourcePath = Path.Combine("", "ReportFiles");
                    if (!Directory.Exists(sourcePath)) Directory.CreateDirectory(sourcePath);

                    var fileName = "Detail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".csv";
                    var pathSave = $"{sourcePath}/{fileName}";
                    var strReadFile = Directory.GetCurrentDirectory() + "/" + pathSave;
                    _exportDataExcel.ReportTransDetailToFileCsv(pathSave, msgList);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Write file csv Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    dateTemp = DateTime.Now;
                    byte[] fileBytes;
                    var fs = new FileStream(strReadFile, FileMode.Open, FileAccess.Read);
                    var br = new BinaryReader(fs);
                    var numBytes = new FileInfo(strReadFile).Length;
                    fileBytes = br.ReadBytes((int)numBytes);
                    var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Pust len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
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

                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
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

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Payload = msgList
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportDetailGetList error: {e}");
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    private async Task<List<ReportItemDetail>> RptTransDetail_Date(ReportTransDetailRequest request, DateTime date,
        string keyCode)
    {
        var dateStart = DateTime.Now;
        try
        {
            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");


            var query = queryTransDetail(request, date.Date, date.Date.AddDays(1));
            query.From(0).Size(10000).Scroll("3m");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu Total:{searchData.Count} => Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return searchData;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu Exception {ex}: Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return new List<ReportItemDetail>();
        }
    }

    private SearchDescriptor<ReportItemDetail> queryTransDetail(ReportTransDetailRequest request, DateTime? fDate,
        DateTime? tDate)
    {
        var query = new SearchDescriptor<ReportItemDetail>();
        DateTime? fromDate = null;
        DateTime? toDate = null;
        if (fDate != null)
            fromDate = fDate.Value.ToUniversalTime();

        if (tDate != null)
            toDate = tDate.Value.ToUniversalTime();

        var accountType = new int[0];
        var status = new string[0];
        var serviceCode = new List<string>();
        var transTypes = new List<string>();
        var venderCode = string.Empty;
        if (request.Status == 1)
            status = new[] { "1" };
        else if (request.Status == 3)
            status = new[] { "3" };
        else if (request.Status == 2)
            status = new[] { "2", "0" };
        else status = new[] { "1", "2", "0", "3" };

        if (request.Type == 1)
            accountType = new[] { 1, 2, 3, 0 };
        else if (request.Type == 2)
            accountType = new[] { 4 };


        var performAccount = string.Empty;
        var accountCode = string.Empty;
        var performAccountSum = string.Empty;
        var accountCodeSum = string.Empty;
        var requestTransCode = string.Empty;
        var filter = string.Empty;

        if (!string.IsNullOrEmpty(request.RequestTransCode))
            requestTransCode = request.RequestTransCode;

        if (!string.IsNullOrEmpty(request.ServiceCode))
        {
            if (request.ServiceCode == ReportServiceCode.REFUND)
            {
                transTypes.Add(ReportServiceCode.REFUND.ToLower());
            }
            else if (request.ServiceCode == ReportServiceCode.TRANSFER)
            {
                serviceCode.Add(request.ServiceCode.ToLower());
                transTypes = serviceCode;
                performAccount = request.AccountCode;
            }
            else if (request.ServiceCode == ReportServiceCode.RECEIVEMONEY)
            {
                accountCode = request.AccountCode;
                serviceCode.Add(ReportServiceCode.TRANSFER.ToLower());
                transTypes = serviceCode;
            }
            else
            {
                serviceCode.Add(request.ServiceCode.ToLower());
                transTypes = serviceCode;
            }
        }

        if (!string.IsNullOrEmpty(request.ProviderCode))
            venderCode = request.ProviderCode;

        if (!string.IsNullOrEmpty(request.Filter))
            filter = "*" + request.Filter + "*";


        if (!string.IsNullOrEmpty(filter))
        {
            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
            b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
               , mu => mu.MultiMatch(r => r.Fields(f => f.Field(c => c.AccountCode).Field(c => c.PerformAccount)).Query(request.AccountCode))
               , mu => mu.MultiMatch(r => r.Fields(f => f.Field(c => c.RequestRef.Suffix("keyword")).Field(c => c.TransCode.Suffix("keyword")).Field(c => c.PaidTransCode.Suffix("keyword"))).Query(requestTransCode))
               , mu => mu.QueryString(r => r.Fields(f => f
               .Field(c => c.RequestRef)
               .Field(c => c.TransCode)
               .Field(c => c.ReceivedAccount)
               .Field(c => c.AccountCode)
               .Field(c => c.AccountInfo)
               .Field(c => c.ServiceName)
               .Field(c => c.ProductName)
               .Field(c => c.CategoryName)
               .Field(c => c.PerformAccount)
               .Field(c => c.VenderName)
               .Field(c => c.PaidTransCode))
               .Query(filter))
               , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
               , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(transTypes.ToArray()))
               , mu => mu.MatchPhrase(r => r.Field(f => f.ReceivedAccount).Query(request.ReceivedAccount))
               , mu => mu.MatchPhrase(r => r.Field(f => f.PerformAccount).Query(performAccount))
               , mu => mu.MatchPhrase(r => r.Field(f => f.VenderCode).Query(venderCode))
               , mu => mu.MatchPhrase(r => r.Field(f => f.CategoryCode).Query(request.CategoryCode))
               , mu => mu.MatchPhrase(r => r.Field(f => f.PerformAccount).Query(request.UserProcess))
               , mu => mu.Terms(r => r.Field(f => f.PerformAgentType).Terms(accountType))
               , mu => mu.MatchPhrase(r => r.Field(f => f.AccountCode).Query(accountCodeSum))
               , mu => mu.MatchPhrase(r => r.Field(f => f.PerformAccount).Query(performAccountSum))
               , mu => mu.MatchPhrase(r => r.Field(f => f.AccountCode).Query(accountCode))
               , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
           )));
        }
        else
        {
            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
           b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
              , mu => mu.MultiMatch(r => r.Fields(f => f.Field(c => c.AccountCode).Field(c => c.PerformAccount)).Query(request.AccountCode))
              , mu => mu.MultiMatch(r => r.Fields(f => f.Field(c => c.RequestRef.Suffix("keyword")).Field(c => c.TransCode.Suffix("keyword")).Field(c => c.PaidTransCode.Suffix("keyword"))).Query(requestTransCode))
              , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
              , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(transTypes.ToArray()))
              , mu => mu.MatchPhrase(r => r.Field(f => f.ReceivedAccount).Query(request.ReceivedAccount))
              , mu => mu.MatchPhrase(r => r.Field(f => f.PerformAccount).Query(performAccount))
              , mu => mu.MatchPhrase(r => r.Field(f => f.VenderCode).Query(venderCode))
              , mu => mu.MatchPhrase(r => r.Field(f => f.CategoryCode).Query(request.CategoryCode))
              , mu => mu.MatchPhrase(r => r.Field(f => f.PerformAccount).Query(request.UserProcess))
              , mu => mu.Terms(r => r.Field(f => f.PerformAgentType).Terms(accountType))
              , mu => mu.MatchPhrase(r => r.Field(f => f.AccountCode).Query(accountCodeSum))
              , mu => mu.MatchPhrase(r => r.Field(f => f.PerformAccount).Query(performAccountSum))
              , mu => mu.MatchPhrase(r => r.Field(f => f.AccountCode).Query(accountCode))
              , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
          )));
        }

        return query;
    }

    /// <summary>
    ///     5.Báo cáo tổng hợp theo dịch vụ
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportServiceTotalGetList(ReportServiceTotalRequest request)
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
                ReportServiceCode.PIN_GAME.ToLower()
            };
            var status = new int[1] { 1 };

            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            var loginCode = "";
            if (request.AccountType > 0)
                loginCode = request.LoginCode;

            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            var agentType = request.AgentType <= 0 ? "" : request.AgentType.ToString();
            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());


            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r =>
                        r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThanOrEquals(toDate))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode))
                            .Query(loginCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ReceiverType).Query(request.ReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderReceiverType).Query(request.ProviderReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                )
            ));

            query.Size(0).Aggregations(cs => cs.MultiTerms("Products",
                    s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                        .Terms(t => t.Field(c => c.ServiceCode.Suffix("keyword")),
                            t => t.Field(c => c.ServiceName.Suffix("keyword")),
                            t => t.Field(c => c.ProductCode.Suffix("keyword")),
                            t => t.Field(c => c.ProductName.Suffix("keyword")),
                            t => t.Field(c => c.CategoryCode.Suffix("keyword")),
                            t => t.Field(c => c.CategoryName.Suffix("keyword"))
                        ).Size(1000)
                        .Aggregations(c => c
                            .Sum("Price", i => i.Field(v => v.TotalPrice))
                            .Sum("Amount", i => i.Field(v => v.Amount))
                            .Sum("Fee", i => i.Field(v => v.Fee))
                            .Sum("Quantity", i => i.Field(v => v.Quantity))
                            .Sum("Discount", i => i.Field(v => v.Discount))))
                );

            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var searchData = new List<ReportServiceTotalDto>();
            var states = scanResults.Aggregations.MultiTerms("Products");
            foreach (var bucket in states.Buckets)
            {
                var tempQuantity = bucket.GetValueOrDefault("Quantity");
                var tempDiscount = bucket.GetValueOrDefault("Discount");
                var tempPrice = bucket.GetValueOrDefault("Price");
                var tempAmount = bucket.GetValueOrDefault("Amount");
                var tempFee = bucket.GetValueOrDefault("Fee");
                var _qty = tempQuantity.ConvertTo<ValueTeam>();
                var _dis = tempDiscount.ConvertTo<ValueTeam>();
                var _amt = tempAmount.ConvertTo<ValueTeam>();
                var _fee = tempFee.ConvertTo<ValueTeam>();
                var _price = tempPrice.ConvertTo<ValueTeam>();
                var key = bucket.Key.ToList();
                searchData.Add(new ReportServiceTotalDto
                {
                    ServiceCode = key[0],
                    ServiceName = key[1],
                    ProductCode = key[2],
                    ProductName = key[3],
                    OrderValue = OrderValue(key[0], key[2]),
                    CategoryCode = key[4],
                    CategoryName = key[5],
                    Quantity = Convert.ToDecimal(_qty.Value),
                    Discount = Convert.ToDecimal(_dis.Value),
                    Fee = Convert.ToDecimal(_fee.Value),
                    Price = Convert.ToDecimal(_price.Value),
                    Value = Convert.ToDecimal(_amt.Value)
                });
            }

            var total = searchData.Count();
            var sumData = new ReportServiceTotalDto
            {
                Quantity = searchData.Sum(c => c.Quantity),
                Value = searchData.Sum(c => c.Value),
                Discount = searchData.Sum(c => c.Discount),
                Fee = searchData.Sum(c => c.Fee),
                Price = searchData.Sum(c => c.Price)
            };

            var lstOrder = new List<ReportServiceTotalDto>();
            var svViewCodes = searchData.OrderBy(c => c.ServiceName).Select(c => c.ServiceCode).Distinct();
            foreach (var s in svViewCodes)
            {
                var svViewItems = searchData.Where(c => c.ServiceCode == s).ToList();
                var cViewCodes = svViewItems.OrderBy(i => i.CategoryName).Select(i => i.CategoryCode).Distinct().ToList();
                foreach (var ct in cViewCodes)
                {
                    var pviewItem = s == ReportServiceCode.TOPUP || s == ReportServiceCode.TOPUP_DATA ||
                                    s == ReportServiceCode.PIN_CODE
                                    || s == ReportServiceCode.PIN_GAME || s == ReportServiceCode.PIN_GAME
                        ? svViewItems.Where(i => i.CategoryCode == ct).OrderBy(i => i.OrderValue).ToList()
                        : svViewItems.Where(i => i.CategoryCode == ct).OrderBy(i => i.ProductName).ToList();

                    lstOrder.AddRange(pviewItem);
                }
            }

            var listView = lstOrder.Skip(request.Offset).Take(request.Limit);

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumData,
                Payload = listView
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

    /// <summary>
    ///     5.1.Báo cáo tổng hợp số liệu theo nhà cung cấp
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportServiceProviderGetList(ReportServiceProviderRequest request)
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
                ReportServiceCode.PIN_GAME.ToLower()
            };
            var status = new int[1] { 1 };

            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            var loginCode = "";
            var agentType = "";
            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            var providerCode = new List<string>();

            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());

            if (request.ProviderCode != null && request.ProviderCode.Count > 0)
                foreach (var a in request.ProviderCode)
                    if (!string.IsNullOrEmpty(a))
                        providerCode.Add(a);


            if (request.AccountType > 0)
                loginCode = request.LoginCode;

            if (request.AgentType > 0)
                agentType = request.AgentType.ToString();

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r =>
                        r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThanOrEquals(toDate))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProvidersCode.Suffix("keyword")).Terms(providerCode.ToArray()))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(request.AgentCodeParent))
                    // , mu => mu.MatchPhrase(m => m.Field(f => f.ProvidersCode.Suffix("keyword")).Query(request.ProviderCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ReceiverType).Query(request.ReceiverType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderReceiverType).Query(request.ProviderReceiverType))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode))
                            .Query(loginCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));

            query.Size(0)
                .Aggregations(cs => cs.MultiTerms("Products",
                    s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                        .Terms(t => t.Field(c => c.ServiceCode.Suffix("keyword")),
                            t => t.Field(c => c.ServiceName.Suffix("keyword")),
                            t => t.Field(c => c.ProductCode.Suffix("keyword")),
                            t => t.Field(c => c.ProductName.Suffix("keyword")),
                            t => t.Field(c => c.CategoryCode.Suffix("keyword")),
                            t => t.Field(c => c.CategoryName.Suffix("keyword")),
                            t => t.Field(c => c.ProvidersCode.Suffix("keyword")),
                            t => t.Field(c => c.ProvidersInfo.Suffix("keyword"))
                        ).Size(1000)
                        .Aggregations(c => c
                            .Sum("Price", i => i.Field(v => v.TotalPrice))
                            .Sum("Amount", i => i.Field(v => v.Amount))
                            .Sum("Fee", i => i.Field(v => v.Fee))
                            .Sum("Quantity", i => i.Field(v => v.Quantity))
                            .Sum("Discount", i => i.Field(v => v.Discount))))
                );

            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var searchData = new List<ReportServiceTotalProviderDto>();
            var states = scanResults.Aggregations.MultiTerms("Products");
            foreach (var bucket in states.Buckets)
            {
                var tempQuantity = bucket.GetValueOrDefault("Quantity");
                var tempDiscount = bucket.GetValueOrDefault("Discount");
                var tempPrice = bucket.GetValueOrDefault("Price");
                var tempAmount = bucket.GetValueOrDefault("Amount");
                var tempFee = bucket.GetValueOrDefault("Fee");
                var _qty = tempQuantity.ConvertTo<ValueTeam>();
                var _dis = tempDiscount.ConvertTo<ValueTeam>();
                var _amt = tempAmount.ConvertTo<ValueTeam>();
                var _fee = tempFee.ConvertTo<ValueTeam>();
                var _price = tempPrice.ConvertTo<ValueTeam>();
                var key = bucket.Key.ToList();
                searchData.Add(new ReportServiceTotalProviderDto
                {
                    ServiceCode = key[0],
                    ServiceName = key[1],
                    ProductCode = key[2],
                    ProductName = key[3],
                    CategoryCode = key[4],
                    CategoryName = key[5],
                    ProviderCode = key[6],
                    ProviderName = key[7],
                    OrderValue = OrderValue(key[0], key[2]),
                    Quantity = Convert.ToDecimal(_qty.Value),
                    Discount = Convert.ToDecimal(_dis.Value),
                    Fee = Convert.ToDecimal(_fee.Value),
                    Price = Convert.ToDecimal(_price.Value),
                    Value = Convert.ToDecimal(_amt.Value)
                });
            }

            var total = searchData.Count();
            var sumData = new ReportServiceTotalProviderDto
            {
                Quantity = searchData.Sum(c => c.Quantity),
                Value = searchData.Sum(c => c.Value),
                Discount = searchData.Sum(c => c.Discount),
                Fee = searchData.Sum(c => c.Fee),
                Price = searchData.Sum(c => c.Price)
            };

            var lstOrder = new List<ReportServiceTotalProviderDto>();
            var svViewCodes = searchData.OrderBy(c => c.ServiceName).Select(c => c.ServiceCode).Distinct();
            foreach (var s in svViewCodes)
            {
                var svViewItems = searchData.Where(c => c.ServiceCode == s).ToList();
                var cViewCodes = svViewItems.OrderBy(i => i.CategoryName).Select(i => i.CategoryCode).Distinct()
                    .ToList();
                foreach (var ct in cViewCodes)
                {
                    var pviewItem = s == ReportServiceCode.TOPUP ||
                                    s == ReportServiceCode.TOPUP_DATA ||
                                    s == ReportServiceCode.PIN_CODE ||
                                    s == ReportServiceCode.PIN_GAME ||
                                    s == ReportServiceCode.PIN_GAME
                        ? svViewItems.Where(i => i.CategoryCode == ct).OrderBy(i => i.OrderValue).ToList()
                        : svViewItems.Where(i => i.CategoryCode == ct).OrderBy(i => i.ProductName).ToList();

                    lstOrder.AddRange(pviewItem);
                }
            }

            var listView = lstOrder.Skip(request.Offset).Take(request.Limit);

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumData,
                Payload = listView
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportServiceTotalProviderGetList error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    /// <summary>
    ///     1.Báo cáo chi tiết log giao dịch
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportTopupRequestLogGetList(ReportTopupRequestLogs request)
    {
        if (request.SearchType == SearchType.Export)
            return await RptTopupRequestLog_Export(request);
        return await RptTopupRequestLogGrid(request);
    }

    private async Task<MessagePagedResponseBase> RptTopupRequestLogGrid(ReportTopupRequestLogs request)
    {
        var keyCode = "TopupRequestLogs_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateStart = DateTime.Now;
        try
        {
            var services = new List<string>
            {
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
            };

            var status = new string[0];

            if (request.Status == 1)
                status = new[] { "1" };
            else if (request.Status == 3)
                status = new[] { "3" };
            else if (request.Status == 2)
                status = new[] { "2", "0" };
            else status = new[] { "1", "2", "0", "3" };

            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());


            var query = new SearchDescriptor<TopupRequestLog>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");

            query.Index(TopupGwIndex.TopupRequestLogIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.PartnerCode).Query(request.PartnerCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.RequestDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransRef))
                    , mu => mu.Match(m => m.Field(f => f.TransRef).Query(request.TransCode))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.MatchPhrase(m =>
                        m.Field(f => f.ProviderCode.Suffix("keyword")).Query(request.ProviderCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));


            var totalQuery = query;
            //query.Aggregations(agg => agg
            //    .Sum("Quantity", s => s.Field(p => p.Quantity))
            //    .Sum("Value", s => s.Field(p => p.Amount))
            //    .Sum("Discount", s => s.Field(p => p.Discount))
            //    .Sum("Fee", s => s.Field(p => p.Fee))
            //    .Sum("Price", s => s.Field(p => p.TotalPrice))
            //    .Sum("Commission", s => s.Field(p => p.CommissionAmount))
            //).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Offset + request.Limit < 10000)
                query.From(0).Size(request.Offset + request.Limit).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");

            var searchData = new List<TopupRequestLog>();
            var scanResults = await _elasticClient.SearchAsync<TopupRequestLog>(query);
            var fQuantity = scanResults.Aggregations.GetValueOrDefault("Quantity");
            var fValue = scanResults.Aggregations.GetValueOrDefault("Value");
            var fDiscount = scanResults.Aggregations.GetValueOrDefault("Discount");
            var fFee = scanResults.Aggregations.GetValueOrDefault("Fee");
            var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
            var fCommission = scanResults.Aggregations.GetValueOrDefault("Commission");
            var quantity = fQuantity.ConvertTo<ValueTeam>();
            var value = fValue.ConvertTo<ValueTeam>();
            var discount = fDiscount.ConvertTo<ValueTeam>();
            var fee = fFee.ConvertTo<ValueTeam>();
            var price = fPrice.ConvertTo<ValueTeam>();
            var commission = fCommission.ConvertTo<ValueTeam>();

            var sumData = new ReportTopupRequestLogDto
            {

            };

            ScrollReportTopupRequestLog(scanResults, request.Offset + request.Limit, ref searchData);
            totalQuery.From(0).Size(1).Scroll("5m");
            var total = int.Parse((await _elasticClient.SearchAsync<TopupRequestLog>(totalQuery)).Total.ToString());

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Lay xong du lieu Total: {total} => Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
            dateTemp = DateTime.Now;

            var listView = searchData.Skip(request.Offset).Take(request.Limit);

            var list = (from x in listView.ToList()
                        select new ReportTopupRequestLogDto
                        {
                            TransRef = x.TransRef,
                            TransCode = x.TransCode,
                            ReceiverInfo = x.ReceiverInfo,
                            ServiceCode = x.ServiceCode,
                            CategoryCode = x.CategoryCode,
                            ProductCode = x.ProductCode,
                            PartnerCode = x.PartnerCode,
                            AddedAtUtc = x.AddedAtUtc,
                            ModifiedDate = x.ModifiedDate,
                            TransAmount = x.TransAmount,
                            RequestDate = x.RequestDate,
                            ReferenceCode = x.ReferenceCode,
                            Vendor = x.Vendor,
                            TransIndex = x.TransIndex,
                            Status = (TransRequestStatus)x.Status,
                            ResponseInfo = x.ResponseInfo,
                            ProviderCode = x.ProviderCode,
                            StatusName = x.Status == (int)TransRequestStatus.Success ? "Thành công"
                                   : x.Status == (int)TransRequestStatus.Init ? "Đang xử lý"
                                   : x.Status == (int)TransRequestStatus.Fail ? "Lỗi"
                                   : "Chưa có KQ",
                        }).ToList();

            _logger.LogInformation(
                $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumData,
                Total = total,
                Payload = list
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportTopupRequestLogsGetList error: {e}");
            _logger.LogInformation(
                $"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    private async Task<MessagePagedResponseBase> RptTopupRequestLog_Export(ReportTopupRequestLogs request)
    {
        var dateStart = DateTime.Now;
        var arrayDates = getArrayDate(request.FromDate.Value.Date, request.ToDate.Value.Date);
        var keyCode = "TopupRequestLogs_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var dateTemp = DateTime.Now;
        var listView = new List<TopupRequestLog>();
        Parallel.ForEach(arrayDates, date =>
        {
            var item = RptTopupRequestLog_DateTime(request, date, keyCode).Result;
            listView.AddRange(item);
        });

        _logger.LogInformation(
            $"KeyCode= {keyCode} [{request.FromDate.Value.ToString("dd/MM/yyyy")} - {request.ToDate.Value.ToString("dd/MM/yyyy")}].Lay xong du lieu SumTotal: {listView.Count}. Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
        dateTemp = DateTime.Now;

        var list = (from x in listView.OrderBy(c => c.RequestDate).ToList()
                    select new ReportTopupRequestLogDto
                    {
                        TransRef = x.TransRef,
                        TransCode = x.TransCode,
                        ReceiverInfo = x.ReceiverInfo,
                        ServiceCode = x.ServiceCode,
                        CategoryCode = x.CategoryCode,
                        ProductCode = x.ProductCode,
                        PartnerCode = x.PartnerCode,
                        AddedAtUtc = x.AddedAtUtc,
                        ModifiedDate = x.ModifiedDate,
                        TransAmount = x.TransAmount,
                        RequestDate = x.RequestDate,
                        ReferenceCode = x.ReferenceCode,
                        Vendor = x.Vendor,
                        TransIndex = x.TransIndex,
                        Status = (TransRequestStatus)x.Status,
                        ResponseInfo = x.ResponseInfo,
                        ProviderCode = x.ProviderCode,
                        StatusName = x.Status == (int)TransRequestStatus.Success ? "Thành công"
                                   : x.Status == (int)TransRequestStatus.Init ? "Đang xử lý"
                                   : x.Status == (int)TransRequestStatus.Fail ? "Lỗi"
                                   : "Chưa có KQ",
                    }).ToList();

        _logger.LogInformation(
            $"KeyCode= {keyCode} .Fill Object Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
        dateTemp = DateTime.Now;
        if (list.Count >= 3000)
        {
            if (request.File == "EXCEL")
            {
                #region .xls
                var excel = _exportDataExcel.ReportTopupRequestLogToFile(list);
                _logger.LogInformation(
                    $"ReportTopupRequestLogGetList : {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                var fileBytes = await _cacheManager.GetFile(excel.FileToken);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} Write file .xlsx Seconds : {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;

                if (excel.FileToken != null)
                {
                    var fileName = "TopupRequestLog_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xlsx";
                    var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);
                    _logger.LogInformation(
                        $"KeyCode= {keyCode} .Push len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                    dateTemp = DateTime.Now;

                    if (!string.IsNullOrEmpty(linkFile))
                    {
                        await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                        {
                            TextDay = DateTime.Now.ToString("yyyyMMdd"),
                            AddedAtUtc = DateTime.Now,
                            Type = "Báo cáo chi tiết lịch sử giao dịch theo NCC",
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
                _exportDataExcel.ReportTopupRequestLogToFileCsv(pathSave, list);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} .Write file csv Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                dateTemp = DateTime.Now;
                byte[] fileBytes;
                var fs = new FileStream(strReadFile, FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fs);
                var numBytes = new FileInfo(strReadFile).Length;
                fileBytes = br.ReadBytes((int)numBytes);
                var linkFile = _uploadFile.UploadFileToDataServer(fileBytes, fileName);

                _logger.LogInformation(
                    $"KeyCode= {keyCode} .Push len ServerFpt Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                fs.Close();
                await fs.DisposeAsync();
                File.Delete(strReadFile);
                await _reportMongoRepository.InsertFileFptInfo(new ReportFileFpt
                {
                    TextDay = DateTime.Now.ToString("yyyyMMdd"),
                    AddedAtUtc = DateTime.Now,
                    Type = "Báo cáo chi tiết lịch sử giao dịch theo NCC",
                    FileName = linkFile
                });

                _logger.LogInformation(
                    $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
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


        _logger.LogInformation(
            $"KeyCode= {keyCode} .Tong thoi gian Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
        return new MessagePagedResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "",
            Payload = list,
            ExtraInfo = ""
        };
    }

    private async Task<List<TopupRequestLog>> RptTopupRequestLog_DateTime(ReportTopupRequestLogs request,
    DateTime dateSearch, string keyCode)
    {
        try
        {
            var dateStart = DateTime.Now;

            var services = new List<string>
            {
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
            };

            var status = new string[0];

            if (request.Status == 1)
                status = new[] { "1" };
            else if (request.Status == 3)
                status = new[] { "3" };
            else if (request.Status == 2)
                status = new[] { "2", "0" };
            else status = new[] { "1", "2", "0", "3" };

            var serviceCode = new List<string>();
            var categoryCode = new List<string>();
            var productCode = new List<string>();
            if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                foreach (var a in request.ServiceCode)
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());

            if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                foreach (var a in request.CategoryCode)
                    if (!string.IsNullOrEmpty(a))
                        categoryCode.Add(a.ToLower());

            if (request.ProductCode != null && request.ProductCode.Count > 0)
                foreach (var a in request.ProductCode)
                    if (!string.IsNullOrEmpty(a))
                        productCode.Add(a.ToLower());


            var query = new SearchDescriptor<TopupRequestLog>();
            var f = dateSearch;
            var fromDate = dateSearch.ToUniversalTime();
            var toDate = f.AddDays(1).ToUniversalTime();

            var dateTemp = DateTime.Now;
            _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData {dateSearch.ToString("dd/MM/yyyy")}");

            query.Index(TopupGwIndex.TopupRequestLogIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.PartnerCode).Query(request.PartnerCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.RequestDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransRef))
                    , mu => mu.Match(m => m.Field(f => f.TransRef).Query(request.TransCode))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.MatchPhrase(m =>
                        m.Field(f => f.ProviderCode.Suffix("keyword")).Query(request.ProviderCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));

            query.From(0).Size(10000).Scroll("5m");

            var searchData = new List<TopupRequestLog>();
            var scanResults = await _elasticClient.SearchAsync<TopupRequestLog>(query);
            ScrollReportTopupRequestLog(scanResults, request.Offset + request.Limit, ref searchData);
            _logger.LogInformation(
                $"KeyCode= {keyCode} [{dateSearch.ToString("dd/MM/yyyy")}] Lay du lieu xong Total: {searchData.Count()} => TotalSeconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return searchData;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"KeyCode= {keyCode} [{dateSearch.ToString("dd/MM/yyyy")}] Lay du lieu xong Total: Exception : {ex}");
            return new List<TopupRequestLog>();
        }
    }
    public async Task<NewMessageResponseBase<ItemMobileCheckDto>> QueryMobileTransData(ReportRegisterInfo config, CheckMobileTransRequest request)
    {
        var dateStart = DateTime.Now;
        try
        {
            var status = new[] { "1" };
            var query = new SearchDescriptor<ReportItemDetail>();
            //int beforDay = config.Total > 0 ? config.Total : 90;


            var serviceCode = new List<string>();
            var providerCode = new List<string>();
            if (!string.IsNullOrEmpty(config.Providers))
            {
                foreach (var a in config.Providers.Split('|', ',', ';'))
                    if (!string.IsNullOrEmpty(a))
                        providerCode.Add(a);
            }

            if (!string.IsNullOrEmpty(config.Extend))
            {
                foreach (var a in config.Extend.Split('|', ',', ';'))
                    if (!string.IsNullOrEmpty(a))
                        serviceCode.Add(a.ToLower());
            }
            else
            {
                serviceCode = new List<string>
                {
                     ReportServiceCode.TOPUP.ToLower(),
                     ReportServiceCode.TOPUP_DATA.ToLower(),
                     ReportServiceCode.PAY_BILL.ToLower(),
                };
            }

            var fromDate = request.FromDate.ToUniversalTime();
            var toDate = request.ToDate.ToUniversalTime();
            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(serviceCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ReceivedAccount).Query(request.Mobile))
                    , mu => mu.Terms(m => m.Field(f => f.ProvidersCode.Suffix("keyword")).Terms(providerCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));

            query.Aggregations(agg => agg
                .Sum("Quantity", s => s.Field(p => p.Quantity))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            query.From(0).Size(2).Scroll("5m");
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            //var fQuantity = scanResults.Aggregations.GetValueOrDefault("Quantity");
            //var quantity = fQuantity.ConvertTo<ValueTeam>();
            _logger.LogInformation($"QueryMobileRevenueData Check_Mobile : {request.Mobile}  Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");

            if (scanResults.Documents.Count > 0)
            {
                var fData = scanResults.Documents.MaxBy(c => c.CreatedTime);
                //var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(query)).Total.ToString());

                var dataItem = new ItemMobileCheckDto()
                {
                    Mobile = fData.ReceivedAccount,
                    Provider = fData.ProvidersCode,
                    Amount = fData.Amount,
                    ReceiveType = fData.ProviderReceiverType,
                    CreatedDate = _dateHepper.ConvertToUserTime(fData.CreatedTime, DateTimeKind.Utc).ToString("dd/MM/yyyy HH:mm:ss")
                };
                return new NewMessageResponseBase<ItemMobileCheckDto>()
                {
                    ResponseStatus = new ResponseStatusApi("01", "Thành công"),
                    Results = dataItem
                };
            }
            else return new NewMessageResponseBase<ItemMobileCheckDto>()
            {
                ResponseStatus = new ResponseStatusApi("00", "Không có dữ liệu"),
            };

        }
        catch (Exception e)
        {
            _logger.LogError($"QueryMobileRevenueData error: {e}");
            _logger.LogInformation($"Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
            return new NewMessageResponseBase<ItemMobileCheckDto>()
            {
                ResponseStatus = new ResponseStatusApi("00", "Truy vấn thất bại"),
            };
        }
    }


    public async Task<object> RemoveKeyData(string key)
    {
        try
        {
            await _cacheManager.DeleteEntity(key);
        }
        catch (Exception ex)
        {
            _logger.LogError($"RemoveKeyData Exception: {ex}");
        }

        return true;
    }
    #endregion
}