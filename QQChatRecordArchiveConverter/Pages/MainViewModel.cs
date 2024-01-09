
using HandyControl.Data;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using QQChatRecordArchiveConverter.CARC.Module;
using QQChatRecordArchiveConverter.CARC.Util;
using Stylet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                AssestSize = Util.SizeParse(Util.GetDirectorySize("AssetsOutput"));
            });
        }
        public void Close(object s, System.ComponentModel.CancelEventArgs e)
        {
            if (!IsFixable || !IsImportable)
            {
                var Ack = MessageBox.Show("正在进行数据修复、导入或搜索操作，中断该操作可能会影响数据记录，是否确定要退出？\n【数据修复】过程中断将导致未完成添加的数据消失\n【导入】过程中断后，未导入的数据不会被导入，并会在下次导入该文件时计入记录冲突表", "中断数据处理操作", MessageBoxButton.OKCancel);
                if (Ack != MessageBoxResult.OK)
                {
                    e.Cancel = true;
                }
            }
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
        public bool IsFixable = true;  
        MHT? mh = null;
        public void Import()
        {
            try
            {
                if (!IsImportable)
                {
                    if (MessageBox.Show("是否要停止导入？", "停止导入", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        mh.Stop();
                    }
                    return;
                }
                if (!IsFixable)
                {
                    MessageBox.Show("当前正在修复数据，为防止冲突，暂时无法操作。");
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
                    var inf = SQLUtil.Instance.DB.Table<DBRecord>().First();
                    IsImportable = false;
                    LoadingBoxView = Visibility.Visible;
                    AssestSize = "计算中";
                    mh = new MHT();
                    mh.StatusChanged += (e) =>
                    {
                        if (e.type == MHT.ProcessStatusType.MessageBox)
                        {
                            Execute.OnUIThreadSync(() => {
                                MessageBox.Show(e.desc);
                            });
                        }
                        if (e.type == MHT.ProcessStatusType.Progress)
                        {
                            Execute.OnUIThreadSync(() => {
                                CurAction = e.desc;
                            });
                        }
                    };
                    var nameImport = mh.GetChatName(fn);
                    if (nameImport != inf.DisplayName)
                    {
                        var conflict = MessageBox.Show($"需要导入的记录消息对象与数据库不一致，是否继续？\n当前导入：'{nameImport}'；当前数据库：'{inf.DisplayName}'", "名称冲突", MessageBoxButton.YesNo);
                        if (conflict != MessageBoxResult.Yes)
                        {
                            mh.GetChatName(fn);
                            mh.Stop();
                        }
                    }
                    mh.StartAsync(fn).ContinueWith(t => {
                        Execute.OnUIThreadSync(() => {
                            LoadingBoxView = Visibility.Collapsed;
                            IsImportable = true;
                        });
                        mh.Stop();
                        mh = null;
                        var totalSize = Util.GetDirectorySize("AssetsOutput");
                        inf.TotalSize = totalSize;
                        SQLUtil.Instance.DB.Update(inf);
                        AssestSize = Util.SizeParse(totalSize);
                    });
                }
            }
            catch (Exception exception)
            {
                mh.Stop();
                mh = null;
                LoadingBoxView = Visibility.Collapsed;
                IsImportable = true;
                File.AppendAllText($".\\ErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", exception.ToString());
                MessageBox.Show("Save As ErrorList File", "Alarm Occurred");
            }
        }
        public void Export() 
        {
            SaveFileDialog saveFileDialog = new()
            {
                Title = "导出QQ聊天记录",
                Filter = "超文本标记语言文档 (*.html)|*.html|逗号分隔符文件 (*.csv)|*.csv"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                // 1-Html; 2-CSV;
                var selEx = saveFileDialog.FilterIndex;
                var fn = saveFileDialog.FileName;
                if (selEx == 1)
                {
                    new HTMLHelper().Export(fn, TotalMessages, RealStartTime, RealEndTime, ObjectName);
                }
                else if(selEx == 2)
                {
                    new CSVHelper().Export(fn, TotalMessages);
                }
                else
                {
                    MessageBox.Show("File Filter Error", "Alarm Occurred");
                }
            }
        }
        public void Backup()
        {
            var name = $"./Data/MainDB_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.fff}.db";
            SQLUtil.Instance.DB.Backup(name);
            MessageBox.Show($"已备份到{name}");
        }
        public void DuplicateFix()
        {
            if (!IsImportable)
            {
                MessageBox.Show("当前正在导入数据，为防止冲突，请先暂停导入。");
                return;
            }
            if (!IsFixable)
            {
                MessageBox.Show("当前正在修复数据，为防止冲突，暂时无法操作。");
                return;
            }
            var alarm = MessageBox.Show("修复记录问题将会删除全部记录并重新导入，确认后将会对数据库进行备份，并开始检查数据库。过程中退出程序会造成数据丢失，可用备份数据库还原。", "DuplicateFix", MessageBoxButton.OKCancel);
            if (alarm == MessageBoxResult.OK)
            {
                IsFixable = false;
                int count = 0;
                LoadingBoxView = Visibility.Visible;
                mh = new MHT();
                mh.StatusChanged += (e) =>
                {
                    if (e.type == MHT.ProcessStatusType.MessageBox)
                    {
                        Execute.OnUIThreadSync(() => {
                            MessageBox.Show(e.desc);
                        });
                    }
                    if (e.type == MHT.ProcessStatusType.Progress)
                    {
                        Execute.OnUIThreadSync(() => {
                            CurAction = e.desc;
                        });
                    }
                };
                var name = $"./Data/MainDB_Backup_DuplicateFix_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.fff}.db";
                SQLUtil.Instance.DB.Backup(name);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        mh.FixDataRecordError(false);
                        IsFixable = true;
                        mh = null;
                        Execute.OnUIThreadSync(() => {
                            LoadingBoxView = Visibility.Collapsed;
                        });
                    }
                    catch (Exception exception)
                    {
                        mh = null;
                        File.AppendAllText($".\\ErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", exception.ToString());
                        MessageBox.Show("Save As ErrorList File", "Alarm Occurred");
                    }
                });
            }
        }
        public void Vacuum()
        {
            if (!IsImportable) 
            {
                MessageBox.Show("当前正在导入数据，为防止冲突，请先暂停导入。");
                return;
            }
            if (!IsFixable)
            {
                MessageBox.Show("当前正在修复数据，为防止冲突，暂时无法操作。");
                return;
            }
            var alarm = MessageBox.Show("执行数据库整理动作确认。", "VACUUM", MessageBoxButton.OKCancel);
            if (alarm == MessageBoxResult.OK)
            {
                IsFixable = false;
                LoadingBoxView = Visibility.Visible;
                mh = new MHT();
                mh.StatusChanged += (e) =>
                {
                    if (e.type == MHT.ProcessStatusType.MessageBox)
                    {
                        Execute.OnUIThreadSync(() => {
                            MessageBox.Show(e.desc);
                        });
                    }
                    if (e.type == MHT.ProcessStatusType.Progress)
                    {
                        Execute.OnUIThreadSync(() => {
                            CurAction = e.desc;
                        });
                    }
                };
                var name = $"./Data/MainDB_Backup_Vacuum_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.fff}.db";
                SQLUtil.Instance.DB.Backup(name);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        mh.FixDataRecordError(true);
                        IsFixable = true;
                        mh = null;
                        Execute.OnUIThreadSync(() => {
                            LoadingBoxView = Visibility.Collapsed;
                        });
                    }
                    catch (Exception exception)
                    {
                        mh = null;
                        File.AppendAllText($".\\ErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", exception.ToString());
                        MessageBox.Show("Save As ErrorList File", "Alarm Occurred");
                    }
                });
            }
        }
    }
}
