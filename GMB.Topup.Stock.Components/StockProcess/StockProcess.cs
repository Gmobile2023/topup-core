using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Stock.Contracts.Dtos;
using GMB.Topup.Stock.Contracts.Enums;
using GMB.Topup.Stock.Domains.BusinessServices;
using GMB.Topup.Stock.Domains.Entities;
using GMB.Topup.Stock.Domains.Grains;
using Microsoft.Extensions.Logging;
using Orleans;
using GMB.Topup.Discovery.Requests.Stocks;
using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.Shared;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.Contracts.Events.Report;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Shared.Utils;
using ServiceStack;

namespace GMB.Topup.Stock.Components.StockProcess;

public class StockProcess : IStockProcess
{
    private readonly ICardService _cardService;
    private readonly IClusterClient _clusterClient;
    //private readonly IServiceGateway _gateway; gunner
    private readonly IDateTimeHelper _dateHepper;
    private readonly ILogger<StockProcess> _logger;
    private readonly GrpcClientHepper _grpcClient;
    public StockProcess(IClusterClient clusterClient, ILogger<StockProcess> logger, ICardService cardService,
        GrpcClientHepper grpcClient, IDateTimeHelper dateHepper)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _cardService = cardService;
        _grpcClient = grpcClient;
        _dateHepper = dateHepper;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task<NewMessageResponseBase<object>> ExchangeRequest(StockCardExchangeRequest request)
    {
        try
        {
            _logger.LogInformation("ExchaneRequest recevied: " + request.ToJson());
            var srcStock =
                _clusterClient.GetGrain<IStockGrain>(string.Join("|", request.SrcStockCode, request.ProductCode));
            var inventory = await srcStock.CheckAvailableInventory();
            if (inventory < request.Amount)
            {
                _logger.LogInformation(
                    $"{request.BatchCode} StockExchangeCommand AvailableInventory: inventory:{inventory}; quantity:{request.Amount}");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_CardNotInventory,
                        "Kho thẻ không đủ")
                };
            }

            var cards = await srcStock.ExportCard(request.Amount, Guid.NewGuid(), request.BatchCode);
            if (cards == null)
            {
                _logger.LogInformation(
                    $"{request.BatchCode} StockExchangeCommand ExportCard: inventory:{inventory}; quantity:{request.Amount};");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_CardNotInventory,
                        "Không lấy được thông tin thẻ")
                };
            }

            _logger.LogInformation($"{request.BatchCode} ExportCard success: {cards.Count}");
            _logger.LogInformation($"{request.BatchCode} Processing import cards");
            var id = Guid.NewGuid();
            var result = await _clusterClient
                .GetGrain<IStockGrain>(request.DesStockCode + "|" + request.ProductCode)
                .ImportCard(cards, id);
            if (result)
            {
                _logger.LogInformation($"{request.BatchCode} ImportCard success: {cards.Count}");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "ImportCard success")
                };
            }

            _logger.LogInformation($"{request.BatchCode} ImportCard fail " + request.DesStockCode);
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success,
                    "ImportCard fail " + request.DesStockCode)
            };
        }
        catch (Exception e)
        {
            _logger.LogInformation($"{request.BatchCode} StockCardExchangeRequest error {e.Message}");
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "ImportCard fail " + request.DesStockCode)
            };
        }
    }


    public async Task<NewMessageResponseBase<object>> InportRequest(StockCardImportRequest request)
    {
        try
        {
            _logger.LogInformation("InportRequest recevied: " + request.ToJson());
            var id = Guid.NewGuid();
            //Chỗ này dùng client mới. cho những kho chưa được tạo
            var result = await _clusterClient
                .GetGrain<IStockGrain>(request.StockCode + "|" + request.ProductCode)
                .ImportInitCard(request.CardValue, request.Amount, id);

            if (result.Item1)
            {
                _logger.LogInformation("INIT_INVENTORY success");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success"),
                    Results = result.Item2
                };
            }

            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Can not init inventory"),
                Results = result.Item2
            };
        }
        catch (Exception e)
        {
            _logger.LogInformation($"InportRequest error {e.Message}");
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }
    }

    public async Task<NewMessageResponseBase<int>> CheckInventoryRequest(StockCardCheckInventoryRequest request)
    {
        try
        {
            _logger.LogInformation("CheckInventoryRequest recevied: " + request.ToJson());
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", request.StockCode.Trim(),
                request.ProductCode));
            var inventory = await stock.CheckAvailableInventory();

            return new NewMessageResponseBase<int>
            {
                Results = inventory,
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Succcess")
            };
        }
        catch (Exception e)
        {
            _logger.LogInformation($"InportRequest error {e.Message}");
            return new NewMessageResponseBase<int>
            {
                Results = 0,
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }
    }

    public async Task<NewMessageResponseBase<List<CardRequestResponseDto>>> ExportCardToSaleRequest(
        StockCardExportSaleRequest request)
    {
        try
        {
            _logger.LogInformation("ExportCardToSaleRequest recevied: " + request.ToJson());
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", request.StockCode.Trim(),
                request.ProductCode));
            var cards = await stock.Sale(request.Amount, Guid.NewGuid(), transCode: request.TransCode);

            if (cards == null)
            {
                _logger.LogInformation($"{request.TransCode} SALE Inventory is not enough");
                return new NewMessageResponseBase<List<CardRequestResponseDto>>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_CardNotInventory,
                        "Inventory is not enough")
                };
            }

            return new NewMessageResponseBase<List<CardRequestResponseDto>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success"),
                Results = cards.ConvertTo<List<CardRequestResponseDto>>()
            };
        }
        catch (Exception e)
        {
            _logger.LogInformation($"{request.TransCode} ExportCardToSaleRequest error {e.Message}");
            return new NewMessageResponseBase<List<CardRequestResponseDto>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }
    }

    public async Task<NewMessageResponseBase<string>> InportListRequest(StockCardImportListRequest request)
    {
        try
        {
            _logger.LogInformation($"InportListRequest recevied: {request.BatchCode}|{request.ProductCode}");
            var cards = request.CardItems.Select(x =>
            {
                var isDate = DateTime.TryParseExact(x.ExpiredDate, "dd/MM/yyyy", null, DateTimeStyles.None,
                    out var expiredDate);
                return new CardSimpleDto
                {
                    ProductCode = request.ProductCode,
                    ExpiredDate = isDate ? expiredDate : DateTime.Now.AddYears(3),
                    CardValue = x.CardValue,
                    CardCode = x.CardCode,
                    Serial = x.Serial
                };
            }).ToList();
            var result = await _cardService.CardsInsertAsync(request.BatchCode, cards);
            if (result.ResponseCode == ResponseCodeConst.Success)
            {
                _logger.LogInformation($"StockCardsImportCommand qua_buoc_1: {result.Payload.ToJson()}");
                var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", StockCodeConst.STOCK_TEMP,
                    request.ProductCode));

                _logger.LogInformation($"StockCardsImportCommand qua_buoc_2:");
                var lst = result.Payload.ConvertTo<List<ProviderCardStockDto>>();
                var providerItems = (from x in lst
                                     select new ProviderCardStockItem
                                     {
                                         BatchCode = x.BatchCode,
                                         ProviderCode = x.ProviderCode,
                                         Quantity = x.Quantity,
                                     }).ToList();

                _logger.LogInformation($"StockCardsImportCommand qua_buoc_3:");
                await stock.UnAllocate(providerItems, request.CardItems.Count, Guid.NewGuid());
                _logger.LogInformation($"StockCardsImportCommand success: {cards.Count}");
            }
            else
            {
                _logger.LogError("StockCardsImportCommand fail import by BatchCode: " + request.BatchCode +
                                 ", count: " + cards.Count());
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_Failed,
                        "StockCardsImportCommand fail import by BatchCode: " + request.BatchCode + ", count: " +
                        cards.Count)
                };
            }

            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
            };
        }
        catch (Exception e)
        {
            _logger.LogInformation($"InportRequest error {e.Message}|{e.InnerException}|{e.StackTrace}");
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }
    }

    public async Task<NewMessageResponseBase<List<NewMessageResponseBase<string>>>> CardImportStockFromProvider(
        StockCardImportApiRequest request)
    {
        var listResponseItem = new List<NewMessageResponseBase<string>>();
        _logger.LogInformation($"CardImportStockProvider : {request.ToJson()}");
        if (string.IsNullOrEmpty(request.Provider))
            return new NewMessageResponseBase<List<NewMessageResponseBase<string>>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Provider not valid")
            };
        if (!request.CardItems.Any())
            return new NewMessageResponseBase<List<NewMessageResponseBase<string>>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "CardItems not valid")
            };
        var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";

        var transCodeProviders = new List<string>();
        try
        {
            var batchDto = new StockBatchDto
            {
                ImportType = "API",
                BatchCode = $"{request.Provider}_{dateLog}",
                Description = request.Description,
                Provider = request.Provider,
                Status = StockBatchStatus.Active,
                CreatedDate = DateTime.Now,
                ExpiredDate = request.ExpiredDate,
                StockBatchItems = new List<StockBatchItemDto>()
            };

            var groupx = (from x in request.CardItems.Where(x => x.Quantity > 0)
                          select new StockBatchItemDto
                          {
                              ServiceCode = x.ServiceCode,
                              CategoryCode = x.CategoryCode,
                              ProductCode = x.ProductCode,
                              ItemValue = (int)x.CardValue,
                              Quantity = x.Quantity,
                              QuantityImport = 0,
                              Discount = x.Discount,
                              Amount = 0,
                              TransCode = x.TransCode,
                              TransCodeProvider = x.TransCodeProvider,
                              ExpiredDate = request.ExpiredDate,
                          }).ToList();


            batchDto.StockBatchItems.AddRange(groupx);

            var batch = await _cardService.StockBatchInsertAsync(batchDto);
            if (null == batch)
                return new NewMessageResponseBase<List<NewMessageResponseBase<string>>>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Batch create error")
                };

            var maxQuantityRequest = 100;
            var lstWait = new List<CardProviderItemWaitDto>();

            foreach (var item in batch.StockBatchItems)
            {
                var config = await _cardService.GetProviderConfigBy(request.Provider, item.ProductCode);
                if (config != null) maxQuantityRequest = config.Quantity;
                else maxQuantityRequest = 100;

                if (item.Quantity <= 0) continue;
                int index = 0;
                var quantity = item.Quantity;

                var responseData = new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error)
                };
                while (quantity > 0)
                {
                    var qty = 0;
                    if (quantity > maxQuantityRequest)
                    {
                        qty = maxQuantityRequest;
                        quantity -= maxQuantityRequest;
                    }
                    else
                    {
                        qty = quantity;
                        quantity = 0;
                    }

                    var response = await CardsApiImportProcessAsync(batch, item, qty, index: index).ConfigureAwait(false);
                    if (responseData.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                        responseData = new NewMessageResponseBase<object>()
                        {
                            ResponseStatus = response.ResponseStatus,
                        };

                    if (response.Results != null && response.Results.Count > 0)
                        lstWait.AddRange(response.Results);
                    index = index + 1;
                }

                listResponseItem.Add(new NewMessageResponseBase<string>
                {
                    Results = item.TransCodeProvider,
                    ResponseStatus = responseData.ResponseStatus
                });
            }

            await CheckUpdateCardProviderWait(request.Provider, batch.BatchCode, lstWait, request.ExpiredDate, isSyncCard: true, retry: false);

            return new NewMessageResponseBase<List<NewMessageResponseBase<string>>>
            {
                ResponseStatus = new ResponseStatusApi
                {
                    TransCode = transCodeProviders.ToJson(),
                    ErrorCode = ResponseCodeConst.Success,
                    Message = "Thành công"
                },
                Results = listResponseItem
            };
        }
        catch (PaygateException ex)
        {
            _logger.LogError("Error Exception: " + ex.Message);
            return new NewMessageResponseBase<List<NewMessageResponseBase<string>>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error Exception")
            };
        }
    }

    private async Task<NewMessageResponseBase<List<CardProviderItemWaitDto>>> CardsApiImportProcessAsync(StockBatchDto batch,
        StockBatchItemDto item, int quantity, int index = 0)
    {
        try
        {
            string transCodeProvider = batch.Provider.StartsWith(ProviderConst.VINNET)
                ? Guid.NewGuid().ToString()
                : index > 0 ? item.TransCodeProvider + $".{index}" : item.TransCodeProvider;

            _logger.LogInformation($"ProcessCardsApiImport: {batch.Provider} - {item.ProductCode} - {quantity}");
            var lstTemp = new List<CardProviderItemWaitDto>();

            var transRequestDto = new StockTransRequest()
            {
                BatchCode = batch.BatchCode,
                CategoryCode = item.CategoryCode,
                ProductCode = item.ProductCode,
                TransCodeProvider = transCodeProvider,
                TransCode = item.TransCode,
                ItemValue = item.ItemValue,
                ServiceCode = item.ServiceCode,
                TotalPrice = Convert.ToDecimal(item.ItemValue * (1 - item.Discount / 100) * quantity),
                Provider = batch.Provider,
                CreatedDate = DateTime.Now,
                Quantity = quantity,
                Status = StockBatchStatus.Init,
                ExpiredDate = batch.ExpiredDate,
                IsSyncCard = false,
                Id = Guid.NewGuid()
            };
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(new GateCardBathToStockRequest
            {
                ServiceCode = item.ServiceCode,
                CategoryCde = item.CategoryCode,
                Vendor = item.ProductCode.Split('_')[0],
                ProviderCode = batch.Provider,
                ProductCode = item.ProductCode,
                Amount = item.ItemValue,
                Quantity = quantity,
                RequestDate = DateTime.Now,
                PartnerCode = string.Empty,
                TransCodeProvider = transCodeProvider,
                TransRef = item.TransCode,
            });
            _logger.LogInformation($"GetCardProvider return: ErrorCode= {response?.ResponseStatus.ErrorCode}|Message= {response?.ResponseStatus.Message}|TotalCard: {response?.Results?.Count()}");

            if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
            {
                transRequestDto.Status = StockBatchStatus.Active;
                transRequestDto.IsSyncCard = true;
                await _cardService.StockTransRequestInsertAsync(transRequestDto);
                _logger.LogInformation("GetCardProvider success - Process import to stock");
                var listCardToImport = response.Results.Select(cardDto => new CardItemsImport
                {
                    Serial = cardDto.Serial,
                    CardCode = cardDto.CardCode,
                    CardValue = decimal.Parse(cardDto.CardValue),
                    ExpiredDate = getExpiredDate(cardDto.ExpireDate, batch.ExpiredDate)
                }).ToList();
                try
                {
                    var rs = await _grpcClient.GetClientCluster(GrpcServiceName.Stock).SendAsync(new StockCardImportListRequest
                    {
                        BatchCode = batch.BatchCode,
                        ProductCode = item.ProductCode,
                        CardItems = listCardToImport
                    });
                    _logger.LogInformation($"Import card to stock response:{rs.ToJson()}");
                    if (rs.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                    {
                        //Chỗ này gửi cảnh báo. Nếu lấy hàng rồi mà k nhập vào kho dc
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"{item.TransCode} StockCardImportListRequest error : {e}");
                }
            }
            else if (response.ResponseStatus.ErrorCode is ResponseCodeConst.ResponseCode_TimeOut
                or ResponseCodeConst.ResponseCode_WaitForResult
                or ResponseCodeConst.ResponseCode_InProcessing)
            {
                transRequestDto.Status = StockBatchStatus.Lock;
                await _cardService.StockTransRequestInsertAsync(transRequestDto);
                lstTemp.Add(new CardProviderItemWaitDto()
                {
                    ProductCode = item.ProductCode,
                    ServiceCode = item.ServiceCode,
                    TransCodeProvider = transCodeProvider
                });
            }
            else
            {
                transRequestDto.Status = StockBatchStatus.Delete;
                await _cardService.StockTransRequestInsertAsync(transRequestDto);
            }

            return new NewMessageResponseBase<List<CardProviderItemWaitDto>>
            {
                ResponseStatus = response.ResponseStatus,
                Results = lstTemp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("ProcessCardsApiImport Exception: " + ex);
            return new NewMessageResponseBase<List<CardProviderItemWaitDto>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error"),
                Results = new List<CardProviderItemWaitDto>()
            };
        }
    }

    /// <summary>
    /// Check lại thẻ ở trạng thái chưa có kết quả
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="list"></param>
    /// <returns></returns>
    private async Task CheckUpdateCardProviderWait(string provider, string batchCode, List<CardProviderItemWaitDto> list, DateTime? expiredDate, bool isSyncCard, bool retry = false)
    {
        foreach (var item in list)
            await CheckUpdateCardProviderItemRetry(provider, batchCode, item, expiredDate, isSyncCard, retry);
    }

    /// <summary>
    /// Cập nhật từng đơn hàng thành công khi check trans
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="batchCode"></param>
    /// <param name="item"></param>
    /// <param name="payLoad"></param>
    /// <returns></returns>
    private async Task<NewMessageResponseBase<string>> CheckUpdateCardProviderItemRetry(string provider, string batchCode, CardProviderItemWaitDto item, DateTime? expiredDate, bool isSyncCard, bool retry = false)
    {
        try
        {
            var checkTrans = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(new GateCheckTransRequest
            {
                ProviderCode = provider,
                ServiceCode = item.ServiceCode,
                TransCodeToCheck = item.TransCodeProvider,
            });

            _logger.LogInformation($"Provider = {provider} - TransCodeProvider = {item.TransCodeProvider} - retry = {retry} - CheckUpdateCardProviderItemRetry CheckTranItem {(checkTrans != null ? checkTrans.ResponseStatus.ErrorCode : "")} - {(checkTrans != null ? checkTrans.ResponseStatus.Message : "")}");

            if (checkTrans.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
            {
                if (checkTrans.Results != null && !string.IsNullOrEmpty(checkTrans.Results.PayLoad) && isSyncCard)
                {
                    var cardList = checkTrans.Results.PayLoad.FromJson<List<CardRequestResponseDto>>();
                    _logger.LogInformation($"Provider = {provider} - TransCodeProvider = {item.TransCodeProvider} - retry = {retry} - CheckUpdateCardProviderItemRetry - Insert-card-retry");
                    var listCardToImport = cardList.Select(cardDto => new CardItemsImport
                    {
                        Serial = cardDto.Serial,
                        CardCode = cardDto.CardCode.DecryptTripleDes(),
                        CardValue = decimal.Parse(cardDto.CardValue),
                        ExpiredDate = getExpiredDate(cardDto.ExpireDate, expiredDate)
                    }).ToList();
                    var rs = await _grpcClient.GetClientCluster(GrpcServiceName.Stock).SendAsync(new StockCardImportListRequest
                    {
                        BatchCode = batchCode,
                        ProductCode = item.ProductCode,
                        CardItems = listCardToImport
                    });
                    _logger.LogInformation($"Provider = {provider} - TransCodeProvider = {item.TransCodeProvider} retry = {retry} - CheckUpdateCardProviderItemRetry stock response : {rs.ToJson()}");
                    if (rs.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                        await _cardService.StockTransRequestUpdateAsync(provider, item.TransCodeProvider, StockBatchStatus.Active, true);
                }
            }

            return new NewMessageResponseBase<string>
            {
                ResponseStatus = checkTrans.ResponseStatus,
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"Provider = {provider} - TransCodeProvider = {item.TransCodeProvider} - retry = {retry} - CheckUpdateCardProviderItemRetry error:{e}");
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_TimeOut, "Chưa kiểm tra được trạng thái mã thẻ."),
            };
        }
    }

    private string getExpiredDate(string expiredDate, DateTime? date)
    {
        try
        {
            if (date == null)
                return expiredDate;
            var dateOne = DateTime.ParseExact(expiredDate, "dd/MM/yyyy",
                                 CultureInfo.InvariantCulture);          
            if (dateOne < date)
                return date.Value.ToString("dd/MM/yyyy");
            else return expiredDate;
        }
        catch 
        {
            return expiredDate;
        }
    }


    /// <summary>
    /// Kiểm tra mã thẻ và đồng bộ mã thẻ
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<NewMessageResponseBase<string>> CheckTransCardFromProvider(StockCardApiCheckTransRequest request)
    {

        _logger.LogInformation($"CheckTransCardFromProvider : {request.ToJson()}");
        if (string.IsNullOrEmpty(request.Provider))
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Provider not valid")
            };

        var transCodeProviders = new List<string>();
        try
        {
            DateTime? expiredDate = null;
            bool isSyncCard = true;
            var checkTransRequest = await _cardService.StockTransRequestGetAsync(request.Provider, request.TransCodeProvider);
            if (checkTransRequest != null)
            {
                if (checkTransRequest.ExpiredDate != null)
                    expiredDate = _dateHepper.ConvertToUserTime(checkTransRequest.ExpiredDate.Value, DateTimeKind.Utc);
                if (checkTransRequest.IsSyncCard == true)
                    isSyncCard = false;
            }
            else
            {
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi
                    {
                        TransCode = transCodeProviders.ToJson(),
                        ErrorCode = ResponseCodeConst.Error,
                        Message = "Không kiểm tra được giao dịch."
                    }
                };
            }
            var reponse = await CheckUpdateCardProviderItemRetry(request.Provider, checkTransRequest.BatchCode, new CardProviderItemWaitDto()
            {
                ProductCode = checkTransRequest.ProductCode,
                ServiceCode = checkTransRequest.ServiceCode,
                TransCodeProvider = checkTransRequest.TransCodeProvider,
            }, expiredDate, isSyncCard: isSyncCard, retry: true);

            return new NewMessageResponseBase<string>
            {
                ResponseStatus = reponse.ResponseStatus,
            };
        }
        catch (PaygateException ex)
        {
            _logger.LogError($"{request.Provider} - {request.TransCodeProvider} -  CheckTransCardFromProvider Exception: " + ex.Message);
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "CheckTransCardFromProvider Exception")
            };
        }
    }
}