﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using SQLitePCL;

namespace QQChatRecordArchiveConverter.CARC.Module
{
    [Table("DB_CONFIG_DB_TABLE_RECORDS")]
    public class DBRecord
    {
        public DBRecord() { }
        [PrimaryKey]
        public int ID { get; set; } = 1;
        public string DisplayName { get; set; }
        public string TableName { get; set; } = null;
        public int TotalSize { get; set; } = 0;
        public DateTime CreateTime { get; set; } = DateTime.Now; 
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }
}
