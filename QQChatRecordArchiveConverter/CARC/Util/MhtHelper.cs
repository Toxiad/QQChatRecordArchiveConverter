using Microsoft.VisualBasic;
using QQChatRecordArchiveConverter.CARC.Module;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Security.Cryptography;
using System.Xml;
using System.Windows.Markup;
using Stylet;

namespace QQChatRecordArchiveConverter.CARC.Util
{
    public class MhtHelper
    {
        public class ProcessStatus
        {
            public double percent { get; set; }
            public string desc { get; set; }
            public DateTime desc2 { get; set; }
            public bool isException { get; set; } = false;
            public Exception exception { get; set; }
        }
        public delegate void ProcessStatusHandler(ProcessStatus status);
        public event ProcessStatusHandler StatusChanged;
        public const string HtmlEndString = @"</html>";
        //生成HTML的正文,第二步进行
        public bool IsGetHtml = false;

        //生成消息附件中的图片,第一步进行
        public bool IsGetImg = false;
        public bool IsStop = false;

        //保存图片ID和后缀的对应关系
        public Dictionary<string, string> ImgDictionary = new Dictionary<string, string>();

        public string OutputPath = "AssestOutput/";

        public string ImgDirName = "Sources";
        public void Stop()
        {
            IsStop = true;
        }

        //进行MHT转HTML的消息记录转化
        public void DoConvert(string strSrcFilePath, bool IsGetImg)
        {
            IsStop = false;
            string strChatName = ""; 
            string strSuffix = ""; 
            string strImgFileName = "";
            bool blBegin = false;           //表示到一个附件开头的标志位
            bool blEnd = false;             //表示到一个附件结尾的标志位
            bool blDicExist = false;        //表示字典文件是否存在的标志位
            this.IsGetHtml = !IsGetImg;
            this.IsGetImg = IsGetImg;
            FileStream fsSrc = new FileStream(strSrcFilePath, FileMode.Open);
            StreamReader rsSrc = new StreamReader(fsSrc);
            StringBuilder sbSrc = new StringBuilder();
            FileStream fsDic = null;
            StreamWriter swDic = null;

            if (OutputPath != "" && !Directory.Exists(OutputPath)) Directory.CreateDirectory(OutputPath);

            if (IsGetHtml)
            {
            }
            if (IsGetImg)
            {
                fsDic = new FileStream(OutputPath + "ImgDictionary.txt", FileMode.Create);
                swDic = new StreamWriter(fsDic);
                if (!Directory.Exists(OutputPath + ImgDirName)) Directory.CreateDirectory(OutputPath + ImgDirName);
            }

            //记录每个生成的html文件中的记录条数
            var count = 0;
            var counttick = DateTime.Now;
            //生成多个html文件时的序号
            if (IsGetHtml)
            {
                //如果是获取QQ消息文本，则事先加载图片文件字典
                if (File.Exists(OutputPath + "ImgDictionary.txt"))
                {
                    blDicExist = true;
                    FileStream fsTmp = new (OutputPath + "ImgDictionary.txt", FileMode.Open);
                    StreamReader srTmp = new (fsTmp);
                    while (!srTmp.EndOfStream)
                    {
                        var strTmpLine = srTmp.ReadLine();
                        if (strTmpLine == null)
                        {
                            continue;
                        }
                        var strTmpId = strTmpLine[..36];
                        var strTmpTrueName = strTmpLine[37..];
                        if (!ImgDictionary.ContainsKey(strTmpId)) ImgDictionary.Add(strTmpId, strTmpTrueName); 
                    }
                    srTmp.Close();
                    fsTmp.Close();
                }
            }
            if (IsStop) return;
            var CurDate = new DateTime(2000, 01, 01);
            List<string> sameList = new();
            while (!rsSrc.EndOfStream)
            {
                var strLine = rsSrc.ReadLine().TrimEnd();
                if (IsGetHtml)
                {
                    //第2步操作,正文部分读取成HTML文件,5万行记录生成一个文件,并根据字典文件中的后缀信息生成对应URL
                    if(strLine.Contains("<tr><td><div"))
                    {
                        if (strLine.Contains("<title>QQ Message</title>"))
                        {
                            strChatName = Regex.Match(strLine, "<tr><td><div.*?>消息对象:(?<objName>.*?)</div>").Groups["objName"].Value;
                            var inf =  SQLUtil.Instance.DB.Table<DBRecord>().First();
                            inf.DisplayName ??= strChatName;
                            if (!string.IsNullOrEmpty(inf.DisplayName) && inf.DisplayName != strChatName) 
                            {
                                Execute.OnUIThreadSync(() =>
                                {
                                    var conflict = MessageBox.Show($"需要导入的记录消息对象与数据库不一致，是否继续？\n导入：'{strChatName}'；当前数据库：'{inf.DisplayName}'", "名称冲突", MessageBoxButton.YesNo);
                                    if (conflict != MessageBoxResult.Yes)
                                    {
                                        IsStop = true;
                                        return;
                                    }
                                });
                            }
                            if (IsStop) return;
                            SQLUtil.Instance.DB.Update(inf);
                        }
                        if (Regex.IsMatch(strLine, "<tr><td.*?>日期:")){
                            var Match = Regex.Match(strLine, "<tr><td.*?>日期: (?<date>.*?)</td>");
                            CurDate = DateTime.Parse(Match.Groups["date"].Value);
                        }
                        if (strLine.Contains("}.dat"))
                        {
                            var strImgs = Regex.Matches(strLine, "<IMG src=\"{(?<Img>.*?)}.dat\">");
                            foreach (Match m in strImgs)
                            {
                                var strImgLabel = m.Groups[0].Value;
                                var strImgId = m.Groups["Img"].Value;
                                if (blDicExist && ImgDictionary.ContainsKey(strImgId))
                                {
                                    strImgId = ImgDirName + "/" + ImgDictionary[strImgId];
                                }
                                else
                                {
                                    strImgId = ImgDirName + "/" + strImgId + ".jpg";
                                }
                                strLine = strLine.Replace(strImgLabel, $"<img src=\"{strImgId}\"/>");
                            }
                        }
                        try
                        {
                            //parse
                            //<tr><td><div><div>{uid}</div>{time}</div><div><font>{content}</font></div></td></tr>
                            var Matches = Regex.Matches(strLine, @"<tr><td><div.*?><div style=float:left;margin-right:6px;>(?<uid>.*?)</div>(?<time>([上下]午)?\d{1,2}:\d{2}:\d{2})</div><div.*?>(?<content>.*?)</div></td></tr>");
                            foreach (Match m in Matches)
                            {
                                var strSender = WebUtility.HtmlDecode(m.Groups["uid"].Value.Replace("&get;", "&gt;"));
                                var strTime = m.Groups["time"].Value;
                                var strContentRaw = WebUtility.HtmlDecode(m.Groups["content"].Value);
                                var contentParse = Regex.Matches(strContentRaw, "<font.*?>(?<span>.*?)</font>|(?<span><img src=.*?>)");
                                string strContent = "";
                                foreach (Match span in contentParse)
                                {
                                    strContent += span.Groups["span"].Value.Replace("<br>", "\r\n");
                                } 
                                var sendTime = CurDate.Add(DateTime.Parse(strTime).TimeOfDay); 
                                SQLUtil.Instance.DB.Insert(new Message(strContent, strSender, sendTime, strContentRaw, strChatName, "Res\\otulogo@2x1231.png"));
                            }
                        }
                        catch (Exception exception)
                        {
                            //StatusChanged.BeginInvoke(new ProcessStatus { isException = true, exception = exception, percent = count, desc = "exRp" }, null, null);
                            if (exception.Message.StartsWith("UNIQUE constraint failed"))
                            {
                                sameList.Add(strLine);
                            }
                            else
                            MessageBox.Show(exception.Message);
                        }
                        count++;
                        if ((DateTime.Now - counttick).TotalMilliseconds > 1000) 
                        {
                            counttick = DateTime.Now;
                            StatusChanged.Invoke(new ProcessStatus { percent = count, desc = "percentRp", desc2 = CurDate });
                        }
                    }
                    else if (strLine.Contains(HtmlEndString))
                    {
                        try
                        {
                            StatusChanged.Invoke(new ProcessStatus { percent = sameList.Count, desc = "totalsame" });
                            File.WriteAllLines(OutputPath + $"DataConflictList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", sameList);
                        }
                        catch (Exception exception)
                        {
                            //StatusChanged.BeginInvoke(new ProcessStatus { isException = true, exception = exception, percent = count, desc = "exRp" }, null, null);
                            MessageBox.Show(exception.Message);
                        }

                        break;
                    }
                    if (IsStop)
                    {
                        StatusChanged.Invoke(new ProcessStatus { percent = sameList.Count, desc = "totalsame" });
                        File.WriteAllLines(OutputPath + $"DataConflictList-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", sameList);
                        break; 
                    }
                }
                else if (IsGetImg)
                {

                    //第1步操作,附件部分读取成相应的图片,并将图片名称和后缀信息保存成字典文件
                    if (strLine == "")
                    {
                        if (blBegin == true && blEnd == true)
                        {
                            blEnd = false;
                        }
                        else if (blBegin == true)
                        {
                            blBegin = false;
                            blEnd = true;
                            var strContent = sbSrc.ToString();
                            sbSrc.Remove(0, sbSrc.Length);
                            //MD5去重
                            byte[] byteContent = Convert.FromBase64String(strContent);
                            StringBuilder sb = new();
                            using (var sha = HashAlgorithm.Create("SHA1")) 
                            {
                                var byteHashResult = sha.ComputeHash(byteContent);
                                for (int i = 0; i < byteHashResult.Length; i++)
                                {
                                    sb.Append(byteHashResult[i].ToString("x2"));
                                }
                            }
                            //后缀名修正
                            try
                            {
                                if (byteContent[0] == 0xFF && byteContent[1] == 0xD8) strSuffix = "jpg";
                                if (byteContent[0] == 0x89 && byteContent[1] == 0x50 && byteContent[2] == 0x4E && byteContent[3] == 0x47) strSuffix = "png";
                                if ((byteContent[0] == 0x49 && byteContent[1] == 0x49) || (byteContent[0] == 0x4D && byteContent[1] == 0x4D)) strSuffix = "tif";
                                if (byteContent[0] == 0x47 && byteContent[1] == 0x49 && byteContent[2] == 0x46) strSuffix = "gif"; 
                                if (byteContent[0] == 0x4D && byteContent[1] == 0x42) strSuffix = "bmp"; 
                                if (byteContent[0] == 0x52 && byteContent[1] == 0x49 && byteContent[8] == 0x57 && byteContent[11] == 0x50) strSuffix = "webp";
                            }
                            catch (Exception)
                            {

                            }
                            //保存成图片文件
                            var trueName = sb.ToString();
                            WriteToImage(sb.ToString(), byteContent, strSuffix, OutputPath, ImgDirName); ;
                            //写入到字典文件,用户读取正文时生成链接
                            swDic.WriteLine(strImgFileName + "," + trueName + "." + strSuffix);
                            if (IsStop) break;
                        }
                    }
                    else if (strLine.Contains("Content-Location:"))
                    {
                        blBegin = true;
                        strImgFileName = strLine.Substring(18, 36);

                    }
                    else if (strLine.Contains("Content-Type:image/"))
                    {
                        strSuffix = strLine.Replace("Content-Type:image/", "");
                    }
                    else if (blBegin == true)
                    {
                        sbSrc.Append(strLine);
                    }
                }
            }
            rsSrc.Close();
            fsSrc.Close();
            if (IsGetImg)
            {
                try
                {
                    swDic?.Close();
                    fsDic?.Close();
                }
                catch (Exception exception)
                {
                    //StatusChanged.BeginInvoke(new ProcessStatus { isException = true, exception = exception, percent = count, desc = "exRp" }, null, null);
                    MessageBox.Show(exception.Message);
                }
            }
            if (IsGetHtml && !blDicExist)
            {
                //StatusChanged.BeginInvoke(new ProcessStatus { isException = false, percent = 100, desc = "完成，缺少词典文件" }, null, null);
                MessageBox.Show("缺少词典文件，可能造成图片读取错误");
            }
            else
            {
                //StatusChanged.Invoke(new ProcessStatus { isException = false, percent = 100, desc = "完成" }); 
                //MessageBox.Show("完成");
            }
        }

        //保存每个图片到对应的文件
        private static void WriteToImage(string strFileName, byte[] byteContent, string strSuffix, string outputPath, string imgDirName)
        {
            if (File.Exists(outputPath + imgDirName + "/" + strFileName + "." + strSuffix)) return;
            FileStream fs = new FileStream(outputPath + imgDirName + "/" + strFileName + "." + strSuffix, FileMode.Create);
            fs.Write(byteContent, 0, byteContent.Length);
            fs.Close();
        }
    }
}
