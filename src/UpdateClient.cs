using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LumaKeys {
    public sealed class UpdateAnnouncement {
        public string Id {get;set;}
        public string Title {get;set;}
        public string Content {get;set;}
        public string PublishedUtc {get;set;}
    }
    public sealed class UpdateManifest {
        public string App {get;set;}
        public string Version {get;set;}
        public string Notes {get;set;}
        public string PackageUrl {get;set;}
        public string Sha256 {get;set;}
        public long Size {get;set;}
        public List<string> Files {get;set;}
        public UpdateAnnouncement Announcement {get;set;}
    }

    public static class UpdateClient {
        public const string CurrentVersion="2026.10.20.1";
        const string Placeholder="YOUR-BAOTA-DOMAIN";
        const int MaxManifestBytes=1024*1024;
        const long MaxPackageBytes=512L*1024L*1024L;

        public static string ManifestUrl {
            get {
                string path=Path.Combine(Store.Root,"update-config.json");
                try {
                    if(File.Exists(path)) {
                        var cfg=Store.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));
                        object value; if(cfg!=null&&cfg.TryGetValue("manifestUrl",out value)&&value!=null&&!string.IsNullOrWhiteSpace(value.ToString()))return value.ToString().Trim();
                    }
                } catch(Exception ex) {Diagnostics.Log("Update config: "+ex.Message);}
                return "http://47.113.189.245:88/manifest.json";
            }
        }

        public static bool IsConfigured(string url) {return !string.IsNullOrWhiteSpace(url)&&url.IndexOf(Placeholder,StringComparison.OrdinalIgnoreCase)<0&&url.IndexOf("example",StringComparison.OrdinalIgnoreCase)<0;}

        sealed class UpdateWebClient : WebClient {
            protected override WebRequest GetWebRequest(Uri address) {
                var request=base.GetWebRequest(address);
                request.Timeout=15000;
                request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                return request;
            }
        }

        static byte[] Download(Uri uri,long maxBytes) {
            if(uri==null)throw new ArgumentNullException("uri");
            if(uri.Scheme!="https"&&uri.Scheme!="http")throw new InvalidOperationException("更新地址必须使用 HTTP 或 HTTPS。\n当前地址："+uri);
            using(var client=new UpdateWebClient()) {
                client.Headers[HttpRequestHeader.UserAgent]="LumaKeys-Updater/1.0";
                byte[] bytes=client.DownloadData(uri);
                if(bytes.LongLength>maxBytes)throw new InvalidOperationException("更新文件过大，已停止下载。");
                return bytes;
            }
        }

        public static async Task<UpdateManifest> FetchAsync() {
            string url=ManifestUrl;
            if(!IsConfigured(url))return null;
            byte[] bytes=await Task.Run(()=>Download(new Uri(url),MaxManifestBytes));
            string text=Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            var info=Store.Json.Deserialize<UpdateManifest>(text);
            if(info==null||string.IsNullOrWhiteSpace(info.Version)||string.IsNullOrWhiteSpace(info.PackageUrl))throw new FormatException("更新清单缺少 version 或 packageUrl。");
            if(!string.Equals(info.App,"LumaKeys",StringComparison.OrdinalIgnoreCase)&&!string.Equals(info.App,"dogeplay",StringComparison.OrdinalIgnoreCase))throw new FormatException("更新清单不是 dogeplay 客户端。");
            if(string.IsNullOrWhiteSpace(info.Sha256)||info.Sha256.Trim().Length!=64)throw new FormatException("更新清单缺少有效的 SHA-256。");
            return info;
        }

        public static bool IsNewer(string remote,string local) {
            Version a,b;
            if(Version.TryParse(remote,out a)&&Version.TryParse(local,out b))return a>b;
            return string.Compare(remote??"",local??"",StringComparison.OrdinalIgnoreCase)>0;
        }

        public static async Task<string> DownloadAndLaunchAsync(UpdateManifest info) {
            string manifestUrl=ManifestUrl;
            Uri package=new Uri(new Uri(manifestUrl),info.PackageUrl);
            if(info.Size<0||info.Size>MaxPackageBytes)throw new InvalidOperationException("更新包大小无效。");
            byte[] bytes=await Task.Run(()=>Download(package,MaxPackageBytes));
            if(info.Size>0&&bytes.LongLength!=info.Size)throw new InvalidDataException("更新包大小校验失败。");
            string actual;
            using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
            if(!string.Equals(actual,info.Sha256.Trim(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包 SHA-256 校验失败，文件可能已损坏或被替换。");
            string temp=Path.Combine(Path.GetTempPath(),"LumaKeys-update-"+Guid.NewGuid().ToString("N")+".zip");
            File.WriteAllBytes(temp,bytes);
            string targetExe=Branding.ExecutablePath;
            // Isolate helper dependencies so it does not lock the installed DLLs.
            string helperRoot=Path.Combine(Path.GetTempPath(),"LumaKeys-Updater-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(helperRoot);
            string helper=Path.Combine(helperRoot,"dogeplay.exe");
            File.Copy(targetExe,helper,true);
            foreach(string dll in Directory.GetFiles(Store.Root,"*.dll"))File.Copy(dll,Path.Combine(helperRoot,Path.GetFileName(dll)),true);
            int pid=Process.GetCurrentProcess().Id;
            string args="--apply-update "+Quote(temp)+" "+Quote(targetExe)+" "+pid.ToString();
            Process.Start(new ProcessStartInfo {FileName=helper,Arguments=args,WorkingDirectory=Store.Root,UseShellExecute=true});
            return temp;
        }

        static string Quote(string value) {return "\""+(value??"").Replace("\\\"","\\\\\"")+"\"";}
        static bool SafeRelative(string path) {
            if(string.IsNullOrWhiteSpace(path)||Path.IsPathRooted(path))return false;
            string normalized=path.Replace('\\','/');
            if(normalized.Split('/').Any(p=>p==".."||p=="."||p.Length==0||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||p.EndsWith(".",StringComparison.Ordinal)||p.EndsWith(" ",StringComparison.Ordinal)))return false;
            if(normalized.StartsWith("data/",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("downloads/",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("app-backups/",StringComparison.OrdinalIgnoreCase))return false;
            if(normalized.EndsWith(".log",StringComparison.OrdinalIgnoreCase)||normalized.IndexOf("Cookies",StringComparison.OrdinalIgnoreCase)>=0)return false;
            return normalized.Equals("dogeplay.exe",StringComparison.OrdinalIgnoreCase)||normalized.Equals("LumaKeys.exe",StringComparison.OrdinalIgnoreCase)||normalized.Equals("MainWindow.xaml",StringComparison.OrdinalIgnoreCase)||normalized.Equals("update-config.json",StringComparison.OrdinalIgnoreCase)||normalized.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("scripts/",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("assets/",StringComparison.OrdinalIgnoreCase);
        }

        public static int ApplyFromArgs(string[] args) {
            if(args==null||args.Length<4)return 2;
            string zip=args[1],targetExe=args[2];int parent;
            if(!int.TryParse(args[3],out parent)||!File.Exists(zip)||!File.Exists(targetExe))return 3;
            try {
                try {using(var p=Process.GetProcessById(parent)){if(!p.WaitForExit(30000))throw new IOException("旧程序尚未退出，请关闭软件后重试更新。");}}catch(ArgumentException){}
                string stage=Path.Combine(Path.GetTempPath(),"LumaKeys-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
                using(var archive=ZipFile.OpenRead(zip)) {
                    foreach(var entry in archive.Entries) {
                        if(string.IsNullOrEmpty(entry.Name))continue;
                        string relative=entry.FullName.Replace('\\','/');
                        while(relative.StartsWith("./",StringComparison.Ordinal))relative=relative.Substring(2);
                        if(!SafeRelative(relative))throw new InvalidDataException("更新包包含不允许覆盖的文件："+relative);
                        string destination=Path.Combine(stage,relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(destination));entry.ExtractToFile(destination,true);
                    }
                }
                string root=Path.GetDirectoryName(targetExe);
                InstallFiles(stage,root);
                string restart=File.Exists(Path.Combine(stage,"LumaKeys.exe"))?Path.Combine(root,"LumaKeys.exe"):targetExe;Process.Start(new ProcessStartInfo {FileName=restart,WorkingDirectory=root,UseShellExecute=true});
                try {File.Delete(zip);}catch{}
                try {Directory.Delete(stage,true);}catch{}
                try {
                    string helperRoot=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                    if(Path.GetFileName(helperRoot).StartsWith("LumaKeys-Updater-",StringComparison.Ordinal)) {
                        foreach(string file in Directory.GetFiles(helperRoot))MoveFileEx(file,null,4);
                        MoveFileEx(helperRoot,null,4);
                    }
                }catch{}
                return 0;
            } catch(Exception ex) {try{File.WriteAllText(Path.Combine(Path.GetDirectoryName(targetExe),"update-error.log"),ex.ToString(),Encoding.UTF8);}catch{}return 4;}
        }

        static void InstallFiles(string stage,string root) {
            var files=Directory.GetFiles(stage,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToList();
            if(files.Count==0)throw new InvalidDataException("更新包为空。");
            string backup=Path.Combine(stage,"backup");Directory.CreateDirectory(backup);
            var copied=new List<string>();
            try {
                foreach(string source in files) {
                    string relative=source.Substring(stage.Length+1);
                    if(!SafeRelative(relative))throw new InvalidDataException("不允许更新："+relative);
                    string destination=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    string old=Path.Combine(backup,relative);
                    if(File.Exists(destination)){Directory.CreateDirectory(Path.GetDirectoryName(old));File.Copy(destination,old,true);}
                    copied.Add(destination);File.Copy(source,destination,true);
                }
            } catch(Exception original) {
                var failed=new List<string>();
                foreach(string destination in copied.AsEnumerable().Reverse()) {
                    try {
                        string old=Path.Combine(backup,destination.Substring(root.Length+1));
                        if(File.Exists(old)){File.Copy(old,destination,true);}else if(File.Exists(destination)){File.Delete(destination);}
                    }catch {failed.Add(destination);}
                }
                if(failed.Count>0)throw new IOException("部分文件恢复失败，备份保存在："+backup,original);
                throw;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] static extern bool MoveFileEx(string existing,string replacement,uint flags);
    }
}
