namespace Infrastructure.ElasticSeach;

public class ElasticSearchConfig
{
    public bool IsEnable { get; set; }
    public string Url { get; set; }
    public string Index { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}