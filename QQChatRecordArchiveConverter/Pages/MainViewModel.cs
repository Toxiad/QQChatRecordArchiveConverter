using QQChatRecordArchiveConverter.CARC.Util;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QQChatRecordArchiveConverter.Pages
{
    public class MainViewModel : Screen
    {
        public MainViewModel() {

            var a = SQLUtil.Instance;
        }
        MainModel model;
    }
}
