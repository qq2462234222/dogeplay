using System;
using System.IO;

namespace LumaKeys {
    // Persist only paths relative to the application's directory. Absolute paths
    // are resolved at the process boundary when starting the native engine.
    public static class StaffEngine {
        public const string BundledPath="tools\\audiveris\\Audiveris\\Audiveris.exe";
        static string repairPath;
        static volatile bool failed;
        static bool InsidePackage(string relative) {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))return false;
            try{return Resolve(relative).StartsWith(Path.GetFullPath(Store.Root).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}catch{return false;}
        }
        public static string Resolve(string relative) {
            return string.IsNullOrWhiteSpace(relative)?"":Path.GetFullPath(Path.Combine(Store.Root,relative));
        }
        public static string Relative(string path) {
            if(string.IsNullOrWhiteSpace(path))return "";
            var root=new Uri(Path.GetFullPath(Store.Root).TrimEnd('\\','/')+Path.DirectorySeparatorChar);
            var target=new Uri(Path.GetFullPath(path));
            if(!string.Equals(root.Scheme,target.Scheme,StringComparison.OrdinalIgnoreCase)||
               !string.Equals(Path.GetPathRoot(root.LocalPath),Path.GetPathRoot(target.LocalPath),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请把整套 Audiveris 复制到软件所在磁盘，再选择其中的 Audiveris.exe，以便保存相对位置。");
            return Uri.UnescapeDataString(root.MakeRelativeUri(target).ToString()).Replace('/','\\');
        }
        public static bool Complete(string executable) {
            if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable))return false;
            string directory=Path.GetDirectoryName(executable);
            return File.Exists(Path.Combine(directory,"app","Audiveris.cfg"))&&File.Exists(Path.Combine(directory,"app","audiveris.jar"))&&
                   File.Exists(Path.Combine(directory,"runtime","bin","server","jvm.dll"));
        }
        public static void Configure(Settings settings) {
            repairPath=null;failed=false;
            // Legacy absolute paths must not override the bundled engine.
            if(settings.AudiverisCustom&&InsidePackage(settings.AudiverisPath))
                repairPath=settings.AudiverisPath;
            settings.AudiverisCustom=repairPath!=null;
            settings.AudiverisPath=repairPath??BundledPath;
        }
        public static string Find() {return Resolve(repairPath??BundledPath);}
        public static string DisplayPath {get{return repairPath??BundledPath;}}
        public static bool NeedsRepair {get{return failed||!Complete(Find());}}
        public static void MarkFailed() {failed=true;}
        public static void MarkHealthy() {failed=false;}
        public static void SelectRepair(string path,Settings settings) {
            if(!NeedsRepair)throw new InvalidOperationException("识别引擎正常，无需更改。");
            if(!string.Equals(Path.GetFileName(path),"Audiveris.exe",StringComparison.OrdinalIgnoreCase)||!Complete(path))
                throw new InvalidOperationException("请选择完整 Audiveris 目录中的 Audiveris.exe；同目录必须保留 app 和 runtime 文件夹。");
            string relative=Relative(path);
            if(!InsidePackage(relative))throw new InvalidOperationException("请先将完整 Audiveris 目录复制到软件包内，再选择其中的 Audiveris.exe；引擎必须随软件包一起保留。");
            repairPath=relative;failed=false;
            settings.AudiverisPath=repairPath;settings.AudiverisCustom=true;
        }
    }
}
