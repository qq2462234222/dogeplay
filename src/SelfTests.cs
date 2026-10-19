using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO.Compression;

namespace LumaKeys {
    public static class SelfTests {
        static List<string> results=new List<string>();
        static void Assert(bool condition,string message) {if(!condition)throw new Exception("FAIL: "+message);results.Add("PASS: "+message);}
        static void Reject(Action action,string message) {try{action();}catch(FormatException){results.Add("PASS: "+message);return;}throw new Exception("FAIL: "+message);}
        public static int Run() {
            string output=Path.GetFullPath(Path.Combine(Store.Root,"..","test-output"));Directory.CreateDirectory(output);
            try {
                var song=Notation.Parse("test","1 2:0.5 3' 4, 0 [1 3 5]:2 -",120);
                Assert(song.Notes.Count==7,"numeric notation, chords and rests");
                Assert(song.Notes[2].Midi==76&&song.Notes[3].Midi==53,"octave markers");
                Assert(Math.Abs(song.Notes.Last().Duration-3)<0.001,"sustain extends all chord notes");
                Assert(Math.Abs(song.Notes.Last().Beat-4.5)<0.001,"rest advances timeline");
                Assert(Notation.Parse("low chord","[1, 3, 5,]",100).Notes.Last().Midi==55,"low octave chord syntax");
                Reject(()=>Notation.Parse("bad","8",100),"reject invalid notes");
                Reject(()=>Notation.Parse("bad","1:0",100),"reject zero duration");
                Reject(()=>Notation.Parse("bad","1:NaN",100),"reject invalid duration syntax");
                var map=new Mapping(new Settings().Profiles[0]);
                Assert(map.Map[48]=='Z'&&map.Map[60]=='A'&&map.Map[72]=='Q',"21-key octave mapping");
                var naraka=new Settings();naraka.EnsureNarakaMaster();naraka.EnsureNarakaInstruments();
                Assert(naraka.Profiles.Where(p=>(p.ProcessHint??"").Contains("Naraka")).All(p=>p.Instruments.Any(i=>i.Name.StartsWith("曲笛"))&&p.Instruments.Any(i=>i.Name.StartsWith("古筝"))),"Naraka standard and master modes include Guzheng and Qudi profiles");
                var qudi=naraka.Profiles.First(p=>p.Name=="永劫无间大师模式").Instruments.First(i=>i.Name.StartsWith("曲笛"));
                qudi.SustainNotes=true;qudi.Monophonic=true;
                var qudiMap=new Mapping(qudi);var qudiSong=new Song{Bpm=120};
                qudiSong.Notes.Add(new Note(0,2,60){Part=0});qudiSong.Notes.Add(new Note(0,2,64){Part=0});qudiSong.Notes.Add(new Note(1,2,67){Part=0});
                qudiSong.Notes.Add(new Note(0,2,48){Part=1});qudiSong.Notes.Add(new Note(1,2,50){Part=1});
                var qudiPlan=GameTimingPlan.Create(qudiSong,qudiMap,0,false,new GameTimingSettings{Enabled=false},false).Notes.OrderBy(n=>n.Beat).ToList();
                Assert(qudiPlan.Count==2&&qudiPlan[0].Midi==64&&qudiPlan[1].Midi==67,"Qudi plan emits one highest melody note per onset and ignores accompaniment");
                Assert(qudiPlan[0].Duration<=qudiPlan[1].Beat-qudiPlan[0].Beat+0.000001,"Qudi sustained notes do not overlap the next note");
                var namedScore=new Song{Bpm=120,PartNames=new List<string>{"旋律","伴奏"}};
                namedScore.Notes.Add(new Note(0,1,55){Part=0});namedScore.Notes.Add(new Note(0,1,76){Part=1});
                Assert(GameTimingPlan.Melody(namedScore).Single().Midi==55,"Qudi prefers the explicitly named melody over a higher accompaniment part");
                var pianoScore=new Song{Bpm=120,PartNames=new List<string>{"钢琴"}};
                pianoScore.Notes.Add(new Note(0,1,48));pianoScore.Notes.Add(new Note(0,1,72));pianoScore.Notes.Add(new Note(1,1,50));pianoScore.Notes.Add(new Note(1,1,74));
                Assert(GameTimingPlan.Melody(pianoScore).Select(n=>n.Midi).SequenceEqual(new[]{72,74}),"Qudi extracts a continuous upper melody from a chord-only piano part");
                Assert(map.Resolve(84,true)==72&&map.Resolve(84,false)==null,"optional octave folding");
                Assert(map.Resolve(61,true)==null,"accidentals never silently changed");
                Assert(InputAccess.IsBlocked(8192,12288),"standard integrity cannot send input to elevated game");
                Assert(!InputAccess.IsBlocked(12288,12288)&&!InputAccess.IsBlocked(12288,8192),"equal and higher integrity can send input");
                Assert(!InputAccess.IsBlocked(null,12288),"unknown integrity is not misreported as confirmed mismatch");
                Reject(()=>new Mapping(new GameProfile{Keys="A A A A A A A",BaseMidi=48}),"reject duplicate key bindings");
                string xml="<score-partwise><work><work-title>Fixture</work-title></work><part-list/><part id='P1'><measure><attributes><divisions>2</divisions></attributes><direction><sound tempo='110'/></direction><note><pitch><step>C</step><octave>4</octave></pitch><duration>2</duration><tie type='start'/></note><note><chord/><pitch><step>E</step><octave>4</octave></pitch><duration>2</duration></note><note><pitch><step>C</step><octave>4</octave></pitch><duration>2</duration><tie type='stop'/></note><backup><duration>4</duration></backup><note><pitch><step>G</step><octave>3</octave></pitch><duration>4</duration><voice>2</voice></note></measure><measure><attributes><divisions>4</divisions></attributes><note><rest/><duration>4</duration></note><note><pitch><step>F</step><alter>1</alter><octave>4</octave></pitch><duration>2</duration></note></measure></part><part id='P2'><measure><note><pitch><step>D</step><octave>5</octave></pitch><duration>1</duration></note></measure></part></score-partwise>";
                string path=Path.Combine(output,"fixture.musicxml");File.WriteAllText(path,xml);
                var imported=Importers.MusicXml(path,false);
                Assert(imported.Notes.Count==4&&imported.Bpm==110,"MusicXML melody-only and tempo");
                Assert(imported.Notes.First(n=>n.Midi==60).Duration==2,"MusicXML ties merged");
                Assert(imported.Notes.First(n=>n.Midi==64).Beat==0&&imported.Notes.First(n=>n.Midi==55).Beat==0,"MusicXML chords and backup voices");
                Assert(imported.Notes.Last().Midi==66&&imported.Notes.Last().Beat==3&&imported.Notes.Last().Duration==0.5,"measure offsets, changed divisions, rests and accidentals");
                Assert(Importers.MusicXml(path,true).Notes.Count==5,"all-part import");
                string mxl=Path.Combine(output,"fixture-"+Guid.NewGuid().ToString("N")+".mxl");
                using(var archive=ZipFile.Open(mxl,ZipArchiveMode.Create)) {using(var w=new StreamWriter(archive.CreateEntry("META-INF/container.xml").Open()))w.Write("<container><rootfiles><rootfile full-path='scores/main.musicxml'/></rootfiles></container>");using(var w=new StreamWriter(archive.CreateEntry("scores/main.musicxml").Open()))w.Write(xml);}
                Assert(Importers.MusicXml(mxl,false).Notes.Count==4,"MXL container rootfile");
                string json=Path.Combine(output,"roundtrip.json");Store.Write(json,imported);var roundtrip=Store.Read<Song>(json);Importers.Validate(roundtrip);
                Assert(roundtrip.Notes.Count==imported.Notes.Count&&roundtrip.Notes.Last().Midi==66,"lossless JSON round trip");
                var invalid=new Song();invalid.Notes.Add(new Note(double.NaN,1,60));Reject(()=>Importers.Validate(invalid),"reject malformed imported event");
                string sample=Path.Combine(Store.Root,"assets","openscore","Just_for_Today.mxl");
                Assert(File.Exists(sample)&&Importers.MusicXml(sample,false).Notes.Count>30,"real OpenScore CC0 sample import");
                var full=Importers.MusicXml(sample,true);var melody=Importers.MusicXml(sample,false);
                Assert(full.Notes.Count>melody.Notes.Count&&full.PartNames.Count>1&&full.Notes.Any(n=>n.Part>0),"OpenScore imports accompaniment and preserves part identity");
                Assert(full.Playback(false).Notes.Count==melody.Notes.Count&&full.Notes.GroupBy(n=>n.Beat).Any(g=>g.Count()>1),"melody-only toggle preserves full score and chords");
                Store.Write(json,full);Assert(Store.Read<Song>(json).Notes.Count(n=>n.Part>0)==full.Notes.Count(n=>n.Part>0),"JSON export retains accompaniment parts");
                string aligned="<score-partwise><part-list/><part id='P1'><measure><attributes><divisions>1</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes><note><pitch><step>C</step><octave>4</octave></pitch><duration>1</duration></note></measure><measure><note><pitch><step>D</step><octave>4</octave></pitch><duration>1</duration></note></measure></part><part id='P2'><measure><attributes><divisions>1</divisions></attributes><note><pitch><step>C</step><octave>3</octave></pitch><duration>4</duration></note></measure><measure><note><pitch><step>G</step><octave>3</octave></pitch><duration>4</duration></note></measure></part></score-partwise>";
                File.WriteAllText(path,aligned);var synced=Importers.MusicXml(path,true);
                Assert(synced.Notes.Single(n=>n.Midi==62).Beat==4&&synced.Notes.Single(n=>n.Midi==55).Beat==4,"short melody measures stay aligned with longer accompaniment measures");
                File.WriteAllText(path,aligned.Replace("<measure>","<measure implicit='yes'>").Replace("<duration>4</duration>","<duration>1</duration>"));
                Assert(Importers.MusicXml(path,true).Notes.Single(n=>n.Midi==62).Beat==1,"pickup measures retain their actual length");
                var demo=Store.ChordDemo();Assert(demo.Notes.GroupBy(n=>n.Beat).Max(g=>g.Count())>=4&&demo.Notes.Any(n=>n.Part==1),"bundled melody-and-accompaniment example contains playable chords");
                var catalog=Catalog.Load();Assert(catalog.Entries.Count>=1400,"bundled searchable catalog contains full OpenScore index");
                Assert(Catalog.Search(catalog,"just TODAY abbott").Any(e=>e.Title=="Just for Today"),"multi-keyword catalog search spans title and composer without case sensitivity");
                Assert(Catalog.Search(catalog,"not-a-real-title-xyz").Count==0,"catalog search returns a genuine empty result");
                var entry=Catalog.Search(catalog,"Just for Today").First(e=>e.Composer.Contains("Abbott"));
                Assert(Catalog.GitHash(File.ReadAllBytes(sample))==entry.Sha,"bundled score matches upstream Git content hash");
                Assert(!Catalog.ValidPath("scores/../secret.mxl")&&new Uri(entry.DownloadUrl).Host=="raw.githubusercontent.com","catalog paths remain within upstream score repository");
                File.WriteAllLines(Path.Combine(output,"self-tests.txt"),results,Encoding.UTF8);return 0;
            }catch(Exception ex){results.Add(ex.ToString());File.WriteAllLines(Path.Combine(output,"self-tests.txt"),results,Encoding.UTF8);return 1;}
        }
    }
}
