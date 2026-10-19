using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LumaKeys {
    public static class InputAccess {
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int type,IntPtr info,uint length,out uint required);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid,uint index);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        public static bool IsAdministrator {get{using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);}}
        public static int? Integrity(uint pid) {
            IntPtr process=OpenProcess(0x1000,false,pid),token=IntPtr.Zero,buffer=IntPtr.Zero;
            try {
                if(process==IntPtr.Zero||!OpenProcessToken(process,8,out token))return null;
                uint size;GetTokenInformation(token,25,IntPtr.Zero,0,out size);if(size==0)return null;
                buffer=Marshal.AllocHGlobal((int)size);if(!GetTokenInformation(token,25,buffer,size,out size))return null;
                IntPtr sid=Marshal.ReadIntPtr(buffer);byte count=Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return count==0?(int?)null:Marshal.ReadInt32(GetSidSubAuthority(sid,(uint)(count-1)));
            }finally{if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);if(token!=IntPtr.Zero)CloseHandle(token);if(process!=IntPtr.Zero)CloseHandle(process);}
        }
        public static bool IsBlocked(int? current,int? target){return current.HasValue&&target.HasValue&&current.Value<target.Value;}
        public static bool RequiresElevation(GameWindow target) {
            if(target==null)return false;uint pid;Native.GetWindowThreadProcessId(target.Handle,out pid);
            int? current=Integrity((uint)Process.GetCurrentProcess().Id),game=Integrity(pid);
            Diagnostics.Log("Input privilege current="+(current.HasValue?current.ToString():"unknown")+" target="+(game.HasValue?game.ToString():"unknown")+" pid="+pid);
            return IsBlocked(current,game);
        }
    }
}
