using Microsoft.Win32;
using QQChatRecordArchiveConverter.CARC.Module;
using Stylet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace QQChatRecordArchiveConverter.CARC.Util
{
    public class HTMLHelper
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
        

        public const string HtmlEndString = @"</table>\n</body>\n</html>";

        public string OutputPath = "/AssestOutput/";



        public void Export(List<Message> TotalMessages, DateTime Earliest, DateTime Latest, string GroupName)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Title = "导出QQ聊天记录",
                Filter = "聊天记录文件 (*.html)|*.html"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var fn = saveFileDialog.FileName;
                Task.Factory.StartNew(() => {
                    string HtmlHeadString = $"<html><head><meta http-equiv=\"Content-Type\"content=\"text/html; charset=UTF-8\"/><title>QQ Message</title><style type=\"text/css\">" +
                    $":root {{--themePri: #00AEEC;--blackText: #000;--blackText2: #333;--grayText: #777;--grayText2: #bbb;--whiteText: #fff;--grayBg: #eee;--grayBg2: #e8edef;--bluet: #80DAF6;--blueh: #BFEDFA;--pinkh: #FF6699;}}html {{background: var(--grayBg2);}}body {{font-size: 14px;line-height: 22px;margin: 50px auto;max-width: 900px;background: var(--whiteText);box-shadow: 0 0 19px 0px #0003;padding: 40px;border-radius: 12px;}}.infobox {{font-size: 14px;line-height: 1em;margin-bottom: 6px;}}.sender {{color: var(--grayText);}}.sendtime {{display: none;color: var(--grayText2);margin-left: 12px;transition: all ease .3s;}}.message-bubble {{overflow-wrap: break-word;display: inline-block;padding: 8px 17px;margin-top: 5px;border-radius: 9px;box-shadow: 0 0 5px 0px #0003;color: var(--whiteText);background: var(--themePri);max-width: 800px;min-height: 22px;flex-shrink: 0;flex-grow: 0;}}.message-content img {{display: inline-block;border-radius: 5px;max-width: 100%;}}.messages {{display: flex;flex-direction: row;color: var(--grayText2);margin-left: 12px;margin-bottom: 15px;align-items: flex-start;}}.messages:hover .sendtime {{display: inline-block;transition: all ease .3s;}}.messages.Normal {{color: var(--grayText2);margin-left: 12px;}}.titlebox {{border-radius: 5px;background: var(--whiteText);border: var(--grayText2) 1px solid;padding: 10px;margin-bottom: 30px;}}.title {{margin: 10px;color: var(--themePri);font-weight: bold;font-size: 1.4em;}}.info {{background: var(--grayBg2);border-radius: 5px;margin-top: 10px;padding: 10px;}}.info-item {{color: var(--grayText2);margin-top: 5px;}}.info-label {{display: inline-block;color: var(--grayText);width: 6em;}}.info-content {{display: inline-block;color: var(--blackText2);}}.messages.System {{margin: auto;margin-bottom: 15px;color: var(--grayText2);text-align: center;display: block;}}.messages.System .message-bubble {{overflow-wrap: break-word;background: var(--grayBg2);padding: 2px 14px;box-shadow: none;border-radius: 18px;}}.messages.System .message-content {{color: var(--grayText2);}}.Unknow.message-content::before {{content: \"[消息类型不支持导出，该记录无任何数据]\";}}.messages.System .sender {{display: none;}}.mc-at, .mc-href {{text-decoration: none;background-color: transparent;color: var(--whiteText);cursor: pointer;}}.mc-at:hover, .mc-href:hover {{color: var(--pinkh);}}.mc-uk::before {{content: \" ！此记录类型错误 \";color: var(--pinkh);}}.Message {{display: flex;flex-direction: column;align-items: flex-start;}}.avatar {{display: block;position: relative;flex-shrink: 0;flex-grow: 0;border-radius: 40px;height: 40px;width: 40px;background: #fff;box-shadow: 0 0 5px 0px #0003;padding: 2px;margin-right: 16px;}}.avatar-img {{display: block;position: relative;border-radius: 50px;height: 100%;width: 100%;}}.messages.System .avatar {{display: none;}}.messages.System .Message {{align-items: center;}}.avatar-img::before {{display: block;width: 100%;height: 100%;position: absolute;top: 0;left: 0;content: \"\";background: url(data:image/png;base64,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) center / cover no-repeat;}}.messages.System .infobox {{display: none;}}.messages.System:hover .infobox {{display: block;position: absolute;}}.headerlabel {{display: flex;margin-top: 10px;}}" +
                    $"</style></head><body><div class='titlebox'><div class=\"title\">QQ聊天记录存档</div><span class='headerlabel'><span class='avatar'><img class='avatar-img' src='Source/Avatar/GroupAvatar/1.png'/></span>{GroupName}</span><div class=\"info\"><div class=\"info-item\"><span class=\"info-label\">起始时间</span><span class=\"info-content\">{Earliest:yyyy-MM-dd HH:mm:ss}</span></div><div class=\"info-item\"><span class=\"info-label\">结束时间</span><span class=\"info-content\">{Latest:yyyy-MM-dd HH:mm:ss}</span></div><div class=\"info-item\"><span class=\"info-label\">记录数</span><span class=\"info-content\">{TotalMessages.Count}</span></div></div></div>";

                    string HtmlEndString = @"</body></html>";
                    File.AppendAllText(fn, HtmlHeadString, Encoding.UTF8);
                    foreach (var m in TotalMessages)
                    {
                        var XmlContent = new XmlDocument();
                        var str = m.OriginMessage.Replace("<br>", "<br/>").Replace("&get;", ">");

                        string Content = "";
                        try
                        {
                            try
                            {
                                XmlContent.LoadXml($"<root>{str}</root>");
                            }
                            catch (XmlException ex)
                            {
                                str = str
                                .Replace("name=a", "")
                                .Replace("&", "&amp;")
                                .Replace("<br>", "&lt;br&gt;")
                                .Replace(" ", " ");
                                XmlContent.LoadXml($"<root>{str}</root>");
                            }
                            foreach (XmlNode node in XmlContent["root"].ChildNodes)
                            {
                                if (node.NodeType == XmlNodeType.Text)
                                {
                                    Content += $"<span class='mc-pl'>{node.InnerText}</span>";
                                }
                                else
                                {
                                    if (node.Name == "font")
                                    {
                                        if (node.InnerText.StartsWith("@"))
                                        {
                                            Content += $"<span class='mc-at'>{node.InnerText}</span>";
                                        }
                                        else if(node.InnerText.StartsWith("http"))
                                        {
                                            Content += $"<a class='mc-href' target='_blank' href='{node.InnerText}'>{node.InnerText}</a>";
                                        }
                                        else
                                        {
                                            Content += $"<span class='mc-pl'>{node.InnerText}</span>";
                                        }
                                    }
                                    else if (node.Name == "img")
                                    {
                                        Content += $"<span class='mc-img'>{node.OuterXml}</span>";
                                    }
                                    else
                                    {
                                        Content += $"<span class='mc-uk'>{node.OuterXml}</span>";
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            Content = HtmlEncoder.Default.Encode(str).Replace("style=", "tc=") + "@ERR!";
                        }
                        File.AppendAllText(fn, $"<div class='messages {m.SenderType}'><span class='avatar'><img class='avatar-img' src='Source/Avatar/MemberAvatar/{m.SenderId}.png'/></span><div class='Message'><div class='infobox'><span class='sender'>{WebUtility.HtmlEncode(m.SenderName)}</span><span class='sendtime'>({m.SenderId}) 发送于{m.SendTime:yyyy-MM-dd HH:mm:ss}</span></div><span class='message-bubble'><span class='message-content {m.MessageType}'>{Content}</span></span></div></div>\n", Encoding.UTF8);
                    }
                    File.AppendAllText(fn, HtmlEndString, Encoding.UTF8);
                    Execute.OnUIThread(() => {
                        MessageBox.Show("导出完成，点击确定打开文件", "记录导出");
                    });
                });
            }
        }
    }
}
