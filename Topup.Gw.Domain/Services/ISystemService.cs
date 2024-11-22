using System.Collections.Generic;
using System.Threading.Tasks;
using Topup.Gw.Model.Dtos;
using Topup.Discovery.Requests.Backends;

namespace Topup.Gw.Domain.Services;

public interface ISystemService
{
    Task<bool> CreatePartnerAsync(CreatePartnerRequest item);
    Task<bool> CreateOrUpdatePartnerAsync(CreateOrUpdatePartnerRequest item);
    Task<bool> UpdatePartnerAsync(UpdatePartnerRequest item);
    Task<PartnerConfigDto> GetPartnerCache(string partnerCode);

    Task<bool> CreateOrUpdateServiceAsync(CreateOrUpdateServiceRequest item);
    Task<ServiceConfigDto> GetServiceCache(string serviceCode);
    Task<List<PartnerConfigDto>> GetListPartner();
    Task<List<PartnerConfigDto>> GetListPartnerCheckLastTrans();
}