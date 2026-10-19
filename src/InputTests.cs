using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace LumaKeys {
    public static class InputTests {
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        static void Activate(Window window) {
            IntPtr handle=new WindowInteropHelper(window).Handle;uint pid;
            uint thread=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid),current=GetCurrentThreadId();
            bool attached=thread!=current&&AttachThreadInput(current,thread,true);
            try{ShowWindow(handle,9);window.Activate();SetForegroundWindow(handle);window.Focus();}
            finally{if(attached)AttachThreadInput(current,thread,false);}
        }
        public static int Run() {
            var application=new Application();var log=new List<string>();int result=0;
            var textBox=new TextBox{Margin=new Thickness(20),FontSize=22,AcceptsReturn=true};InputMethod.SetIsInputMethodEnabled(textBox,false);
            var target=new Window{Title="LumaKeys input verification",Width=540,Height=200,WindowStartupLocation=WindowStartupLocation.CenterScreen,Content=textBox};
            var downs=new List<Key>();var ups=new List<Key>();var held=new HashSet<Key>();int maxHeld=0;
            target.PreviewKeyDown+=(s,e)=>{downs.Add(e.Key);held.Add(e.Key);maxHeld=Math.Max(maxHeld,held.Count);};target.PreviewKeyUp+=(s,e)=>{ups.Add(e.Key);held.Remove(e.Key);};
            Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);log.Add("PASS: "+message);};
            target.Loaded+=async(s,e)=>{
                var player=new Player();Window other=null;
                try {
                    await Task.Delay(300);Activate(target);textBox.Focus();Keyboard.Focus(textBox);await Task.Delay(300);
                    var game=new GameWindow{Handle=new WindowInteropHelper(target).Handle,Title=target.Title};var map=new Mapping(new Settings().Profiles[0]);
                    check(Native.GetForegroundWindow()==game.Handle,"test receiver is foreground");
                    var song=Notation.Parse("input","[1 3 5]:0.3 2:0.3",240);
                    await player.Start(song,map,0,false,false,game,0);await Task.Delay(100);
                    log.Add("Observed down="+string.Join(",",downs)+" up="+string.Join(",",ups)+" text="+textBox.Text);
                    check(downs.Contains(Key.A)&&downs.Contains(Key.D)&&downs.Contains(Key.G)&&downs.Contains(Key.S),"real SendInput scancodes and chord delivery");
                    check(maxHeld==3&&held.Count==0,"real window observed three chord keys held simultaneously then released");
                    check(textBox.Text.ToLowerInvariant().Contains("a")&&textBox.Text.ToLowerInvariant().Contains("s"),"real text input receives generated letters");
                    check(ups.Contains(Key.A)&&ups.Contains(Key.D)&&ups.Contains(Key.G)&&ups.Contains(Key.S),"normal completion releases keys");
                    downs.Clear();ups.Clear();song=Notation.Parse("cancel","1:16",60);
                    var task=player.Start(song,map,0,false,false,game,0);await Task.Delay(180);player.Stop();await task;await Task.Delay(80);
                    check(downs.Contains(Key.A)&&ups.Contains(Key.A)&&!player.Running,"stop during sustained note releases key");
                    downs.Clear();ups.Clear();task=player.Start(song,map,0,false,false,game,0);await Task.Delay(150);player.TogglePause();await Task.Delay(120);
                    check(ups.Contains(Key.A)&&player.Paused,"pause releases held key");player.TogglePause();await Task.Delay(120);player.Stop();await task;
                    check(downs.Count(k=>k==Key.A)==2,"resume restores sustained note");
                    downs.Clear();ups.Clear();task=player.Start(song,map,0,false,false,game,0);await Task.Delay(150);
                    other=new Window{Title="LumaKeys focus loss verification",Width=300,Height=140,Owner=target};other.Show();Activate(other);bool stopped=false;
                    try{await task;}catch(InvalidOperationException){stopped=true;}
                    check(stopped&&!player.Running,"focus loss stops playback");other.Close();other=null;Activate(target);textBox.Focus();await Task.Delay(100);
                    downs.Clear();task=player.Start(song,map,0,false,false,game,3);await Task.Delay(100);player.Stop();await task;
                    check(downs.Count==0,"countdown cancellation sends no keys");
                    await player.Start(Notation.Parse("preview","1:0.2",240),map,0,false,true,null,0);
                    check(!player.Running,"system MIDI preview opens and closes");
                }catch(Exception ex){result=1;log.Add("FAIL: "+ex);player.Stop();}finally{if(other!=null)other.Close();Directory.CreateDirectory(Path.Combine(Store.Root,"..","test-output"));File.WriteAllLines(Path.Combine(Store.Root,"..","test-output","input-tests.txt"),log);target.Close();}
            };
            application.Run(target);return result;
        }
    }
}
