using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using SQLitePCL;

namespace QQChatRecordArchiveConverter.CARC.Module
{
    [Table("DB_CONTENT_DB_MESSAGE_CONTAINER")]
    public class Message
    {
        public Message() { }
        public Message(string content, string sender, DateTime sendTime) { 
            Content = content;
            Sender = sender;
            SendTime = sendTime;
        }
        [Indexed(Name = "CompositeKey", Order = 1, Unique = true)]
        public string? Content { get; set; }
        [Indexed(Name = "CompositeKey", Order = 2, Unique = true)]
        public string? Sender { get; set; }
        [Indexed(Name = "CompositeKey", Order = 3, Unique = true)]
        public DateTime SendTime { get; set; } = DateTime.Now;
    }
}
