namespace HLS.Paygate.Stock.Contracts.Dtos
{
    public class GrainsStockGrainDto
    {
        public string Id { get; set; }
        public int Version { get; set; }
        public string ETag { get; set; }
        public CardStockDto CardStock { get; set; }
    }
}