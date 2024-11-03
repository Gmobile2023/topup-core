using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Common.Model.Dtos.CmsDto;
using GMB.Topup.Common.Model.Dtos.RequestDto;
using GMB.Topup.Shared;

namespace GMB.Topup.Common.Domain.Services;

public interface ICmsService
{
    Task<ResponsePagingMessage<List<CmsPostDto>>> GetPosts(GetPostsRequest request);
    Task<CmsPostDto> GetPost(GetPostRequest input);
    Task<List<CmsCategoryDto>> GetCategories(GetCategoriesRequest input);
}