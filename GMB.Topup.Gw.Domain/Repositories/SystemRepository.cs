using System;
using System.Collections.Generic;
using ServiceStack;
using System.Threading.Tasks;
using NLog;
using ServiceStack.OrmLite;
using HLS.Paygate.Gw.Model.Dtos;
using System.Linq;
using HLS.Paygate.Gw.Domain.Entities;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Domain.Repositories
{
    public class SystemRepository : ISystemRepository
    {
        private readonly IPaygateConnectionFactory _paygateConnectionFactory;
        private readonly Logger _logger = LogManager.GetLogger("ServiceRepository");

        public SystemRepository(IPaygateConnectionFactory paygateConnectionFactory)
        {
            _paygateConnectionFactory = paygateConnectionFactory;
        }

        // public async Task<List<ServiceConfigDto>> GetServiceConfig(int serviceId, int categoryId, int productId)
        // {
        //     try
        //     {
        //         using (var db = _paygateConnectionFactory.Open())
        //         {
        //             var query = db.From<ServiceConfigs>().Where(p => p.ServiceId == serviceId);
        //             if (categoryId > 0)
        //                 query = query.Where(x => x.CategorysId == categoryId);
        //             if (productId > 0)
        //                 query = query.Where(x => x.CategorysId == productId);
        //
        //             var item = await db.SelectAsync(query);
        //             if (item == null || !item.Any())
        //                 return null;
        //             return item.ConvertTo<List<ServiceConfigDto>>().OrderBy(x => x.Priority).ToList();
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         return null;
        //     }
        // }
        //
        // public async Task<List<ServiceConfigDto>> GetServiceConfig(string serviceCode, string categoryCode)
        // {
        //     try
        //     {
        //         using (var db = _paygateConnectionFactory.Open())
        //         {
        //             var query = db.From<ServiceConfigs>().Join<Entities.Services>((p, s) => p.ServiceId == s.Id)
        //             .Where<Entities.Services>(x => x.ServiceCode == serviceCode)
        //              .Where<Entities.ServiceConfigs>(x => x.IsOpened)
        //             .Join<Entities.Suppliers>((p, s) => p.SupplierId == s.Id);
        //             if (!string.IsNullOrEmpty(categoryCode))
        //                 query = query.Join<Entities.Categoryses>((p, s) => p.CategorysId == s.Id).Where<Entities.Services>(x => x.ServiceCode == serviceCode);
        //             return await db.SelectAsync<ServiceConfigDto>(query);
        //             //if (item == null || !item.Any())
        //             //return null;
        //             //return item.ConvertTo<List<ServiceConfigDto>>().OrderBy(x => x.Priority).ToList();
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         return null;
        //     }
        // }
        //
        // public async Task<Partners> GetPartnerByCode(string code, PartnerStatus status)
        // {
        //     try
        //     {
        //         using (var db = _paygateConnectionFactory.Open())
        //         {
        //             return await db.SingleAsync<Partners>(x => x.PartnerCode == code && x.Status == status);
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         return null;
        //     }
        //
        // }
        //
        // public async Task<Categoryses> GetCategoryByCode(string code, bool checkStatus = false)
        // {
        //     using (var db = _paygateConnectionFactory.Open())
        //     {
        //         var query = db.From<Categoryses>().Where(x => x.CategoryCode == code);
        //         if (checkStatus)
        //             query = query.Where(x => x.Status == CategoryStatus.Active);
        //
        //         return await db.SingleAsync(query);
        //     }
        // }
        //
        // public async Task<Games> GetGame(string code, bool checkStatus)
        // {
        //     try
        //     {
        //         using (var db = _paygateConnectionFactory.Open())
        //         {
        //             var query = db.From<Games>().Where(x => x.GameCode == code);
        //             if (checkStatus)
        //                 query = query.Where(x => x.Status == GameStatus.Active);
        //             return await db.SingleAsync(query);
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         return null;
        //     }
        // }
        public async Task<Providers> GetProvider(string code, bool checkStatus)
        {
            try
            {
                using (var db = _paygateConnectionFactory.Open())
                {
                    var query = db.From<Providers>().Where(x => x.Code == code);
                    if (checkStatus)
                        query = query.Where(x => x.Status == ProviderStatus.Active);
                    return await db.SingleAsync(query);
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        // public async Task<GameDto> GetGameProvider(string gamecode, string providercode, bool checkStatus)
        // {
        //     try
        //     {
        //         using (var db = _paygateConnectionFactory.Open())
        //         {
        //             var query = db.From<Games>().Join<Entities.Providers>((p, s) => p.ProviderId == s.Id)
        //             .Where<Entities.Games>(x => x.GameCode == gamecode)
        //             .Where<Entities.Providers>(x => x.Code == providercode);
        //             if (checkStatus)
        //             {
        //                 query = query.Where<Entities.Games>(x => x.Status == GameStatus.Active).Where<Entities.Providers>(x => x.Status == ProviderStatus.Active);
        //             }
        //             return await db.SingleAsync<GameDto>(query);
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         return null;
        //     }
        // }
    }
}