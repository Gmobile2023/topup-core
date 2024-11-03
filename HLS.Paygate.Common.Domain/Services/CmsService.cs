using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos.CmsDto;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServiceStack;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace HLS.Paygate.Common.Domain.Services;

public class CmsService : ICmsService
{
    private readonly WordPressClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CmsService> _logger;

    public CmsService(ILogger<CmsService> logger, IConfiguration configuration)
    {
        _configuration = configuration;
        _client = new WordPressClient(_configuration["CmsConfig:Url"]);
        _logger = logger;
    }

    public virtual async Task<ResponsePagingMessage<List<CmsPostDto>>> GetPosts(GetPostsRequest input)
    {
        var page = 1;
        if (input.Offset > 0) page = input.Offset / input.Limit + 1;

        var query = new PostsQueryBuilder
        {
            Page = page,
            PerPage = input.Offset,
            Embed = true,
            //Offset = input.Offset,
            Order = Order.DESC,
            OrderBy = PostsOrderBy.Date,
            Search = input.Search,
            Categories = input.Categories
        };
        var responseHeader = new WebHeaderCollection();

        var client = new JsonServiceClient()
        {
            ResponseFilter = res => responseHeader = res.Headers
        };
        try
        {
            var rs = await client.GetAsync<string>($"{_client.WordPressUri}wp/v2/posts{query.BuildQuery()}");
            if (rs == null)
                return new ResponsePagingMessage<List<CmsPostDto>>
                {
                    Items = new List<CmsPostDto>(),
                    Total = 0,
                    TotalPage = 0
                };
            var headerTotal = responseHeader["X-WP-Total"];
            var headerTotalPage = responseHeader["X-WP-TotalPages"];

            var total = headerTotal != null ? int.Parse(headerTotal) : 0;
            var totalPage = headerTotalPage != null ? int.Parse(headerTotalPage) : 0;
            var data = JsonConvert.DeserializeObject<List<Post>>(rs);
            if (data == null || !data.Any())
                return new ResponsePagingMessage<List<CmsPostDto>>
                {
                    Items = new List<CmsPostDto>(),
                    Total = 0,
                    TotalPage = 0
                };
            var lst = data.Select(ConvertPost).ToList();
            return new ResponsePagingMessage<List<CmsPostDto>>
            {
                Items = lst,
                Total = total,
                TotalPage = totalPage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetPostQuery error: {ex}");
            return new ResponsePagingMessage<List<CmsPostDto>>
            {
                Items = new List<CmsPostDto>(),
                Total = 0,
                TotalPage = 0
            };
        }
    }

    public virtual async Task<CmsPostDto> GetPost(GetPostRequest input)
    {
        var item = await _client.Posts.GetByIDAsync(input.Id, true);
        return item == null ? null : ConvertPost(item);
    }

    public async Task<List<CmsCategoryDto>> GetCategories(GetCategoriesRequest input)
    {
        var item = await _client.Categories.GetAllAsync(true);
        return item?.ConvertTo<List<CmsCategoryDto>>();
    }

    private CmsPostDto ConvertPost(Post item)
    {
        var post = item.ConvertTo<CmsPostDto>();
        if (item.Embedded?.WpFeaturedmedia != null && item.Embedded.WpFeaturedmedia.Any())
        {
            var media = item.Embedded.WpFeaturedmedia.FirstOrDefault();
            if (media != null)
            {
                post.MediaDetails = media.MediaDetails;
                post.Image = media.SourceUrl;
            }
        }

        return post;
    }
}