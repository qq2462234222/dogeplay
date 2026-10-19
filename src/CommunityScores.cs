using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LumaKeys {
    public class SkySheet {
        public string name {get;set;}
        public string author {get;set;}
        public string transcribedBy {get;set;}
        public double bpm {get;set;}
        public int keyCount {get;set;}
        public bool isEncrypted {get;set;}
        public List<SkyNote> songNotes {get;set;}
    }
    public class SkyNote {public double time {get;set;}public string key {get;set;}}
    public static class CommunityScores {
        static string ReadText(string path) {
            var data=File.ReadAllBytes(path);if(data.Length>20000000)throw new FormatException("曲谱文件过大。");
            // SkyStudio exports include UTF-16 LE, UTF-8, and older files without BOM.
            if(data.Length>=2&&((data[0]==0xff&&data[1]==0xfe)||data[1]==0))return Encoding.Unicode.GetString(data).TrimStart('\ufeff');
            if(data.Length>=2&&((data[0]==0xfe&&data[1]==0xff)||data[0]==0))return Encoding.BigEndianUnicode.GetString(data).TrimStart('\ufeff');
            return new UTF8Encoding(false,true).GetString(data).TrimStart('\ufeff');
        }
        public static Song ReadLocal(string path,bool allParts) {
            string text=ReadText(path).Trim();
            if(text.StartsWith("{")) {
                var fields=Store.Json.DeserializeObject(text) as Dictionary<string,object>;
                if(fields!=null&&!fields.Keys.Any(k=>k.Equals("songNotes",StringComparison.OrdinalIgnoreCase))) {
                    var song=Store.Json.Deserialize<Song>(text);Importers.Validate(song);return song.Playback(allParts);
                }
            }
            return Parse(text,allParts);
        }
        public static Song Read(string path,bool allParts){return Parse(ReadText(path).Trim(),allParts);}
        static Song Parse(string text,bool allParts) {
            SkySheet sheet;
            if(text.StartsWith("[")){var sheets=Store.Json.Deserialize<SkySheet[]>(text);if(sheets==null||sheets.Length!=1)throw new FormatException("此文件需要拆分成单首 SkyStudio 曲谱后导入。");sheet=sheets[0];}
            else sheet=Store.Json.Deserialize<SkySheet>(text);
            if(sheet==null||sheet.songNotes==null||sheet.songNotes.Count==0)throw new FormatException("这份社区曲谱不是支持的 SkyStudio songNotes 格式。");
            if(sheet.isEncrypted)throw new FormatException("这份曲谱已加密，请选择作者提供的未加密版本。");
            if(sheet.songNotes.Count>100000)throw new FormatException("曲谱音符过多。");
            int keys=sheet.keyCount==0?15:sheet.keyCount;
            if(keys!=15&&keys!=21)throw new FormatException("暂支持 15 键与 21 键 SkyStudio 曲谱。");
            // Note timestamps are authoritative. Normalize unusual editor BPM values
            // into the supported range without changing any real onset times.
            int bpm=double.IsNaN(sheet.bpm)||double.IsInfinity(sheet.bpm)||sheet.bpm<=0?120:(int)Math.Max(30,Math.Min(240,sheet.bpm));
            var song=new Song{Title=sheet.name,Bpm=bpm,Source=string.Join(" / ",new[]{sheet.author,sheet.transcribedBy}.Where(s=>!string.IsNullOrWhiteSpace(s)&&s!="Unknown").Distinct()),License="社区曲谱，见原作者说明"};
            foreach(var note in sheet.songNotes) {
                if(note==null||double.IsNaN(note.time)||double.IsInfinity(note.time)||note.time<0||note.time>21600000)throw new FormatException("曲谱包含无效时间戳。");
                var match=Regex.Match(note.key??"",@"^(\d*)Key(\d+)$",RegexOptions.IgnoreCase);
                int layer,index;
                if(!match.Success||!int.TryParse(match.Groups[2].Value,out index)||index<0||index>=keys||!int.TryParse(match.Groups[1].Value.Length==0?"0":match.Groups[1].Value,out layer)||layer<0||layer>1000)
                    throw new FormatException("曲谱含有不支持的键位："+note.key);
                int midi=(keys==21?48:60)+(index/7)*12+Notation.Steps[index%7];
                song.Notes.Add(new Note(note.time*bpm/60000.0,0.5,midi){Part=layer});
            }
            var layers=song.Notes.Select(n=>n.Part).Distinct().OrderBy(p=>p).ToList();
            song.PartNames=layers.Select(p=>"图层 "+p).ToList();
            var partMap=layers.Select((p,i)=>new{p,i}).ToDictionary(x=>x.p,x=>x.i);
            foreach(var note in song.Notes)note.Part=partMap[note.Part];
            // This format records taps, not key-up events. Infer a short duration
            // from the next onset in the same layer; retain all simultaneous notes.
            foreach(var layer in song.Notes.GroupBy(n=>n.Part)) {
                var groups=layer.GroupBy(n=>n.Beat).OrderBy(g=>g.Key).ToList();
                for(int i=0;i<groups.Count;i++) {
                    double duration=i+1<groups.Count?Math.Min(1,groups[i+1].Key-groups[i].Key):0.5;
                    foreach(var note in groups[i])note.Duration=Math.Max(0.03,duration);
                }
            }
            song.Notes=song.Notes.OrderBy(n=>n.Beat).ThenBy(n=>n.Part).ThenBy(n=>n.Midi).ToList();
            if(string.IsNullOrWhiteSpace(song.Source))song.Source="社区制谱";
            Importers.Validate(song);return song.Playback(allParts);
        }
    }
}
