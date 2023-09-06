using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SQLite;
using SQLitePCL;
using Windows.Devices.Sensors;
using Windows.Media.Ocr;

namespace QQChatRecordArchiveConverter.CARC.Module
{
    public enum MessageType
    {
        Text,
        Complex,
        Unknow = 9
    }
    public enum MessageSenderType
    {
        Normal,
        System = 9
    }
    [Table("DB_CONTENT_DB_MESSAGE_CONTAINER")]
    public class Message
    {
        public Message() { }
        public Message(string content, string sender, DateTime sendTime, string origin, string group, string avatar) 
        {
            var idMatch = Regex.Match(sender, "(\\((?<id>\\d+)\\)|<(?<id>.*?)>)$");
            Content = content;
            SendTime = sendTime;
            OriginMessage = origin;
            SendTimeMinute = new DateTime(sendTime.Year, sendTime.Month, sendTime.Day, sendTime.Hour, sendTime.Minute, 0);
            Group = group;
            if (string.IsNullOrEmpty(content))
            {
                Content += "[消息类型不支持导出，该记录无任何数据]";
                MessageType = MessageType.Unknow;
            }
            else
            {
                MessageType = content.Contains("<img src=") ? MessageType.Complex : MessageType.Text;
            }
            SenderStr = sender;
            SenderName = sender.Replace(idMatch.Groups[0].Value, "");
            SenderId = idMatch.Groups["id"].Value;
            SenderAvatar = avatar;
            SenderType = sender.Contains("系统消息(10000") ? MessageSenderType.System : MessageSenderType.Normal;
        }
        public MessageType MessageType { get; set; }
        public string Content { get; set; }
        public string Group { get; set; }
        public DateTime SendTime { get; set; } = DateTime.Now;
        [Indexed(Name = "CompositeKey", Order = 1, Unique = true)]
        public string OriginMessage { get; set; }
        [Indexed(Name = "CompositeKey", Order = 2, Unique = true)]
        public string SenderId { get; set; }
        [Indexed(Name = "CompositeKey", Order = 3, Unique = true)]
        public DateTime SendTimeMinute { get; set; } = DateTime.Now;
        public MessageSenderType SenderType { get; set; }
        public string SenderName { get; set; }
        public string SenderStr { get; set; }
        public string SenderAvatar { get; set; }
    }
}
