using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnilyContentExport.Models
{
    public class GraphQLDataItem
    {
        public long Id { get; set; }
        public string NodeName { get; set; }
        public string Path { get; set; }
        public JsonElement Properties { get; set; }
    }
}
