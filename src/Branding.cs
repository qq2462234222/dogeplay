using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

[assembly: AssemblyTitle("dogeplay")]
[assembly: AssemblyProduct("dogeplay")]
[assembly: AssemblyCompany("doge")]
[assembly: AssemblyDescription("游戏演奏与悬浮学习谱 · 免费使用 · 仅供学习交流")]
[assembly: AssemblyCopyright("Copyright © 2026 doge")]
[assembly: AssemblyVersion("2026.10.18.6")]
[assembly: AssemblyFileVersion("2026.10.18.6")]

namespace LumaKeys {
    public static class Branding {
        public static string ExecutablePath {get{return Path.Combine(Store.Root,"dogeplay.exe");}}
        // Earlier updaters only accept LumaKeys.exe. A bridge patch carries the
        // new application under that filename, then migrates after it restarts.
        public static bool MigrateLegacyExecutable(string[] args) {
            string current=Assembly.GetExecutingAssembly().Location;
            if(Path.GetFileName(current).Equals("LumaKeys.exe",StringComparison.OrdinalIgnoreCase)) {
                File.Copy(current,ExecutablePath,true);
                Process.Start(new ProcessStartInfo{FileName=ExecutablePath,Arguments="--elevated-restart --remove-legacy "+Process.GetCurrentProcess().Id,WorkingDirectory=Store.Root,UseShellExecute=true});return true;
            }
            int marker=Array.IndexOf(args,"--remove-legacy"),pid;
            if(marker>=0&&marker+1<args.Length&&int.TryParse(args[marker+1],out pid)) {
                try{using(var process=Process.GetProcessById(pid))process.WaitForExit(5000);}catch(ArgumentException){}
                string legacy=Path.Combine(Store.Root,"LumaKeys.exe");try{if(File.Exists(legacy))File.Delete(legacy);}catch(Exception ex){Diagnostics.Log("Legacy executable cleanup: "+ex.Message);}
            }
            return false;
        }
    }
}
