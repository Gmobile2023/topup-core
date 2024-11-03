namespace Orleans.Providers.MongoDB.Configuration;

public sealed class MongoDBGrainStorageOptionsValidator : IConfigurationValidator
{
    private readonly string name;
    private readonly MongoDBGrainStorageOptions options;

    public MongoDBGrainStorageOptionsValidator(MongoDBGrainStorageOptions options, string name)
    {
        this.options = options;
        this.name = name;
    }

    public void ValidateConfiguration()
    {
        options.Validate(name);
    }
}