using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace LumaKeys {
    public static class MidiImporter {
        static int U16(byte[] b,ref int p){if(p+2>b.Length)throw new FormatException("MIDI 文件头提前结束。");int v=(b[p]<<8)|b[p+1];p+=2;return v;}
        static int U32(byte[] b,ref int p){if(p+4>b.Length)throw new FormatException("MIDI 文件长度无效。");int v=(b[p]<<24)|(b[p+1]<<16)|(b[p+2]<<8)|b[p+3];p+=4;return v;}
        static int Var(byte[] b,ref int p,int end){int value=0,count=0;while(true){if(p>=end||count++>=4)throw new FormatException("MIDI 可变长度数据无效。");int x=b[p++];value=(value<<7)|(x&0x7f);if((x&0x80)==0)return value;}}
        static string Text(byte[] b,int p,int len){return p>=0&&len>=0&&p+len<=b.Length?Encoding.UTF8.GetString(b,p,len).Trim():"";}
        public static Song Read(string path,bool allParts=true){
            byte[] data=File.ReadAllBytes(path);if(data.Length>50000000)throw new FormatException("MIDI 文件过大。");
            int p=0;if(data.Length<14||Text(data,0,4)!="MThd")throw new FormatException("不是标准 MIDI 文件。");p=4;int headerLen=U32(data,ref p);if(headerLen<6||p+headerLen>data.Length)throw new FormatException("MIDI 文件头无效。");
            int format=U16(data,ref p),trackCount=U16(data,ref p),division=U16(data,ref p);p=8+headerLen;
            if(format>2||trackCount==0||division==0||(division&0x8000)!=0)throw new FormatException("只支持 PPQ 格式的 MIDI（不支持 SMPTE 时间码）。");
            var song=new Song{Title=Path.GetFileNameWithoutExtension(path),Source="MIDI 文件",License="用户导入",Bpm=120};int part=0;bool tempoSeen=false;
            for(int track=0;track<trackCount&&p+8<=data.Length;track++){
                if(Text(data,p,4)!="MTrk")throw new FormatException("MIDI 音轨块无效。");p+=4;int length=U32(data,ref p),end=p+length;if(length<0||end>data.Length)throw new FormatException("MIDI 音轨提前结束。");
                long tick=0;int running=0;var active=new Dictionary<int,Queue<Note>>();string trackName="音轨 "+(part+1);
                while(p<end){
                    tick+=Var(data,ref p,end);if(p>=end)break;int status=data[p++];
                    if(status<0x80){if(running==0)throw new FormatException("MIDI running status 无效。");p--;status=running;}else if(status<0xf0)running=status;
                    if(status==0xff){
                        if(p>=end)break;int meta=data[p++],len=Var(data,ref p,end);if(meta==0x03&&len>0)trackName=Text(data,p,len);
                        if(meta==0x51&&len==3&&!tempoSeen&&p+3<=end){int us=(data[p]<<16)|(data[p+1]<<8)|data[p+2];if(us>0)song.Bpm=(int)Math.Max(30,Math.Min(240,60000000.0/us));tempoSeen=true;}
                        p=Math.Min(end,p+len);continue;
                    }
                    if(status==0xf0||status==0xf7){int len=Var(data,ref p,end);p=Math.Min(end,p+len);running=0;continue;}
                    if(status>=0xf0){int skip=(status==0xf1||status==0xf3)?1:(status==0xf2?2:0);p=Math.Min(end,p+skip);continue;}
                    int command=status&0xf0,channel=status&0x0f;if(p>=end)break;int data1=data[p++],data2=0;
                    if(command!=0xc0&&command!=0xd0){if(p>=end)break;data2=data[p++];}
                    if(channel==9)continue;
                    if(command==0x90&&data2>0){int key=(channel<<8)|data1;Queue<Note> q;if(!active.TryGetValue(key,out q)){q=new Queue<Note>();active[key]=q;}q.Enqueue(new Note((double)tick/division,0.5,data1){Part=part});}
                    else if(command==0x80||(command==0x90&&data2==0)){int key=(channel<<8)|data1;Queue<Note> q;if(active.TryGetValue(key,out q)&&q.Count>0){var note=q.Dequeue();note.Duration=Math.Max(0.03,(double)tick/division-note.Beat);song.Notes.Add(note);if(q.Count==0)active.Remove(key);}}
                }
                foreach(var q in active.Values)foreach(var note in q){note.Duration=0.5;song.Notes.Add(note);}p=end;song.PartNames.Add(trackName);part++;
            }
            if(!allParts&&song.Notes.Count>0){int first=song.Notes.Min(n=>n.Part);song.Notes=song.Notes.Where(n=>n.Part==first).ToList();}
            song.Notes=song.Notes.OrderBy(n=>n.Beat).ThenBy(n=>n.Part).ThenBy(n=>n.Midi).ToList();Importers.Validate(song);return song;
        }
    }
}
