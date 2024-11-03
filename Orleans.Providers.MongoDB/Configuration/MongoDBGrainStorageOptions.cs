// ReSharper disable InheritdocConsiderUsage

using System;
using Newtonsoft.Json;

namespace Orleans.Providers.MongoDB.Configuration;

/// <summary>
///     Option to configure MongoDB Storage.
/// </summary>
public class MongoDBGrainStorageOptions : MongoDBOptions
{
    public MongoDBGrainStorageOptions()
    {
        CollectionPrefix = "Grains";
    }

    public Action<JsonSerializerSettings> ConfigureJsonSerializerSettings { get; set; }
}