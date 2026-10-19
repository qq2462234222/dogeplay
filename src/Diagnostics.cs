using System;
using System.IO;
using System.Text;

namespace LumaKeys {
    public static class Diagnostics {
        static readonly object Sync=new object();
        public static void Log(string message) {
            try {lock(Sync){Directory.CreateDirectory(Store.Data);File.AppendAllText(Path.Combine(Store.Data,"playback.log"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" "+message+Environment.NewLine,Encoding.UTF8);}}catch{}
        }
    }
}
