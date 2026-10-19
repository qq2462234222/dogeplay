using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LumaKeys {
    public class Note {
        public double Beat { get; set; }
        public double Duration { get; set; }
        public int Midi { get; set; }
        public int Part { get; set; }
        [ScriptIgnore] public string PitchName {
            get{return Notation.Label(Midi);}
            set {var parsed=Notation.Parse("音高",value,90);if(parsed.Notes.Count!=1)throw new FormatException("每行填写一个音符，如 1、3'、5,。");Midi=parsed.Notes[0].Midi;}
        }
        public Note() {}
        public Note(double beat, double duration, int midi) { Beat=beat; Duration=duration; Midi=midi; }
    }
    public class Song {
        [ScriptIgnore] public string FavoriteMark {get;set;}
        [ScriptIgnore] public string DisplayDuration {get{return TimeSpan.FromSeconds(Length*60/Math.Max(1,Bpm)).ToString(@"hh\:mm\:ss");}}
        public string Id { get; set; }
        public string Title { get; set; }
        public string Source { get; set; }
        public string SourceUrl { get; set; }
        public string License { get; set; }
        public int Bpm { get; set; }
        public List<Note> Notes { get; set; }
        public List<string> PartNames {get;set;}
        public Song() { Id=Guid.NewGuid().ToString("N"); Title="未命名曲谱"; Bpm=90; Notes=new List<Note>();PartNames=new List<string>(); }
        public Song Playback(bool allParts) {int first=Notes.Count==0?0:Notes.Min(n=>n.Part);return new Song{Id=Id,Title=Title,Bpm=Bpm,Source=Source,SourceUrl=SourceUrl,License=License,PartNames=PartNames,Notes=allParts?Notes:Notes.Where(n=>n.Part==first).ToList()};}
        public double Length { get { return Notes.Count==0 ? 0 : Notes.Max(n=>n.Beat+n.Duration); } }
        public override string ToString() { return Title; }
    }
    public class InstrumentProfile {
        public string Id {get;set;}
        public string MidiPitches {get;set;}
        public int PreviewProgram {get;set;}
        public bool SustainNotes {get;set;}
        public bool Monophonic {get;set;}
        public PerformancePreferences Performance {get;set;}
        public GameTimingSettings Timing {get;set;}
        public InstrumentProfile(){Id=Guid.NewGuid().ToString("N");Timing=new GameTimingSettings();}
        public string Name { get; set; }
        public string Keys { get; set; }
        public int BaseMidi { get; set; }
        public override string ToString() { return Name; }
    }
    public class GameProfile : InstrumentProfile {
        [ScriptIgnore] public string GameName {get{return (Name??"").Split(new[]{" · "},StringSplitOptions.None)[0];}}
        public string ProcessHint { get; set; }
        public List<InstrumentProfile> Instruments {get;set;}
        public string SelectedInstrumentId {get;set;}
    }
    public class Settings {
        public bool BundledSongsInitialized {get;set;}
        public bool OnboardingCompleted {get;set;}
        // Announcement prompts are dismissed per announcement/version key;
        // a new server version automatically makes the prompt visible again.
        public string AnnouncementDismissedKey {get;set;}
        public double LearningWidth {get;set;}
        public double LearningHeight {get;set;}
        public double LearningLeft {get;set;}
        public double LearningTop {get;set;}
        public double LearningOpacity {get;set;}
        public List<string> Favorites {get;set;}
        public List<string> RecentSongs {get;set;}
        public List<string> QueueIds {get;set;}
        public List<LocalPlaylist> Playlists {get;set;}
        public int LibrarySort {get;set;}
        public List<GameProfile> Profiles {get;set;}
        public string SelectedGame {get;set;}
        public string SelectedSongId {get;set;}
        public int PlaybackModeIndex {get;set;}
        public string AudiverisPath {get;set;}
        public bool AudiverisCustom {get;set;}
        public Settings() {
            Favorites=new List<string>();RecentSongs=new List<string>();QueueIds=new List<string>();Playlists=new List<LocalPlaylist>();
            Profiles=new List<GameProfile> {
                new GameProfile {Name="永劫无间 · 21 键", ProcessHint="Naraka", Keys="Z X C V B N M A S D F G H J Q W E R T Y U",BaseMidi=48},
                NarakaMaster(),
                new GameProfile {Name="原神 · 风物之诗琴", ProcessHint="YuanShen|GenshinImpact", Keys="Z X C V B N M A S D F G H J Q W E R T Y U",BaseMidi=48},
                new GameProfile {Name="光遇 · 15 键", ProcessHint="Sky", Keys="Z X C V B A S D F G Q W E R T",BaseMidi=60},
                new GameProfile {Name="自定义游戏", ProcessHint="", Keys="Z X C V B N M A S D F G H J Q W E R T Y U",BaseMidi=48}
            }; SelectedGame=Profiles[0].Name;
        }
        public static GameProfile NarakaMaster() {
            // Note/key correspondence: https://www.bilibili.com/video/BV1pA4m1L7L8/
            var game=new GameProfile{Id="naraka-master-v1",Name="永劫无间大师模式",ProcessHint="Naraka",BaseMidi=48,
                Keys="Z S X D C V G B H N J M Comma L Period Semicolon Slash Q 2 W 3 E 4 R T 6 Y 7 U I 9 O 0 P Minus LeftBracket",
                MidiPitches=string.Join(" ",Enumerable.Range(48,36)),PreviewProgram=107,Performance=new PerformancePreferences()};
            return game;
        }
        public void EnsureNarakaMaster() {
            if(!Profiles.Any(p=>p.Id=="naraka-master-v1"||p.Name=="永劫无间大师模式"))Profiles.Add(NarakaMaster());
        }
        public void EnsureNarakaInstruments() {
            foreach(var game in Profiles.Where(p=>(p.ProcessHint??"").IndexOf("Naraka",System.StringComparison.OrdinalIgnoreCase)>=0)) {
                if(game.Instruments==null)game.Instruments=new List<InstrumentProfile>();
                bool master=game.Id=="naraka-master-v1"||game.Name.Contains("大师模式");
                if(game.Instruments.Count==0) {
                    var guzheng=new InstrumentProfile{Id=master?"naraka-guzheng-master-v1":"naraka-guzheng-standard-v1",Name=master?"古筝 · 大师 36 键":"古筝 · 普通 21 键",Keys=game.Keys,BaseMidi=game.BaseMidi,MidiPitches=game.MidiPitches,PreviewProgram=107,Performance=new PerformancePreferences(),Timing=new GameTimingSettings()};
                    game.Instruments.Add(guzheng);game.SelectedInstrumentId=guzheng.Id;
                }
                if(!game.Instruments.Any(i=>i!=null&&((i.Id??"")=="naraka-qudi-v1"||(i.Name??"").StartsWith("曲笛",System.StringComparison.Ordinal)))) {
                    var qudi=new InstrumentProfile{Id="naraka-qudi-v1",Name=master?"曲笛 · 大师长按":"曲笛 · 长按",Keys=game.Keys,BaseMidi=game.BaseMidi,MidiPitches=game.MidiPitches,PreviewProgram=73,SustainNotes=true,Monophonic=true,Performance=new PerformancePreferences{AllParts=false},Timing=new GameTimingSettings()};
                    game.Instruments.Add(qudi);
                }
            }
        }
    }
    public class PerformancePreferences {
        public int Transpose {get;set;}
        public int Countdown {get;set;}
        public bool Fold {get;set;}
        public bool SkipIntro {get;set;}
        public bool AllParts {get;set;}
        public bool GameAdapt {get;set;}
        public bool Background {get;set;}
        public PerformancePreferences(){Countdown=5;Fold=true;SkipIntro=true;AllParts=true;GameAdapt=true;}
    }
    public class LocalPlaylist {
        public string Id {get;set;}
        public string Name {get;set;}
        public List<string> SongIds {get;set;}
        public LocalPlaylist(){Id=Guid.NewGuid().ToString("N");Name="新建歌单";SongIds=new List<string>();}
        public override string ToString(){return Name;}
    }
    public static class Store {
        public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string Data=Path.Combine(Root,"data");
        public static JavaScriptSerializer Json { get { return new JavaScriptSerializer { MaxJsonLength=16000000 }; } }
        public static T Read<T>(string path) { return Json.Deserialize<T>(File.ReadAllText(path,Encoding.UTF8)); }
        public static void Write(string path,object data) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";
            File.WriteAllText(temp,Json.Serialize(data),new UTF8Encoding(false));
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
        public static List<Song> Demos() {
            var songs=new List<Song> {
                Notation.Parse("小星星", "1 1 5 5 6 6 5:2 | 4 4 3 3 2 2 1:2 | 5 5 4 4 3 3 2:2 | 5 5 4 4 3 3 2:2 | 1 1 5 5 6 6 5:2 | 4 4 3 3 2 2 1:2",100),
                Notation.Parse("欢乐颂", "3 3 4 5 | 5 4 3 2 | 1 1 2 3 | 3:1.5 2:0.5 2:2 | 3 3 4 5 | 5 4 3 2 | 1 1 2 3 | 2:1.5 1:0.5 1:2",108),
                Notation.Parse("月光练习 · 和弦", "[1,3,5]:2 3 5 | [2,4,6]:2 4 6 | [3,5,7]:2 5 7 | [1',3',5']:4",76)
            };
            foreach(var song in songs){song.Source="经典练习曲 · 本地";song.License="公有领域旋律，重新录入";}
            songs[2].Source="dogeplay示例";songs[2].License="CC0-1.0";return songs;
        }
        public static Song ChordDemo() {
            var song=Notation.Parse("小星星 · 旋律与伴奏", "1 1 5 5 6 6 5:2 | 4 4 3 3 2 2 1:2 | 5 5 4 4 3 3 2:2 | 5 5 4 4 3 3 2:2 | 1 1 5 5 6 6 5:2 | 4 4 3 3 2 2 1:2",100);
            int[][] chords={new[]{48,52,55},new[]{48,52,55},new[]{53,57,60},new[]{48,52,55},new[]{53,57,60},new[]{48,52,55},new[]{55,59,62},new[]{48,52,55},new[]{48,52,55},new[]{53,57,60},new[]{55,59,62},new[]{55,59,62},new[]{48,52,55},new[]{53,57,60},new[]{55,59,62},new[]{55,59,62},new[]{48,52,55},new[]{48,52,55},new[]{53,57,60},new[]{48,52,55},new[]{53,57,60},new[]{48,52,55},new[]{55,59,62},new[]{48,52,55}};
            for(int i=0;i<chords.Length;i++)foreach(int pitch in chords[i])song.Notes.Add(new Note(i*2,2,pitch){Part=1});
            song.Notes=song.Notes.OrderBy(n=>n.Beat).ThenBy(n=>n.Midi).ToList();song.PartNames=new List<string>{"旋律","和弦伴奏"};song.Source="dogeplay编配 · 多声部示例";song.License="CC0-1.0";return song;
        }
    }
    public static class Notation {
        public static readonly int[] Steps={0,2,4,5,7,9,11};
        static int Pitch(string token) {
            Match m=Regex.Match(token,@"^([#b]?)([1-7])([' ,]*)$");
            if(!m.Success) throw new FormatException("无法识别音符："+token+"。示例：1 2 3' 5, [1,3,5]:2");
            int octave=m.Groups[3].Value.Count(c=>c=='\'')-m.Groups[3].Value.Count(c=>c==',');
            return 60+Steps[int.Parse(m.Groups[2].Value)-1]+octave*12+(m.Groups[1].Value=="#"?1:m.Groups[1].Value=="b"?-1:0);
        }
        public static Song Parse(string title,string text,int bpm) {
            var song=new Song {Title=title,Bpm=bpm,Source="本地简谱",License="用户导入"};
            double beat=0;
            foreach(Match match in Regex.Matches(text,@"\[[^\]]+\](?::[0-9.]+)?|[^\s|]+")) {
                string token=match.Value;
                if(token=="-") { var prev=song.Notes.Where(n=>Math.Abs(n.Beat+n.Duration-beat)<0.001).ToList(); foreach(var n in prev) n.Duration+=1; beat+=1; continue; }
                string[] pair=token.Split(':'); double duration=1;
                if(pair.Length>2 || (pair.Length==2 && (!double.TryParse(pair[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out duration) || double.IsNaN(duration) || double.IsInfinity(duration) || duration<=0 || duration>64))) throw new FormatException("时值必须在 0～64 拍之间："+token);
                if(pair[0]=="0") {beat+=duration; continue;}
                string notes=pair[0];
                if(notes.StartsWith("[")) {
                    string body=notes.Trim('[',']');
                    // Whitespace separates chords with octave commas; comma-only chords remain supported.
                    char[] separators=body.Contains(" ")?new[]{' '}:new[]{','};
                    foreach(string s in body.Split(separators,StringSplitOptions.RemoveEmptyEntries)) song.Notes.Add(new Note(beat,duration,Pitch(s)));
                } else song.Notes.Add(new Note(beat,duration,Pitch(notes)));
                beat+=duration;
            }
            if(song.Notes.Count==0) throw new FormatException("没有找到音符，请输入 1～7 的简谱。");
            if(song.Notes.Any(n=>n.Midi<0||n.Midi>127)) throw new FormatException("音高超出 MIDI 范围。");
            return song;
        }
        public static string Label(int midi) {
            int pc=((midi%12)+12)%12; int index=Array.IndexOf(Steps,pc); string accidental="";
            if(index<0) {index=Array.IndexOf(Steps,pc-1); accidental="#";}
            int oct=(midi/12)-5;
            return accidental+(index+1)+(oct>0?new string('\'',oct):new string(',',-oct));
        }
        public static string Export(Song song) {
            var result=new StringBuilder(); double cursor=0;
            foreach(var group in song.Notes.OrderBy(n=>n.Beat).GroupBy(n=>n.Beat)) {
                if(group.Key>cursor+0.001) result.Append("0:"+(group.Key-cursor).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" ");
                var list=group.ToList();
                // Exact heterogeneous chords use the event editor instead of a lossy round trip.
                string note=list.Count==1?Label(list[0].Midi):"["+string.Join(" ",list.Select(n=>Label(n.Midi)))+"]";
                result.Append(note+":"+list.Max(n=>n.Duration).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" ");
                cursor=group.Key+list.Max(n=>n.Duration);
            }return result.ToString();
        }
    }
    public class Mapping {
        public Dictionary<int,char> Map=new Dictionary<int,char>();
        public bool SustainNotes {get;private set;}
        public bool Monophonic {get;private set;}
        static readonly Dictionary<string,char> KeyNames=new Dictionary<string,char>(StringComparer.OrdinalIgnoreCase) {
            {"Comma",','},{"Period",'.'},{"Slash",'/'},{"Semicolon",';'},{"Minus",'-'},
            {"LeftBracket",'['},{"RightBracket",']'},{"Backslash",'\\'},{"Equal",'='},{"Apostrophe",'\''},{"Backtick",'`'}
        };
        public static uint VirtualKey(char key) {
            key=char.ToUpperInvariant(key);
            if((key>='A'&&key<='Z')||(key>='0'&&key<='9'))return key;
            switch(key) {
                case ';':return 0xBA;case '=':return 0xBB;case ',':return 0xBC;case '-':return 0xBD;
                case '.':return 0xBE;case '/':return 0xBF;case '`':return 0xC0;
                case '[':return 0xDB;case '\\':return 0xDC;case ']':return 0xDD;case '\'':return 0xDE;
                default:throw new FormatException("不支持的按键："+key);
            }
        }
        public static char[] ParseKeys(string text) {
            // Preserve old comma/semicolon-separated letter configurations. Use named aliases for these keys.
            var tokens=(text??"").Split(new[]{' ','，',',',';','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries);
            var keys=new List<char>();
            foreach(string token in tokens) {
                if(token.Equals("Esc",StringComparison.OrdinalIgnoreCase)||token.Equals("Escape",StringComparison.OrdinalIgnoreCase)||token.Equals("VK_ESCAPE",StringComparison.OrdinalIgnoreCase)||token.Equals("0x1B",StringComparison.OrdinalIgnoreCase))
                    throw new FormatException("Esc 不作为软件或游戏演奏键使用，请改用字母、数字或支持的符号键。");
                char key;if(!KeyNames.TryGetValue(token,out key)){if(token.Length!=1)throw new FormatException("无法识别按键："+token+"。符号键可填 Comma、Period、Slash、Semicolon、Minus、LeftBracket。");key=char.ToUpperInvariant(token[0]);}
                VirtualKey(key);keys.Add(key);
            }
            if(keys.Count<1||keys.Count>47||keys.Distinct().Count()!=keys.Count)throw new FormatException("键位应为 1～47 个不重复的字母、数字或符号键，用空格分隔。");
            return keys.ToArray();
        }
        public Mapping(InstrumentProfile p) {
            SustainNotes=p.SustainNotes;Monophonic=p.Monophonic;
            char[] keys=ParseKeys(p.Keys);
            if(!string.IsNullOrWhiteSpace(p.MidiPitches)) {
                var values=p.MidiPitches.Split(new[]{' ','，',',',';','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries);int value;
                if(values.Length!=keys.Length||values.Any(v=>!int.TryParse(v,out value)||value<0||value>127))throw new FormatException("逐键音高应填写 0～127 的 MIDI 整数，数量须与按键数量一致。");
                int[] pitches=values.Select(int.Parse).ToArray();
                if(pitches.Distinct().Count()!=pitches.Length)throw new FormatException("逐键音高不能重复。");
                for(int i=0;i<keys.Length;i++)Map[pitches[i]]=keys[i];
                return;
            }
            if(p.BaseMidi<24||p.BaseMidi>84||p.BaseMidi%12!=0) throw new FormatException("起始音必须是 C（MIDI 24～84 且为 12 的倍数）。");
            for(int i=0;i<keys.Length;i++){int pitch=p.BaseMidi+(i/7)*12+Notation.Steps[i%7];if(pitch>127)throw new FormatException("自然音阶超过 MIDI 127，请减少按键或降低起始音。");Map[pitch]=keys[i];}
        }
        public int? Resolve(int pitch,bool fold) {
            if(Map.ContainsKey(pitch)) return pitch;
            if(!fold) return null;
            var candidates=Map.Keys.Where(k=>k%12==pitch%12).OrderBy(k=>Math.Abs(k-pitch)).ToList();
            return candidates.Count==0?(int?)null:candidates[0];
        }
        public int? ResolveGame(int pitch,bool fold,bool approximate) {
            var exact=Resolve(pitch,fold);if(exact.HasValue||!approximate)return exact;
            // Fold chromatic notes as well as naturals before approximating.
            int target=pitch;
            if(fold){int low=Map.Keys.Min(),high=Map.Keys.Max();while(target<low)target+=12;while(target>high)target-=12;}
            int nearest=Map.Keys.OrderBy(k=>Math.Abs(k-target)).ThenBy(k=>k).First();
            return Math.Abs(nearest-target)<=2?(int?)nearest:null;
        }
        public static int BestTranspose(Song song,Mapping map,int preferred,bool fold,bool approximate) {
            int best=preferred,bestCount=-1,bestExact=-1,bestDistance=int.MaxValue;
            double bestOctaves=double.MaxValue;
            var pitches=song.Notes.GroupBy(n=>n.Midi).Select(g=>new{Pitch=g.Key,Count=g.Count()}).ToList();
            for(int offset=-24;offset<=24;offset++) {
                int count=0,exactCount=0;double octaves=0;
                foreach(var note in pitches){int pitch=note.Pitch+offset;var resolved=map.ResolveGame(pitch,fold,approximate);if(resolved.HasValue){count+=note.Count;octaves+=Math.Abs(resolved.Value-pitch)*note.Count;}if(map.Resolve(pitch,fold).HasValue)exactCount+=note.Count;}
                int distance=Math.Abs(offset-preferred);
                if(count>bestCount||(count==bestCount&&(exactCount>bestExact||(exactCount==bestExact&&(distance<bestDistance||(distance==bestDistance&&octaves<bestOctaves)))))){best=offset;bestCount=count;bestExact=exactCount;bestDistance=distance;bestOctaves=octaves;}
            }
            return best;
        }
    }
}
