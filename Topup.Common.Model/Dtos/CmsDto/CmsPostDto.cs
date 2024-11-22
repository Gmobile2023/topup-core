using System;
using Newtonsoft.Json;
using WordPressPCL.Models;

namespace Topup.Common.Model.Dtos.CmsDto;

public class CmsPostDto
{
    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("date", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public DateTime Date { get; set; }

    /// <summary>The date the object was published, as GMT.</summary>
    /// <remarks>Context: view, edit</remarks>
    [JsonProperty("date_gmt", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public DateTime DateGmt { get; set; }

    /// <summary>
    ///     The date the object was last modified, in the site's timezone.
    /// </summary>
    /// <remarks>
    ///     Read only
    ///     Context: view, edit
    /// </remarks>
    [JsonProperty("modified", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public DateTime Modified { get; set; }

    /// <summary>The date the object was last modified, as GMT.</summary>
    /// <remarks>
    ///     Read only
    ///     Context: view, edit
    /// </remarks>
    [JsonProperty("modified_gmt", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public DateTime ModifiedGmt { get; set; }

    /// <summary>
    ///     An alphanumeric identifier for the object unique to its type.
    /// </summary>
    /// <remarks>Context: view, edit, embed</remarks>
    [JsonProperty("slug")]
    public string Slug { get; set; }

    /// <summary>A named status for the object.</summary>
    /// <remarks>
    ///     Context: edit
    ///     One of: publish, future, draft, pending, private
    /// </remarks>
    [JsonProperty("status")]
    public Status Status { get; set; }

    /// <summary>Type of Post for the object.</summary>
    /// <remarks>
    ///     Read only
    ///     Context: view, edit, embed
    /// </remarks>
    [JsonProperty("type")]
    public string Type { get; set; }

    /// <summary>The title for the object.</summary>
    /// <remarks>Context: view, edit, embed</remarks>
    [JsonProperty("title")]
    public Title Title { get; set; }

    /// <summary>URL to the object.</summary>
    /// <remarks>
    ///     Read only
    ///     Context: view, edit, embed
    /// </remarks>
    [JsonProperty("link")]
    public string Link { get; set; }

    /// <summary>The content for the object.</summary>
    /// <remarks>Context: view, edit</remarks>
    [JsonProperty("content")]
    public Content Content { get; set; }

    /// <summary>The format for the object.</summary>
    /// <remarks>
    ///     Context: view, edit
    ///     One of: standard
    /// </remarks>
    [JsonProperty("format")]
    public string Format { get; set; }

    /// <summary>
    ///     The terms assigned to the object in the category taxonomy.
    /// </summary>
    /// <remarks>Context: view, edit</remarks>
    [JsonProperty("categories", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int[] Categories { get; set; }

    /// <summary>
    ///     The terms assigned to the object in the post_tag taxonomy.
    /// </summary>
    /// <remarks>Context: view, edit</remarks>
    [JsonProperty("tags", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int[] Tags { get; set; }

    [JsonProperty("media_details")] public MediaDetails MediaDetails { get; set; }

    public string Image { get; set; }

    [JsonProperty("excerpt")] public Excerpt Excerpt { get; set; }
}