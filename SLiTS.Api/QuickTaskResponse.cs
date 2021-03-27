using Newtonsoft.Json;

namespace SLiTS.Api
{
    public class QuickTaskResponse
    {
        [JsonIgnore]
        public string Id { get; set; }
        public virtual string Metadata { get; set; }
        public virtual string Data { get; set; }
    }
}
