using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LumaKeys {
    public static class PlaybackTests {
        class Receiver : IPlaybackIo {
            public volatile bool Focused=true;public volatile uint Pid=1;
            public DateTimeOffset? FirstDownUtc;
            public List<string> Events=new List<string>();public HashSet<char> Held=new HashSet<char>();
            public List<List<KeyStroke>> Batches=new List<List<KeyStroke>>();
            public bool IsWindow(IntPtr handle){return handle==new IntPtr(1);}
            public IntPtr Foreground(){return new IntPtr(Focused?1:2);}
            public uint ProcessId(IntPtr handle){return Pid;}
            public void Key(char key,bool down){lock(Events){Events.Add(key+(down?"+":"-"));if(down){if(!FirstDownUtc.HasValue)FirstDownUtc=DateTimeOffset.UtcNow;Held.Add(key);}else Held.Remove(key);}}
            public void Keys(IList<KeyStroke> keys){lock(Events){Batches.Add(keys.ToList());foreach(var key in keys)Key(key.Key,key.Down);}}
            public bool Contains(string value){lock(Events)return Events.Contains(value);}
            public int Count(string value){lock(Events)return Events.Count(e=>e==value);}
        }
        public static int Run(){return RunAsync().GetAwaiter().GetResult();}
        static async Task<int> RunAsync() {
            var log=new List<string>();int result=0;Player player=null;
            Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);log.Add("PASS: "+message);};
            try {
                var io=new Receiver();player=new Player(io);var map=new Mapping(new Settings().Profiles[0]);var target=new GameWindow{Handle=new IntPtr(1)};
                await player.Start(Notation.Parse("chord","[1 3 5]:0.2 2:0.2",240),map,0,false,false,target,0);
                check(io.Contains("A+")&&io.Contains("D+")&&io.Contains("G+")&&io.Contains("S+"),"scheduled chord and sequential key mapping");
                check(io.Batches.Any(b=>b.Count==3&&b.All(k=>k.Down)&&new string(b.Select(k=>k.Key).ToArray())=="ADG"),"chord note-ons are submitted together in one input batch");
                check(io.Batches.Any(b=>b.Count==3&&b.All(k=>!k.Down)),"chord note-offs are submitted together");
                check(io.Held.Count==0&&io.Contains("A-")&&io.Contains("S-"),"completion releases all held keys");
                io=new Receiver();player=new Player(io);var song=Notation.Parse("sustain","1:10",240);var task=player.Start(song,map,0,false,false,target,0);await Task.Delay(120);player.Stop();await task;
                check(io.Contains("A+")&&io.Contains("A-")&&io.Held.Count==0&&!player.Running,"stop during note releases keys and clears state");
                io=new Receiver();player=new Player(io);task=player.Start(song,map,0,false,false,target,0);await Task.Delay(100);player.TogglePause();await Task.Delay(80);
                check(io.Contains("A-")&&io.Held.Count==0&&player.Paused,"pause releases held keys");player.TogglePause();await Task.Delay(80);player.Stop();await task;
                check(io.Count("A+")==2&&io.Held.Count==0,"resume restores notes then stop releases them");
                io=new Receiver();player=new Player(io);task=player.Start(song,map,0,false,false,target,0);await Task.Delay(90);io.Focused=false;await Task.Delay(80);
                check(player.Paused&&io.Held.Count==0&&player.Running,"focus loss pauses and releases keys");player.Stop();await task;bool rejected=false;
                io=new Receiver();player=new Player(io);task=player.Start(song,map,0,false,false,target,0);await Task.Delay(90);io.Pid=2;rejected=false;try{await task;}catch(InvalidOperationException){rejected=true;}
                check(rejected&&io.Held.Count==0,"reused window handle with different process is rejected");
                io=new Receiver();player=new Player(io);task=player.Start(song,map,0,false,false,target,3);await Task.Delay(50);player.Stop();await task;
                check(io.Events.Count==0,"cancel countdown sends no input");
                io=new Receiver();player=new Player(io);var overlap=new Song{Bpm=240};overlap.Notes.Add(new Note(0,1,60));overlap.Notes.Add(new Note(.3,1,60));await player.Start(overlap,map,0,false,false,target,0);
                check(io.Count("A+")==2&&io.Count("A-")==2,"overlapping same-key notes release and retrigger at each onset");
                io=new Receiver();player=new Player(io);await player.Start(Notation.Parse("skip","#1 2:0.2",240),map,0,false,false,target,0,adaptGame:false);
                check(io.Events.Count==2&&io.Contains("S+"),"unsupported accidental is skipped without shifting timing");
                io=new Receiver();player=new Player(io);var intro=new Song{Bpm=120};intro.Notes.Add(new Note(15,1,60));
                double reportedBeat=0;player.Progress=b=>{if(b>0)reportedBeat=b;};task=player.Start(intro,map,0,false,false,target,0);await Task.Delay(150);
                check(io.Contains("A+")&&reportedBeat>=15,"15-beat leading rest is skipped and absolute score progress preserved");player.Stop();await task;
                io=new Receiver();player=new Player(io);task=player.Start(intro,map,0,false,false,target,0,false);await Task.Delay(100);player.Stop();await task;
                check(io.Events.Count==0,"disabling intro skip preserves written leading rest");
                io=new Receiver();player=new Player(io);var scheduled=DateTimeOffset.UtcNow.AddMilliseconds(500);task=player.Start(Notation.Parse("scheduled","1:0.2",240),map,0,false,false,target,0,true,null,false,null,false,0,scheduled);
                await Task.Delay(150);check(io.Events.Count==0,"scheduled playback does not send notes before the requested wall-clock time");await task;
                check(io.FirstDownUtc.HasValue&&io.FirstDownUtc.Value>=scheduled,"scheduled playback sends its first note no earlier than the requested time");
                io=new Receiver{Focused=false};player=new Player(io);task=player.Start(song,map,0,false,false,target,0);await Task.Delay(120);
                check(player.Running&&io.Events.Count==0,"waits for selected window before sending any input");io.Focused=true;await Task.Delay(160);player.Stop();await task;
                check(io.Contains("A+")&&io.Held.Count==0,"foreground arrival starts playback and stop releases key");
                io=new Receiver{Focused=false};player=new Player(io);task=player.Start(song,map,0,false,false,target,0);await Task.Delay(80);player.Stop();await task;
                check(io.Events.Count==0&&!player.Running,"foreground wait can be cancelled without input");
                io=new Receiver();player=new Player(io);var polyphonic=new Song{Bpm=240};polyphonic.Notes.Add(new Note(0,2,48){Part=1});polyphonic.Notes.Add(new Note(0,.5,60));polyphonic.Notes.Add(new Note(.5,.5,62));
                string activeKeys="";player.ActiveKeys=keys=>activeKeys=keys;task=player.Start(polyphonic,map,0,false,false,target,0);await Task.Delay(155);
                lock(io.Events)check(io.Held.Contains('Z')&&io.Held.Contains('S')&&!io.Held.Contains('A'),"bass remains held while melody advances independently");
                check(activeKeys=="Z + S","active key feedback includes accompaniment and current melody");
                player.Stop();await task;check(io.Held.Count==0&&activeKeys=="","polyphonic stop releases all voices and clears feedback");
                var flute=new InstrumentProfile{Keys=new Settings().Profiles[0].Keys,BaseMidi=48,SustainNotes=true,Monophonic=true};
                io=new Receiver();player=new Player(io);task=player.Start(Notation.Parse("flute","1:4",240),new Mapping(flute),0,false,false,target,0);await Task.Delay(910);
                check(io.Held.Contains('A'),"flute remains held beyond the former 85 percent note cutoff");
                player.TogglePause();await Task.Delay(60);check(player.Paused&&io.Held.Count==0,"flute pause releases sustained key");
                player.TogglePause();await Task.Delay(40);check(io.Count("A+")==2,"flute resume restores sustained key");player.Stop();await task;
                check(io.Held.Count==0&&!player.Running,"flute stop releases sustained key");
            }catch(Exception ex){result=1;log.Add("FAIL: "+ex);if(player!=null)player.Stop();}
            string directory=Path.GetFullPath(Path.Combine(Store.Root,"..","test-output"));Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,"playback-tests.txt"),log);return result;
        }
    }
}
