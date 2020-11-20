using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data;

namespace SLiTS.Api
{
    public class DataResponse : FastTaskResponse
    {
        public List<ColumnInfo> Columns { get; set; }
        public List<dynamic> Records { get; set; }
        public override string Metadata
        {
            get => JsonConvert.SerializeObject(Columns);
            set => Columns = JsonConvert.DeserializeObject<List<ColumnInfo>>(value);
        }
        public override string Data
        {
            get => JsonConvert.SerializeObject(Records);
            set => Records = JsonConvert.DeserializeObject<List<dynamic>>(value);
        }
        public class ColumnInfo
        {
            public string PropertyName { get; set; }
            public string ColumnName { get; set; }
            public SqlDbType DataType { get; set; }
            public bool CanPrint { get; set; }
            public bool CanShow { get; set; }
        }
    }
}
