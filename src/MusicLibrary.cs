using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace LumaKeys {
    public partial class App {
        string libraryView="all";
        bool refreshingLibrary,loadingPreferences,sessionActive,sessionPreview;
        Song requestedNext;
        readonly Random shuffle=new Random();
        InstrumentProfile loadedProfile;

        void InitializeMusicLibrary() {
            settings.Favorites=settings.Favorites??new List<string>();
            settings.RecentSongs=settings.RecentSongs??new List<string>();
            settings.QueueIds=settings.QueueIds??new List<string>();
            settings.Playlists=(settings.Playlists??new List<LocalPlaylist>()).Where(p=>p!=null).ToList();
            foreach(var p in settings.Playlists)p.SongIds=p.SongIds??new List<string>();
            PruneLibraryReferences();
            LoadPerformancePreferences();
            RoutedEventHandler change=(s,e)=>{if(ready&&!loadingPreferences){CapturePerformancePreferences(false);SaveSettings();RefreshBackgroundHint();}};
            foreach(string name in new[]{"FoldCheck","SkipIntroCheck","AllPartsCheck","GameAdaptCheck","BackgroundInputCheck"}) {
                C<CheckBox>(name).Checked+=change;C<CheckBox>(name).Unchecked+=change;
            }
            C<ComboBox>("TransposeCombo").SelectionChanged+=(s,e)=>{if(ready&&!loadingPreferences){CapturePerformancePreferences(false);SaveSettings();}};
            C<ComboBox>("CountdownCombo").AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>{if(ready&&!loadingPreferences){CapturePerformancePreferences(false);SaveSettings();}}));
            Click("SaveSettingsButton",()=>{CapturePerformancePreferences(true);PersistTempo(true);SaveSettings();Status("已保存“"+Profile.Name+"”的演奏设置，下次打开自动恢复。");});
            C<TextBox>("BpmBox").LostFocus+=(s,e)=>{if(ready&&!sessionActive)PersistTempo(false);};
            Click("ForegroundButton",()=>{C<CheckBox>("BackgroundInputCheck").IsChecked=false;Status("已切换前台演奏。保持游戏乐器窗口在前台；切出会暂停，返回后按 F9 继续。");});
            Click("LibraryButton",()=>ShowLibrary("all"));
            Click("FavoritesButton",()=>ShowLibrary("favorites"));
            Click("HistoryButton",()=>ShowLibrary("history"));
            Click("QueueButton",QueueDialog);
            Click("NewPlaylistButton",()=>PlaylistNameDialog(null));
            Click("RenamePlaylistButton",()=>{var p=CurrentPlaylist;if(p!=null)PlaylistNameDialog(p);else Status("请先在侧栏选择一个自建歌单。");});
            Click("DeletePlaylistButton",DeletePlaylist);
            Click("RemoveFromPlaylistButton",RemoveFromPlaylist);
            Click("SelectAllButton",()=>C<ListBox>("SongList").SelectAll());
            Click("ClearSelectionButton",()=>C<ListBox>("SongList").UnselectAll());
            Click("FavoriteButton",ToggleFavorites);
            Click("AddQueueButton",AddSelectionToQueue);
            Click("AddPlaylistButton",AddToPlaylistDialog);
            Click("PreviousButton",()=>SkipTrack(-1));Click("NextButton",()=>SkipTrack(1));
            Click("PlayAllButton",()=>{if(sessionActive)return;var visible=VisibleSongs();if(visible.Count==0)return;settings.QueueIds=visible.Select(s=>s.Id).ToList();C<ComboBox>("PlaybackModeCombo").SelectedIndex=1;SelectTrack(visible[0]);SaveSettings();Play(false);});
            C<ComboBox>("SortCombo").ItemsSource=new[]{"导入顺序","曲名升序","曲名降序","时长从短到长","时长从长到短","来源升序","来源降序","BPM 从低到高","BPM 从高到低"};
            C<ComboBox>("SortCombo").SelectedIndex=Math.Max(0,Math.Min(8,settings.LibrarySort));
            C<ComboBox>("SortCombo").SelectionChanged+=(s,e)=>{if(ready){settings.LibrarySort=C<ComboBox>("SortCombo").SelectedIndex;SaveSettings();RefreshLibrary();UpdateSortHeaders();}};
            Click("SortTitleButton",()=>ToggleLibrarySort(1,2));Click("SortSourceButton",()=>ToggleLibrarySort(5,6));
            Click("SortBpmButton",()=>ToggleLibrarySort(7,8));Click("SortDurationButton",()=>ToggleLibrarySort(3,4));UpdateSortHeaders();
            C<ListBox>("PlaylistList").SelectionChanged+=(s,e)=>{var p=C<ListBox>("PlaylistList").SelectedItem as LocalPlaylist;if(p!=null)ShowLibrary(p.Id);};
            C<ListBox>("SongList").MouseDoubleClick+=(s,e)=>{var row=ItemsControl.ContainerFromElement(C<ListBox>("SongList"),e.OriginalSource as DependencyObject) as ListBoxItem;var song=row==null?null:row.Content as Song;if(song==null)return;if(sessionActive){requestedNext=song;player.Stop();}else{SelectTrack(song);Play(true);}};
            var menu=new ContextMenu();
            foreach(var action in new[]{new KeyValuePair<string,Action>("喜欢 / 取消喜欢",ToggleFavorites),new KeyValuePair<string,Action>("加入播放队列",AddSelectionToQueue),new KeyValuePair<string,Action>("加入歌单…",AddToPlaylistDialog),new KeyValuePair<string,Action>("导出选中曲目…",ExportSelectedSongs),new KeyValuePair<string,Action>("删除选中曲目…",DeleteSelectedSongs)}) {
                var item=new MenuItem{Header=action.Key};Action handler=action.Value;item.Click+=(s,e)=>{try{handler();}catch(Exception ex){Error(ex);}};menu.Items.Add(item);
            }
            C<ListBox>("SongList").ContextMenu=menu;
            C<ListBox>("SongList").PreviewMouseRightButtonDown+=(s,e)=>{var row=ItemsControl.ContainerFromElement(C<ListBox>("SongList"),e.OriginalSource as DependencyObject) as ListBoxItem;if(row!=null&&!row.IsSelected){C<ListBox>("SongList").UnselectAll();row.IsSelected=true;}};
            C<ListBox>("SongList").PreviewKeyDown+=(s,e)=>{if(e.Key==Key.A&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){C<ListBox>("SongList").SelectAll();e.Handled=true;}};
            RefreshPlaylistList();RefreshBackgroundHint();
        }
        void CapturePerformancePreferences(bool strict) {
            if(loadingPreferences||loadedProfile==null)return;
            int seconds;bool valid=int.TryParse(C<ComboBox>("CountdownCombo").Text.Trim(),out seconds)&&seconds>=0&&seconds<=120;
            if(!valid&&strict)throw new FormatException("开始倒计时应填写 0～120 秒的整数。");
            var p=loadedProfile.Performance??new PerformancePreferences();
            p.Transpose=Transpose;p.Fold=Fold;p.AllParts=AllParts;p.SkipIntro=SkipIntro;p.GameAdapt=GameAdapt;p.Background=C<CheckBox>("BackgroundInputCheck").IsChecked==true;
            if(valid)p.Countdown=seconds;
            loadedProfile.Performance=p;
        }
        void SaveSettings(){Store.Write(Path.Combine(Store.Data,"settings.json"),settings);}
        void PersistTempo(bool strict) {
            if(selected==null)return;int bpm;
            if(!int.TryParse(C<TextBox>("BpmBox").Text,out bpm)||bpm<30||bpm>240){if(strict)throw new FormatException("速度应在 30～240 BPM 之间。");return;}
            if(selected.Bpm!=bpm){selected.Bpm=bpm;Save();RefreshSong();RefreshLibrary();}
        }
        void LoadPerformancePreferences() {
            loadingPreferences=true;
            try {
                loadedProfile=Profile;var p=Profile.Performance??new PerformancePreferences();Profile.Performance=p;
                C<ComboBox>("TransposeCombo").SelectedItem=Math.Max(-24,Math.Min(24,p.Transpose));
                C<ComboBox>("CountdownCombo").Text=Math.Max(0,Math.Min(120,p.Countdown)).ToString();
                C<CheckBox>("FoldCheck").IsChecked=p.Fold;C<CheckBox>("AllPartsCheck").IsChecked=p.AllParts;
                C<CheckBox>("SkipIntroCheck").IsChecked=p.SkipIntro;C<CheckBox>("GameAdaptCheck").IsChecked=p.GameAdapt;
                C<CheckBox>("BackgroundInputCheck").IsChecked=p.Background;
            }finally{loadingPreferences=false;}
            RefreshBackgroundHint();
        }
        void RefreshBackgroundHint() {
            var target=C<ComboBox>("WindowCombo").SelectedItem as GameWindow;
            bool naraka=(target!=null&&(target.Process??"").IndexOf("Naraka",StringComparison.OrdinalIgnoreCase)>=0)||(Game!=null&&Game.Name.Contains("永劫"));
            C<TextBlock>("BackgroundHint").Text=C<CheckBox>("BackgroundInputCheck").IsChecked==true?
                (naraka?"永劫后台会持续向所选窗口发消息，不会抢焦点，也不需要游戏接口。如果进度走动但游戏无声，表示当前版本没有响应后台消息；请使用下方“切回前台演奏”。管理员权限不等于支持后台。":"后台模式仅向所选窗口持续发送消息，不抢焦点。软件无法确认游戏是否采纳；无声时请切回前台模式。"):
                "前台模式使用键盘输入。进入游戏乐器界面后开始；切去其他软件自动暂停，回到游戏按 F9 继续。";
        }
        LocalPlaylist CurrentPlaylist {get{return settings.Playlists.FirstOrDefault(p=>p.Id==libraryView);}}
        List<Song> ResolveSongs(IEnumerable<string> ids) {var byId=songs.GroupBy(s=>s.Id).ToDictionary(g=>g.Key,g=>g.First());return ids.Where(id=>id!=null&&byId.ContainsKey(id)).Distinct().Select(id=>byId[id]).ToList();}
        List<Song> VisibleSongs() {
            IEnumerable<Song> source=songs;
            if(libraryView=="favorites")source=ResolveSongs(settings.Favorites);
            else if(libraryView=="history")source=ResolveSongs(settings.RecentSongs);
            else if(CurrentPlaylist!=null)source=ResolveSongs(CurrentPlaylist.SongIds);
            string query=C<TextBox>("SearchBox").Text.Trim();source=source.Where(s=>(s.Title??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||(s.Source??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
            int sort=C<ComboBox>("SortCombo").SelectedIndex;
            if(sort==1)source=source.OrderBy(s=>s.Title,StringComparer.CurrentCultureIgnoreCase);
            else if(sort==2)source=source.OrderByDescending(s=>s.Title,StringComparer.CurrentCultureIgnoreCase);
            else if(sort==3)source=source.OrderBy(s=>s.Length*60/Math.Max(1,s.Bpm));
            else if(sort==4)source=source.OrderByDescending(s=>s.Length*60/Math.Max(1,s.Bpm));
            else if(sort==5)source=source.OrderBy(s=>s.Source,StringComparer.CurrentCultureIgnoreCase);
            else if(sort==6)source=source.OrderByDescending(s=>s.Source,StringComparer.CurrentCultureIgnoreCase);
            else if(sort==7)source=source.OrderBy(s=>s.Bpm);
            else if(sort==8)source=source.OrderByDescending(s=>s.Bpm);
            return source.ToList();
        }
        void ToggleLibrarySort(int ascending,int descending) {C<ComboBox>("SortCombo").SelectedIndex=C<ComboBox>("SortCombo").SelectedIndex==ascending?descending:ascending;}
        void UpdateSortHeaders() {
            int sort=C<ComboBox>("SortCombo").SelectedIndex;
            string[] names={"SortTitleButton","SortSourceButton","SortBpmButton","SortDurationButton"},labels={"音乐标题","来源","BPM","时长"};
            int[] ascending={1,5,7,3},descending={2,6,8,4};
            for(int i=0;i<names.Length;i++){var b=C<Button>(names[i]);bool active=sort==ascending[i]||sort==descending[i];b.Content=labels[i]+(sort==ascending[i]?" ↑":sort==descending[i]?" ↓":"");b.Foreground=Brush(active?"#7657E8":"#746A88");}
        }
        void ShowLibrary(string view) {libraryView=view;if(CurrentPlaylist==null)C<ListBox>("PlaylistList").SelectedItem=null;RefreshLibrary();}
        void RefreshMusicLibrary() {
            if(settings.Favorites==null)return;
            var list=C<ListBox>("SongList");var picked=list.SelectedItems.Cast<Song>().Select(s=>s.Id).ToList();
            refreshingLibrary=true;
            try {
                foreach(var s in songs)s.FavoriteMark=settings.Favorites.Contains(s.Id)?"♥":"";
                var visible=VisibleSongs();list.ItemsSource=visible;
                foreach(var s in visible.Where(s=>picked.Contains(s.Id)))list.SelectedItems.Add(s);
                if(list.SelectedItems.Count==0&&selected!=null&&visible.Contains(selected))list.SelectedItem=selected;
                C<TextBlock>("SongCount").Text=visible.Count+" 首 · 本地共 "+songs.Count+" 首";
                C<TextBlock>("LibraryTitle").Text=libraryView=="favorites"?"我喜欢的音乐":libraryView=="history"?"最近播放":CurrentPlaylist!=null?CurrentPlaylist.Name:"本地音乐";
                C<TextBlock>("EmptyLibraryHint").Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;
                C<Button>("RemoveFromPlaylistButton").Visibility=CurrentPlaylist==null?Visibility.Collapsed:Visibility.Visible;
                C<Button>("RenamePlaylistButton").Visibility=C<Button>("DeletePlaylistButton").Visibility=CurrentPlaylist==null?Visibility.Collapsed:Visibility.Visible;
                foreach(var pair in new[]{new[]{"LibraryButton","all"},new[]{"FavoritesButton","favorites"},new[]{"HistoryButton","history"}})C<Button>(pair[0]).Background=Brush(viewMatches(pair[1])?"#FFE5E8":"#F7F7F9");
                C<Button>("QueueButton").Content="播放队列 · "+settings.QueueIds.Count;
            }finally{refreshingLibrary=false;}
            UpdateSelectionHint();
        }
        bool viewMatches(string view){return libraryView==view;}
        List<Song> SelectedSongs(){return C<ListBox>("SongList").SelectedItems.Cast<Song>().ToList();}
        void UpdateSelectionHint() {
            var picked=SelectedSongs();C<TextBlock>("SelectionHint").Text="已选 "+picked.Count+" 首 · Ctrl / Shift 多选";
            C<Button>("FavoriteButton").Content=picked.Count>0&&picked.All(s=>settings.Favorites.Contains(s.Id))?"取消喜欢":"♡ 喜欢";
        }
        void SelectTrack(Song song) {
            if(song==null)return;selected=song;settings.SelectedSongId=song.Id;pendingStartBeat=null;
            C<TextBox>("BpmBox").Text=song.Bpm.ToString();refreshingLibrary=true;
            try{C<ListBox>("SongList").SelectedItem=song;}finally{refreshingLibrary=false;}
            RefreshSong();UpdateSelectionHint();
        }
        void PruneLibraryReferences() {
            var valid=new HashSet<string>(songs.Select(s=>s.Id));
            settings.Favorites=settings.Favorites.Where(id=>id!=null&&valid.Contains(id)).Distinct().ToList();
            settings.RecentSongs=settings.RecentSongs.Where(id=>id!=null&&valid.Contains(id)).Distinct().Take(200).ToList();
            settings.QueueIds=settings.QueueIds.Where(id=>id!=null&&valid.Contains(id)).Distinct().ToList();
            foreach(var p in settings.Playlists)p.SongIds=p.SongIds.Where(id=>id!=null&&valid.Contains(id)).Distinct().ToList();
        }
        void ToggleFavorites() {
            var picked=SelectedSongs();if(picked.Count==0){Status("请先选择曲目。");return;}
            bool remove=picked.All(s=>settings.Favorites.Contains(s.Id));
            foreach(var s in picked){settings.Favorites.Remove(s.Id);if(!remove)settings.Favorites.Add(s.Id);}
            SaveSettings();RefreshLibrary();Status((remove?"已取消喜欢 ":"已收藏 ")+picked.Count+" 首曲目。");
        }
        void AddSelectionToQueue() {
            var picked=SelectedSongs();foreach(var s in picked)if(!settings.QueueIds.Contains(s.Id))settings.QueueIds.Add(s.Id);
            SaveSettings();RefreshLibrary();Status("已将选中曲目加入播放队列，同一曲目不会重复添加。");
        }
        void RecordRecent(Song song) {settings.RecentSongs.Remove(song.Id);settings.RecentSongs.Insert(0,song.Id);if(settings.RecentSongs.Count>200)settings.RecentSongs.RemoveRange(200,settings.RecentSongs.Count-200);SaveSettings();}
        void EnsureQueue() {
            if(settings.QueueIds.Count==0)settings.QueueIds=VisibleSongs().Select(s=>s.Id).ToList();
            if(selected!=null&&!settings.QueueIds.Contains(selected.Id))settings.QueueIds.Insert(0,selected.Id);
            SaveSettings();C<Button>("QueueButton").Content="播放队列 · "+settings.QueueIds.Count;
        }
        Song QueueNext(int mode,int direction,bool manual) {
            var queue=ResolveSongs(settings.QueueIds);if(queue.Count==0)return null;
            if(!manual&&mode==2)return selected;
            if(mode==4){var other=queue.Where(s=>s!=selected).ToList();return other.Count==0?queue[0]:other[shuffle.Next(other.Count)];}
            int current=queue.IndexOf(selected);if(current<0)return direction>0?queue[0]:queue[queue.Count-1];int next=current+direction;
            if(next>=0&&next<queue.Count)return queue[next];
            return manual||mode==3?queue[(next+queue.Count)%queue.Count]:null;
        }
        void SkipTrack(int direction) {
            EnsureQueue();var next=QueueNext(PlaybackMode,direction,true);if(next==null)return;
            if(sessionActive){requestedNext=next;player.Stop();Status("正在切换到："+next.Title);}else{SelectTrack(next);SaveSettings();}
        }
        void StopPlayback(){requestedNext=null;player.Stop();}
        void QueueDialog() {
            var dialog=Dialog("播放队列",680,570);var root=new DockPanel{Margin=new Thickness(22)};dialog.Content=root;
            var info=Label("拖动进度可跳转；上一曲 / 下一曲按此队列切换。顺序与曲目会保存到下次启动。");DockPanel.SetDock(info,Dock.Top);root.Children.Add(info);
            var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
            var list=new ListBox{SelectionMode=SelectionMode.Extended};root.Children.Add(list);
            Action refresh=()=>{list.ItemsSource=ResolveSongs(settings.QueueIds);SaveSettings();C<Button>("QueueButton").Content="播放队列 · "+settings.QueueIds.Count;};
            actions.Children.Add(Button("移到上一位",()=>{var s=list.SelectedItem as Song;if(s==null)return;int i=settings.QueueIds.IndexOf(s.Id);if(i>0){settings.QueueIds.RemoveAt(i);settings.QueueIds.Insert(i-1,s.Id);refresh();list.SelectedItem=s;}}));
            actions.Children.Add(Button("移到下一位",()=>{var s=list.SelectedItem as Song;if(s==null)return;int i=settings.QueueIds.IndexOf(s.Id);if(i>=0&&i<settings.QueueIds.Count-1){settings.QueueIds.RemoveAt(i);settings.QueueIds.Insert(i+1,s.Id);refresh();list.SelectedItem=s;}}));
            actions.Children.Add(Button("移出选中",()=>{foreach(var s in list.SelectedItems.Cast<Song>().ToList())settings.QueueIds.Remove(s.Id);refresh();}));
            actions.Children.Add(Button("清空队列",()=>{settings.QueueIds.Clear();refresh();}));
            actions.Children.Add(Button("播放选中",()=>{var s=list.SelectedItem as Song;if(s==null)return;dialog.Close();if(sessionActive){requestedNext=s;player.Stop();}else{SelectTrack(s);Play(sessionPreview);}}));
            refresh();dialog.ShowDialog();
        }
        void RefreshPlaylistList(){var list=C<ListBox>("PlaylistList");list.ItemsSource=null;list.ItemsSource=settings.Playlists;}
        void PlaylistNameDialog(LocalPlaylist playlist) {
            var dialog=Dialog(playlist==null?"新建歌单":"重命名歌单",430,230);var panel=new StackPanel{Margin=new Thickness(22)};dialog.Content=panel;
            panel.Children.Add(Label("歌单名称"));var name=new TextBox{Text=playlist==null?"我的歌单":playlist.Name};panel.Children.Add(name);
            panel.Children.Add(Button("保存歌单",()=>{string value=name.Text.Trim();if(value.Length==0||value.Length>60)throw new FormatException("歌单名称应为 1～60 个字符。");var p=playlist??new LocalPlaylist();p.Name=value;if(playlist==null)settings.Playlists.Add(p);SaveSettings();RefreshPlaylistList();C<ListBox>("PlaylistList").SelectedItem=p;dialog.Close();}));dialog.ShowDialog();
        }
        void AddToPlaylistDialog() {
            var picked=SelectedSongs();if(picked.Count==0){Status("请先多选需要加入歌单的曲目。");return;}
            var dialog=Dialog("加入歌单",450,330);var panel=new StackPanel{Margin=new Thickness(22)};dialog.Content=panel;panel.Children.Add(Label("将 "+picked.Count+" 首曲目加入已有歌单，或输入名称新建。"));
            var list=new ComboBox{ItemsSource=settings.Playlists,SelectedIndex=settings.Playlists.Count>0?0:-1};panel.Children.Add(list);var name=new TextBox{ToolTip="输入名称则新建歌单"};panel.Children.Add(name);
            panel.Children.Add(Button("确认加入",()=>{LocalPlaylist p;string title=name.Text.Trim();if(title.Length>0){if(title.Length>60)throw new FormatException("歌单名不能超过 60 字。");p=new LocalPlaylist{Name=title};settings.Playlists.Add(p);}else p=list.SelectedItem as LocalPlaylist;if(p==null)throw new FormatException("请选择歌单或填写新歌单名称。");foreach(var s in picked)if(!p.SongIds.Contains(s.Id))p.SongIds.Add(s.Id);SaveSettings();RefreshPlaylistList();dialog.Close();Status("已加入歌单："+p.Name);}));dialog.ShowDialog();
        }
        void DeletePlaylist() {
            var p=CurrentPlaylist;if(p==null)return;
            if(MessageBox.Show(window,"删除歌单“"+p.Name+"”？曲目仍保留在本地曲库。","删除歌单",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            settings.Playlists.Remove(p);libraryView="all";SaveSettings();RefreshPlaylistList();RefreshLibrary();
        }
        void RemoveFromPlaylist() {var p=CurrentPlaylist;if(p==null)return;foreach(var s in SelectedSongs())p.SongIds.Remove(s.Id);SaveSettings();RefreshLibrary();}
        void DeleteSelectedSongs() {
            if(sessionActive)return;var picked=SelectedSongs();if(picked.Count==0)return;
            if(MessageBox.Show(window,"从本地曲库删除选中的 "+picked.Count+" 首曲目？收藏、歌单和队列中的引用会一并移除。原始文件不会删除。","批量删除",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            foreach(var s in picked)songs.Remove(s);PruneLibraryReferences();
            if(selected!=null&&picked.Contains(selected))selected=songs.FirstOrDefault();
            settings.SelectedSongId=selected==null?null:selected.Id;Save();RefreshLibrary();
            if(selected!=null)SelectTrack(selected);else ClearSongDisplay();Status("已删除 "+picked.Count+" 首本地曲目。");
        }
        void ClearSongDisplay() {
            selected=null;pendingStartBeat=null;tiles.Clear();tileBeats.Clear();C<WrapPanel>("NotePanel").Children.Clear();
            C<TextBlock>("SongTitle").Text="选择一首曲目";C<TextBlock>("SongMeta").Text="导入曲谱，开始你的演奏";
            C<TextBlock>("MapStatus").Text="等待选择曲目";C<TextBlock>("GameAdaptHint").Text="";C<TextBlock>("IntroHint").Text="";
            C<TextBlock>("TimeLabel").Text="00:00";C<TextBlock>("DurationLabel").Text="00:00";
            updatingProgress=true;try{C<Slider>("ProgressBar").Value=0;}finally{updatingProgress=false;}
        }
        void ExportSelectedSongs() {
            var picked=SelectedSongs();if(picked.Count==0)return;
            if(picked.Count==1){var picker=new SaveFileDialog{Filter="dogeplay曲谱|*.json",FileName=string.Join("_",picked[0].Title.Split(Path.GetInvalidFileNameChars()))+".json"};if(picker.ShowDialog(window)==true){Store.Write(picker.FileName,picked[0]);Status("已导出曲谱。");}return;}
            var zip=new SaveFileDialog{Filter="曲谱合集 ZIP|*.zip",FileName="dogeplay曲谱合集.zip"};if(zip.ShowDialog(window)!=true)return;
            string temp=zip.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var archive=System.IO.Compression.ZipFile.Open(temp,System.IO.Compression.ZipArchiveMode.Create)) {
                    int index=0;foreach(var s in picked){string title=string.Join("_",s.Title.Split(Path.GetInvalidFileNameChars()));if(title.Length>80)title=title.Substring(0,80);var entry=archive.CreateEntry((++index).ToString("000")+"_"+title+".json");using(var writer=new StreamWriter(entry.Open(),new System.Text.UTF8Encoding(false)))writer.Write(Store.Json.Serialize(s));}
                }
                if(File.Exists(zip.FileName))File.Replace(temp,zip.FileName,null);else File.Move(temp,zip.FileName);
            }finally{if(File.Exists(temp))File.Delete(temp);}
            Status("已导出 "+picked.Count+" 首曲谱；解压后可逐个导入 JSON。");
        }
    }
}
