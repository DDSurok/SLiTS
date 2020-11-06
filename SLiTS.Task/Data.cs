using System.Collections.Generic;
using System.Data;

namespace SLiTS.Api
{
    public class Data
    {
        public List<ColumnInfo> Columns { get; set; }
        public List<dynamic> Records { get; set; }

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
