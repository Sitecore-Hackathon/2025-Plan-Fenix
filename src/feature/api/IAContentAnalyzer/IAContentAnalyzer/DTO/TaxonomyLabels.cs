using Newtonsoft.Json;

namespace IAContentAnalyzer.Models
{
    public class TaxonomyLabels
    {
        [JsonProperty("taxonomy_labels")]
        public List<string>? Labels { get; set; }
    }
}
