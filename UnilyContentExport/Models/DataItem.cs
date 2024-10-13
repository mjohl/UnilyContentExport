using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnilyContentExport.Models
{
    public class DataItem
    {
        public long Id { get; set; }
        public string NodeName { get; set; } = string.Empty;
        public string[] Path { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}
