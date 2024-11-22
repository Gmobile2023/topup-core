using System.Collections.Generic;
using System.Threading.Tasks;
using Topup.Common.Model.Dtos.CmsDto;
using Topup.Common.Model.Dtos.RequestDto;
using Topup.Shared;

namespace Topup.Common.Domain.Services;

public interface ICmsService
{
    Task<ResponsePagingMessage<List<CmsPostDto>>> GetPosts(GetPostsRequest request);
    Task<CmsPostDto> GetPost(GetPostRequest input);
    Task<List<CmsCategoryDto>> GetCategories(GetCategoriesRequest input);
}