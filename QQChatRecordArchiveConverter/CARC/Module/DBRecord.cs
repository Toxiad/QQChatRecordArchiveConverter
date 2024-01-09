using SQLite;
using System;
using System.Windows;
using System.Windows.Resources;

namespace QQChatRecordArchiveConverter.CARC.Module
{
    [Table("DB_CONFIG_DB_TABLE_RECORDS")]
    public class DBRecord
    {
        public DBRecord() {
            Uri DefaultImg = new("Res\\otulogo@2x1231.png", UriKind.Relative);
            StreamResourceInfo info = Application.GetResourceStream(DefaultImg);
            GroupAvatar = "Res\\otulogo@2x1231.png";
        }
        [PrimaryKey]
        public int ID { get; set; } = 1;
        public string DisplayName { get; set; }
        public string TableName { get; set; } = null;
        public long TotalSize { get; set; } = 0;
        public DateTime CreateTime { get; set; } = DateTime.Now; 
        public DateTime UpdateTime { get; set; } = DateTime.Now;
        public string GroupAvatar { get; set; } 
    }
}
