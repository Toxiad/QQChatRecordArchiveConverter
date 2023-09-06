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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Shapes;

namespace QQChatRecordArchiveConverter.Pages
{
    public class MainViewModel : Screen
    {
        public ObservableCollection<Message> Messages { get; set; } = new();
        public List<Message> TotalMessages { get; set; } = new(); 
        public DateTime SearchParamStartTime { get; set; } = DateTime.Today.AddDays(-6);
        public DateTime SearchParamEndTime { get; set; } = DateTime.Today.Add(new TimeSpan(23, 59, 59));
        public string SearchParamSender { get; set; } = string.Empty;
        public string SearchParamContent { get; set; } = string.Empty;
        public ImageSource GroupAvatar { get; set; } = null;
        public int TotalRecordCount { get; set; } = 0;
        public int PageIndex { get; set; } = 1; 
        public int PageCount { get; set; } = 1;
        public int PageRealSize { get; set; } = 0;
        public int PageSize { get; set; } = 200;
        public string AssestSize { get; set; } = "0 B"; 
        public string ObjectName { get; set; } = "NAME";
        public bool IsSearchable { get; set; } = true;
        public bool IsSearching { get { return !IsSearchable; } }
        public string CurAction { get; set; } = "";
        public DateTime RealStartTime { get; set; } = DateTime.Today;
        public DateTime RealEndTime { get; set; } = DateTime.Today;
        public Visibility LoadingBoxView { get; set; } = Visibility.Collapsed;

        public MainViewModel()
        {
            var DBInfo = SQLUtil.Instance.DB.Table<DBRecord>().First();
            Uri DefaultFont = new ("Res\\Font\\HYRunYuan-75W.ttf", UriKind.Relative); 
            Uri DefaultImg = new("Res\\otulogo@2x1231.png", UriKind.Relative); 
            GroupAvatar = new BitmapImage(DefaultImg);

            TotalRecordCount = SQLUtil.Instance.DB.Table<Message>().Count();
            ObjectName = DBInfo.DisplayName;
            AssestSize = "计算中";
            Task.Factory.StartNew(() =>
            {
                AssestSize = Util.SizeParse(Util.GetDirectorySize("AssestOutput"));
            });
        }
        public void Search()
        {
            Task.Factory.StartNew(() =>
            {
                IsSearchable = false;
                //CurAction = $"获取数据...";
                TotalMessages = SQLUtil.Instance.DB.Table<Message>()
                    .Where(message => message.SendTime > SearchParamStartTime)
                    .Where(message => message.SendTime < SearchParamEndTime)
                    .Where(message => message.SenderStr.Contains(SearchParamSender))
                    .Where(message => message.Content.Contains(SearchParamContent))
                    .OrderBy(m=>m.SendTime).ToList();
                //CurAction = $"装载数据...";
                // 装载Pagesize条数据到渲染List
                var tempOC = new ObservableCollection<Message>(TotalMessages.Take(PageSize));
                if (TotalMessages.Count > 0)
                {
                    RealStartTime = TotalMessages.First().SendTime;
                    RealEndTime = TotalMessages.Last().SendTime;
                }
                PageIndex = 1; //重置页码计数器
                PageCount = (int)Math.Ceiling(TotalMessages.Count / (double)PageSize); //计算总页数
                //CurAction = $"渲染...";
                //将Message替换为新的数据列，渲染
                Messages = tempOC;
                //刷新页面当前条数
                PageRealSize = Messages.Count();
                IsSearchable = true;
            }, TaskCreationOptions.LongRunning);
        }
        public void PageUpdated(FunctionEventArgs<int> info)
        {
            Task.Factory.StartNew(() =>
            { 
                IsSearchable = false;
                // 从TotalMessage缓存中拉取渲染数据
                var tempOC = new ObservableCollection<Message>(TotalMessages.Skip((info.Info - 1) * PageSize).Take(PageSize));
                // 渲染
                Messages = tempOC;
                //刷新页面当前条数
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
                            MessageBox.Show($"本次导入跳过{e.percent}个相同消息，详细列表保存在 AssestOutput 目录下。");
                        }
                    };
                    mh.DoConvert(fn, false);
                    LoadingBoxView = Visibility.Collapsed;
                    IsImportable = true;
                    mh = null;
                    TotalRecordCount = SQLUtil.Instance.DB.Table<Message>().Count();
                    ObjectName = SQLUtil.Instance.DB.Table<DBRecord>().First().DisplayName;

                    var inf = SQLUtil.Instance.DB.Table<DBRecord>().First();
                    var totalSize = Util.GetDirectorySize("AssestOutput");
                    if (inf.DisplayName == null) inf.TotalSize = totalSize;
                    SQLUtil.Instance.DB.Update(inf);
                    AssestSize = Util.SizeParse(totalSize);
                });
            }
        }
        HTMLHelper hh = null;
        public void Export() 
        {
            hh = new HTMLHelper();
            hh.Export(TotalMessages, RealStartTime, RealEndTime, ObjectName);
        }
    }
}
