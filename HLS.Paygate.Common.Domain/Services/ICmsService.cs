using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos.CmsDto;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Common.Domain.Services;

public interface ICmsService
{
    Task<ResponsePagingMessage<List<CmsPostDto>>> GetPosts(GetPostsRequest request);
    Task<CmsPostDto> GetPost(GetPostRequest input);
    Task<List<CmsCategoryDto>> GetCategories(GetCategoriesRequest input);
}