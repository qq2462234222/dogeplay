using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LumaKeys {
    // A normal desktop overlay. No code or rendering is injected into the game.
    public partial class App {
        Window learningWindow;
        LearningScoreView learningView;
        DispatcherTimer learningTimer;
        TextBlock learningTitle,learningState,learningTime,learningKeys;
        Slider learningSeek;
        Button learningPlay,learningLock;
        bool learningRunning,learningLocked,learningSeeking,learningFollowing;
        double learningBeat,learningLastTime;
        readonly Stopwatch learningClock=new Stopwatch();
        IntPtr learningHandle;
        List<char> learningMapKeys=new List<char>();

        void OpenLearningOverlay() {
            if(learningWindow!=null){learningWindow.Topmost=true;return;}
            if(selected==null){Status("请先选择一首想练习的曲目。");return;}
            learningWindow=new Window{Title="dogeplay · 悬浮学习谱",Resources=window.Resources,Width=settings.LearningWidth>=600?settings.LearningWidth:980,Height=settings.LearningHeight>=280?settings.LearningHeight:350,MinWidth=600,MinHeight=280,
                WindowStyle=WindowStyle.None,AllowsTransparency=true,ResizeMode=ResizeMode.CanResizeWithGrip,Background=Brush("#211D32"),Foreground=Brush("#F8F4FF"),FontFamily=window.FontFamily,FontSize=12,Topmost=true,ShowInTaskbar=false,ShowActivated=false,Opacity=settings.LearningOpacity>=0.4&&settings.LearningOpacity<=1?settings.LearningOpacity:0.94};
            var area=SystemParameters.WorkArea;
            learningWindow.Left=settings.LearningWidth>=600?Math.Max(area.Left,Math.Min(area.Right-160,settings.LearningLeft)):area.Left+(area.Width-learningWindow.Width)/2;
            learningWindow.Top=settings.LearningWidth>=600?Math.Max(area.Top,Math.Min(area.Bottom-80,settings.LearningTop)):area.Top+55;
            var root=new DockPanel{Margin=new Thickness(16)};learningWindow.Content=root;
            var top=new DockPanel{Margin=new Thickness(0,0,0,10)};DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
            var close=LearningButton("关闭",()=>learningWindow.Close());DockPanel.SetDock(close,Dock.Right);top.Children.Add(close);
            learningLock=LearningButton("锁定穿透 · F7",()=>SetLearningLocked(!learningLocked));DockPanel.SetDock(learningLock,Dock.Right);top.Children.Add(learningLock);
            learningTitle=new TextBlock{Text="dogeplay · 学习谱",Foreground=Brush("#EEE7FF"),FontSize=15,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Cursor=Cursors.SizeAll};
            learningTitle.MouseLeftButtonDown+=(s,e)=>{if(!learningLocked&&e.ButtonState==MouseButtonState.Pressed)try{learningWindow.DragMove();}catch{}};top.Children.Add(learningTitle);
            var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            learningSeek=new Slider{Minimum=0,Maximum=100,IsMoveToPointEnabled=true,Margin=new Thickness(0,9,0,8)};Automation(learningSeek,"LearningSeek");bottom.Children.Add(learningSeek);
            learningSeek.ValueChanged+=(s,e)=>{if(learningSeeking||selected==null)return;double start=Player.StartBeat(CurrentScore,SkipIntro),target=start+(CurrentScore.Length-start)*learningSeek.Value/100;
                if(player.Running)player.SeekTo(target);else{learningBeat=target;learningView.Beat=target;learningView.InvalidateVisual();}UpdateLearningLabels();};
            var controls=new DockPanel();bottom.Children.Add(controls);
            var opacity=new Slider{Minimum=0.4,Maximum=1,Value=learningWindow.Opacity,Width=70,VerticalAlignment=VerticalAlignment.Center,ToolTip="谱窗透明度",Margin=new Thickness(8,0,0,0)};
            opacity.ValueChanged+=(s,e)=>{if(learningWindow!=null)learningWindow.Opacity=opacity.Value;};DockPanel.SetDock(opacity,Dock.Right);controls.Children.Add(opacity);
            learningTime=new TextBlock{Foreground=Brush("#C5B9E2"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(9,0,0,0)};DockPanel.SetDock(learningTime,Dock.Right);controls.Children.Add(learningTime);
            learningPlay=LearningButton("▶ 开始练习走谱",ToggleLearningPractice);Automation(learningPlay,"LearningPlay");DockPanel.SetDock(learningPlay,Dock.Left);controls.Children.Add(learningPlay);
            var reset=LearningButton("回到开头",()=>{double start=Player.StartBeat(CurrentScore,SkipIntro);if(player.Running)player.SeekTo(start);else{learningBeat=start;learningView.Beat=start;learningView.InvalidateVisual();UpdateLearningLabels();}});DockPanel.SetDock(reset,Dock.Left);controls.Children.Add(reset);
            learningKeys=new TextBlock{Foreground=Brush("#86E4BE"),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(8,0,0,0)};controls.Children.Add(learningKeys);
            learningState=new TextBlock{Foreground=Brush("#B5A6D0"),FontSize=11,Text="可拖动标题、缩放窗口；F7 锁定 / 解锁。独立走谱不发送按键。",Margin=new Thickness(0,8,0,0),TextWrapping=TextWrapping.Wrap};bottom.Children.Add(learningState);
            learningView=new LearningScoreView{MinHeight=90};Automation(learningView,"LearningScore");root.Children.Add(learningView);
            learningWindow.SourceInitialized+=(s,e)=>{
                learningHandle=new WindowInteropHelper(learningWindow).Handle;
                int style=GetWindowLong(learningHandle,-20);SetWindowLong(learningHandle,-20,style|0x08000000|0x80);
                HwndSource.FromHwnd(learningHandle).AddHook((IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
                if(!Native.RegisterHotKey(hotkeyHandle,91,0x4000,0x76))learningState.Text="F7 被占用，未启用穿透锁定；可拖动标题移动谱窗。";
                else learningHotkeyAvailable=true;
            };
            learningWindow.Closed+=(s,e)=>{
                learningTimer.Stop();learningRunning=false;learningClock.Stop();
                settings.LearningWidth=learningWindow.Width;settings.LearningHeight=learningWindow.Height;settings.LearningLeft=learningWindow.Left;settings.LearningTop=learningWindow.Top;settings.LearningOpacity=learningWindow.Opacity;
                Native.UnregisterHotKey(hotkeyHandle,91);learningHotkeyAvailable=false;learningLocked=false;learningHandle=IntPtr.Zero;learningWindow=null;learningView=null;
                C<Button>("LearningButton").Content="悬浮学习谱";try{SaveSettings();}catch(Exception ex){Diagnostics.Log("Learning settings: "+ex.Message);}
            };
            RefreshLearningScore();learningClock.Restart();learningLastTime=0;
            learningTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};learningTimer.Tick+=(s,e)=>TickLearningOverlay();learningTimer.Start();
            learningWindow.Show();C<Button>("LearningButton").Content="学习谱已置顶";
            Status("学习谱已置顶。F7 锁定 / 解锁鼠标穿透；无边框窗口或窗口模式下可覆盖游戏。");
        }
        bool learningHotkeyAvailable;
        Button LearningButton(string title,Action action) {var button=new Button{Content=title,Padding=new Thickness(10,6,10,6),Background=Brush("#3B3153"),Foreground=Brush("#F3EBFF"),Margin=new Thickness(0,0,7,0)};button.Click+=(s,e)=>{try{action();}catch(Exception ex){Error(ex);}};return button;}
        void SetLearningLocked(bool value) {
            if(learningWindow==null||learningHandle==IntPtr.Zero)return;
            if(value&&!learningHotkeyAvailable){learningState.Text="F7 被其他软件占用，无法启用穿透锁定；请先释放 F7。";return;}
            learningLocked=value;int style=GetWindowLong(learningHandle,-20);
            SetWindowLong(learningHandle,-20,value?style|0x20:style&~0x20);
            learningLock.Content=value?"已锁定 · F7 解锁":"锁定穿透 · F7";
            learningWindow.ResizeMode=value?ResizeMode.NoResize:ResizeMode.CanResizeWithGrip;
            learningWindow.IsHitTestVisible=!value;
        }
        void RefreshLearningScore() {
            if(learningView==null||selected==null||Profile==null)return;
            var score=CurrentScore;var map=new Mapping(Profile);int transpose=GameAdapt?Mapping.BestTranspose(score,map,Transpose,Fold,true):Transpose;
            learningView.Load(score,map,transpose,Fold,GameAdapt);learningMapKeys=map.Map.Values.Distinct().ToList();
            learningTitle.Text="dogeplay  /  "+selected.Title+"  ·  "+Profile.Name;
            learningBeat=player.Running?player.CurrentBeat:pendingStartBeat??Player.StartBeat(score,SkipIntro);learningView.Beat=learningBeat;
            learningRunning=false;learningFollowing=player.Running;learningView.InvalidateVisual();UpdateLearningLabels();
        }
        void SyncLearningProgress(double beat) {
            if(learningWindow==null)return;
            if(player.Running){learningRunning=false;learningFollowing=true;learningBeat=beat;}
            else if(learningFollowing){learningFollowing=false;learningBeat=Player.StartBeat(CurrentScore,SkipIntro);}
            else if(!learningRunning)learningBeat=beat;
            learningView.Beat=learningBeat;learningView.InvalidateVisual();UpdateLearningLabels();
        }
        void ToggleLearningPractice() {
            if(player.Running){player.TogglePause();UpdatePauseButton();return;}
            if(selected==null)return;
            if(learningBeat>=CurrentScore.Length)learningBeat=Player.StartBeat(CurrentScore,SkipIntro);
            learningRunning=!learningRunning;learningLastTime=learningClock.Elapsed.TotalSeconds;UpdateLearningLabels();
        }
        void TickLearningOverlay() {
            if(learningWindow==null||selected==null)return;
            double now=learningClock.Elapsed.TotalSeconds,delta=now-learningLastTime;learningLastTime=now;
            if(player.Running){learningRunning=false;learningFollowing=true;learningBeat=player.CurrentBeat;}
            else if(learningFollowing){learningFollowing=false;learningBeat=Player.StartBeat(CurrentScore,SkipIntro);}
            else if(learningRunning){learningBeat=Math.Min(CurrentScore.Length,learningBeat+Math.Max(0,delta)*selected.Bpm/60.0);if(learningBeat>=CurrentScore.Length)learningRunning=false;}
            var target=C<ComboBox>("WindowCombo").SelectedItem as GameWindow;
            var pressed=new HashSet<char>();
            if(target!=null&&Native.GetForegroundWindow()==target.Handle)foreach(char key in learningMapKeys)if((GetAsyncKeyState((int)Mapping.VirtualKey(key))&0x8000)!=0)pressed.Add(key);
            learningView.Pressed=pressed;learningView.Beat=learningBeat;learningView.InvalidateVisual();
            learningKeys.Text=pressed.Count==0?"按键反馈：进入所选游戏后显示":"当前按下："+string.Join(" + ",pressed.OrderBy(k=>k));UpdateLearningLabels();
        }
        void UpdateLearningLabels() {
            if(learningWindow==null||selected==null)return;
            double start=Player.StartBeat(CurrentScore,SkipIntro),length=Math.Max(0,CurrentScore.Length-start);
            learningSeeking=true;try{learningSeek.Value=length>0?Math.Max(0,Math.Min(100,(learningBeat-start)/length*100)):0;}finally{learningSeeking=false;}
            learningTime.Text=Time(Math.Max(0,learningBeat-start)*60/selected.Bpm)+" / "+Time(length*60/selected.Bpm)+"  ·  "+selected.Bpm+" BPM";
            learningPlay.Content=player.Running?(player.Paused?"▶ 继续演奏":"Ⅱ 暂停演奏"):learningRunning?"Ⅱ 暂停走谱":"▶ 开始练习走谱";
            if(learningHotkeyAvailable)learningState.Text=(player.Running?"跟随软件演奏高亮":learningRunning?"练习走谱中 · 仅显示提示，不发送按键":"手动学习 · 可拖动进度选择起点")+(learningLocked?"  ·  鼠标穿透已锁定，按 F7 解锁":"  ·  拖动标题移动，F7 锁定穿透")+"  ·  同列为和弦，音符为原谱，按键按当前游戏方案显示。";
        }
        [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr h,int index,int value);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    }

    public sealed class LearningChord {
        public double Beat,End;
        public int[] Pitches;
        public string[] Keys;
    }
    public sealed class LearningScoreView : FrameworkElement {
        public readonly List<LearningChord> Chords=new List<LearningChord>();
        public double Beat;
        public HashSet<char> Pressed=new HashSet<char>();
        static Brush Color(string value){return (Brush)new BrushConverter().ConvertFromString(value);}
        static readonly Brush Background=Color("#211D32"),Current=Color("#7657E8"),Upcoming=Color("#332B47"),White=Color("#FAF7FF"),Muted=Color("#B9ABCD"),Green=Color("#86E4BE");
        public void Load(Song song,Mapping map,int transpose,bool fold,bool adapt) {
            Chords.Clear();Chords.AddRange(song.Notes.GroupBy(n=>n.Beat).OrderBy(g=>g.Key).Select(g=>{var notes=g.GroupBy(n=>n.Midi).Select(n=>n.First()).OrderByDescending(n=>n.Midi).ToArray();return new LearningChord{Beat=g.Key,End=g.Max(n=>n.Beat+n.Duration),Pitches=notes.Select(n=>n.Midi).ToArray(),Keys=notes.Select(n=>{var p=map.ResolveGame(n.Midi+transpose,fold,adapt);return p.HasValue?map.Map[p.Value].ToString():"—";}).ToArray()};}));
            InvalidateVisual();
        }
        public int IndexAt(double beat) {int low=0,high=Chords.Count-1,index=-1;while(low<=high){int middle=(low+high)/2;if(Chords[middle].Beat<=beat){index=middle;low=middle+1;}else high=middle-1;}return index;}
        void Text(DrawingContext dc,string value,double size,Brush color,double x,double y,double maxWidth) {
            var text=new FormattedText(value,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,color,VisualTreeHelper.GetDpi(this).PixelsPerDip);text.MaxTextWidth=Math.Max(1,maxWidth);text.MaxTextHeight=size*1.6;text.Trimming=TextTrimming.CharacterEllipsis;dc.DrawText(text,new Point(x,y));
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);double width=ActualWidth,height=ActualHeight;dc.DrawRectangle(Background,null,new Rect(0,0,width,height));
            if(Chords.Count==0){Text(dc,"先选择一首曲目",18,Muted,12,20,width-24);return;}
            int current=IndexAt(Beat),first=Math.Max(0,current),count=Math.Max(3,(int)(width/108)),maxNotes=Math.Min(6,Chords.Skip(first).Take(count).Max(c=>c.Pitches.Length));
            double cell=Math.Min(140,(width-4)/count),line=Math.Max(16,Math.Min(28,(height-65)/Math.Max(1,maxNotes)));
            dc.PushClip(new RectangleGeometry(new Rect(0,0,width,height)));
            for(int i=0;i<count&&first+i<Chords.Count;i++) {
                var chord=Chords[first+i];double x=i*cell;bool active=first+i==current&&Beat<chord.End;
                dc.DrawRoundedRectangle(active?Current:Upcoming,null,new Rect(x+3,0,Math.Max(1,cell-8),Math.Max(1,height-3)),11,11);
                Text(dc,active?"正在弹奏":current<0&&i==0?"即将开始":"第 "+chord.Beat.ToString("0.##")+" 拍",10,active?White:Muted,x+13,8,cell-24);
                for(int n=0;n<Math.Min(6,chord.Pitches.Length);n++) {
                    double y=32+n*line;bool down=chord.Keys[n].Length==1&&Pressed.Contains(chord.Keys[n][0]);
                    Text(dc,Notation.Label(chord.Pitches[n]),Math.Min(21,line),White,x+13,y,cell*0.52);
                    Text(dc,chord.Keys[n],Math.Min(17,line),down?Green:active?White:Muted,x+cell*0.59,y+1,cell*0.35-6);
                }
                if(chord.Pitches.Length>6)Text(dc,"另 "+(chord.Pitches.Length-6)+" 音",10,Muted,x+13,height-35,cell-24);
                double duration=Math.Max(0.01,chord.End-chord.Beat),progress=active?Math.Max(0,Math.Min(1,(Beat-chord.Beat)/duration)):0;
                dc.DrawRoundedRectangle(Color("#534469"),null,new Rect(x+13,height-15,Math.Max(1,cell-30),3),1.5,1.5);
                if(progress>0)dc.DrawRoundedRectangle(Green,null,new Rect(x+13,height-15,Math.Max(1,(cell-30)*progress),3),1.5,1.5);
            }
            dc.Pop();
        }
    }
}
