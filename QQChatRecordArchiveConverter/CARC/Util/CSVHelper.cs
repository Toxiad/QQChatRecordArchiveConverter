using Microsoft.Win32;
using QQChatRecordArchiveConverter.CARC.Module;
using Stylet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace QQChatRecordArchiveConverter.CARC.Util
{
    public static class CSVStringHelper
    {
        public static string CSVSafety(this string input)
        {
            //return input;
            return input.Replace("\r\n", ";;;;").Replace("\n", ";;").Replace("\r", ";;").Replace(',', ';');
        }
    }
    public class CSVHelper
    {
        public class ProcessStatus
        {
            double percent { get; set; }
            string desc { get; set; }
            bool isException { get; set; } = false;
            Exception exception { get; set; }
        }
        public delegate void ProcessStatusHandler(ProcessStatus status);
        public event ProcessStatusHandler StatusChanged;


        public string HtmlEndString = string.Empty;

        public string OutputPath = "/AssestOutput/";



        public void Export(string fn, List<Message> TotalMessages)
        {
            Task.Factory.StartNew(() => {
                string HtmlHeadString = $"Sender,SenderType,SenderId,SenderName,SendTime,MessageType,Content,OriginMessage\n";

                string HtmlEndString = string.Empty;
                File.AppendAllText(fn, HtmlHeadString, Encoding.UTF8);
                foreach (var m in TotalMessages)
                {
                    File.AppendAllText(fn, $"{m.SenderStr.CSVSafety()},{m.SenderType},{m.SenderId.CSVSafety()},{m.SenderName.CSVSafety()},{m.SendTime:yyyy-MM-dd HH:mm:ss},{m.MessageType},{m.Content.CSVSafety()},{m.OriginMessage.CSVSafety()}\n", Encoding.UTF8);
                }
                File.AppendAllText(fn, HtmlEndString, Encoding.UTF8);
                Execute.OnUIThread(() => {
                    MessageBox.Show("导出完成，点击确定打开文件", "记录导出");
                });
            });
        }
    }
}
