using System.Collections.Generic;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Common.Model.Dtos.RequestDto;

[Route("/api/v1/common/cms/posts", "GET")]
public class GetPostsRequest : PaggingBase
{
    /// <summary>
    /// Current page of the collection.
    /// </summary>
    /// <remarks>Default: 1</remarks>
    //public int Page { get; set; }

    /// <summary>
    /// Maximum number of items to be returned in result set.
    /// </summary>
    /// <remarks>Default: 10</remarks>
    //public int PerPage { get; set; }

    /// <summary>
    ///     Limit results to those matching a string.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Offset the result set by a specific number of items.
    /// </summary>
    //public int Offset { get; set; }

    ///
    public List<int> Categories { get; set; }
}

[Route("/api/v1/common/cms/post", "GET")]
public class GetPostRequest
{
    public int Id { get; set; }
}

[Route("/api/v1/common/cms/categories", "GET")]
public class GetCategoriesRequest
{
}