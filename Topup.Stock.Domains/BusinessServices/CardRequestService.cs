using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using NLog;
using ServiceStack;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Stock.Contracts.ApiRequests;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Domains.Entities;
using HLS.Paygate.Stock.Domains.Repositories;

namespace HLS.Paygate.Stock.Domains.BusinessServices
{
    public class CardRequestService : BusinessServiceBase, ICardRequestService
    {
        private readonly ICardMongoRepository _paygateMongoRepository;
        private readonly ICardMongoRepository _cardMongoRepository;
        private readonly Logger _logger = LogManager.GetLogger("CardRequestService");
        private readonly IDateTimeHelper _dateTimeHelper;

        public CardRequestService(ICardMongoRepository paygateMongoRepository, ICardMongoRepository cardMongoRepository,
            IDateTimeHelper dateTimeHelper)
        {
            _paygateMongoRepository = paygateMongoRepository;
            _cardMongoRepository = cardMongoRepository;
            _dateTimeHelper = dateTimeHelper;
        }

        public async Task<CardRequestDto> CardRequestCreateAsync(CardRequestDto cardRequest)
        {
            try
            {
                _logger.LogInformation("CardRequestCreateAsync request: " + cardRequest.ToJson());
                var cardRequestEnt = cardRequest.ConvertTo<CardRequest>();
                if (string.IsNullOrEmpty(cardRequestEnt.TransCode))
                    cardRequestEnt.TransCode = "C" + DateTime.Now.ToString("ddmmyyyyhhmmss");

                cardRequestEnt.CreatedTime = DateTime.Now;
                cardRequestEnt.ProviderCode = cardRequest.ProviderCode;
                cardRequestEnt.SupplierCode = cardRequest.SupplierCode;
                await _paygateMongoRepository.AddOneAsync(cardRequestEnt);
                return cardRequestEnt.ConvertTo<CardRequestDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error insert CardRequestCreateAsync: " + ex.Message);
                return null;
            }
        }

        public async Task<CardRequestDto> CardRequestGetAsync(Guid id)
        {
            var card = await _paygateMongoRepository.GetByIdAsync<CardRequest>(id);
            return card?.ConvertTo<CardRequestDto>();
        }

        public async Task<bool> CardRequestUpdateAsync(CardRequestDto cardRequest)
        {
            try
            {
                var cardRequestEnt = await _paygateMongoRepository.GetByIdAsync<CardRequest>(cardRequest.Id);

                if (cardRequestEnt != null)
                {
                    if (cardRequestEnt.Status != cardRequest.Status)
                        cardRequestEnt.Status = cardRequest.Status;
                    if (cardRequestEnt.RealValue != cardRequest.RealValue)
                        cardRequestEnt.RealValue = cardRequest.RealValue;
                    if (cardRequestEnt.ProcessPartner != cardRequest.ProcessPartner)
                        cardRequestEnt.ProcessPartner = cardRequest.ProcessPartner;
                    if (cardRequestEnt.TransRef != cardRequest.TransRef)
                        cardRequestEnt.TransRef = cardRequest.TransRef;
                    if (cardRequestEnt.ProcessPartner != cardRequest.ProcessPartner)
                        cardRequestEnt.PartnerTransCode = cardRequest.PartnerTransCode;
                    if (cardRequestEnt.SupplierCode != cardRequest.SupplierCode)
                        cardRequestEnt.SupplierCode = cardRequest.SupplierCode;
                    if (cardRequestEnt.ExportedDate != cardRequest.ExportedDate)
                        cardRequestEnt.ExportedDate = cardRequest.ExportedDate;
                    await _paygateMongoRepository.UpdateOneAsync(cardRequestEnt);
                    return true;
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError("Error update saleRequest: " + ex.Message);
            }

            return false;
        }

        public async Task<bool> CardRequestUpdateStatusAsync(Guid id, CardRequestStatus status)
        {
            var card = await _paygateMongoRepository.GetByIdAsync<CardRequest, Guid>(id);
            card.Status = status;
            try
            {
                await _paygateMongoRepository.UpdateOneAsync(card);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("Update cardrequest error: " + e.Message);
                return false;
            }
        }

        public async Task<MessagePagedResponseBase> CardRequestGetListAsync(CardRequestGetList cardGetList)
        {
            try
            {
                Expression<Func<CardRequest, bool>> query = p => true;

                if (cardGetList.Status != CardRequestStatus.Undefined)
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.Status == cardGetList.Status;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(cardGetList.Vendor))
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.Vendor == cardGetList.Vendor;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(cardGetList.PartnerCode))
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.ProviderCode == cardGetList.PartnerCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(cardGetList.TransCode))
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.TransCode == cardGetList.TransCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(cardGetList.TransRef))
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.TransRef == cardGetList.TransRef;
                    query = query.And(newQuery);
                }

                if (cardGetList.CardValue > 0)
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.RequestValue == cardGetList.CardValue;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(cardGetList.Serial))
                {
                    Expression<Func<CardRequest, bool>> newQuery = p => p.Serial == cardGetList.Serial;
                    query = query.And(newQuery);
                }

                if (cardGetList.FromImportDate != DateTime.MinValue)
                {
                    Expression<Func<CardRequest, bool>> newQuery = p =>
                        p.CreatedTime >= cardGetList.FromImportDate.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (cardGetList.ToImportDate != DateTime.MinValue)
                {
                    var date = cardGetList.ToImportDate.ToUniversalTime().AddHours(23).AddMinutes(59).AddSeconds(59);
                    Expression<Func<CardRequest, bool>> newQuery = p => p.CreatedTime <= date;
                    query = query.And(newQuery);
                }

                if (cardGetList.FromExpiredDate != DateTime.MinValue)
                {
                    Expression<Func<CardRequest, bool>> newQuery = p =>
                        p.ExpiredDate >= cardGetList.FromExpiredDate.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (cardGetList.ToExpiredDate != DateTime.MinValue)
                {
                    var date = cardGetList.ToExpiredDate.ToUniversalTime().AddHours(23).AddMinutes(59).AddSeconds(59);
                    Expression<Func<CardRequest, bool>> newQuery = p => p.ExpiredDate <= date;
                    query = query.And(newQuery);
                }

                if (cardGetList.FromExportedDate != DateTime.MinValue)
                {
                    Expression<Func<CardRequest, bool>> newQuery = p =>
                        p.ExportedDate >= cardGetList.FromExportedDate.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (cardGetList.ToExportedDate != DateTime.MinValue)
                {
                    var date = cardGetList.ToExportedDate.ToUniversalTime().AddHours(23).AddMinutes(59).AddSeconds(59);
                    Expression<Func<CardRequest, bool>> newQuery = p => p.ExportedDate <= date;
                    query = query.And(newQuery);
                }

                var total = await _cardMongoRepository.CountAsync<CardRequest>(query);
                var cardList = await _cardMongoRepository.GetSortedPaginatedAsync<CardRequest, Guid>(query,
                    s => s.AddedAtUtc, false,
                    cardGetList.Offset, cardGetList.Limit);

                if (cardList.Count > 0)
                {
                    Parallel.ForEach(cardList, card =>
                    {
                        card.CardCode = "... encrypted ...";
                        if (card.ExpiredDate != null && card.ExpiredDate != DateTime.MinValue)
                            card.ExpiredDate =
                                _dateTimeHelper.ConvertToUserTime(card.ExpiredDate.Value, DateTimeKind.Utc);
                        card.CreatedTime = _dateTimeHelper.ConvertToUserTime(card.CreatedTime, DateTimeKind.Utc);
                        if (card.ExportedDate != null && card.ExportedDate != DateTime.MinValue)
                            card.ExportedDate =
                                _dateTimeHelper.ConvertToUserTime(card.ExportedDate.Value, DateTimeKind.Utc);
                        else
                            card.ExportedDate = null;
                    });
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int) total,
                    Payload = cardList.OrderBy(x => x.Vendor).ThenBy(x => x.RequestValue)
                        .ThenByDescending(x => x.CreatedTime)
                        .ConvertTo<List<CardRequestDto>>()
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<bool> CardRequestCheckDuplicate(string serial, string cardCode)
        {
            var cardCheck =
                await _cardMongoRepository.GetOneAsync<CardRequest>(x => x.Serial == serial || x.CardCode == cardCode);
            return cardCheck == null;
        }

        public async Task<CardDto> CardRequestForMappingAsync(int cardValue, string stockType, bool isCardTimeOut = false)
        {
            CardRequest cardRequest;
            if (isCardTimeOut)
            {
                cardRequest = await _cardMongoRepository.GetCardRequestTimeOut(cardValue, stockType);
            }
            else
            {
                cardRequest = await _cardMongoRepository.GetCardRequestForUsing(cardValue, stockType);
            }

            if (cardRequest == null)
                return null;
            var cardReturn = cardRequest.ConvertTo<CardDto>();
            cardReturn.CardTransCode = cardRequest.TransCode;
            cardReturn.CardValue = cardRequest.RequestValue;
            return cardReturn;
        }

        public async Task<MessageResponseBase> CardRequestCheckAsync(string transCode, string providerCode)
        {
            var cardRequest = await _paygateMongoRepository.GetOneAsync<CardRequest>(p =>
                p.TransRef == transCode && p.ProviderCode == providerCode);
            var returnMessage = new MessageResponseBase
            {
                ResponseCode = "00"
            };
            if (null != cardRequest)
            {
                returnMessage.Payload = new CardResponseMesssage
                {
                    CardRealValue = int.Parse(cardRequest.RealValue.ToString()),
                    TransCode = cardRequest.TransRef = transCode,
                    ServerTransCode = cardRequest.TransCode
                };
                switch (cardRequest.Status)
                {
                    case CardRequestStatus.Success:
                        returnMessage.ResponseCode = "01";
                        returnMessage.ResponseMessage = "Thành công!";
                        break;
                    case CardRequestStatus.Canceled:
                        returnMessage.ResponseCode = "02";
                        returnMessage.ResponseMessage = "Thẻ lỗi";
                        break;
                    case CardRequestStatus.Failed:
                        returnMessage.ResponseCode = "03";
                        returnMessage.ResponseMessage = "Lỗi xử lý thẻ thất bại";
                        break;
                    case CardRequestStatus.TimeOver:
                        returnMessage.ResponseCode = "04";
                        returnMessage.ResponseMessage = "TimeOver.";
                        break;
                    case CardRequestStatus.WaitForResult:
                        returnMessage.ResponseCode = "05";
                        returnMessage.ResponseMessage = "Chưa có kết quả";
                        break;
                    case CardRequestStatus.InProcessing:
                        returnMessage.ResponseCode = "06";
                        returnMessage.ResponseMessage = "Thẻ đang xử lý";
                        break;
                    case CardRequestStatus.Init:
                        returnMessage.ResponseCode = "06";
                        returnMessage.ResponseMessage = "Thẻ đã tiếp nhận thành công";
                        break;
                    case CardRequestStatus.ProcessTimeout:
                        returnMessage.ResponseCode = "07";
                        returnMessage.ResponseMessage = "ProcessTimeout.";
                        break;
                    case CardRequestStatus.InvalidCardValue:
                        returnMessage.ResponseCode = "08";
                        returnMessage.ResponseMessage = "Thẻ sai mệnh giá";
                        break;
                    default:
                        returnMessage.ResponseMessage = "Lỗi thẻ không rõ trạng thái";
                        break;
                }
            }
            else
            {
                returnMessage.ResponseCode = "10";
                returnMessage.ResponseMessage = "Không tồn tại thông tin thẻ";
            }

            return returnMessage;
        }

        public async Task<bool> CardRequestUpdateStatusAsync(string transcode, CardRequestStatus status)
        {
            try
            {
                var cardRequest =
                    await _paygateMongoRepository.GetOneAsync<CardRequest>(p => p.TransCode == transcode);
                if (cardRequest == null)
                    return false;
                cardRequest.Status = status;
                await _paygateMongoRepository.UpdateOneAsync(cardRequest);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
