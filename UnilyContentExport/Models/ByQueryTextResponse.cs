namespace UnilyContentExport.Models
{
    public class ByQueryTextResponse
    {
        public int TotalRows { get; set; }
        public List<GraphQLDataItem> Data { get; set; }
    }
}
