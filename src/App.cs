using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LumaKeys {
    public partial class App {
        Window window; Settings settings;List<Song> songs; Song selected;Player player=new Player();
        bool ready,closing,testing,updatingProgress; double? pendingStartBeat; int noteHighlight=-1;List<Border> tiles=new List<Border>();List<double> tileBeats=new List<double>();
        UpdateAnnouncement latestAnnouncement;
        bool nonInteractiveStartup,checkingUpdates,installingUpdate,windowClosed,startupLoaded,onboardingOpen;
        T C<T>(string name) where T:FrameworkElement {return (T)window.FindName(name);}
        void InstallScheduleControls() {
            // The controls are declared in MainWindow.xaml near the top of the settings panel
            // so the scheduling option remains visible on compact windows.
        }
        Grid FindMainContentGrid(DependencyObject root) {
            if(root==null)return null;
            var grid=root as Grid;if(grid!=null&&Grid.GetRow(grid)==2&&grid.ColumnDefinitions.Count==3)return grid;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindMainContentGrid(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}
            return null;
        }
        void AdjustWindowLayout() {
            if(window==null)return;
            var grid=FindMainContentGrid(window.Content as DependencyObject);if(grid==null)return;
            bool compact=window.ActualWidth<1120;
            bool narrow=window.ActualWidth<920;
            grid.ColumnDefinitions[0].Width=new GridLength(compact?0:232);
            grid.ColumnDefinitions[2].Width=new GridLength(narrow?244:compact?268:304);
            if(grid.Children.Count>0)grid.Children[0].Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            if(grid.Children.Count>1){var library=grid.Children[1] as FrameworkElement;if(library!=null)library.Margin=new Thickness(compact?0:14,0,compact?0:14,0);}
            var search=C<TextBox>("SearchBox");var header=search.Parent as Grid;
            if(header!=null&&header.ColumnDefinitions.Count==3){
                header.ColumnDefinitions[0].Width=new GridLength(compact?170:220);
                search.Width=compact?170:360;
                if(header.Children.Count>2){var actions=header.Children[2] as StackPanel;if(actions!=null&&actions.Children.Count>0)actions.Children[0].Visibility=compact?Visibility.Collapsed:Visibility.Visible;}
            }
        }
        void Click(string name,Action action) { C<Button>(name).Click+=(s,e)=>{try{action();}catch(Exception ex){Error(ex);}}; }
        string DisplayAppPath(string path) {
            if(string.IsNullOrWhiteSpace(path))return "";
            try {
                string full=Path.GetFullPath(path),root=Path.GetFullPath(Store.Root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                if(full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return full.Substring(root.Length+1).Replace('\\','/');
            }catch{}
            return Path.GetFileName(path);
        }
        void Status(string text) {Diagnostics.Log(text);if(!window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke(new Action(()=>{C<TextBlock>("StatusText").Text="●  "+text;C<TextBlock>("PlaybackStatus").Text=text;UpdatePauseButton();}));}
        void Error(Exception ex) {Diagnostics.Log(ex.ToString());Status(ex.Message);if(!testing)MessageBox.Show(window,ex.Message,"dogeplay · 提示",MessageBoxButton.OK,MessageBoxImage.Information);}
        [STAThread] public static int Main(string[] args) {
            InstanceGuard instance=null;
            try {
                if(args.Contains("--apply-update")) return UpdateClient.ApplyFromArgs(args);
                if(args.Contains("--self-test")) return SelfTests.Run();
                if(args.Contains("--input-test")) return InputTests.Run();
                if(args.Contains("--playback-test")) return PlaybackTests.Run();
                if(args.Contains("--catalog-test")) return CatalogTests.Run();
                if(!args.Any(a=>a.StartsWith("--"))||args.Contains("--elevated-restart"))if(Branding.MigrateLegacyExecutable(args))return 0;
                if(!args.Contains("--ui-playback-test")&&!args.Contains("--screenshot")&&!args.Contains("--compact-screenshot")) {instance=new InstanceGuard(args.Contains("--elevated-restart"));if(!instance.Acquired)return 0;}
                var application=new Application();var app=new App();app.nonInteractiveStartup=args.Contains("--ui-playback-test")||args.Contains("--screenshot")||args.Contains("--compact-screenshot");app.Initialize();
                if(args.Contains("--ui-playback-test"))return app.RunUiPlaybackTests(application);
                if(args.Contains("--compact-screenshot")){app.window.Width=900;app.window.Height=600;}
                if(args.Contains("--screenshot")||args.Contains("--compact-screenshot")) app.window.Loaded+=(s,e)=>app.window.Dispatcher.BeginInvoke(new Action(()=>{
                    app.window.UpdateLayout();var visual=(FrameworkElement)app.window.Content;var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));Directory.CreateDirectory(Path.Combine(Store.Root,"..","test-output"));using(var file=File.Create(Path.Combine(Store.Root,"..","test-output",args.Contains("--compact-screenshot")?"interface-compact.png":"interface.png")))png.Save(file);app.window.Close();}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                application.Run(app.window);return 0;
            }catch(Exception ex) {File.WriteAllText(Path.Combine(Store.Root,"error.log"),ex.ToString());if(!args.Contains("--self-test"))MessageBox.Show(ex.Message,"dogeplay启动失败");return 1;}
            finally {if(instance!=null)instance.Dispose();}
        }
        void Initialize() {
            window=EmbeddedAssets.LoadInterface();
            var workArea=SystemParameters.WorkArea;
            window.MinWidth=820;window.MinHeight=540;
            window.MaxWidth=Math.Max(window.MinWidth,workArea.Width);window.MaxHeight=Math.Max(window.MinHeight,workArea.Height);
            window.Width=Math.Min(1360,window.MaxWidth);window.Height=Math.Min(860,window.MaxHeight);
            window.WindowStartupLocation=WindowStartupLocation.CenterScreen;
            InstallScheduleControls();
            window.SizeChanged+=(s,e)=>AdjustWindowLayout();
            window.Loaded+=(s,e)=>AdjustWindowLayout();
            string icon=Path.Combine(Store.Root,"assets","dogeplay.ico");if(File.Exists(icon))window.Icon=BitmapFrame.Create(new Uri(icon));
            try {
                foreach(string doc in new[]{"LICENSE","README.md","THIRD_PARTY_NOTICES.md"}) {
                    string current=Path.Combine(Store.Root,"assets","release",doc);
                    if(File.Exists(current))File.Copy(current,Path.Combine(Store.Root,doc),true);
                }
                string legacyInterface=Path.Combine(Store.Root,"MainWindow.xaml");
                if(File.Exists(legacyInterface))File.Delete(legacyInterface);
            }catch(Exception ex){Diagnostics.Log("Release document sync: "+ex.Message);}
            Directory.CreateDirectory(Store.Data);
            settings=LoadOrDefault<Settings>("settings.json",()=>new Settings());
            songs=LoadOrDefault<List<Song>>("library.json",()=>Store.Demos());
            foreach(var song in songs)if(song.Source=="弦间示例"||song.Source=="弦间编配 · 多声部示例")song.Source=song.Source.Replace("弦间","dogeplay");
            if(settings.Profiles==null||settings.Profiles.Count==0)settings=new Settings();
            try{foreach(var profile in settings.Profiles)new Mapping(profile);}catch{settings.Profiles=new Settings().Profiles;Status("已恢复无效的游戏键位设置。");}
            settings.EnsureNarakaMaster();
            settings.EnsureNarakaInstruments();
            foreach(var profile in settings.Profiles){try{if(profile.Timing==null)profile.Timing=new GameTimingSettings();profile.Timing.Validate();}catch{profile.Timing=new GameTimingSettings();}}
            foreach(var song in songs.ToList()) {try{Importers.Validate(song);}catch{songs.Remove(song);}}
            StaffEngine.Configure(settings);
            string sample=Path.Combine(Store.Root,"assets","openscore","Just_for_Today.mxl");
            if(!settings.BundledSongsInitialized&&File.Exists(sample)&&!songs.Any(s=>s.SourceUrl!=null&&s.SourceUrl.Contains("lc6583477"))) {
                try {var song=Importers.MusicXml(sample,true);song.Title="Just for Today · OpenScore";song.Source="OpenScore · CC0";song.License="CC0-1.0";song.SourceUrl="https://github.com/OpenScore/Lieder/blob/main/scores/Abbott%2C_Jane_Bingham/_/Just_for_Today/lc6583477.mxl";songs.Add(song);}catch(Exception ex){Status("开源示例载入失败："+ex.Message);}
            }
            UpgradeBundledSongs(sample);
            settings.BundledSongsInitialized=true;
            C<ComboBox>("GameCombo").ItemsSource=settings.Profiles;
            C<ComboBox>("GameCombo").SelectedItem=settings.Profiles.FirstOrDefault(p=>p.Name==settings.SelectedGame)??settings.Profiles[0];
            C<ComboBox>("TransposeCombo").ItemsSource=Enumerable.Range(-24,49).ToList();C<ComboBox>("TransposeCombo").SelectedItem=0;
            C<ComboBox>("CountdownCombo").ItemsSource=new[]{0,3,5,8,10,15};C<ComboBox>("CountdownCombo").IsEditable=true;C<ComboBox>("CountdownCombo").SelectedItem=5;C<ComboBox>("CountdownCombo").Text="5";
            C<TextBox>("ScheduledStartBox").ToolTip="固定按北京时间（UTC+8）解析，格式：2026-10-18 21:30:00.000；留空则使用普通倒计时。跨设备同步前请先同步 Windows 系统时间。";
            C<ComboBox>("PlaybackModeCombo").ItemsSource=new[]{"播放当前曲目","顺序播放队列","单曲循环","列表循环","随机播放"};C<ComboBox>("PlaybackModeCombo").SelectedIndex=Math.Max(0,Math.Min(4,settings.PlaybackModeIndex));C<ComboBox>("PlaybackModeCombo").SelectionChanged+=(s,e)=>{if(ready){settings.PlaybackModeIndex=C<ComboBox>("PlaybackModeCombo").SelectedIndex;SaveSettings();}};
            InitializeInstrumentControls();
            InitializeTimingControls();
            C<ComboBox>("TransposeCombo").SelectionChanged+=(s,e)=>{if(ready)RefreshSong();};
            C<CheckBox>("FoldCheck").Checked+=(s,e)=>{if(ready)RefreshSong();};C<CheckBox>("FoldCheck").Unchecked+=(s,e)=>{if(ready)RefreshSong();};
            C<CheckBox>("SkipIntroCheck").Checked+=(s,e)=>{if(ready)RefreshSong();};C<CheckBox>("SkipIntroCheck").Unchecked+=(s,e)=>{if(ready)RefreshSong();};
            C<CheckBox>("AllPartsCheck").Checked+=(s,e)=>{if(ready)RefreshSong();};C<CheckBox>("AllPartsCheck").Unchecked+=(s,e)=>{if(ready)RefreshSong();};
            C<CheckBox>("GameAdaptCheck").Checked+=(s,e)=>{if(ready)RefreshSong();};C<CheckBox>("GameAdaptCheck").Unchecked+=(s,e)=>{if(ready)RefreshSong();};
            C<ComboBox>("WindowCombo").SelectionChanged+=(s,e)=>{if(ready){RefreshInputAccess();RefreshBackgroundHint();}};
            C<Slider>("ProgressBar").ValueChanged+=(s,e)=>{if(!ready||updatingProgress||selected==null)return;var score=CurrentScore;double start=Player.StartBeat(score,SkipIntro),length=Math.Max(0,score.Length-start),target=start+length*C<Slider>("ProgressBar").Value/100.0;pendingStartBeat=target;if(player.Running)player.SeekTo(target);else UpdateProgress(target);};
            C<ListBox>("SongList").SelectionChanged+=(s,e)=>{if(refreshingLibrary||!ready)return;UpdateSelectionHint();if(!sessionActive&&C<ListBox>("SongList").SelectedItems.Count==1){SelectTrack((Song)C<ListBox>("SongList").SelectedItem);SaveSettings();}};
            C<TextBox>("SearchBox").TextChanged+=(s,e)=>RefreshLibrary();
            Click("ImportButton",ImportDialog);Click("SideImport",ImportDialog);Click("NewButton",()=>TextEditor(null,null));Click("RenameButton",()=>{if(SelectedSongs().Count!=1){Status("重命名时请选择一首曲目。");return;}SelectTrack(SelectedSongs()[0]);RenameSelectedSong();});Click("DeleteButton",DeleteSelectedSongs);
            Click("PreviewButton",()=>Play(true));Click("PlayButton",()=>Play(false));Click("StopButton",StopPlayback);Click("PauseButton",()=>{player.TogglePause();UpdatePauseButton();});
            Click("EditButton",EditSong);Click("KeymapButton",KeymapDialog);Click("RefreshWindows",RefreshWindows);Click("ExportButton",ExportSelectedSongs);Click("SideOpen",OpenCatalog);Click("HelpButton",ShowHelp);Click("CheckUpdateButton",CheckForUpdates);
            Click("AnnouncementButton",ShowAnnouncement);
            Click("LearningButton",OpenLearningOverlay);
            Click("ScheduledStartNowButton",()=>{C<TextBox>("ScheduledStartBox").Text=DateTimeOffset.UtcNow.AddSeconds(10).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss.fff");Status("已填入北京时间 +10 秒；请将相同时间填到所有演奏设备。系统时钟未同步时，各设备启动时刻也会有偏差。");});
            window.Loaded+=(s,e)=>LoadStartupExperience();
            window.Closed+=(s,e)=>windowClosed=true;
            window.Closed+=(s,e)=>{if(learningWindow!=null)learningWindow.Close();};
            InitializeMusicLibrary();
            Click("AdminButton",RestartAsAdministrator);Click("KeyboardTestButton",KeyboardTestDialog);
            window.Title+=InputAccess.IsAdministrator?" · 管理员":" · 普通权限";
            player.Status=Status;player.Progress=beat=>window.Dispatcher.BeginInvoke(new Action(()=>UpdateProgress(beat)));
            player.ActiveKeys=keys=>window.Dispatcher.BeginInvoke(new Action(()=>C<TextBlock>("ActiveKeysText").Text=keys.Length==0?"当前按键：—":"当前按键："+keys));
            window.SourceInitialized+=(s,e)=>{
                IntPtr handle=new WindowInteropHelper(window).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);
                hotkeyHandle=handle;var failed=new List<string>();for(int i=0;i<3;i++)if(!Native.RegisterHotKey(handle,80+i,0x4000,(uint)(0x77+i)))failed.Add("F"+(8+i));
                if(failed.Count>0) C<TextBlock>("HotkeyStatus").Text="快捷键被占用："+string.Join(" / ",failed)+"，请使用按钮";
            };
            window.Closing+=(s,e)=>{if(sessionActive||player.Running){e.Cancel=true;if(!closing){closing=true;StopPlayback();CloseWhenStopped();}return;}CapturePerformancePreferences(false);PersistTempo(false);Save();UnregisterSeekHotkeys();IntPtr handle=new WindowInteropHelper(window).Handle;for(int i=0;i<3;i++)Native.UnregisterHotKey(handle,80+i);};
            ready=true;RefreshLibrary();SelectTrack(songs.FirstOrDefault(s=>s.Id==settings.SelectedSongId)??songs.FirstOrDefault(s=>s.Source=="dogeplay编配 · 多声部示例")??songs.FirstOrDefault());C<Button>("PauseButton").IsEnabled=false;C<Button>("StopButton").IsEnabled=false;RefreshWindows();RefreshInputAccess();Save();
        }
        async void CloseWhenStopped() {while(player.Running||sessionActive)await Task.Delay(30);window.Close();}
        T LoadOrDefault<T>(string file,Func<T> fallback) {
            string path=Path.Combine(Store.Data,file);if(!File.Exists(path))return fallback();
            try {var value=Store.Read<T>(path);if(value==null)throw new FormatException("空文件");return value;}catch(Exception ex){string backup=path+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss");File.Copy(path,backup,true);Status("读取失败，已备份原文件："+ex.Message);return fallback();}
        }
        IntPtr hotkeyHandle=IntPtr.Zero;readonly int[] seekDeltas={16,-16,-4,4,16,-16,-4,4};
        void RegisterSeekHotkeys() {
            if(hotkeyHandle==IntPtr.Zero)return;var keys=new uint[]{0x26,0x28,0x25,0x27,0x68,0x62,0x64,0x66};var failed=new List<string>();
            for(int i=0;i<keys.Length;i++)if(!Native.RegisterHotKey(hotkeyHandle,83+i,0x4000,keys[i]))failed.Add(i<4?new[]{"上","下","左","右"}[i]:"小键盘"+(new[]{"8","2","4","6"}[i-4]));
            C<TextBlock>("HotkeyStatus").Text=failed.Count==0?"F8 开始 · F9 暂停 · F10 停止 · 小键盘方向键调进度":"小键盘调进度部分被占用："+string.Join(" / ",failed);
        }
        void UnregisterSeekHotkeys() {if(hotkeyHandle==IntPtr.Zero)return;for(int i=0;i<8;i++)Native.UnregisterHotKey(hotkeyHandle,83+i);}
        IntPtr Hook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled) {
            if(msg==0x0312) {int id=wParam.ToInt32();if(id==80)Play(false);else if(id==81){player.TogglePause();UpdatePauseButton();}else if(id==82)StopPlayback();else if(id==91){SetLearningLocked(!learningLocked);handled=true;}else if(id>=83&&id<=90){player.Seek(seekDeltas[id-83]);handled=true;}if(id<=82)handled=true;}return IntPtr.Zero;
        }
        InstrumentProfile Profile {get{return C<ComboBox>("InstrumentCombo").SelectedItem as InstrumentProfile;}}
        int Transpose {get{return (int)C<ComboBox>("TransposeCombo").SelectedItem;}}
        bool Fold {get{return C<CheckBox>("FoldCheck").IsChecked==true;}}
        bool SkipIntro {get{return C<CheckBox>("SkipIntroCheck").IsChecked==true;}}
        bool AllParts {get{return C<CheckBox>("AllPartsCheck").IsChecked==true;}}
        bool GameAdapt {get{return C<CheckBox>("GameAdaptCheck").IsChecked==true;}}
        Song CurrentScore {get{return selected==null?null:selected.Playback(Profile!=null&&Profile.Monophonic||AllParts);}}
        void UpgradeBundledSongs(string sample) {
            if(!settings.BundledSongsInitialized&&!songs.Any(s=>s.Source=="dogeplay编配 · 多声部示例"))songs.Add(Store.ChordDemo());
            if(!File.Exists(sample))return;
            var old=songs.FirstOrDefault(s=>s.SourceUrl!=null&&s.SourceUrl.Contains("lc6583477")&&!s.Notes.Any(n=>n.Part>0));
            if(old==null)return;
            try {
                var full=Importers.MusicXml(sample,true);var melody=Importers.MusicXml(sample,false);
                bool unchanged=old.Notes.Count==melody.Notes.Count&&old.Notes.Zip(melody.Notes,(a,b)=>a.Midi==b.Midi&&Math.Abs(a.Beat-b.Beat)<0.001&&Math.Abs(a.Duration-b.Duration)<0.001).All(x=>x);
                if(unchanged){old.Notes=full.Notes;old.PartNames=full.PartNames;}
                else if(!songs.Any(s=>s.SourceUrl==old.SourceUrl&&s.Notes.Any(n=>n.Part>0))){full.Title=old.Title+" · 全声部";full.Source=old.Source;full.SourceUrl=old.SourceUrl;full.License=old.License;songs.Add(full);}
            }catch(Exception ex){Diagnostics.Log("Full score upgrade: "+ex.Message);}
        }
        void Save() {Store.Write(Path.Combine(Store.Data,"settings.json"),settings);Store.Write(Path.Combine(Store.Data,"library.json"),songs);}
        void RefreshLibrary() {
            RefreshMusicLibrary();
        }
        void RefreshWindows() {
            var combo=C<ComboBox>("WindowCombo");var old=combo.SelectedItem as GameWindow;var windows=Native.Windows();combo.ItemsSource=windows;
            combo.SelectedItem=windows.FirstOrDefault(w=>old!=null&&w.Handle==old.Handle)??windows.FirstOrDefault(w=>!string.IsNullOrEmpty(Game.ProcessHint)&&Game.ProcessHint.Split('|').Any(h=>w.Process.IndexOf(h,StringComparison.OrdinalIgnoreCase)>=0));
        }
        static string Time(double seconds) {return TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"mm\:ss");}
        void RefreshSong() {
            if(selected==null||Profile==null)return;var map=new Mapping(Profile);var score=CurrentScore;
            int partCount=score.Notes.Select(n=>n.Part).Distinct().Count(),chordSize=score.Notes.Count==0?0:score.Notes.GroupBy(n=>n.Beat).Max(g=>g.Select(n=>n.Midi).Distinct().Count());
            C<TextBlock>("SongTitle").Text=selected.Title;C<TextBlock>("SongMeta").Text=score.Notes.Count+" 个音符  /  "+selected.Bpm+" BPM  /  "+partCount+" 声部 · 同拍最多 "+chordSize+" 音";
            int adaptedTranspose=GameAdapt?Mapping.BestTranspose(score,map,Transpose,Fold,true):Transpose;
            var mapped=score.Notes.Select(n=>n.Midi).Distinct().ToDictionary(p=>p,p=>map.ResolveGame(p+adaptedTranspose,Fold,GameAdapt));
            var exact=score.Notes.Select(n=>n.Midi).Distinct().ToDictionary(p=>p,p=>map.Resolve(p+adaptedTranspose,Fold));
            int skipped=score.Notes.Count(n=>!mapped[n.Midi].HasValue);
            int near=score.Notes.Count(n=>mapped[n.Midi].HasValue&&!exact[n.Midi].HasValue);
            int folded=score.Notes.Count(n=>exact[n.Midi].HasValue&&exact[n.Midi].Value!=n.Midi+adaptedTranspose);
            int merged=score.Notes.Where(n=>mapped[n.Midi].HasValue).GroupBy(n=>new{n.Beat,Pitch=mapped[n.Midi].Value}).Sum(g=>g.Count()-1);
            C<TextBlock>("MapStatus").Text="游戏实际映射："+skipped+" 个跳过，"+near+" 个半音近似，"+folded+" 个保持音名折回八度；同拍同键合并 "+merged+" 个音。";
            C<TextBlock>("GameAdaptHint").Text="游戏端移调 "+(adaptedTranspose>=0?"+":"")+adaptedTranspose+" 半音。近似音与原谱有差别，合并音只能发出一次按键。"+(partCount==1?" 当前只有一个含音符的声部，关闭全部声部不会分离旋律与伴奏。":"");
            double intro=Player.StartBeat(score,true);
            C<TextBlock>("IntroHint").Text=intro>0?"开头有 "+intro.ToString("0.##")+" 拍休止（约 "+(intro*60/selected.Bpm).ToString("0.#")+" 秒）"+(SkipIntro?"，已跳过。":"，会按原谱等待。"):"从第一个音符开始。";
            double startPosition=Player.StartBeat(score,SkipIntro),lengthPosition=Math.Max(0,score.Length-startPosition);C<TextBlock>("DurationLabel").Text=Time(lengthPosition*60/selected.Bpm);C<TextBlock>("TimeLabel").Text=Time(Math.Max(0,(pendingStartBeat??startPosition)-startPosition)*60/selected.Bpm);SetProgressValue(pendingStartBeat??startPosition,startPosition,lengthPosition);tiles.Clear();tileBeats.Clear();C<WrapPanel>("NotePanel").Children.Clear();noteHighlight=-1;
            foreach(var group in score.Notes.GroupBy(n=>n.Beat).Take(400)) {
                var notes=group.ToList();bool playable=notes.All(n=>mapped[n.Midi].HasValue);
                var stack=new StackPanel();stack.Children.Add(new TextBlock{Text=string.Join("·",notes.Select(n=>Notation.Label(n.Midi+Transpose))),FontSize=17,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,Foreground=Brush(playable?"#786398":"#C2868D")});
                stack.Children.Add(new TextBlock {Text=string.Join("+",notes.Select(n=>{var pitch=mapped[n.Midi];return pitch.HasValue?map.Map[pitch.Value].ToString():"—";}).Distinct()),FontSize=10,Foreground=Brush("#B0A5BC"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,0),ToolTip="实际发送的游戏按键；上方为原谱音符"});
                var tile=new Border {Child=stack,CornerRadius=new CornerRadius(9),Background=Brush("#F7F4FB"),Padding=new Thickness(11,12,11,12),Margin=new Thickness(0,0,7,9),MinWidth=48,ToolTip="第 "+group.Key.ToString("0.##")+" 拍"};
                C<WrapPanel>("NotePanel").Children.Add(tile);tiles.Add(tile);tileBeats.Add(group.Key);
            }
            if(score.Notes.Select(n=>n.Beat).Distinct().Count()>400) C<WrapPanel>("NotePanel").Children.Add(new TextBlock {Text="预览前 400 组音符，演奏包含完整曲目。",FontSize=11});
            RefreshTimingHint();
            RefreshLearningScore();
        }
        void UpdateProgress(double beat) {
            if(selected==null)return;var score=CurrentScore;double start=Player.StartBeat(score,SkipIntro),length=Math.Max(0,score.Length-start),position=Math.Max(0,Math.Min(length,beat-start));SetProgressValue(start+position,start,length);C<TextBlock>("TimeLabel").Text=Time(position*60/selected.Bpm);
            int next=beat<=0?-1:tileBeats.FindLastIndex(b=>b<=beat);
            SyncLearningProgress(beat);
            if(next==noteHighlight)return;if(noteHighlight>=0&&noteHighlight<tiles.Count)tiles[noteHighlight].Background=Brush("#F7F4FB");noteHighlight=next;
            if(next>=0&&next<tiles.Count){tiles[next].Background=Brush("#DDD0FA");tiles[next].BringIntoView();}
        }
        void SetProgressValue(double beat,double start,double length) {if(selected==null)return;updatingProgress=true;try{C<Slider>("ProgressBar").Value=length>0?Math.Max(0,Math.Min(100,(beat-start)/length*100)):0;}finally{updatingProgress=false;}}
        int PlaybackMode {get{return C<ComboBox>("PlaybackModeCombo").SelectedIndex;}}
        Song NextSong(int mode) {
            return QueueNext(mode,1,false);
        }
        async void Play(bool preview,bool instrumentPreview=false) {
            if(onboardingOpen||installingUpdate)return;
            Diagnostics.Log("Click "+(preview?"preview":"game"));
            if(sessionActive||player.Running){Status("正在演奏，可用上一曲 / 下一曲或队列切歌。");return;}
            if(!ready||closing){Status("软件正在准备或关闭，请稍后重试。");return;}
            if(selected==null){Status("请先从左侧选择一首曲目。");return;}
            if(window.OwnedWindows.Cast<Window>().Any(w=>w.IsVisible&&w!=learningWindow)){Status("请先关闭曲谱编辑或导入窗口，再开始播放。");return;}
            bool background=!preview&&C<CheckBox>("BackgroundInputCheck").IsChecked==true;
            try {
                Status(preview?"正在准备试听音频…":"正在检查游戏窗口…");
                var map=new Mapping(Profile);var target=C<ComboBox>("WindowCombo").SelectedItem as GameWindow;
                if(!preview&&!testing&&(target==null||!Native.IsWindow(target.Handle))) {RefreshWindows();target=C<ComboBox>("WindowCombo").SelectedItem as GameWindow;}
                if(!preview&&target==null)throw new InvalidOperationException("没有找到目标游戏窗口。请先打开游戏，再点“刷新”并选择窗口。");
                if(!preview&&!CheckInputAccess(target))return;
                bool skip=SkipIntro;int countdown;
                DateTimeOffset? scheduledStart=null;
                string scheduledText=C<TextBox>("ScheduledStartBox").Text.Trim();
                if(!preview&&scheduledText.Length>0) {
                    DateTime parsed;
                    string[] formats={"yyyy-MM-dd HH:mm:ss.fff","yyyy-MM-dd HH:mm:ss"};
                    if(!DateTime.TryParseExact(scheduledText,formats,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out parsed))throw new FormatException("北京时间格式应为 yyyy-MM-dd HH:mm:ss.fff，例如 2026-10-18 21:30:00.000。");
                    scheduledStart=new DateTimeOffset(DateTime.SpecifyKind(parsed,DateTimeKind.Unspecified),TimeSpan.FromHours(8));
                    if(scheduledStart.Value<=DateTimeOffset.UtcNow.AddMilliseconds(100))throw new FormatException("开始时间必须晚于当前北京时间，并预留至少 0.1 秒准备时间。");
                }
                if(!int.TryParse(C<ComboBox>("CountdownCombo").Text.Trim(),out countdown)||countdown<0||countdown>120)throw new FormatException("开始倒计时应填写 0～120 秒的整数。");
                CapturePerformancePreferences(true);PersistTempo(true);EnsureQueue();sessionActive=true;sessionPreview=preview;requestedNext=null;bool first=true;
                uint sessionPid=0;if(!preview&&!testing)Native.GetWindowThreadProcessId(target.Handle,out sessionPid);
                SetPlaying(true,preview);if(!background)RegisterSeekHotkeys();else C<TextBlock>("HotkeyStatus").Text="后台发送 · F9 暂停 / 继续 · F10 停止 · 方向键供其他应用使用";
                do {
                    if(!preview&&!testing){uint actualPid;Native.GetWindowThreadProcessId(target.Handle,out actualPid);if(!Native.IsWindow(target.Handle)||actualPid!=sessionPid)throw new InvalidOperationException("目标游戏窗口已关闭或改变，播放队列已停止。");}
                    int bpm;if(!int.TryParse(C<TextBox>("BpmBox").Text,out bpm)||bpm<30||bpm>240)throw new FormatException("速度应在 30～240 BPM 之间。");selected.Bpm=bpm;RecordRecent(selected);RefreshSong();
                    Diagnostics.Log("Start title="+selected.Title+" preview="+preview+" firstBeat="+Player.StartBeat(selected,true)+" skipIntro="+skip+" target="+(target==null?"none":target.Process));
                    if(!preview&&!testing&&!background){bool switched=Native.TryActivate(target);Diagnostics.Log("Foreground switch="+switched);}
                    await player.Start(CurrentScore,map,Transpose,Fold,preview,target,countdown,skip,Profile.Timing,GameAdapt,first?pendingStartBeat:null,background,preview&&instrumentPreview?Profile.PreviewProgram:0,first?scheduledStart:null);
                    pendingStartBeat=null;first=false;
                    int mode=PlaybackMode;Song next=requestedNext;requestedNext=null;
                    if(next==null){if(!player.Completed||mode==0||closing)break;next=NextSong(mode);}if(next==null||closing)break;
                    SelectTrack(next);RefreshLibrary();Status(mode==2?"循环当前曲目："+selected.Title:"下一曲："+selected.Title);
                    if(!preview&&!testing&&!background)Native.TryActivate(target);
                }while(true);
            }catch(Exception ex){if(background){Diagnostics.Log(ex.ToString());Status("后台发送已停止："+ex.Message);}else Error(ex);}finally{sessionActive=false;requestedNext=null;UnregisterSeekHotkeys();SetPlaying(false);RefreshLibrary();}
        }
        void SetPlaying(bool active,bool preview=false) {
            C<CheckBox>("BackgroundInputCheck").IsEnabled=!active;
            foreach(string name in new[]{"InstrumentCombo","AddInstrumentButton","EditInstrumentButton","DeleteInstrumentButton","InstrumentPreviewButton","GameCombo","WindowCombo","BpmBox","TransposeCombo","FoldCheck","SkipIntroCheck","AllPartsCheck","GameAdaptCheck","EditButton","ImportButton","NewButton","SideImport","SideOpen","KeymapButton","TimingCheck","TimingButton","PreviewButton","PlayButton","RefreshWindows","AdminButton","KeyboardTestButton","RenameButton","DeleteButton","CountdownCombo","ScheduledStartBox","ScheduledStartNowButton","SaveSettingsButton","ForegroundButton","PlayAllButton","CheckUpdateButton"}) C<FrameworkElement>(name).IsEnabled=!active;
            C<Button>("PauseButton").IsEnabled=active;C<Button>("StopButton").IsEnabled=active;UpdatePauseButton();
            C<Button>("CheckUpdateButton").IsEnabled=!active&&!checkingUpdates;
            C<Button>("PreviewButton").Content=active&&preview?"♫  正在试听…":"♫  原谱试听";
            C<Button>("PlayButton").Content=active&&!preview?"▶  演奏进行中…":"▶  开始游戏弹奏";
        }
        void UpdatePauseButton() {if(window==null)return;C<Button>("PauseButton").Content=player.Paused?"继续 / F9":"暂停 / F9";}
        void AddSong(Song song) {Importers.Validate(song);if(songs.Any(existing=>existing.Id==song.Id))song.Id=Guid.NewGuid().ToString("N");songs.Add(song);libraryView="all";selected=song;C<TextBox>("SearchBox").Text="";Save();RefreshLibrary();SelectTrack(song);Status("已加入曲库："+song.Title);}
        void RenameSelectedSong() {
            if(selected==null)return;var dialog=Dialog("重命名曲目",460,220);var panel=new StackPanel{Margin=new Thickness(28)};dialog.Content=panel;panel.Children.Add(new TextBlock{Text="修改本地曲库中的显示名称",FontSize=20,FontWeight=FontWeights.Bold});panel.Children.Add(Label("曲名"));var title=new TextBox{Text=selected.Title};panel.Children.Add(title);panel.Children.Add(Button("保存名称",()=>{string value=title.Text.Trim();if(value.Length==0)throw new FormatException("曲名不能为空。");if(value.Length>120)throw new FormatException("曲名不能超过 120 个字符。");selected.Title=value;Save();RefreshLibrary();RefreshSong();dialog.Close();Status("已重命名："+value);}));dialog.ShowDialog();
        }
        void DeleteSelectedSong() {
            if(selected==null)return;var removing=selected;if(MessageBox.Show(window,"确定从本地曲库删除“"+removing.Title+"”？\n原始文件不会被删除。","删除曲目",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            songs.Remove(removing);selected=songs.FirstOrDefault();settings.SelectedSongId=selected==null?null:selected.Id;Save();RefreshLibrary();if(selected!=null){C<ListBox>("SongList").SelectedItem=selected;RefreshSong();}else{C<TextBlock>("SongTitle").Text="选择一首曲目";C<TextBlock>("SongMeta").Text="曲库为空";}Status("已从本地曲库删除："+removing.Title);
        }
        static SolidColorBrush Brush(string color){return (SolidColorBrush)new BrushConverter().ConvertFromString(color);}
        Window Dialog(string title,int width,int height) {return new Window{Title=title,Width=width,Height=height,MinWidth=Math.Min(width,640),MinHeight=Math.Min(height,420),Owner=window,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#F6F5FA"),FontFamily=window.FontFamily,FontSize=13,Resources=window.Resources};}
        TextBlock Label(string text) {return new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Brush("#81758F"),Margin=new Thickness(0,10,0,8)};}
        Button Button(string text,Action click) {var b=new Button{Content=text,Margin=new Thickness(0,8,10,8)};b.Click+=(s,e)=>{try{click();}catch(Exception ex){Error(ex);}};return b;}
        void TextEditor(string imagePath,string recognized) {
            var dialog=Dialog("简谱导入 · 对照原图校对",1000,720);var root=new DockPanel {Margin=new Thickness(25)};dialog.Content=root;
            var bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            header.Children.Add(Label("曲名"));var title=new TextBox{Text=imagePath==null?"我的新曲":Path.GetFileNameWithoutExtension(imagePath)};header.Children.Add(title);
            header.Children.Add(Label("1～7 = 音符；0 = 休止；3' = 高八度；3, = 低八度；1:0.5 = 半拍；5:2 = 两拍；[1 3 5]:2 = 和弦。"));
            header.Children.Add(new TextBlock{Text="图片识别仅生成草稿：请务必核对高低音点、减时线、节拍、和弦与标题中的数字。",Foreground=Brush("#B37868"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,15)});
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(imagePath==null?0:1,GridUnitType.Star)});root.Children.Add(grid);
            var input=new TextBox{Text=recognized??"1 1 5 5 6 6 5:2 |\n4 4 3 3 2 2 1:2",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontSize=20,Margin=new Thickness(0,0,12,0)};grid.Children.Add(input);
            if(imagePath!=null){var image=new Image{Source=LoadImage(imagePath),Stretch=Stretch.Uniform};var scroll=new ScrollViewer{Content=image,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);grid.Children.Add(scroll);}
            bottom.Children.Add(Button("确认校对并加入曲库",()=>{var song=Notation.Parse(title.Text,input.Text,90);song.Source=imagePath==null?"本地简谱":"图片简谱 · 已校对";AddSong(song);dialog.Close();}));
            dialog.ShowDialog();
        }
        static BitmapImage LoadImage(string path) {var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.UriSource=new Uri(path);b.EndInit();b.Freeze();return b;}
        void ImportDialog() {
            var dialog=Dialog("导入曲谱",730,580);var panel=new StackPanel{Margin=new Thickness(30)};dialog.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            panel.Children.Add(new TextBlock{Text="把喜欢的旋律带进来",FontSize=25,FontWeight=FontWeights.Bold});panel.Children.Add(Label("图片在本机处理。数字简谱使用 Windows OCR；五线谱使用开源 Audiveris。"));
            var type=new ComboBox{ItemsSource=new[]{"数字简谱图片（PNG / JPG / BMP）","五线谱图片（PNG / JPG / PDF）","MIDI / MusicXML / MXL / dogeplay JSON / SkyStudio"},SelectedIndex=0};panel.Children.Add(type);
            var all=new CheckBox{Content="导入旋律、和弦与伴奏全部声部",IsChecked=true};panel.Children.Add(all);
            panel.Children.Add(Label("五线谱识别引擎 · 软件包内置"));var engine=new TextBox{Name="StaffEnginePath",Text=StaffEngine.DisplayPath,IsReadOnly=true,TextWrapping=TextWrapping.Wrap};panel.Children.Add(engine);
            panel.Children.Add(Button("打开 Animenz / A叔公开谱页面 ↗",()=>System.Diagnostics.Process.Start("https://animenz.wordpress.com/tag/sheet/")));
            var repairHint=Label("");panel.Children.Add(repairHint);
            Action refreshEngine=null;
            var repair=Button("修复识别引擎位置",()=>{try{var chooser=new OpenFileDialog{Filter="Audiveris|Audiveris.exe",Title="选择完整识别引擎中的 Audiveris.exe"};if(chooser.ShowDialog(dialog)==true){StaffEngine.SelectRepair(chooser.FileName,settings);engine.Text=StaffEngine.DisplayPath;Save();refreshEngine();}}catch(Exception ex){Error(ex);}});
            repair.Name="StaffEngineRepair";panel.Children.Add(repair);
            refreshEngine=()=>{bool broken=StaffEngine.NeedsRepair;repair.Visibility=broken?Visibility.Visible:Visibility.Collapsed;repair.IsEnabled=broken;engine.Text=StaffEngine.DisplayPath;repairHint.Text=broken?"识别引擎缺失或运行异常，请恢复完整软件包；也可修复引擎位置。":"内置引擎已就绪，位置已锁定，无需设置。";};
            refreshEngine();dialog.Activated+=(s,e)=>refreshEngine();
            panel.Children.Add(Label("保留和弦与各声部节奏，同一拍的多个音会同步弹奏。暂不展开反复记号与速度变化；可在主界面切换仅旋律。"));
            var state=Label("支持清晰的印刷谱与截图。建议先裁去标题和歌词。");panel.Children.Add(state);
            var import=new Button{Content="选择文件并导入",Background=Brush("#7660DB"),Foreground=Brush("#FFFFFF"),Margin=new Thickness(0,15,0,0)};panel.Children.Add(import);
            bool busy=false;dialog.Closing+=(s,e)=>{if(busy){e.Cancel=true;state.Text="正在处理，请等待完成后关闭（最长 15 分钟）。";}};
            import.Click+=async(s,e)=>{
                var picker=new OpenFileDialog{Filter=type.SelectedIndex==2?"曲谱文件|*.mxl;*.musicxml;*.xml;*.json;*.txt;*.skystudio;*.mid;*.midi":type.SelectedIndex==1?"五线谱文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.pdf":"简谱图片|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=type.SelectedIndex==2};
                if(picker.ShowDialog(dialog)!=true)return;int choice=type.SelectedIndex;busy=true;import.IsEnabled=false;type.IsEnabled=false;state.Text=choice==1?"正在识别五线谱，首次启动通常需要 1～3 分钟…":"正在读取曲谱…";
                try {
                    if(choice==0){string data=await Importers.NumberOcr(picker.FileName);var obj=Store.Json.Deserialize<Dictionary<string,object>>(data);string text=obj.ContainsKey("Text")?Convert.ToString(obj["Text"]):"";busy=false;dialog.Close();TextEditor(picker.FileName,text);}
                    else if(choice==1){var song=await Importers.StaffOcr(picker.FileName,all.IsChecked==true);AddSong(song);busy=false;dialog.Close();EditSong();}
                    else {
                        bool allParts=all.IsChecked==true;var imported=new List<Song>();var failures=new List<string>();
                        foreach(string file in picker.FileNames){state.Text="正在导入 "+(imported.Count+failures.Count+1)+" / "+picker.FileNames.Length+"："+Path.GetFileName(file);try{var song=await Task.Run(()=>{string ext=Path.GetExtension(file).ToLowerInvariant();return ext==".mid"||ext==".midi"?MidiImporter.Read(file,allParts):new[]{".json",".txt",".skystudio"}.Contains(ext)?CommunityScores.ReadLocal(file,allParts):Importers.MusicXml(file,allParts);});Importers.Validate(song);if(songs.Any(existing=>existing.Id==song.Id)||imported.Any(existing=>existing.Id==song.Id))song.Id=Guid.NewGuid().ToString("N");imported.Add(song);}catch(Exception ex){failures.Add(Path.GetFileName(file)+"："+ex.Message);}}
                        if(imported.Count>0){songs.AddRange(imported);libraryView="all";selected=imported[0];C<TextBox>("SearchBox").Text="";Save();RefreshLibrary();SelectTrack(selected);}
                        busy=false;
                        if(failures.Count>0){state.Text="已导入 "+imported.Count+" 首，失败 "+failures.Count+" 个文件。";MessageBox.Show(dialog,string.Join("\n",failures.Take(12)),"部分文件未导入");}
                        else{dialog.Close();Status("已导入 "+imported.Count+" 首曲谱。");if(imported.Count==1)EditSong();}
                    }
                }catch(Exception ex){state.Text="导入未完成："+ex.Message;}finally{busy=false;import.IsEnabled=true;type.IsEnabled=true;refreshEngine();}
            };dialog.ShowDialog();
        }
        void EditSong() {
            if(selected==null)return;var original=selected;var dialog=Dialog("曲谱校对 · 音高与时值",820,670);var root=new DockPanel{Margin=new Thickness(25)};dialog.Content=root;
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);var title=new TextBox{Text=original.Title};header.Children.Add(title);header.Children.Add(Label("可直接修改每个音符：1～7 表示 do～si，3' 为高八度，3, 为低八度。时值 0.5 为半拍，2 为两拍。相同起始拍表示和弦。"));
            header.Children.Add(Label("来源："+original.Source+"  /  "+original.License+"\n"+(original.SourceUrl??"")));
            var notes=original.Notes.Select(n=>new Note(n.Beat,n.Duration,n.Midi){Part=n.Part}).ToList();
            var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
            var grid=new DataGrid{ItemsSource=notes,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,Background=Brush("#FFFFFF"),GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,BorderThickness=new Thickness(0),RowHeight=32};
            grid.Columns.Add(new DataGridTextColumn{Header="起始拍",Binding=new System.Windows.Data.Binding("Beat"){ValidatesOnExceptions=true},Width=new DataGridLength(1,DataGridLengthUnitType.Star)});grid.Columns.Add(new DataGridTextColumn{Header="时值（拍）",Binding=new System.Windows.Data.Binding("Duration"){ValidatesOnExceptions=true},Width=new DataGridLength(1,DataGridLengthUnitType.Star)});grid.Columns.Add(new DataGridTextColumn{Header="音符（简谱）",Binding=new System.Windows.Data.Binding("PitchName"){ValidatesOnExceptions=true},Width=new DataGridLength(1,DataGridLengthUnitType.Star)});root.Children.Add(grid);
            grid.Columns.Add(new DataGridTextColumn{Header="声部（0=旋律）",Binding=new System.Windows.Data.Binding("Part"){ValidatesOnExceptions=true},Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            actions.Children.Add(Button("保存校对",()=>{if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))throw new FormatException("请修正表格中无效的数字。");var copy=new Song{Notes=notes,Bpm=original.Bpm};Importers.Validate(copy);original.Title=title.Text;original.Notes=notes.OrderBy(n=>n.Beat).ToList();Save();RefreshLibrary();RefreshSong();dialog.Close();}));
            actions.Children.Add(Button("删除选中音符",()=>{var note=grid.SelectedItem as Note;if(note!=null){notes.Remove(note);grid.Items.Refresh();}}));
            actions.Children.Add(Button("添加音符",()=>{notes.Add(new Note(notes.Count==0?0:notes.Max(n=>n.Beat+n.Duration),1,60));grid.Items.Refresh();}));dialog.ShowDialog();
        }
        void KeymapDialog() {InstrumentDialog(false);}
        void ExportSong() {if(selected==null)return;var picker=new SaveFileDialog{Filter="dogeplay曲谱|*.json",FileName=string.Join("_",selected.Title.Split(Path.GetInvalidFileNameChars()))+".json"};if(picker.ShowDialog(window)==true){Store.Write(picker.FileName,selected);Status("曲谱已导出");}}
        string AnnouncementText(UpdateAnnouncement announcement) {
            if(announcement==null||string.IsNullOrWhiteSpace(announcement.Content))return "";
            string title=string.IsNullOrWhiteSpace(announcement.Title)?"维护公告":announcement.Title;
            string date=string.IsNullOrWhiteSpace(announcement.PublishedUtc)?"":"\n发布时间："+announcement.PublishedUtc;
            return "【"+title+"】"+date+"\n\n"+announcement.Content.Trim();
        }
        void DisplayAnnouncement(UpdateAnnouncement announcement) {
            latestAnnouncement=announcement;
            string text=AnnouncementText(announcement);
            C<TextBlock>("AnnouncementTitle").Text=text.Length==0?"公告":"公告 · "+(string.IsNullOrWhiteSpace(announcement.Title)?"维护公告":announcement.Title);
            C<TextBlock>("AnnouncementSummary").Text=text.Length==0?"暂无公告，可点击下方刷新。":announcement.Content.Trim();
            C<TextBlock>("AnnouncementSummary").ToolTip=text.Length==0?null:text;
        }
        string AnnouncementKey(UpdateManifest info) {
            if(info==null||info.Announcement==null||string.IsNullOrWhiteSpace(info.Announcement.Content))return "";
            return CurrentAnnouncementKey(info);
        }
        static string CurrentAnnouncementKey(UpdateManifest info) {
            using(var sha=System.Security.Cryptography.SHA256.Create()) {
                string value=UpdateClient.CurrentVersion+"|"+(info.Version??"")+"|"+Store.Json.Serialize(info.Announcement);
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }
        void ShowAnnouncementDialog(UpdateAnnouncement announcement,string key,bool allowSuppress) {
            string text=AnnouncementText(announcement);if(text.Length==0)return;
            var dialog=Dialog("dogeplay · 公告",650,520);dialog.MinWidth=520;dialog.MinHeight=380;
            var root=new DockPanel{Margin=new Thickness(26)};dialog.Content=root;
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            header.Children.Add(new TextBlock{Text=string.IsNullOrWhiteSpace(announcement.Title)?"维护公告":announcement.Title,FontSize=24,FontWeight=FontWeights.Bold,Foreground=Brush("#302C3D"),TextWrapping=TextWrapping.Wrap});
            if(!string.IsNullOrWhiteSpace(announcement.PublishedUtc))header.Children.Add(Label("发布时间："+announcement.PublishedUtc));
            var content=new TextBlock{Text=announcement.Content.Trim(),TextWrapping=TextWrapping.Wrap,FontSize=14,LineHeight=26,Foreground=Brush("#62596F")};
            var scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var footer=new DockPanel{Margin=new Thickness(0,20,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var close=new Button{Content="关闭",Background=Brush("#7657E8"),Foreground=Brush("#FFFFFF"),Padding=new Thickness(16,8,16,8),Margin=new Thickness(10,0,0,0)};DockPanel.SetDock(close,Dock.Right);footer.Children.Add(close);
            var suppress=new CheckBox{Content="不再提示，直到下次更新",VerticalAlignment=VerticalAlignment.Center,IsEnabled=allowSuppress,IsChecked=allowSuppress&&settings.AnnouncementDismissedKey==key};footer.Children.Add(suppress);
            Action saveDismiss=()=>{if(allowSuppress&&!string.IsNullOrWhiteSpace(key)){settings.AnnouncementDismissedKey=suppress.IsChecked==true?key:null;try{SaveSettings();}catch(Exception ex){Diagnostics.Log("Announcement preference: "+ex);}}};
            close.Click+=(s,e)=>dialog.Close();dialog.Closing+=(s,e)=>saveDismiss();
            root.Children.Add(scroll);
            dialog.ShowDialog();
        }
        bool ShouldShowAnnouncement(UpdateManifest info) {
            string key=AnnouncementKey(info);return key.Length>0&&!string.Equals(settings.AnnouncementDismissedKey,key,StringComparison.Ordinal);
        }
        async void LoadStartupExperience() {
            if(nonInteractiveStartup||startupLoaded)return;
            startupLoaded=true;
            await CheckForUpdatesAsync(true);
        }
        async void ShowAnnouncement() {
            var button=C<Button>("AnnouncementButton");button.IsEnabled=false;
            try {
                var info=await UpdateClient.FetchAsync();
                if(windowClosed||window.Dispatcher.HasShutdownStarted)return;
                DisplayAnnouncement(info==null?null:info.Announcement);
                if(info==null||info.Announcement==null||string.IsNullOrWhiteSpace(info.Announcement.Content))MessageBox.Show(window,"服务器暂未发布公告。","dogeplay · 公告",MessageBoxButton.OK,MessageBoxImage.Information);
                else ShowAnnouncementDialog(info.Announcement,AnnouncementKey(info),true);
            }catch(Exception ex){
                Diagnostics.Log("Announcement refresh: "+ex);
                if(windowClosed)return;
                C<TextBlock>("AnnouncementSummary").Text="刷新失败："+ex.Message+" · 点击下方重试。";
                string cached=AnnouncementText(latestAnnouncement);
                MessageBox.Show(window,"公告刷新失败："+ex.Message+(cached.Length==0?"":"\n\n上次加载的公告：\n"+cached),"dogeplay · 公告",MessageBoxButton.OK,MessageBoxImage.Information);
            }finally{if(!windowClosed&&!window.Dispatcher.HasShutdownStarted)button.IsEnabled=true;}
        }
        async void CheckForUpdates() {await CheckForUpdatesAsync(false);}
        async Task CheckForUpdatesAsync(bool automatic) {
            if(checkingUpdates||windowClosed)return;
            if(!automatic&&(player.Running||sessionActive)){Status("请先停止演奏，再检查更新。");return;}
            checkingUpdates=true;
            var button=C<Button>("CheckUpdateButton");button.IsEnabled=false;
            var announcementButton=C<Button>("AnnouncementButton");announcementButton.IsEnabled=false;
            C<TextBlock>("StartupUpdateStatus").Text="正在检查更新与公告…";
            if(!automatic)Status("正在检查更新…");
            try {
                // Start the request immediately; show any update prompt after the guide closes.
                var request=UpdateClient.FetchAsync();
                if(automatic&&!settings.OnboardingCompleted)ShowOnboarding();
                var info=await request;
                if(windowClosed||window.Dispatcher.HasShutdownStarted)return;
                if(info==null){C<TextBlock>("StartupUpdateStatus").Text="更新服务器尚未配置";return;}
                DisplayAnnouncement(info.Announcement);
                if(automatic&&ShouldShowAnnouncement(info))ShowAnnouncementDialog(info.Announcement,AnnouncementKey(info),true);
                if(windowClosed||window.Dispatcher.HasShutdownStarted)return;
                if(!UpdateClient.IsNewer(info.Version,UpdateClient.CurrentVersion)) {
                    C<TextBlock>("StartupUpdateStatus").Text="已检查 · 当前版本 "+UpdateClient.CurrentVersion;
                    if(!automatic){Status("已检查更新并刷新公告");MessageBox.Show(window,"当前已经是最新版本（"+UpdateClient.CurrentVersion+"）。\n公告已刷新到左侧。","dogeplay · 检查更新",MessageBoxButton.OK,MessageBoxImage.Information);}return;
                }
                C<TextBlock>("StartupUpdateStatus").Text="发现新版本 "+info.Version+" · 等待确认";
                if(player.Running||sessionActive||window.OwnedWindows.Cast<Window>().Any(w=>w.IsVisible))return;
                string notes=string.IsNullOrWhiteSpace(info.Notes)?"维护者没有填写更新说明。":info.Notes;
                string prompt="发现新版本："+info.Version+"\n\n"+notes+"\n\n只下载更新文件，保留本地曲库和设置。现在更新吗？";
                installingUpdate=true;
                if(MessageBox.Show(window,prompt,"dogeplay · 发现更新",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes){Status("稍后可从顶部检查更新。");return;}
                Status("正在下载更新包并校验…");await UpdateClient.DownloadAndLaunchAsync(info);Status("更新包已校验，软件即将重启完成更新。");window.Close();
            } catch(Exception ex) {
                Diagnostics.Log("Update check: "+ex);
                if(windowClosed||window.Dispatcher.HasShutdownStarted)return;
                C<TextBlock>("StartupUpdateStatus").Text="联网检查失败 · 可点击下方重试";
                if(latestAnnouncement==null)C<TextBlock>("AnnouncementSummary").Text="公告刷新失败："+ex.Message;
                if(!automatic){Status("检查更新失败："+ex.Message);MessageBox.Show(window,ex.Message,"dogeplay · 更新失败",MessageBoxButton.OK,MessageBoxImage.Warning);}
            } finally {checkingUpdates=false;installingUpdate=false;if(!windowClosed&&!window.Dispatcher.HasShutdownStarted){button.IsEnabled=!player.Running&&!sessionActive;announcementButton.IsEnabled=true;}}
        }
        void ShowHelp() {
            ShowOnboarding();
        }
    }
}
