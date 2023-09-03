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
using System.Xml;

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
        public const string HtmlEndString = @"</table>\n</body>\n</html>";
        //生成HTML的正文,第二步进行
        public bool IsGetHtml = false;

        //生成消息附件中的图片,第一步进行
        public bool IsGetImg = false;
        public bool IsStop = false;

        //保存图片ID和后缀的对应关系
        public Dictionary<string, string> ImgDictionary = new Dictionary<string, string>();

        public string OutputPath = "AssestOutput";

        public string ImgDirName = "/Sources";
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
                    FileStream fsTmp = new FileStream(OutputPath + "ImgDictionary.txt", FileMode.Open);
                    StreamReader srTmp = new StreamReader(fsTmp);
                    while (!srTmp.EndOfStream)
                    {
                        var strTmpLine = srTmp.ReadLine();
                        if (strTmpLine == null)
                        {
                            continue;
                        }
                        var strTmpId = strTmpLine.Substring(0, 36);
                        var strTmpSuffix = strTmpLine.Substring(37);
                        if (!ImgDictionary.ContainsKey(strTmpId)) ImgDictionary.Add(strTmpId, strTmpSuffix);
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
                            if (inf.DisplayName == null) inf.DisplayName = strChatName;
                            SQLUtil.Instance.DB.Update(inf);
                        }
                        if (Regex.IsMatch(strLine, "<tr><td.*?>日期:")){
                            var Match = Regex.Match(strLine, "<tr><td.*?>日期: (?<date>.*?)</td>");
                            CurDate = DateTime.Parse(Match.Groups["date"].Value);
                        }
                        if (strLine.Contains("}.dat"))
                        {
                            var strImgId = strLine.Substring(strLine.IndexOf('{') + 1, 36);
                            if (blDicExist && ImgDictionary.ContainsKey(strImgId))
                            {
                                strLine = strLine.Replace("}.dat", "." + ImgDictionary[strImgId]);
                            }
                            else
                            {
                                strLine = strLine.Replace("}.dat", ".jpg");
                            }
                            strLine = strLine.Replace("src=\"{", "src=\"" + ImgDirName + "/");
                        }
                        try
                        {
                            //parse
                            //<tr><td><div><div>{uid}</div>{time}</div><div><font>{content}</font></div></td></tr>
                            var Matches = Regex.Matches(strLine, @"<tr><td><div.*?><div style=float:left;margin-right:6px;>(?<uid>.*?)</div>(?<time>\d{1,2}:\d{2}:\d{2})</div><div.*?>(?<content>.*?)</div></td></tr>");
                            foreach (Match m in Matches)
                            {

                                var strSender = WebUtility.HtmlDecode(m.Groups["uid"].Value.Replace("&get;", "&gt;"));
                                var strTime = m.Groups["time"].Value;
                                var strContentRaw = WebUtility.HtmlDecode(m.Groups["content"].Value);
                                var contentParse = Regex.Matches(strContentRaw, "<font.*?>(?<span>.*?)</font>|(?<span><IMG src=.*?>)");
                                string strContent = "";
                                foreach (Match span in contentParse)
                                {
                                    strContent += span.Groups["span"].Value.Replace("<br>", "\r\n");
                                } 
                                var sendTime = CurDate.Add(TimeSpan.Parse(strTime)); 
                                var senderType = strSender == "系统消息(10000)" ? MessageSenderType.System : MessageSenderType.Normal;
                                var messageType = strContent.Contains("<IMG src=") ? MessageType.Complex : MessageType.Text;
                                SQLUtil.Instance.DB.Insert(new Message(strContent, strSender, sendTime, senderType, messageType, strContentRaw));
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
                            //保存成图片文件
                            WriteToImage(strImgFileName, strContent, strSuffix, OutputPath, ImgDirName);
                            //写入到字典文件,用户读取正文时生成链接
                            swDic.WriteLine(strImgFileName + "," + strSuffix);
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
            }
        }

        //保存每个图片到对应的文件
        private static void WriteToImage(string strFileName, string strContent, string strSuffix, string outputPath, string imgDirName)
        {
            if (File.Exists(outputPath + imgDirName + "/" + strFileName + "." + strSuffix)) return;
            byte[] byteContent = Convert.FromBase64String(strContent);
            FileStream fs = new FileStream(outputPath + imgDirName + "/" + strFileName + "." + strSuffix, FileMode.Create);
            fs.Write(byteContent, 0, byteContent.Length);
            fs.Close();
        }
    }
}
