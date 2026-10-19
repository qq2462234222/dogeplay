using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace LumaKeys {
    public sealed class InstanceGuard : IDisposable {
        Mutex mutex;bool owns;
        public bool Acquired {get{return owns;}}
        public InstanceGuard(bool waitForRestart) {
            try {mutex=new Mutex(false,"Local\\LumaKeys.Desktop");try{owns=mutex.WaitOne(waitForRestart?10000:0);}catch(AbandonedMutexException){owns=true;}}
            catch(UnauthorizedAccessException){owns=false;}
            if(!owns) {
                foreach(var process in Process.GetProcessesByName("dogeplay"))using(process){try{if(process.Id!=Process.GetCurrentProcess().Id&&process.MainWindowHandle!=IntPtr.Zero){Native.TryActivate(new GameWindow{Handle=process.MainWindowHandle});break;}}catch{}}
                MessageBox.Show("dogeplay已经在运行，已尝试切换到现有窗口。请使用任务栏中已打开的dogeplay。","dogeplay",MessageBoxButton.OK,MessageBoxImage.Information);
            }
        }
        public void Dispose(){if(mutex!=null){if(owns)mutex.ReleaseMutex();mutex.Dispose();mutex=null;}}
    }
}
