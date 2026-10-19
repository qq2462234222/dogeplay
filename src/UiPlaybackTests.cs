using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LumaKeys {
    public partial class App {
        sealed class UiReceiver : IPlaybackIo {
            public int Downs;public int Ups;
            public bool IsWindow(IntPtr h){return h==new IntPtr(1);}
            public IntPtr Foreground(){return new IntPtr(1);}
            public uint ProcessId(IntPtr h){return 1;}
            public void Key(char key,bool down){if(down)Downs++;else Ups++;}
            public void Keys(IList<KeyStroke> keys){foreach(var key in keys)Key(key.Key,key.Down);}
        }
        int RunUiPlaybackTests(Application application) {
            testing=true;int result=0;var log=new List<string>();
            Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);log.Add("PASS: "+text);};
            window.Loaded+=async(s,e)=>{
                var receiver=new UiReceiver();player=new Player(receiver);player.Status=Status;player.Progress=b=>window.Dispatcher.BeginInvoke(new Action(()=>UpdateProgress(b)));
                try {
                    selected=new Song{Title="Leading rest regression",Bpm=120};selected.Notes.Add(new Note(15,8,60));C<TextBox>("BpmBox").Text="120";RefreshSong();
                    C<Button>("PreviewButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(250);
                    check(player.Running&&player.NotesStarted>0,"real preview button sends audio immediately despite 15-beat intro");
                    check(C<TextBlock>("PlaybackStatus").Text.Contains("试听")&&!C<Button>("PreviewButton").IsEnabled,"preview state is visible and duplicate starts disabled");
                    C<Button>("StopButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));for(int i=0;player.Running&&i<30;i++)await Task.Delay(20);await Task.Delay(50);
                    check(!player.Running&&C<Button>("PreviewButton").IsEnabled,"stop button completes preview and restores controls");
                    var target=new GameWindow{Handle=new IntPtr(1),Title="Receiver",Process="test"};C<ComboBox>("WindowCombo").ItemsSource=new[]{target};C<ComboBox>("WindowCombo").SelectedItem=target;C<ComboBox>("CountdownCombo").ItemsSource=new[]{0,3};C<ComboBox>("CountdownCombo").SelectedItem=0;
                    C<Button>("PlayButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(180);
                    check(player.Running&&receiver.Downs>0,"real game-start button reaches mapped input receiver immediately");
                    C<Button>("StopButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));for(int i=0;player.Running&&i<30;i++)await Task.Delay(20);await Task.Delay(50);
                    check(receiver.Downs==receiver.Ups&&C<Button>("PlayButton").IsEnabled,"UI stop releases game keys and restores start button");
                    C<ComboBox>("CountdownCombo").SelectedItem=3;C<Button>("PlayButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(120);
                    check(C<TextBlock>("PlaybackStatus").Text.Contains("3 秒"),"countdown is displayed beside playback controls");player.Stop();while(player.Running)await Task.Delay(20);await Task.Delay(50);
                    C<ComboBox>("WindowCombo").SelectedItem=null;C<Button>("PlayButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(80);
                    check(!player.Running&&C<TextBlock>("PlaybackStatus").Text.Contains("没有找到"),"missing game produces actionable visible error");
                    selected=Store.ChordDemo();C<TextBox>("BpmBox").Text=selected.Bpm.ToString();RefreshSong();
                    check(CurrentScore.Notes.Count>CurrentScore.Playback(false).Notes.Count&&C<TextBlock>("SongMeta").Text.Contains("4 音"),"main UI reports melody plus accompaniment with four-note onsets");
                    C<CheckBox>("AllPartsCheck").IsChecked=false;check(CurrentScore.Notes.All(n=>n.Part==0),"melody toggle filters accompaniment in playback selection");C<CheckBox>("AllPartsCheck").IsChecked=true;
                    Exception catalogError=null;var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
                    timer.Tick+=async(sender,args)=>{
                        timer.Stop();Window catalogWindow=null;
                        try {
                            catalogWindow=window.OwnedWindows.Cast<Window>().First(w=>w.IsVisible);
                            var search=FindTestControl<TextBox>(catalogWindow,"CatalogSearch");var list=FindTestControl<DataGrid>(catalogWindow,"CatalogResults");var download=FindTestControl<Button>(catalogWindow,"CatalogDownload");var status=FindTestControl<TextBlock>(catalogWindow,"CatalogStatus");
                            search.Text="Think of Today Abbott";check(list.Items.Count==1,"catalog UI searches title and composer");
                            search.Text="absent-title-xyz";check(list.Items.Count==0&&!download.IsEnabled,"empty search disables download");
                            search.Text="Think of Today Abbott";download.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                            for(int i=0;!download.IsEnabled&&i<500;i++)await Task.Delay(100);
                            check(download.IsEnabled&&songs.Any(x=>x.Title=="Think of Today"&&x.Notes.Any(n=>n.Part>0)),"catalog download button adds full score to local library");
                            catalogWindow.UpdateLayout();var visual=(FrameworkElement)catalogWindow.Content;var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(visual);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Store.Root,"..","test-output","catalog-interface.png")))png.Save(file);
                        }catch(Exception ex){catalogError=ex;}
                        if(catalogWindow!=null)catalogWindow.Close();
                    };timer.Start();OpenCatalog();if(catalogError!=null)throw catalogError;
                }catch(Exception ex){result=1;log.Add("FAIL: "+ex);player.Stop();}
                while(player.Running)await Task.Delay(20);
                {Directory.CreateDirectory(Path.Combine(Store.Root,"..","test-output"));File.WriteAllLines(Path.Combine(Store.Root,"..","test-output","ui-playback-tests.txt"),log);window.Close();}
            };
            application.Run(window);return result;
        }
        static T FindTestControl<T>(System.Windows.DependencyObject root,string id) where T:FrameworkElement {
            if(root is T&&System.Windows.Automation.AutomationProperties.GetAutomationId(root)==id)return (T)root;
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++){var found=FindTestControl<T>(System.Windows.Media.VisualTreeHelper.GetChild(root,i),id);if(found!=null)return found;}return null;
        }
    }
}
