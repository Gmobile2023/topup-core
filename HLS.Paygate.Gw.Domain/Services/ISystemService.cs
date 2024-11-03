using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Dtos;
using Paygate.Discovery.Requests.Backends;

namespace HLS.Paygate.Gw.Domain.Services;

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