using Newtonsoft.Json;
using System;

namespace SLiTS.Api
{
    public class QuickTaskRequest
    {
        public virtual string Query { get; set; }
        public string Title { get; set; }
        [JsonIgnore]
        public string Id { get; set; }
    }
}
