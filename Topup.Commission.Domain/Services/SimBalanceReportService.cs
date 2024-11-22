using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NLog;
using ServiceStack;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Domain.Repositories;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Helpers;

namespace HLS.Paygate.Report.Domain.Services
{
    public class SimBalanceReportService : ISimBalanceReportService
    {
        private readonly Logger _logger = LogManager.GetLogger("SimBalanceReportService");
        private readonly IReportMongoRepository _reportMongoRepository;
        private readonly ISimReportMongoRepository _simReportMongoRepository;
        private readonly IDateTimeHelper _dateTimeHelper;

        public SimBalanceReportService(IReportMongoRepository reportMongoRepository,
            ISimReportMongoRepository simReportMongoRepository, IDateTimeHelper dateTimeHelper)
        {
            _reportMongoRepository = reportMongoRepository;
            _simReportMongoRepository = simReportMongoRepository;
            _dateTimeHelper = dateTimeHelper;
        }

        public async Task<MessageResponseBase> SimBalanceReportInsertAsync(SimBalanceMessage request)
        {
            try
            {
                _logger.LogInformation($"SimBalanceReportInsertAsync request: {request.ToJson()}");
                var item = request.ConvertTo<SimBalanceHistories>();
                item.CreatedDate = DateTime.Now;
                await _reportMongoRepository.AddOneAsync(item);
                return new MessageResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Success"
                };
            }
            catch (Exception e)
            {
                _logger.LogInformation($"SimBalanceReportInsertAsync error: {e}");
                return new MessageResponseBase
                {
                    ResponseCode = "00",
                    ResponseMessage = "Error"
                };
            }
        }

        public async Task<MessageResponseBase> CreateOrUpdateReportSimBalanceDate(SimBalanceMessage request)
        {
            try
            {
                var date = request.CreatedDate.Date;
                var exist = await _simReportMongoRepository.GetSimBalanceByDate(request.SimNumber, date);

                if (exist != null)
                {
                    exist.BalanceAfter = request.BalanceAfter;
                    exist.Increase += request.Increase;
                    exist.Decrease += request.Decrease;
                    exist.ModifiedDate = DateTime.Now;
                    await _reportMongoRepository.UpdateOneAsync(exist);
                }
                else
                {
                    var create = request.ConvertTo<SimBalanceByDate>();
                    create.ShortDate = request.CreatedDate.ToShortDateString();
                    await _reportMongoRepository.AddOneAsync(create);
                }

                return new MessageResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Success"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"CreateOrUpdateReportSimBalanceDate Exception: {ex}");
                return new MessageResponseBase
                {
                    ResponseCode = "00",
                    ResponseMessage = "Error"
                };
            }
        }

        public async Task<MessagePagedResponseBase> SimBalanceHistories(SimBalanceHistoriesRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<SimBalanceHistories, bool>> query = p => true;
                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<SimBalanceHistories, bool>> newQuery = p => p.TransCode == request.TransCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.SimNumber))
                {
                    Expression<Func<SimBalanceHistories, bool>> newQuery = p => p.SimNumber == request.SimNumber;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.TransRef))
                {
                    Expression<Func<SimBalanceHistories, bool>> newQuery = p =>
                        p.TransRef == request.TransRef;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.Serial))
                {
                    Expression<Func<SimBalanceHistories, bool>> newQuery = p =>
                        p.Serial == request.Serial;
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<SimBalanceHistories, bool>> newQuery = p =>
                        p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<SimBalanceHistories, bool>> newQuery = p =>
                        p.CreatedDate <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync<SimBalanceHistories>(query);

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<SimBalanceHistories, Guid>(query,
                    s => s.CreatedDate, false,
                    request.Offset, request.Limit);

                foreach (var item in lst)
                {
                    item.CreatedDate = _dateTimeHelper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int) total,
                    Payload = lst.ConvertTo<List<SimBalanceHistoriesDto>>()
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"SimBalanceHistories error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public async Task<MessagePagedResponseBase> SimBalanceDate(SimBalanceDateRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<SimBalanceByDate, bool>> query = p => true;

                if (!string.IsNullOrEmpty(request.SimNumber))
                {
                    Expression<Func<SimBalanceByDate, bool>> newQuery = p =>
                        p.SimNumber == request.SimNumber;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<SimBalanceByDate, bool>> newQuery = p =>
                        p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<SimBalanceByDate, bool>> newQuery = p =>
                        p.CreatedDate <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var total = await _reportMongoRepository.CountAsync(query);

                var lst = await _reportMongoRepository.GetSortedPaginatedAsync<SimBalanceByDate, Guid>(query,
                    s => s.CreatedDate, true,
                    request.Offset, request.Limit);
                foreach (var item in lst)
                {
                    item.CreatedDate = _dateTimeHelper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int) total,
                    Payload = lst.OrderBy(x => x.CreatedDate).ThenBy(x => x.SimNumber)
                        .ConvertTo<List<SimBalanceByDateDto>>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"SimBalanceDate error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        public Task SysSimBalanceDay()
        {
            throw new System.NotImplementedException();
        }
    }
}
