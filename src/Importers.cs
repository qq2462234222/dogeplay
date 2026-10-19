using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;

namespace LumaKeys {
    public static class Importers {
        static XDocument SafeXml(Stream stream) { return XDocument.Load(XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=20000000})); }
        static string Value(XElement e,string key,string fallback) { var n=e.Element(key); return n==null?fallback:n.Value; }
        static double Number(XElement e,string key,double fallback) { double v; return double.TryParse(Value(e,key,""),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out v)?v:fallback; }
        static List<double> MeasureStarts(List<XElement> parts) {
            var lengths=new List<double>();
            foreach(var part in parts) {
                double divisions=1,expected=0;int index=0;
                foreach(var measure in part.Elements("measure")) {
                    double cursor=0,end=0,chordStart=0;
                    foreach(var e in measure.Elements()) {
                        if(e.Name=="attributes") {
                            divisions=Number(e,"divisions",divisions);if(divisions<=0)divisions=1;
                            var time=e.Element("time");if(time!=null){double beats=0;foreach(var b in time.Elements("beats")){foreach(var term in b.Value.Split('+')){double count;if(double.TryParse(term,out count))beats+=count;}}double type=Number(time,"beat-type",4);if(type>0)expected=beats*4/type;}
                        }
                        if(e.Name=="backup")cursor-=Number(e,"duration",0)/divisions;
                        if(e.Name=="forward"){cursor+=Number(e,"duration",0)/divisions;end=Math.Max(end,cursor);}
                        if(e.Name!="note"||e.Element("grace")!=null)continue;
                        double duration=Number(e,"duration",divisions)/divisions;bool chord=e.Element("chord")!=null;double start=chord?chordStart:cursor;
                        if(!chord){chordStart=cursor;cursor+=duration;}end=Math.Max(end,start+duration);
                    }
                    if((string)measure.Attribute("implicit")!="yes")end=Math.Max(end,expected);
                    while(lengths.Count<=index)lengths.Add(0);lengths[index]=Math.Max(lengths[index],Math.Max(0,end));index++;
                }
            }
            var starts=new List<double>();double beat=0;foreach(double length in lengths){starts.Add(beat);beat+=length;}return starts;
        }
        public static Song MusicXml(string path,bool allParts) {
            XDocument doc;
            if(Path.GetExtension(path).Equals(".mxl",StringComparison.OrdinalIgnoreCase)) {
                using(var archive=ZipFile.OpenRead(path)) {
                    ZipArchiveEntry entry=null; var container=archive.GetEntry("META-INF/container.xml");
                    if(container!=null) using(var s=container.Open()) {
                        var root=SafeXml(s).Descendants().FirstOrDefault(n=>n.Name.LocalName=="rootfile");
                        if(root!=null) entry=archive.GetEntry((string)root.Attribute("full-path"));
                    }
                    if(entry==null) entry=archive.Entries.FirstOrDefault(e=>(e.FullName.EndsWith(".xml")||e.FullName.EndsWith(".musicxml"))&&!e.FullName.StartsWith("META-INF/"));
                    if(entry==null) throw new FormatException("压缩曲谱中没有 MusicXML 文件。");
                    using(var s=entry.Open()) doc=SafeXml(s);
                }
            }else using(var s=File.OpenRead(path)) doc=SafeXml(s);
            foreach(var e in doc.Descendants()) e.Name=e.Name.LocalName;
            if(doc.Root.Name.LocalName!="score-partwise") throw new FormatException("仅支持 score-partwise 格式的 MusicXML。");
            var song=new Song {Title=Path.GetFileNameWithoutExtension(path),Source="MusicXML",License="用户导入"};
            var title=doc.Descendants("work-title").FirstOrDefault()??doc.Descendants("movement-title").FirstOrDefault();
            if(title!=null&&!string.IsNullOrWhiteSpace(title.Value)) song.Title=title.Value;
            var tempo=doc.Descendants("sound").Attributes("tempo").FirstOrDefault(); double bpm;
            if(tempo!=null&&double.TryParse(tempo.Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out bpm)) song.Bpm=(int)Math.Max(30,Math.Min(240,bpm));
            else { var per=doc.Descendants("per-minute").FirstOrDefault(); if(per!=null&&double.TryParse(per.Value,out bpm)) song.Bpm=(int)Math.Max(30,Math.Min(240,bpm)); }
            var parts=doc.Root.Elements("part").Where(p=>p.Descendants("pitch").Any()).ToList();
            var starts=MeasureStarts(parts);
            if(!allParts) parts=parts.Take(1).ToList();
            int partIndex=0;
            foreach(var part in parts) {
                var definition=doc.Descendants("score-part").FirstOrDefault(p=>(string)p.Attribute("id")== (string)part.Attribute("id"));
                song.PartNames.Add(definition==null?"声部 "+(partIndex+1):Value(definition,"part-name","声部 "+(partIndex+1)));
                double divisions=1; int transpose=0,measureIndex=0;
                var ties=new Dictionary<string,Note>();
                foreach(var measure in part.Elements("measure")) {
                    double measureStart=starts[measureIndex++];
                    double cursor=0, end=0, chordStart=0;
                    foreach(var e in measure.Elements()) {
                        if(e.Name=="attributes") {
                            divisions=Number(e,"divisions",divisions); if(divisions<=0) divisions=1;
                            var tr=e.Element("transpose"); if(tr!=null) transpose=(int)Number(tr,"chromatic",0)+12*(int)Number(tr,"octave-change",0);
                        }
                        if(e.Name=="backup") cursor-=Number(e,"duration",0)/divisions;
                        if(e.Name=="forward") {cursor+=Number(e,"duration",0)/divisions;end=Math.Max(end,cursor);}
                        if(e.Name!="note"||e.Element("grace")!=null) continue;
                        double duration=Number(e,"duration",divisions)/divisions;
                        bool chord=e.Element("chord")!=null; double start=chord?chordStart:cursor;
                        if(!chord) {chordStart=cursor;cursor+=duration;}
                        end=Math.Max(end,start+duration);
                        var pitch=e.Element("pitch");
                        if(pitch==null||duration<=0) continue;
                        int[] pitchSteps={9,11,0,2,4,5,7}; string step=Value(pitch,"step","C");
                        if(step.Length!=1||step[0]<'A'||step[0]>'G') continue;
                        int midi=((int)Number(pitch,"octave",4)+1)*12+pitchSteps[step[0]-'A']+(int)Number(pitch,"alter",0)+transpose;
                        string key=Value(e,"voice","1")+"/"+Value(e,"staff","1")+"/"+midi;
                        bool stop=e.Elements("tie").Any(t=>(string)t.Attribute("type")=="stop");
                        bool begin=e.Elements("tie").Any(t=>(string)t.Attribute("type")=="start"); Note note;
                        if(stop&&ties.TryGetValue(key,out note)&&Math.Abs(note.Beat+note.Duration-(measureStart+start))<0.02) note.Duration+=duration;
                        else {note=new Note(measureStart+start,duration,midi){Part=partIndex};song.Notes.Add(note);}
                        if(begin) ties[key]=note; else ties.Remove(key);
                    }
                }
                partIndex++;
            }
            song.Notes=song.Notes.OrderBy(n=>n.Beat).ThenBy(n=>n.Midi).ToList();
            Validate(song); return song;
        }
        public static void Validate(Song song) {
            if(song==null||song.Notes==null||song.Notes.Count==0) throw new FormatException("曲谱中没有可演奏音符。");
            if(song.Notes.Count>100000||song.Notes.Any(n=>n==null||double.IsNaN(n.Beat)||double.IsInfinity(n.Beat)||n.Beat<0||n.Beat>100000||double.IsNaN(n.Duration)||double.IsInfinity(n.Duration)||n.Duration<=0||n.Duration>10000||n.Midi<0||n.Midi>127)) throw new FormatException("曲谱含有无效音高或时值。");
            if(string.IsNullOrWhiteSpace(song.Title))song.Title="未命名曲谱";
            if(string.IsNullOrWhiteSpace(song.Source))song.Source="本地曲谱";
            if(song.PartNames==null)song.PartNames=new List<string>();
            if(song.Notes.Any(n=>n.Part<0||n.Part>1000))throw new FormatException("曲谱声部编号无效。");
            song.Bpm=Math.Max(30,Math.Min(240,song.Bpm));
        }
        public static string Quote(string s) { if(s.Contains("\"")||s.Contains("\r")||s.Contains("\n")) throw new ArgumentException("路径无效"); return "\""+s+"\""; }
        public static string FindAudiveris() {
            return StaffEngine.Find();
        }
        public static async Task<string> Run(string program,string args,int timeoutSeconds,string capturePath=null) {
            var info=new ProcessStartInfo(program,args) {WorkingDirectory=Store.Root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.StandardOutputEncoding=Encoding.UTF8;info.StandardErrorEncoding=Encoding.UTF8;
            using(var p=Process.Start(info)) {
                var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync();
                bool exited=await Task.Run(()=>p.WaitForExit(timeoutSeconds*1000));
                if(!exited) {try {p.Kill();}catch{}}
                string output=await stdout;string error=await stderr;
                if(!string.IsNullOrWhiteSpace(capturePath)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(capturePath));
                    File.WriteAllText(capturePath,"[stdout]\n"+output+"\n[stderr]\n"+error,Encoding.UTF8);
                }
                string logHint=string.IsNullOrWhiteSpace(capturePath)?"":"识别日志："+StaffEngine.Relative(capturePath);
                if(!exited) throw new TimeoutException("识别超过 "+timeoutSeconds+" 秒，已停止。请先裁剪页数、降低图片分辨率后重试。"+logHint);
                if(p.ExitCode!=0) throw new InvalidOperationException("识别程序未完成（退出代码 "+p.ExitCode+"）。请查看识别日志或在导入文件窗口修复引擎。"+logHint);
                return output;
            }
        }
        public static async Task<string> NumberOcr(string path) {
            string output=Path.Combine(Store.Data,"ocr-"+Guid.NewGuid().ToString("N")+".json");
            await Run("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Quote(Path.Combine(Store.Root,"scripts","number-ocr.ps1"))+" -ImagePath "+Quote(path)+" -OutputPath "+Quote(output),90);
            try {return File.ReadAllText(output,Encoding.UTF8);} finally {if(File.Exists(output))File.Delete(output);}
        }
        public static async Task<Song> StaffOcr(string path,bool allParts) {
            string engine=StaffEngine.Find();
            if(!StaffEngine.Complete(engine)){StaffEngine.MarkFailed();throw new FileNotFoundException("内置五线谱识别引擎缺失或不完整。请恢复完整软件包，或在“导入文件”中修复引擎位置。\n包内位置："+StaffEngine.DisplayPath);}
            string output=Path.Combine(Store.Data,"omr",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            Directory.CreateDirectory(output);
            string args="-batch -transcribe -export -output "+Quote(output)+" -- "+Quote(path);
            string logPath=Path.Combine(output,"recognition.log");
            try{await Run(engine,args,900,logPath);StaffEngine.MarkHealthy();}
            catch(System.ComponentModel.Win32Exception ex){StaffEngine.MarkFailed();Diagnostics.Log(ex.ToString());throw new InvalidOperationException("内置识别引擎无法启动。请在“导入文件”中修复引擎，确认 app 和 runtime 文件夹完整。位置："+StaffEngine.DisplayPath);}
            catch(InvalidOperationException){StaffEngine.MarkFailed();throw;}
            catch(TimeoutException){StaffEngine.MarkFailed();throw;}
            string score=Directory.GetFiles(output,"*.*",SearchOption.AllDirectories).FirstOrDefault(p=>new[]{".mxl",".musicxml"}.Contains(Path.GetExtension(p).ToLowerInvariant()));
            if(score==null) throw new FormatException("未识别到完整五线谱。请使用清晰、端正且包含谱号的图片。日志："+StaffEngine.Relative(logPath));
            var song=MusicXml(score,allParts);song.Source="五线谱图片 · Audiveris";return song;
        }
    }
}
