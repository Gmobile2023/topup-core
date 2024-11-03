using Newtonsoft.Json;

namespace GMB.Topup.Common.Model.Dtos.CmsDto;

public class CmsCategoryDto
{
    public int Id { get; set; }

    /// <summary>Number of published posts for the term.</summary>
    /// <remarks>
    ///     Read only
    ///     Context: view, edit
    /// </remarks>
    [JsonProperty("count")]
    public int Count { get; set; }

    /// <summary>HTML description of the term.</summary>
    /// <remarks>Context: view, edit</remarks>
    [JsonProperty("description")]
    public string Description { get; set; }

    /// <summary>The parent term ID.</summary>
    /// <remarks>Context: view, edit</remarks>
    [JsonProperty("parent", NullValueHandling = NullValueHandling.Ignore)]
    public int Parent { get; set; }

    /// <summary>HTML title for the term.</summary>
    /// <remarks>Context: view, embed, edit</remarks>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    ///     An alphanumeric identifier for the term unique to its type.
    /// </summary>
    /// <remarks>Context: view, embed, edit</remarks>
    [JsonProperty("slug")]
    public string Slug { get; set; }
}