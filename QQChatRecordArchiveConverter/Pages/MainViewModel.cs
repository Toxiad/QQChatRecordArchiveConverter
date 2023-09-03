using HandyControl.Data;
using Microsoft.Win32;
using QQChatRecordArchiveConverter.CARC.Module;
using QQChatRecordArchiveConverter.CARC.Util;
using Stylet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace QQChatRecordArchiveConverter.Pages
{
    public class MainViewModel : Screen
    {
        public ObservableCollection<Message> Messages { get; set; } = new();
        public List<Message> TotalMessages { get; set; } = new(); 
        public DateTime SearchParamStartTime { get; set; } = DateTime.Today;
        public DateTime SearchParamEndTime { get; set; } = DateTime.Today.Add(new TimeSpan(23, 59, 59));
        public string SearchParamSender { get; set; } = string.Empty;
        public string SearchParamContent { get; set; } = string.Empty;
        public int TotalRecordCount { get; set; } = 0;
        public int PageIndex { get; set; } = 1; 
        public int PageCount { get; set; } = 1;
        public int PageRealSize { get; set; } = 0;
        public int PageSize { get; set; } = 200;
        public string AssestSize { get; set; } = "0 K"; 
        public string ObjectName { get; set; } = "NAME";
        public bool IsSearchable { get; set; } = true;
        public bool IsSearching { get { return !IsSearchable; } }
        public string CurAction { get; set; } = "";
        public Visibility LoadingBoxView { get; set; } = Visibility.Collapsed;

        public MainViewModel()
        {
            TotalRecordCount = SQLUtil.Instance.DB.Table<Message>().Count();
            ObjectName = SQLUtil.Instance.DB.Table<DBRecord>().First().DisplayName;
            Task.Factory.StartNew(() =>
            {
                AssestSize = Util.SizeParse(Util.GetDirectorySize("AssestOutput"));
            });
            AssestSize = "计算中";
        }
        public void Search()
        {
            //var query = from message in SQLUtil.Instance.DB.Table<Message>()
            //where message.Sender.Contains(SearchParamSender)
            //&& message.Content.Contains(SearchParamContent)
            //&& message.SendTime > SearchParamStartTime
            //&& message.SendTime < SearchParamEndTime
            //orderby message.SendTime
            //select message;
            Task.Factory.StartNew(() =>
            {
                IsSearchable = false;
                LoadingBoxView = Visibility.Visible;
                CurAction = $"获取数据...";
                TotalMessages = SQLUtil.Instance.DB.Table<Message>()
                    .Where(message => message.SendTime > SearchParamStartTime)
                    .Where(message => message.SendTime < SearchParamEndTime)
                    .Where(message => message.Sender.Contains(SearchParamSender))
                    .Where(message => message.Content.Contains(SearchParamContent))
                    .ToList();
                CurAction = $"装载数据...";
                var tempOC = new ObservableCollection<Message>(TotalMessages.Take(PageSize));
                CurAction = $"渲染...";
                PageIndex = 1;
                PageCount = (int)Math.Ceiling(TotalMessages.Count / (double)PageSize);
                Messages = tempOC;
                PageRealSize = Messages.Count();
                //Messages = new List<Message>(query);
                LoadingBoxView = Visibility.Collapsed;
                IsSearchable = true;
            }, TaskCreationOptions.LongRunning);
        }
        public void PageUpdated(FunctionEventArgs<int> info)
        {
            Task.Factory.StartNew(() =>
            { 
                IsSearchable = false;
                var tempOC = new ObservableCollection<Message>(TotalMessages.Skip((info.Info - 1) * PageSize).Take(PageSize));
                Messages = tempOC;
                PageRealSize = Messages.Count();
                IsSearchable = true;
            });
        }
        public bool IsImportable = true;
        MhtHelper mh = null;
        public void Import()
        {
            if (!IsImportable)
            {
                if (MessageBox.Show("是否要停止导入？") == MessageBoxResult.OK)
                {
                    mh.Stop();
                }
                return;
            }
            if (mh != null) return;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "导入QQ聊天记录";
            openFileDialog.Filter = "聊天记录文件 (*.mht)|*.mht";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == true)
            {
                var fn = openFileDialog.FileName;
                Task.Factory.StartNew(() => {
                    IsImportable = false;
                    LoadingBoxView = Visibility.Visible;
                    AssestSize = "计算中";
                    CurAction = $"正在解析图片...";
                    mh = new MhtHelper();
                    mh.DoConvert(fn, true);
                    mh = new MhtHelper();
                    mh.StatusChanged += (e) =>
                    {
                        if (e.desc == "percentRp")
                        {
                            CurAction = $"正在导入第{e.percent}行,{e.desc2:yyyy-MM-dd}";
                        }
                        if (e.desc == "totalsame" && e.percent > 0)
                        {
                            MessageBox.Show($"本次导入跳过{e.percent}个相同消息");
                        }
                    };
                    mh.DoConvert(fn, false);
                    LoadingBoxView = Visibility.Collapsed;
                    IsImportable = true;
                    mh = null;
                    TotalRecordCount = SQLUtil.Instance.DB.Table<Message>().Count();
                    ObjectName = SQLUtil.Instance.DB.Table<DBRecord>().First().DisplayName;
                    AssestSize = Util.SizeParse(Util.GetDirectorySize("AssestOutput"));
                });
            }
        }
        public void Export() 
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "导入QQ聊天记录";
            saveFileDialog.Filter = "聊天记录文件 (*.html)|*.html";
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == true)
            {
                var fn = saveFileDialog.FileName;
                Task.Factory.StartNew(() => {
                    string HtmlHeadString = @"<html xmlns=""http://www.w3.org/1999/xhtml"">"
                                                       + @"<head>"
                                                       + @"<meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />"
                                                       + @"<title>QQ Message</title>"
                                                       + @"<style type=""text/css"">"
                                                       + @"body{font-size:12px; line-height:22px; margin:2px;}td{font-size:12px; line-height:22px;}"
                                                       + @"</style>"
                                                       + @"</head>"
                                                       + @"<body>";
                    string HtmlEndString = @"</body></html>";
                    File.AppendAllText(fn, HtmlHeadString, Encoding.UTF8);
                    foreach (var m in TotalMessages) {
                        File.AppendAllText(fn, $"<div class='messages {m.SenderType}'><div class='infobox'><span class='sender'>{WebUtility.HtmlEncode(m.Sender)}</span><span class='sendtime'>{m.SendTime:yyyy-MM-dd HH-mm-ss}</span></div><span class='message-content'>{m.OriginMessage}</span></div>\n", Encoding.UTF8);
                    }
                    File.AppendAllText(fn, HtmlEndString, Encoding.UTF8);
                });
            }
        }
    }
}
