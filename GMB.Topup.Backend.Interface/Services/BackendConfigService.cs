using System.Threading.Tasks;
using GMB.Topup.Discovery.Requests.Backends;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.Backend.Interface.Services
{
    public partial class BackendService
    {
        public async Task<object> GetAsync(GetPartnerRequest request)
        {
            _logger.LogInformation("GetPartnerRequest request {Request}", request.ToJson());
            return new NewMessageResponseBase<PartnerConfigDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success"),
                Results = await _systemService.GetPartnerCache(request.PartnerCode)
            };
        }

        public async Task<object> PostAsync(CreateOrUpdatePartnerRequest request)
        {
            _logger.LogInformation("CreateOrUpdatePartnerRequest request {Request}", request.ToJson());
            var response = await _systemService.CreateOrUpdatePartnerAsync(request);
            _logger.LogInformation("CreateOrUpdatePartnerRequest return {Response}", response.ToJson());
            if (response)
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
                };
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }

        public async Task<object> PostAsync(CreatePartnerRequest request)
        {
            _logger.LogInformation("CreatePartnerRequest request {Request}", request.ToJson());
            var response = await _systemService.CreatePartnerAsync(request);
            _logger.LogInformation("CreatePartnerRequest return {Response}", response.ToJson());
            if (response)
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
                };
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }

        public async Task<object> PutAsync(UpdatePartnerRequest request)
        {
            _logger.LogInformation("UpdatePartnerRequest request {Request}", request.ToJson());
            var response = await _systemService.UpdatePartnerAsync(request);
            _logger.LogInformation("UpdatePartnerRequest return {Response}", response.ToJson());
            if (response)
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
                };
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }

        public async Task<object> PostAsync(CreateOrUpdateServiceRequest request)
        {
            _logger.LogInformation("CreateOrUpdateServiceRequest request {Request}", request.ToJson());
            var response = await _systemService.CreateOrUpdateServiceAsync(request);
            _logger.LogInformation("CreateOrUpdateServiceRequest return {Response}", response.ToJson());
            if (response)
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
                };
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        }
    }
}