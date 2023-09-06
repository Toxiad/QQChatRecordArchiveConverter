using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QQChatRecordArchiveConverter.CARC.Util
{
    public class Util
    {
        public static long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);

                foreach (FileInfo file in dir.GetFiles())
                {
                    size += file.Length;
                }

                foreach (DirectoryInfo subDir in dir.GetDirectories())
                {
                    size += GetDirectorySize(subDir.FullName);
                }

            }
            catch
            {
                return 0;
            }
            return size;
        }
        public static string SizeParse(long size)
        {
            double temp = size;
            int unitpnt = 0;
            var unitarr = new string[] {"B", "K", "M", "G", "T"};
            while (temp > 1000) {
                temp /= 1024;
                unitpnt++;
            }
            return temp.ToString("0.00") + unitarr[unitpnt];
        }
    }
}
