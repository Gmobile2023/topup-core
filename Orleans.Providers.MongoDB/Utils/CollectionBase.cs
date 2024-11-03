using System;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ArrangeAccessorOwnerBody

namespace Orleans.Providers.MongoDB.Utils;

public class CollectionBase<TEntity>
{
    private const string CollectionFormat = "{0}Set";

    protected static readonly UpdateOptions Upsert = new() {IsUpsert = true};
    protected static readonly SortDefinitionBuilder<TEntity> Sort = Builders<TEntity>.Sort;
    protected static readonly UpdateDefinitionBuilder<TEntity> Update = Builders<TEntity>.Update;
    protected static readonly FilterDefinitionBuilder<TEntity> Filter = Builders<TEntity>.Filter;
    protected static readonly IndexKeysDefinitionBuilder<TEntity> Index = Builders<TEntity>.IndexKeys;
    protected static readonly ProjectionDefinitionBuilder<TEntity> Project = Builders<TEntity>.Projection;
    private readonly bool createShardKey;
    private readonly Lazy<IMongoCollection<TEntity>> mongoCollection;

    private readonly IMongoDatabase mongoDatabase;

    protected CollectionBase(IMongoClient mongoClient, string databaseName, bool createShardKey)
    {
        this.Client = mongoClient;

        mongoDatabase = mongoClient.GetDatabase(databaseName);
        mongoCollection = CreateCollection();

        this.createShardKey = createShardKey;
    }

    protected IMongoCollection<TEntity> Collection
    {
        get { return mongoCollection.Value; }
    }

    protected IMongoDatabase Database
    {
        get { return mongoDatabase; }
    }

    public IMongoClient Client { get; }

    protected virtual MongoCollectionSettings CollectionSettings()
    {
        return new MongoCollectionSettings();
    }

    protected virtual string CollectionName()
    {
        return string.Format(CultureInfo.InvariantCulture, CollectionFormat, typeof(TEntity).Name);
    }

    protected virtual void SetupCollection(IMongoCollection<TEntity> collection)
    {
    }

    private Lazy<IMongoCollection<TEntity>> CreateCollection()
    {
        return new Lazy<IMongoCollection<TEntity>>(() =>
        {
            var databaseCollection = mongoDatabase.GetCollection<TEntity>(
                CollectionName(),
                CollectionSettings() ?? new MongoCollectionSettings());

            if (createShardKey)
                try
                {
                    Database.RunCommand<BsonDocument>(new BsonDocument
                    {
                        ["key"] = new BsonDocument
                        {
                            ["_id"] = "hashed"
                        },
                        ["shardCollection"] = $"{mongoDatabase.DatabaseNamespace.DatabaseName}.{CollectionName()}"
                    });
                }
                catch (MongoException)
                {
                    // Shared key probably created already.
                }

            SetupCollection(databaseCollection);

            return databaseCollection;
        });
    }
}