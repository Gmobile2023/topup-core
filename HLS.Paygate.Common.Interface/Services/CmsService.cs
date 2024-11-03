using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos.CmsDto;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.Common.Interface.Services;

public partial class CommonService
{
    public async Task<object> GetAsync(GetCategoriesRequest request)
    {
        //_logger.LogInformation($"GetCategoriesRequest:{request.ToJson()}");
        var rs = await _cmsService.GetCategories(request);
        return new NewMessageReponseBase<List<CmsCategoryDto>>
        {
            Results = rs,
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
        };
    }

    public async Task<object> GetAsync(GetPostsRequest request)
    {
        //_logger.LogInformation($"GetPostsRequest:{request.ToJson()}");
        var rs = await _cmsService.GetPosts(request);
        return new NewMessageReponseBase<ResponsePagingMessage<List<CmsPostDto>>>
        {
            Results = rs,
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
        };
    }

    public async Task<object> GetAsync(GetPostRequest request)
    {
        _logger.LogInformation($"GetPostRequest:{request.ToJson()}");
        var rs = await _cmsService.GetPost(request);
        return new NewMessageReponseBase<CmsPostDto>
        {
            Results = rs,
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
        };
    }
}