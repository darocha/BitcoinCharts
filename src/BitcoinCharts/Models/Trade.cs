using BitcoinCharts.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCharts.Models {
    public class Trade {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("timestamp")]
        [JsonConverter(typeof(EpochDateTimeOffsetConverter))]
        public DateTimeOffset Datetime { get; set; }
        
        [JsonProperty("price")]
        public double Price { get; set; }
        
        [JsonProperty("volume")]
        public double Quantity { get; set; }

        public override string ToString() {
            return (new { Symbol, Datetime, Price, Quantity }).ToString();
        }
    }
}
