using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QQChatRecordArchiveConverter
{
    internal class Test
    {
        internal static void Toast(string title, string message)
        {
            new ToastContentBuilder()
               .AddArgument("action", "viewConversation")
               .AddArgument("conversationId", 9813)
               .AddText(title)
               .AddText(message)
               .Show();
        }
    }
}
