using System;
using System.Collections.Generic;
using System.Linq;

namespace LumaKeys {
    public class GameTimingSettings {
        public bool Enabled {get;set;}
        public int HoldMs {get;set;}
        public int ReleaseMs {get;set;}
        public GameTimingSettings(){Enabled=true;HoldMs=60;ReleaseMs=15;}
        public void Validate() {
            if(HoldMs<1||HoldMs>500||ReleaseMs<1||ReleaseMs>500)
                throw new FormatException("按住与松开目标时间应为 1～500 毫秒。");
        }
    }
    public class GameTimingPlan {
        public List<Note> Notes;
        public static GameTimingPlan Create(Song song,Mapping map,int transpose,bool fold,GameTimingSettings settings,bool approximate=false) {
            settings.Validate();
            var notes=new List<Note>();
            var pitches=song.Notes.Select(n=>n.Midi).Distinct().ToDictionary(p=>p,p=>map.ResolveGame(p+transpose,fold,approximate));
            var source=map.Monophonic?Melody(song):song.Notes;
            foreach(var note in source) {
                int? pitch=pitches[note.Midi];if(!pitch.HasValue)continue;
                notes.Add(new Note(note.Beat,map.SustainNotes?note.Duration:Math.Max(0.03,note.Duration*0.85),pitch.Value){Part=note.Part});
            }
            var plan=new GameTimingPlan{Notes=notes};
            if(notes.Count==0)return plan;
            // Merge duplicate keys only at the same onset. A later note on the same
            // game key must release and retrigger even if another voice sustains it.
            notes=notes.GroupBy(n=>new {n.Beat,n.Midi})
                .Select(g=>new Note(g.Key.Beat,g.Max(n=>n.Duration),g.Key.Midi)).OrderBy(n=>n.Beat).ToList();
            double msPerBeat=60000.0/song.Bpm;
            var voices=map.Monophonic?new List<List<Note>>{notes.OrderBy(n=>n.Beat).ToList()}:notes.GroupBy(n=>n.Midi).Select(g=>g.OrderBy(n=>n.Beat).ToList()).ToList();
            double hold=settings.Enabled?settings.HoldMs/msPerBeat:0,gap=settings.ReleaseMs/msPerBeat;
            foreach(var voice in voices)for(int i=0;i<voice.Count;i++) {
                double duration=map.SustainNotes?voice[i].Duration:Math.Max(hold,voice[i].Duration);
                if(i+1<voice.Count) {
                    double interval=voice[i+1].Beat-voice[i].Beat;
                    // Keep every original onset. Fit key-up into the available
                    // interval instead of slowing the score to meet target timings.
                    double release=map.SustainNotes&&voice[i].Midi!=voice[i+1].Midi?0:Math.Min(gap,interval*0.4);
                    duration=Math.Min(duration,interval-release);
                }
                voice[i].Duration=duration;
            }
            plan.Notes=notes;return plan;
        }
        public static List<Note> Melody(Song song) {
            var notes=song.Notes;if(notes.Count==0)return new List<Note>();
            var parts=notes.GroupBy(n=>n.Part).Select(g=>new{Part=g.Key,Notes=g.ToList(),Register=g.Select(n=>n.Midi).OrderBy(p=>p).ElementAt(g.Count()/2)}).ToList();
            if(parts.Count>1) {
                int named=parts.Where(p=>p.Part>=0&&p.Part<song.PartNames.Count&&IsLeadName(song.PartNames[p.Part]))
                    .OrderByDescending(p=>p.Notes.Count).Select(p=>p.Part).DefaultIfEmpty(int.MinValue).First();
                int lead=named!=int.MinValue?named:parts.OrderByDescending(p=>p.Register).ThenByDescending(p=>p.Notes.Count).ThenBy(p=>p.Part).First().Part;
                return OneNotePerOnset(notes.Where(n=>n.Part==lead));
            }
            // Piano-only scores put melody and accompaniment in one part. Track a
            // smooth upper voice through each chord instead of blindly taking the
            // highest note, which often belongs to an arpeggio or inner voice.
            var groups=notes.GroupBy(n=>n.Beat).OrderBy(g=>g.Key).Select(g=>g.GroupBy(n=>n.Midi).Select(p=>p.OrderByDescending(n=>n.Duration).First()).ToList()).ToList();
            if(groups.Count==1)return OneNotePerOnset(notes);
            var costs=new List<double[]>();var previous=new List<int[]>();
            for(int i=0;i<groups.Count;i++) {
                var level=new double[groups[i].Count];var back=Enumerable.Repeat(-1,groups[i].Count).ToArray();
                for(int j=0;j<groups[i].Count;j++) {
                    var note=groups[i][j];double bias=-note.Midi*0.24-Math.Min(4,note.Duration)*0.18;
                    if(i==0)level[j]=bias;
                    else for(int k=0;k<groups[i-1].Count;k++) {
                        int leap=Math.Abs(note.Midi-groups[i-1][k].Midi);double candidate=costs[i-1][k]+bias+leap*0.48+Math.Max(0,leap-7)*0.12;
                        if(back[j]<0||candidate<level[j]){level[j]=candidate;back[j]=k;}
                    }
                }
                costs.Add(level);previous.Add(back);
            }
            int index=Array.IndexOf(costs[costs.Count-1],costs[costs.Count-1].Min());var melody=new List<Note>();
            for(int i=groups.Count-1;i>=0;i--){melody.Add(groups[i][index]);if(i>0)index=previous[i][index];}
            melody.Reverse();return melody;
        }
        static List<Note> OneNotePerOnset(IEnumerable<Note> source) {return source.GroupBy(n=>n.Beat).Select(g=>g.OrderByDescending(n=>n.Midi).ThenByDescending(n=>n.Duration).First()).OrderBy(n=>n.Beat).ToList();}
        static bool IsLeadName(string name) {string s=(name??"").ToLowerInvariant();return s.Contains("旋律")||s.Contains("主奏")||s.Contains("主旋律")||s.Contains("melody")||s.Contains("lead")||s.Contains("soprano")||s.Contains("vocal");}
    }
}
