

using Microsoft.VisualBasic;
using QQChatRecordArchiveConverter.CARC.Module;
using Stylet;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Windows.Gaming.Preview.GamesEnumeration;
using Windows.Media.Ocr;

namespace QQChatRecordArchiveConverter.CARC.Util
{
    public class MHT
    {
        public enum ProcessStatusType
        {
            MessageBox,
            Progress
        }
        enum ExportAssetsStatus
        {
            Standby,
            Start,
            End
        }
        public class ProcessStatus
        {
            public ProcessStatusType type { get; set; }
            public string desc { get; set; }
        }
        public delegate void ProcessStatusHandler(ProcessStatus status);
        public event ProcessStatusHandler StatusChanged;
        private bool isStop = false;
        private Dictionary<string, string> AssetsMapping = new Dictionary<string, string>();
        public string OutputPathName { get; set; } = "AssetsOutput";
        public string ImgDirName { get; set; } = "Sources";
        public void Stop()
        {
            isStop = true;
        }
        public string GetChatName(string QQMHTPath)
        {
            using FileStream fs = new(QQMHTPath, FileMode.Open, FileAccess.Read);
            using StreamReader sr = new(fs);
            var line = sr.ReadLine()?.TrimEnd();
            var name = "";
            if (line != null && line.Contains("<title>QQ Message</title>"))
            {
                name = Regex.Match(line, "<tr><td><div.*?>消息对象:(?<objName>.*?)</div>").Groups["objName"].Value;
            }
            return name;
        }
        public Task StartAsync(string QQMHTPath)
        {
            return Task.Factory.StartNew(() => {
                try
                {
                    isStop = false;
                    ExportAssets(QQMHTPath);
                    LoadMessages(QQMHTPath);
                } 
                catch (Exception exception)
                {
                    File.AppendAllText($".\\ErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", exception.ToString());
                    MessageBox.Show("Save As ErrorList File", "Alarm Occurred");
                }
                finally { isStop = true; }
            });
        }
        public void FixDataRecordError(bool isVacOnly)
        {
            int delcnt = 0; 
            int inscnt = 0;
            if (!isVacOnly)
            {
                StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"开始导出记录" });
                var a = SQLUtil.Instance.DB.Table<Message>().ToArray();
                int cc = a.Length;
                StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"删除数据 {cc}" });
                SQLUtil.Instance.DB.DropTable<Message>();
                delcnt += a.Length;
                string ErrorOutputPath = Path.Combine(OutputPathName, $"MessageRecordFixErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt");
                StringBuilder sb = new();
                SQLUtil.Instance.DB.CreateTable<Message>();
                foreach (var m in a)
                {
                    var isImgErr = Regex.IsMatch(m.Content, "<img src=\"Sources/[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
                    if (!isImgErr)
                    {
                        try
                        {
                            inscnt += SQLUtil.Instance.DB.Insert(m);
                        }
                        catch (Exception exception)
                        {
                            sb.Append(exception.ToString());
                            sb.Append("\nCONTENT\n======================\n");
                            sb.Append(m.Content);
                            sb.Append("\n======================\n");
                            sb.Append("\nSENDER\n======================\n");
                            sb.Append(m.SenderStr);
                            sb.Append("\n======================\n");
                            sb.Append("\nTIME\n======================\n");
                            sb.Append(m.SendTime.ToString("yyyy-MM-dd-HH-mm-ss-fff"));
                            sb.Append("\n======================\n");
                            sb.Append("\nORIGIN\n======================\n");
                            sb.Append(m.OriginMessage);
                            sb.Append("\n======================\n\n\n");
                            File.AppendAllText(ErrorOutputPath, sb.ToString());
                            sb.Clear();
                        }
                    }
                    else
                    {
                        sb.Append("IMAGE MAPPING ERROR");
                        sb.Append("\nCONTENT======================\n");
                        sb.Append(m.Content);
                        sb.Append("\nSENDER======================\n");
                        sb.Append(m.SenderStr);
                        sb.Append("\nTIME======================\n");
                        sb.Append(m.SendTime.ToString("yyyy-MM-dd-HH-mm-ss-fff"));
                        sb.Append("\n======================\n");
                        sb.Append("\n======================\n\n\n");
                        File.AppendAllText(ErrorOutputPath, sb.ToString());
                        sb.Clear();
                    }
                    StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"添加数据 {inscnt}/{cc}" });
                }
            }
            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"正在执行VACUUM" });
            SQLUtil.Instance.DB.Execute("VACUUM");
            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"完成" });

            if (!isVacOnly)
            {
                MessageBox.Show($"扫描{delcnt}行，删除{delcnt - inscnt}行。");
            }
            //return SQLUtil.Instance.DB.Table<Message>().Delete(m => Regex.IsMatch(m.Content, "<img src=\"[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}", RegexOptions.IgnoreCase));
        }
        public void LoadMessages(string QQMHTPath)
        {
            if (OutputPathName != "" && !Directory.Exists(OutputPathName)) Directory.CreateDirectory(OutputPathName);
            isStop = false;
            string strChatName = "";
            using FileStream fs = new(QQMHTPath, FileMode.Open, FileAccess.Read);
            using StreamReader sr = new(fs);
            int currRowCount = 0;
            var CurDate = new DateTime(2000, 01, 01);
            List<string> sameList = new();
            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"开始导入记录" });
            string ErrorOutputPath = Path.Combine(OutputPathName, $"MessageTreatmentErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt");
            string DataConflictListOutputPath = Path.Combine(OutputPathName, $"DataConflictList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt");
            while (!sr.EndOfStream)
            {
                string currLine = string.Empty;
                string strContent = string.Empty;
                try
                {
                    currLine = sr.ReadLine()?.TrimEnd();
                    if (currLine != null && currLine.Contains("<tr><td><div"))
                    {
                        if (currLine.Contains("<title>QQ Message</title>"))
                        {
                            strChatName = Regex.Match(currLine, "<tr><td><div.*?>消息对象:(?<objName>.*?)</div>").Groups["objName"].Value;
                            var inf = SQLUtil.Instance.DB.Table<DBRecord>().First();
                            inf.DisplayName ??= strChatName;
                            inf.UpdateTime = DateTime.Now;
                            SQLUtil.Instance.DB.Update(inf);
                        }
                        if (Regex.IsMatch(currLine, "<tr><td.*?>日期:"))
                        {
                            var Match = Regex.Match(currLine, "<tr><td.*?>日期: (?<date>.*?)</td>");
                            CurDate = DateTime.Parse(Match.Groups["date"].Value);
                        }
                        if (currLine.Contains("}.dat"))
                        {
                            var strImgs = Regex.Matches(currLine, "<IMG src=\"{(?<Img>.*?)}.dat\">");
                            foreach (Match m in strImgs)
                            {
                                var strImgLabel = m.Groups[0].Value;
                                var strImgId = m.Groups["Img"].Value;
                                if (AssetsMapping.ContainsKey(strImgId))
                                {
                                    strImgId = ImgDirName + "/" + AssetsMapping[strImgId];
                                }
                                else
                                {
                                    // 防止记录重复
                                    strImgId = ImgDirName + "/" + strImgId + ".jpg\" tag=\"imageMappingError";
                                    currLine = currLine.Replace(strImgLabel, $"<img src=\"{strImgId}\"/>");
                                    throw new Exception("ImageMappingNotFound");
                                }
                                currLine = currLine.Replace(strImgLabel, $"<img src=\"{strImgId}\"/>");
                            }
                        }
                        try
                        {
                            //parse
                            //<tr><td><div><div>{uid}</div>{time}</div><div><font>{content}</font></div></td></tr>
                            var Matches = Regex.Matches(currLine, @"<tr><td><div.*?><div style=float:left;margin-right:6px;>(?<uid>.*?)</div>(?<time>([上下]午)?\d{1,2}:\d{2}:\d{2})</div><div.*?>(?<content>.*?)</div></td></tr>");
                            foreach (Match m in Matches)
                            {
                                var strSender = WebUtility.HtmlDecode(m.Groups["uid"].Value.Replace("&get;", "&gt;"));
                                var strTime = m.Groups["time"].Value;
                                var strContentRaw = WebUtility.HtmlDecode(m.Groups["content"].Value);
                                var contentParse = Regex.Matches(strContentRaw, "<font.*?>(?<span>.*?)</font>|(?<span><img src=.*?>)");
                                strContent = "";
                                foreach (Match span in contentParse)
                                {
                                    strContent += span.Groups["span"].Value.Replace("<br>", "\r\n");
                                }
                                var sendTime = CurDate.Add(DateTime.Parse(strTime).TimeOfDay);
                                SQLUtil.Instance.DB.Insert(new Message(strContent, strSender, sendTime, strContentRaw, strChatName));
                            }
                        }
                        catch (Exception exception)
                        {
                            //StatusChanged.BeginInvoke(new ProcessStatus { isException = true, exception = exception, percent = count, desc = "exRp" }, null, null);
                            if (exception.Message.StartsWith("UNIQUE constraint failed"))
                            {
                                sameList.Add(currLine);
                            }
                            else
                                throw;
                        }
                        currRowCount++;
                        StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"当前第{currRowCount}行，{CurDate:yyyy-MM-dd}" });
                    }
                    else if (currLine.Contains(@"</html>"))
                    {
                        if (sameList.Count > 0)
                        {
                            try
                            {
                                StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.MessageBox, desc = $"总计{currRowCount}行，跳过重复行{sameList.Count}行" });
                                File.WriteAllLines(DataConflictListOutputPath, sameList);
                            }
                            catch
                            {
                                //StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.MessageBox, desc = exception.Message });
                                throw;
                            }
                        }
                        else
                        {
                            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.MessageBox, desc = $"已导入总计{currRowCount}行" });
                        }
                        break;
                    }
                    if (isStop)
                    {
                        if (sameList.Count > 0)
                        {
                            try
                            {
                                StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.MessageBox, desc = $"总计{currRowCount}行，跳过重复行{sameList.Count}行" });
                                File.WriteAllLines(DataConflictListOutputPath, sameList);
                            }
                            catch
                            {
                                throw;
                            }
                        }
                        else
                        {
                            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.MessageBox, desc = $"已导入总计{currRowCount}行" });
                        }
                        break;
                    }
                }
                catch (Exception exception)
                {
                    File.AppendAllText(ErrorOutputPath, exception.ToString());
                    File.AppendAllText(ErrorOutputPath, "\nCURRENT_LINE\n======================\n");
                    File.AppendAllText(ErrorOutputPath, currLine);
                    File.AppendAllText(ErrorOutputPath, "\n======================\n");
                    File.AppendAllText(ErrorOutputPath, "\nSTRING_CONTENT\n======================\n");
                    File.AppendAllText(ErrorOutputPath, strContent);
                    File.AppendAllText(ErrorOutputPath, "\n======================\n\n\n");
                }
            }
        }
        public void ExportAssets(string QQMHTPath)
        {
            isStop = false;
            AssetsMapping.Clear();
            string fileExt = "";
            string fileName = "";
            string ImageOutputPath = Path.Combine(OutputPathName, ImgDirName);
            ExportAssetsStatus status = ExportAssetsStatus.End;
            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"开始导入图片" });
            if (!Directory.Exists(ImageOutputPath)) Directory.CreateDirectory(ImageOutputPath);
            StringBuilder sb = new();
            using FileStream fs = new (QQMHTPath, FileMode.Open, FileAccess.Read);
            using StreamReader sr = new (fs);
            int currImg = 0;
            string? currLine = string.Empty;
            string ContentString = string.Empty;
            string ErrorOutputPath = Path.Combine(OutputPathName, $"ImageExportErrorList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt");
            while (!sr.EndOfStream)
            {
                try
                {
                    currLine = sr.ReadLine()?.TrimEnd();
                    if (currLine == "")
                    {
                        if (status == ExportAssetsStatus.Standby)
                        {
                            status = ExportAssetsStatus.Start;
                        }
                        else if (status == ExportAssetsStatus.Start)
                        {
                            status = ExportAssetsStatus.End;
                            ContentString = sb.ToString();
                            sb.Length = 0;
                            //去重
                            byte[] byteContent = Convert.FromBase64String(ContentString);
                            StringBuilder namesb = new();
                            using (var sha = HashAlgorithm.Create("SHA1"))
                            {
                                var byteHashResult = sha.ComputeHash(byteContent);
                                for (int i = 0; i < byteHashResult.Length; i++)
                                {
                                    namesb.Append(byteHashResult[i].ToString("x2"));
                                }
                            }
                            //扩展名修正
                            try
                            {
                                if (byteContent[0] == 0xFF && byteContent[1] == 0xD8) fileExt = "jpg";
                                if (byteContent[0] == 0x89 && byteContent[1] == 0x50 && byteContent[2] == 0x4E && byteContent[3] == 0x47) fileExt = "png";
                                if ((byteContent[0] == 0x49 && byteContent[1] == 0x49) || (byteContent[0] == 0x4D && byteContent[1] == 0x4D)) fileExt = "tif";
                                if (byteContent[0] == 0x47 && byteContent[1] == 0x49 && byteContent[2] == 0x46) fileExt = "gif";
                                if (byteContent[0] == 0x4D && byteContent[1] == 0x42) fileExt = "bmp";
                                if (byteContent[0] == 0x52 && byteContent[1] == 0x49 && byteContent[8] == 0x57 && byteContent[11] == 0x50) fileExt = "webp";
                            }
                            catch (Exception)
                            {
                                // 报错则应用记录的格式
                            }
                            //保存成图片文件
                            var trueName = namesb.ToString();
                            SaveImage(trueName, byteContent, fileExt);
                            currImg++;
                            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"导入图片，第{currImg}个" });
                            //写入到字典文件,用户读取正文时生成链接
                            if (!AssetsMapping.ContainsKey(fileName)) AssetsMapping.Add(fileName, trueName + "." + fileExt);
                            if (isStop) break;
                        }
                    }
                    else if (currLine.Contains("Content-Location:"))
                    {
                        fileName = currLine.Substring(18, 36);
                    }
                    else if (currLine.Contains("Content-Type:image/"))
                    {
                        fileExt = currLine.Replace("Content-Type:image/", "");
                        status = ExportAssetsStatus.Standby;
                    }
                    else if (status == ExportAssetsStatus.Start)
                    {
                        sb.Append(currLine);
                    }
                }
                catch (Exception exception)
                {
                    File.AppendAllText(ErrorOutputPath, exception.ToString());
                    File.AppendAllText(ErrorOutputPath, "\nCURRENT_LINE\n======================\n");
                    File.AppendAllText(ErrorOutputPath, currLine);
                    File.AppendAllText(ErrorOutputPath, "\n======================\n");
                    File.AppendAllText(ErrorOutputPath, "\nIMAGE_CONTENT\n======================\n");
                    File.AppendAllText(ErrorOutputPath, ContentString);
                    File.AppendAllText(ErrorOutputPath, "\n======================\n\n\n");
                }
            }
            StatusChanged.Invoke(new ProcessStatus { type = ProcessStatusType.Progress, desc = $"导入图片完成" });
        }
        private void SaveImage(string fileName, byte[] fileContent, string fileExt)  
        {
            string filePath = Path.Combine(OutputPathName, ImgDirName, fileName + "." + fileExt);
            if (File.Exists(filePath)) return; 
            FileStream fs = new(filePath, FileMode.Create);
            fs.Write(fileContent, 0, fileContent.Length);
            fs.Close();
        }
    }
}
