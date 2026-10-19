using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LumaKeys {
    public partial class App {
        bool loadingInstruments;
        GameProfile Game {get{return C<ComboBox>("GameCombo").SelectedItem as GameProfile;}}
        static readonly string[] InstrumentSounds={"钢琴","尼龙弦吉他","竖琴","小提琴","大提琴","长笛","单簧管","笛管","筝（近似）"};
        static readonly int[] InstrumentPrograms={0,24,46,40,42,73,71,75,107};

        static InstrumentProfile CopyInstrument(InstrumentProfile source) {
            // A deep copy keeps timing and performance preferences independent.
            return new InstrumentProfile{Id=Guid.NewGuid().ToString("N"),Name=source.Name,Keys=source.Keys,BaseMidi=source.BaseMidi,MidiPitches=source.MidiPitches,PreviewProgram=source.PreviewProgram,SustainNotes=source.SustainNotes,Monophonic=source.Monophonic,
                Timing=Store.Json.Deserialize<GameTimingSettings>(Store.Json.Serialize(source.Timing??new GameTimingSettings())),
                Performance=Store.Json.Deserialize<PerformancePreferences>(Store.Json.Serialize(source.Performance??new PerformancePreferences()))};
        }
        void PrepareInstruments() {
            foreach(var game in settings.Profiles) {
                if(game.Instruments==null||game.Instruments.Count==0) {
                    var original=CopyInstrument(game);original.Name=game.Id=="naraka-master-v1"?"古筝 · 大师 36 键":game.Name.Contains(" · ")?game.Name.Substring(game.Name.IndexOf(" · ")+3):"原有乐器";
                    game.Instruments=new List<InstrumentProfile>{original};game.SelectedInstrumentId=original.Id;
                }
                var ids=new HashSet<string>();
                foreach(var instrument in game.Instruments.Where(i=>i!=null)) {
                    if(string.IsNullOrWhiteSpace(instrument.Id)||!ids.Add(instrument.Id)){instrument.Id=Guid.NewGuid().ToString("N");ids.Add(instrument.Id);}
                    if(string.IsNullOrWhiteSpace(instrument.Name))instrument.Name="未命名乐器";
                    if(instrument.PreviewProgram<0||instrument.PreviewProgram>127)instrument.PreviewProgram=0;
                    if(instrument.Performance==null)instrument.Performance=new PerformancePreferences();
                    try{if(instrument.Timing==null)instrument.Timing=new GameTimingSettings();instrument.Timing.Validate();}catch{instrument.Timing=new GameTimingSettings();}
                }
                game.Instruments=game.Instruments.Where(i=>i!=null).ToList();
                if(game.Instruments.Count==0)game.Instruments.Add(CopyInstrument(game));
                foreach(var flute in game.Instruments.Where(i=>i.Id=="naraka-qudi-v1"||(i.Name??"").StartsWith("曲笛",StringComparison.Ordinal))) {
                    flute.SustainNotes=true;flute.Monophonic=true;
                    if(flute.Performance==null)flute.Performance=new PerformancePreferences();
                    flute.Performance.AllParts=false;
                }
            }
        }
        void InitializeInstrumentControls() {
            PrepareInstruments();LoadInstrumentList();
            C<ComboBox>("GameCombo").SelectionChanged+=(s,e)=>{
                if(!ready||Game==null)return;CapturePerformancePreferences(false);settings.SelectedGame=Game.Name;
                LoadInstrumentList();LoadPerformancePreferences();LoadTimingControls();SaveSettings();
                C<ComboBox>("WindowCombo").SelectedItem=null;RefreshWindows();RefreshSong();RefreshInstrumentHint();
            };
            C<ComboBox>("InstrumentCombo").SelectionChanged+=(s,e)=>{
                if(!ready||loadingInstruments||Profile==null)return;
                CapturePerformancePreferences(false);Game.SelectedInstrumentId=Profile.Id;
                LoadPerformancePreferences();LoadTimingControls();SaveSettings();RefreshSong();RefreshInstrumentHint();
            };
            Click("AddInstrumentButton",()=>InstrumentDialog(true));
            Click("EditInstrumentButton",()=>InstrumentDialog(false));
            Click("DeleteInstrumentButton",DeleteInstrument);
            Click("InstrumentPreviewButton",()=>Play(true,true));
        }
        void LoadInstrumentList() {
            loadingInstruments=true;
            try {
                C<ComboBox>("InstrumentCombo").ItemsSource=null;
                C<ComboBox>("InstrumentCombo").ItemsSource=Game.Instruments;
                C<ComboBox>("InstrumentCombo").SelectedItem=Game.Instruments.FirstOrDefault(i=>i.Id==Game.SelectedInstrumentId)??Game.Instruments[0];
                Game.SelectedInstrumentId=Profile.Id;
            }finally{loadingInstruments=false;}
            RefreshInstrumentHint();
        }
        void RefreshInstrumentHint() {
            if(Profile==null)return;
            try{var map=new Mapping(Profile);C<TextBlock>("InstrumentHint").Text=map.Map.Count+" 键 · MIDI "+map.Map.Keys.Min()+"～"+map.Map.Keys.Max()+"。"+(Profile.SustainNotes?"长按至谱面音符结束；" : "")+(Profile.Monophonic?"从完整原谱提取连贯旋律，合奏由古筝演奏者承担伴奏；":"")+"请在游戏里切换到同名乐器，并核对实际键位与音域。";}
            catch(Exception ex){C<TextBlock>("InstrumentHint").Text="此乐器键位需要修正："+ex.Message;}
        }
        void InstrumentDialog(bool create) {
            if(sessionActive)return;CapturePerformancePreferences(false);
            var original=Profile;var dialog=Dialog(create?"添加乐器方案":"编辑乐器方案",660,740);
            var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var panel=new StackPanel{Margin=new Thickness(26)};scroll.Content=panel;dialog.Content=scroll;
            panel.Children.Add(new TextBlock{Text=create?"同一份谱，换一种乐器":"编辑“"+original.Name+"”",FontSize=22,FontWeight=FontWeights.Bold});
            panel.Children.Add(Label("按游戏内的乐器键位和实际音域填写。新方案复制当前演奏设置，之后独立保存；不会复制或改写歌曲。"));
            panel.Children.Add(Label("乐器名称 / 模式"));var name=new TextBox{Text=create?"新乐器":original.Name};panel.Children.Add(name);
            panel.Children.Add(Label("按键（按音高顺序，用空格分隔；支持字母、主键盘数字和符号）"));var keys=new TextBox{Text=original.Keys,TextWrapping=TextWrapping.Wrap};panel.Children.Add(keys);
            panel.Children.Add(Label("逗号=Comma，句号=Period，斜杠=Slash，分号=Semicolon，减号=Minus，左方括号=LeftBracket。数字键不是小键盘。"));
            panel.Children.Add(Label("自然音阶最低音 MIDI（24～84 的 C 音；C3=48，C4=60）"));var baseMidi=new TextBox{Text=original.BaseMidi.ToString()};panel.Children.Add(baseMidi);
            panel.Children.Add(Label("逐键音高（可选）：与上方每个键一一对应，如 60 62 64 65 67 69 71。填写后替代自然音阶；支持半音和不连续音域。"));
            var pitches=new TextBox{Text=original.MidiPitches??"",TextWrapping=TextWrapping.Wrap,MinHeight=56};panel.Children.Add(pitches);
            var templates=new WrapPanel();panel.Children.Add(templates);
            templates.Children.Add(Button("按自然音阶生成音高",()=>{int low;if(!int.TryParse(baseMidi.Text,out low))throw new FormatException("请填写最低音 MIDI。");var temp=new InstrumentProfile{Keys=keys.Text,BaseMidi=low};var map=new Mapping(temp);pitches.Text=string.Join(" ",map.Map.Keys.OrderBy(n=>n));}));
            templates.Children.Add(Button("按连续半音生成音高",()=>{int low;if(!int.TryParse(baseMidi.Text,out low)||low<0||low>127)throw new FormatException("请填写 0～127 的起始 MIDI 音高。");int count=Mapping.ParseKeys(keys.Text).Length;if(low+count-1>127)throw new FormatException("音域超过 MIDI 127。");pitches.Text=string.Join(" ",Enumerable.Range(low,count));}));
            panel.Children.Add(Label("本地试听音色（用于“乐器试听”，不会改变游戏内音色）"));
            var sound=new ComboBox{ItemsSource=InstrumentSounds,SelectedIndex=Math.Max(0,Array.IndexOf(InstrumentPrograms,original.PreviewProgram))};panel.Children.Add(sound);
            var sustain=new CheckBox{Content="持续长按：按谱面时值保持按键（适合曲笛等吹奏乐器）",IsChecked=original.SustainNotes};panel.Children.Add(sustain);
            var monophonic=new CheckBox{Content="单旋律：优先旋律声部，同拍选择最高音",IsChecked=original.Monophonic};panel.Children.Add(monophonic);
            panel.Children.Add(Label("游戏进程名（同游戏共用；多个名称用 | 分隔）"));var process=new TextBox{Text=Game.ProcessHint};panel.Children.Add(process);
            panel.Children.Add(Label("需要吹奏单旋律时，可在主界面取消“旋律与伴奏全部声部”。游戏实际音色由游戏里选用的乐器决定；这里的试听仅作近似参考。"));
            panel.Children.Add(Button(create?"创建并使用":"保存乐器",()=>{
                string title=name.Text.Trim();if(title.Length==0||title.Length>60)throw new FormatException("乐器名称应为 1～60 个字符。");
                if(Game.Instruments.Any(i=>i!=original&&string.Equals(i.Name,title,StringComparison.CurrentCultureIgnoreCase))||(create&&string.Equals(original.Name,title,StringComparison.CurrentCultureIgnoreCase)))throw new FormatException("此游戏已有同名乐器，请换个名称或注明模式。");
                int low;if(!int.TryParse(baseMidi.Text,out low))throw new FormatException("最低音 MIDI 必须是整数。");
                var candidate=CopyInstrument(original);candidate.Name=title;candidate.Keys=keys.Text.Trim().ToUpperInvariant();candidate.BaseMidi=low;candidate.MidiPitches=pitches.Text.Trim();candidate.PreviewProgram=InstrumentPrograms[sound.SelectedIndex];candidate.SustainNotes=sustain.IsChecked==true;candidate.Monophonic=monophonic.IsChecked==true;new Mapping(candidate);
                if(create){Game.Instruments.Add(candidate);Game.SelectedInstrumentId=candidate.Id;}
                else{original.Name=candidate.Name;original.Keys=candidate.Keys;original.BaseMidi=candidate.BaseMidi;original.MidiPitches=candidate.MidiPitches;original.PreviewProgram=candidate.PreviewProgram;original.SustainNotes=candidate.SustainNotes;original.Monophonic=candidate.Monophonic;}
                Game.ProcessHint=process.Text.Trim();LoadInstrumentList();LoadPerformancePreferences();LoadTimingControls();SaveSettings();RefreshWindows();RefreshSong();dialog.Close();Status("已保存乐器："+Profile.Name+"。请在游戏中切换对应乐器。");
            }));dialog.ShowDialog();
        }
        void DeleteInstrument() {
            if(sessionActive)return;if(Game.Instruments.Count<=1){Status("每个游戏至少保留一个乐器方案。");return;}
            var old=Profile;if(MessageBox.Show(window,"删除乐器方案“"+old.Name+"”？曲谱不会删除。","删除乐器",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            Game.Instruments.Remove(old);Game.SelectedInstrumentId=Game.Instruments[0].Id;
            LoadInstrumentList();LoadPerformancePreferences();LoadTimingControls();SaveSettings();RefreshSong();Status("已删除乐器方案，曲谱保持不变。");
        }
    }
}
