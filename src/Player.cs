using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LumaKeys {
    public class GameWindow {
        public IntPtr Handle; public string Title; public string Process;
        public override string ToString() {return Title+"  ·  "+Process;}
    }
    public static class Native {
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hwnd,int command);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,INPUT[] input,int size);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint type);
        [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent,IntPtr child);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread,ref GUITHREADINFO info);
        [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO {
            public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public RECT CaretRect;
        }
        public static IntPtr BackgroundReceiver(IntPtr target,uint expectedPid) {
            uint pid;uint thread=GetWindowThreadProcessId(target,out pid);
            var info=new GUITHREADINFO{Size=(uint)Marshal.SizeOf(typeof(GUITHREADINFO))};
            // Some applications receive keyboard messages in a child control. Never target another process.
            if(pid==expectedPid&&GetGUIThreadInfo(thread,ref info)&&info.Focus!=IntPtr.Zero&&IsChild(target,info.Focus)) {
                uint childPid;GetWindowThreadProcessId(info.Focus,out childPid);
                if(childPid==expectedPid)return info.Focus;
            }
            return target;
        }
        static bool SendWindowMessage(IntPtr target,uint message,IntPtr wParam,IntPtr lParam) {
            if(target==IntPtr.Zero||!IsWindow(target))return false;
            IntPtr result;
            // SendMessageTimeout handles ordinary Win32 windows that need a synchronous
            // delivery. PostMessage remains a fallback for windows with a busy UI thread.
            bool delivered=SendMessageTimeout(target,message,wParam,lParam,0x0002u|0x0001u,80,out result)!=IntPtr.Zero;
            if(!delivered)delivered=PostMessage(target,message,wParam,lParam);
            return delivered;
        }
        public static void WindowKey(IntPtr root,uint expectedPid,char key,bool down) {
            uint actualPid;GetWindowThreadProcessId(root,out actualPid);
            if(!IsWindow(root)||actualPid!=expectedPid)throw new InvalidOperationException("目标游戏窗口已关闭或改变，后台发送已停止。");
            // Games can replace their focused child window while entering/leaving the
            // instrument screen. Resolve it for every event instead of retaining a stale
            // HWND captured at playback start.
            IntPtr target=BackgroundReceiver(root,expectedPid);
            uint vk=Mapping.VirtualKey(key);uint flags=1u|((MapVirtualKey(vk,0)&0xffu)<<16);
            if(!down)flags|=0xc0000000u;
            uint message=down?0x0100u:0x0101u;var wParam=new IntPtr(vk);var lParam=new IntPtr(unchecked((int)flags));
            bool delivered=SendWindowMessage(target,message,wParam,lParam);
            // If the focused child disappeared between GetGUIThreadInfo and delivery,
            // retry the stable top-level game window once.
            if(!delivered&&target!=root)delivered=SendWindowMessage(root,message,wParam,lParam);
            if(!delivered)throw new InvalidOperationException("后台窗口消息发送失败（错误 "+Marshal.GetLastWin32Error()+"）。请检查目标窗口和运行权限。");
        }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION data; }
        [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public KEYBOARD keyboard; [FieldOffset(0)] public MOUSE mouse; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBOARD { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSE {public int dx,dy;public uint mouseData,flags,time;public UIntPtr extra;}
        public static void Key(char key,bool down) {
            Keys(new[]{new KeyStroke(key,down)});
        }
        public static void Keys(IList<KeyStroke> keys) {
            if(keys.Count==0)return;
            INPUT[] inputs=keys.Select(k=>new INPUT{type=1,data=new UNION{keyboard=new KEYBOARD{scan=(ushort)MapVirtualKey(Mapping.VirtualKey(k.Key),0),flags=(uint)(0x0008|(k.Down?0:0x0002))}}}).ToArray();
            uint sent=SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(INPUT)));
            if(sent!=inputs.Length)throw new InvalidOperationException("Windows 仅接受了 "+sent+" / "+inputs.Length+" 个按键事件。请检查游戏与软件的运行权限是否一致。");
        }
        [DllImport("winmm.dll")] public static extern uint midiOutOpen(out IntPtr handle,uint device,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("winmm.dll")] public static extern uint midiOutShortMsg(IntPtr handle,uint message);
        [DllImport("winmm.dll")] public static extern uint midiOutReset(IntPtr handle);
        [DllImport("winmm.dll")] public static extern uint midiOutClose(IntPtr handle);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint period);
        public static void MidiMessage(IntPtr handle,uint message) {
            uint error=midiOutShortMsg(handle,message);
            if(error!=0)throw new InvalidOperationException("试听音频输出失败（错误 "+error+"）。请检查 Windows 默认播放设备和音量混合器。");
        }
        public static bool TryActivate(GameWindow target) {
            if(target==null||!IsWindow(target.Handle))return false;
            if(IsIconic(target.Handle))ShowWindowAsync(target.Handle,9);
            return SetForegroundWindow(target.Handle);
        }
        public static List<GameWindow> Windows() {
            var result=new List<GameWindow>();
            foreach(var p in System.Diagnostics.Process.GetProcesses()) using(p) {try { if(p.Id!=System.Diagnostics.Process.GetCurrentProcess().Id && p.MainWindowHandle!=IntPtr.Zero&&!string.IsNullOrWhiteSpace(p.MainWindowTitle)) result.Add(new GameWindow {Handle=p.MainWindowHandle,Title=p.MainWindowTitle,Process=p.ProcessName});}catch{} }
            return result.OrderBy(p=>p.Title).ToList();
        }
    }
    class PlayEvent { public double Beat;public int Pitch;public bool Down; }
    public class KeyStroke {public char Key;public bool Down;public KeyStroke(char key,bool down){Key=key;Down=down;}}
    public interface IPlaybackIo {
        bool IsWindow(IntPtr handle);
        IntPtr Foreground();
        uint ProcessId(IntPtr handle);
        void Key(char key,bool down);
        void Keys(IList<KeyStroke> keys);
    }
    public class NativePlaybackIo : IPlaybackIo {
        public bool IsWindow(IntPtr handle){return Native.IsWindow(handle);}
        public IntPtr Foreground(){return Native.GetForegroundWindow();}
        public uint ProcessId(IntPtr handle){uint pid;Native.GetWindowThreadProcessId(handle,out pid);return pid;}
        public void Key(char key,bool down){Native.Key(key,down);}
        public void Keys(IList<KeyStroke> keys){Native.Keys(keys);}
    }
    public class Player {
        readonly IPlaybackIo io;
        public Player() : this(new NativePlaybackIo()) {}
        public Player(IPlaybackIo playbackIo){io=playbackIo;}
        CancellationTokenSource cancel;volatile bool paused;int seekBeats;readonly object seekLock=new object();double? seekTarget;
        public bool Running { get {return cancel!=null;} }
        public bool Paused {get{return paused;}}
        public int NotesStarted {get;private set;}
        public bool Completed {get;private set;}
        public double CurrentBeat {get;private set;}
        public Action<string> Status;
        public Action<double> Progress;
        public Action<string> ActiveKeys;
        public void Stop() {var c=cancel;if(c!=null)c.Cancel();}
        public void Seek(int beats) {if(Running&&beats!=0)Interlocked.Add(ref seekBeats,beats);}
        public void SeekTo(double beat) {if(Running){lock(seekLock)seekTarget=beat;}}
        public void TogglePause() {if(Running) {paused=!paused;Tell(paused?"已暂停 · F9 继续":"正在演奏");if(Progress!=null)Progress(CurrentBeat);}}
        void Tell(string s) {if(Status!=null) Status(s);}
        static void WaitUntilUtc(DateTimeOffset target,CancellationToken token) {
            DateTime targetUtc=target.UtcDateTime;
            while(true) {
                token.ThrowIfCancellationRequested();
                double remaining=(targetUtc-DateTime.UtcNow).TotalMilliseconds;
                if(remaining<=0)return;
                if(remaining>250)token.WaitHandle.WaitOne((int)Math.Min(1000,remaining-150));
                else if(remaining>3)token.WaitHandle.WaitOne((int)Math.Max(1,remaining-2));
                else Thread.SpinWait(100);
            }
        }
        public static double StartBeat(Song song,bool skipIntro) {return skipIntro&&song.Notes.Count>0?song.Notes.Min(n=>n.Beat):0;}
        public async Task Start(Song song,Mapping map,int transpose,bool fold,bool preview,GameWindow target,int countdown,bool skipIntro=true,GameTimingSettings timing=null,bool adaptGame=true,double? initialBeat=null,bool background=false,int previewProgram=0,DateTimeOffset? scheduledStart=null) {
            if(Running) return;
            Importers.Validate(song);
            if(!preview&&(target==null||!io.IsWindow(target.Handle))) throw new InvalidOperationException("请先选择游戏窗口，并进入游戏中的乐器演奏界面。");
            uint targetPid=target==null?0:io.ProcessId(target.Handle);
            Action<char,bool> outputKey=(key,down)=>{if(background&&!preview)Native.WindowKey(target.Handle,targetPid,key,down);else io.Key(key,down);};
            Action<IList<KeyStroke>> outputKeys=keys=>{if(background&&!preview){foreach(var key in keys)outputKey(key.Key,key.Down);}else io.Keys(keys);};
            double startBeat=StartBeat(song,skipIntro);if(initialBeat.HasValue)startBeat=Math.Max(startBeat,Math.Min(song.Length,initialBeat.Value));
            int gameTranspose=!preview&&adaptGame?Mapping.BestTranspose(song,map,transpose,fold,true):transpose;
            GameTimingPlan plan=!preview?GameTimingPlan.Create(song,map,gameTranspose,fold,timing??new GameTimingSettings{Enabled=false},adaptGame):null;
            var events=new List<PlayEvent>();
            foreach(var n in plan==null?song.Notes:plan.Notes) {
                int shifted=n.Midi+gameTranspose; int? pitch=plan!=null?(int?)n.Midi:preview?(shifted>=0&&shifted<=127?(int?)shifted:null):map.ResolveGame(shifted,fold,adaptGame);
                if(pitch==null) continue;
                events.Add(new PlayEvent {Beat=n.Beat-startBeat,Pitch=pitch.Value,Down=true});
                events.Add(new PlayEvent {Beat=n.Beat-startBeat+(plan==null?Math.Max(0.03,n.Duration*0.85):n.Duration),Pitch=pitch.Value,Down=false});
            }
            if(events.Count==0) throw new InvalidOperationException("当前键位无法演奏这些音符，请调整移调或音域。");
            events=events.Where(e=>e.Beat>=-0.000001).OrderBy(e=>e.Beat).ThenBy(e=>e.Down?1:0).ToList();
            if(events.Count==0)throw new InvalidOperationException("选定的起始位置后没有可演奏音符。");
            cancel=new CancellationTokenSource();var local=cancel;paused=false;Completed=false;Interlocked.Exchange(ref seekBeats,0);lock(seekLock)seekTarget=null;NotesStarted=0;CurrentBeat=startBeat;
            try {
                if(!preview&& !scheduledStart.HasValue) for(int i=countdown;i>0;i--) {
                    while(paused){Tell("倒计时已暂停 · F9 继续 / F10 取消");await Task.Delay(50,local.Token);}
                    Tell(i+(background?" 秒后开始后台发送，请让游戏停留在乐器界面":" 秒后开始，请切换到游戏")+" · F9 暂停 / F10 取消");
                    int waited=0;
                    while(waited<1000){
                        while(paused){Tell("倒计时已暂停 · F9 继续 / F10 取消");await Task.Delay(50,local.Token);}
                        int step=Math.Min(50,1000-waited);await Task.Delay(step,local.Token);waited+=step;
                    }
                }
                if(!preview&&!background) {
                    // A blocked foreground switch must not silently consume the countdown.
                    for(int i=15;io.Foreground()!=target.Handle&&i>0;i--) {
                        if(!io.IsWindow(target.Handle)||io.ProcessId(target.Handle)!=targetPid)throw new InvalidOperationException("目标游戏窗口已关闭，请刷新后重新选择。");
                        Tell("等待切回所选游戏窗口（"+i+" 秒）· F10 取消");
                        for(int j=0;j<10&&io.Foreground()!=target.Handle;j++)await Task.Delay(100,local.Token);
                    }
                    if(io.Foreground()!=target.Handle)throw new InvalidOperationException("没有切回所选的游戏窗口，尚未发送按键。请进入乐器界面后按 F8 开始。");
                }
                await Task.Factory.StartNew(()=> {
                    IntPtr midi=IntPtr.Zero;var held=new Dictionary<int,int>();
                    bool timerResolution=false;
                    Action<int,bool> send=(pitch,down)=> {if(preview) Native.MidiMessage(midi,(uint)((down?0x90:0x80)|(pitch<<8)|((down?105:0)<<16))); else outputKey(map.Map[pitch],down);};
                    Action<bool> sendHeld=down=>{if(preview){foreach(int p in held.Keys)send(p,down);}else outputKeys(held.Keys.Select(p=>new KeyStroke(map.Map[p],down)).ToList());};
                    Action<bool> showHeld=visible=>{if(ActiveKeys!=null)ActiveKeys(!visible?"":string.Join(" + ",held.Keys.OrderBy(p=>p).Select(p=>preview?Notation.Label(p):map.Map[p].ToString())));};
                    try {
                        timerResolution=Native.timeBeginPeriod(1)==0;
                        if(preview) {
                            uint error=Native.midiOutOpen(out midi,unchecked((uint)-1),IntPtr.Zero,IntPtr.Zero,0);
                            if(error!=0)throw new InvalidOperationException("无法打开试听音频设备（错误 "+error+"）。请检查 Windows 默认音频设备。");
                            Native.MidiMessage(midi,0xC0u|((uint)Math.Max(0,Math.Min(127,previewProgram))<<8));
                            Native.MidiMessage(midi,0x007F07B0); // Channel volume.
                            Native.MidiMessage(midi,0x007F0BB0); // Expression.
                        }
                        if(scheduledStart.HasValue) {
                            if(scheduledStart.Value<=DateTimeOffset.UtcNow)throw new InvalidOperationException("设定的北京时间已到或已过，请重新设置未来时刻。");
                            Tell("等待北京时间 "+scheduledStart.Value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss.fff")+" 准时开始 · F10 取消");
                            WaitUntilUtc(scheduledStart.Value,local.Token);
                        }
                        Tell(preview?"正在试听 · 不发送游戏按键":background?"正在向后台窗口发送消息："+(target==null?"未选择窗口":target.Title)+" · 游戏是否接收需实测 · F9 暂停 / F10 停止":"正在自动弹奏 · 游戏适配移调 "+(gameTranspose>=0?"+":"")+gameTranspose+" 半音 · F9 暂停 / F10 停止");
                        var clock=Stopwatch.StartNew();double previous=0,elapsed=0,lastProgress=-100;int index=0;bool wasPaused=false;
                        while(index<events.Count) {
                            local.Token.ThrowIfCancellationRequested();
                            if(!preview) {
                                if(!io.IsWindow(target.Handle)||io.ProcessId(target.Handle)!=targetPid)throw new InvalidOperationException("目标游戏窗口已关闭或进程已改变，演奏已停止。");
                                if(!background&&io.Foreground()!=target.Handle&&!paused){paused=true;Tell("游戏窗口失去焦点，已自动暂停 · 切回游戏后按 F9 继续");}
                            }
                            double now=clock.Elapsed.TotalMilliseconds,delta=now-previous;previous=now;
                            int seek=Interlocked.Exchange(ref seekBeats,0);
                            double? requestedTarget=null;lock(seekLock){requestedTarget=seekTarget;seekTarget=null;}
                            if(seek!=0||requestedTarget.HasValue) {
                                double targetBeat=Math.Max(startBeat,Math.Min(song.Length,requestedTarget.HasValue?requestedTarget.Value:CurrentBeat+seek));
                                elapsed=Math.Max(0,(targetBeat-startBeat)*60000.0/song.Bpm);
                                index=events.FindIndex(e=>e.Beat>=targetBeat-startBeat-0.000001);if(index<0)index=events.Count;
                                foreach(int pitch in held.Keys.ToList()){try{send(pitch,false);}catch{} }held.Clear();showHeld(false);
                                CurrentBeat=targetBeat;if(Progress!=null)Progress(targetBeat);Tell("已跳到 "+TimeSpan.FromSeconds((targetBeat-startBeat)*60.0/song.Bpm).ToString(@"mm\:ss")+(paused?" · 已暂停":""));wasPaused=false;continue;
                            }
                            if(paused) {
                                if(!wasPaused) {sendHeld(false);showHeld(false);wasPaused=true;}
                                local.Token.WaitHandle.WaitOne(10);continue;
                            }
                            if(wasPaused) {sendHeld(true);showHeld(true);wasPaused=false;delta=0;}
                            elapsed+=delta;double beat=elapsed*song.Bpm/60000.0;CurrentBeat=beat+startBeat;
                            while(index<events.Count&&events[index].Beat<=beat) {
                                double onset=events[index].Beat;var batch=new List<KeyStroke>();
                                // One SendInput call per score onset keeps every chord key in the
                                // same ordered Windows input batch, including simultaneous releases.
                                do {
                                    var e=events[index++];int count;held.TryGetValue(e.Pitch,out count);
                                    if(e.Down) {if(count==0){if(preview)send(e.Pitch,true);else batch.Add(new KeyStroke(map.Map[e.Pitch],true));NotesStarted++;}held[e.Pitch]=count+1;}
                                    else if(count>0) {if(count==1){if(preview)send(e.Pitch,false);else batch.Add(new KeyStroke(map.Map[e.Pitch],false));held.Remove(e.Pitch);}else held[e.Pitch]=count-1;}
                                }while(index<events.Count&&Math.Abs(events[index].Beat-onset)<0.000001);
                                if(!preview&&batch.Count>0){try{outputKeys(batch);}catch{foreach(var key in batch){try{outputKey(key.Key,false);}catch{}}throw;}}
                                showHeld(true);
                            }
                            if(Progress!=null&&elapsed-lastProgress>=33){Progress(beat+startBeat);lastProgress=elapsed;}
                            if(index<events.Count) {
                                // Absolute score deadlines prevent cumulative tempo drift.
                                // A dedicated thread avoids thread-pool continuation delays.
                                double remaining=events[index].Beat*60000.0/song.Bpm-elapsed-(clock.Elapsed.TotalMilliseconds-previous);
                                if(remaining>2)local.Token.WaitHandle.WaitOne((int)Math.Min(10,Math.Max(1,remaining-1)));
                                else if(remaining>0)Thread.SpinWait(80);
                            }
                        }
                        Completed=true;Tell(background&&!preview?"后台消息发送结束 · 无法确认游戏是否接收":preview?"试听完成":"演奏完成");
                    } finally {
                        foreach(int pitch in held.Keys.ToList()) {try{send(pitch,false);}catch{}}
                        if(midi!=IntPtr.Zero) {Native.midiOutReset(midi);Native.midiOutClose(midi);}
                        if(timerResolution)Native.timeEndPeriod(1);
                    }
                },local.Token,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            }catch(OperationCanceledException) {Tell("已停止 · 所有按键已释放");}
            finally {cancel=null;paused=false;CurrentBeat=0;local.Dispose();if(ActiveKeys!=null)ActiveKeys("");if(Progress!=null)Progress(0);}
        }
    }
}
