namespace GMB.Topup.Shared;

public class PaggingBase
{
    public int Limit { get; set; } = 20;
    public int Offset { get; set; }
    public string Order { get; set; }
    public SearchType SearchType { get; set; } = SearchType.Search;
    public int RowMax { get; set; } = int.MaxValue;
    public OrderType OrderType { get; set; } = OrderType.Desc;
}

public enum SearchType
{
    Search = 1,
    Export = 2
}

public enum OrderType
{
    Asc = 1,
    Desc = 2
}