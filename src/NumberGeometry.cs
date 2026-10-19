using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace LumaKeys {
    // OCR finds digits; connected components recover separate octave dots and underline beams.
    // This is intentionally a draft aid for clean, horizontal, single-voice printed jianpu.
    public sealed class NumberGeometry {
        class Blob { public int X,Y,R,B,Area; public int Width{get{return R-X+1;}}public int Height{get{return B-Y+1;}}public double Cx{get{return (X+R)/2.0;}} }
        List<Blob> blobs=new List<Blob>();
        byte[] ink;int imageWidth;
        public NumberGeometry(string path) {
            using(var source=new Bitmap(path)) using(var bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.DrawImage(source,0,0,source.Width,source.Height);}
                int w=bitmap.Width,h=bitmap.Height;var data=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                byte[] pixels=new byte[data.Stride*h];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);bitmap.UnlockBits(data);
                var mask=new byte[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=y*data.Stride+x*4;if(pixels[p]+pixels[p+1]+pixels[p+2]<480)mask[y*w+x]=1;}
                ink=(byte[])mask.Clone();imageWidth=w;
                var queue=new Queue<int>();
                for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                    int p=y*w+x;if(mask[p]!=1)continue;mask[p]=2;queue.Enqueue(p);var b=new Blob{X=x,R=x,Y=y,B=y};
                    while(queue.Count>0){int current=queue.Dequeue(),cx=current%w,cy=current/w;b.Area++;b.X=Math.Min(b.X,cx);b.R=Math.Max(b.R,cx);b.Y=Math.Min(b.Y,cy);b.B=Math.Max(b.B,cy);
                        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=cx+dx,ny=cy+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int np=ny*w+nx;if(mask[np]==1){mask[np]=2;queue.Enqueue(np);}}
                    }if(b.Area>=3)blobs.Add(b);
                }
            }
        }
        static byte[] Normalize(byte[] pixels,int width,Blob box) {
            var result=new byte[32*40];double scale=Math.Min(28.0/box.Width,36.0/box.Height);int rw=(int)(box.Width*scale),rh=(int)(box.Height*scale),ox=(32-rw)/2,oy=(40-rh)/2;
            for(int y=0;y<rh;y++)for(int x=0;x<rw;x++){int sx=Math.Min(box.R,box.X+(int)(x/scale)),sy=Math.Min(box.B,box.Y+(int)(y/scale));result[(y+oy)*32+x+ox]=pixels[sy*width+sx];}return result;
        }
        public string FallbackDraft() {
            // Short, widely spaced number rows can be ignored by Windows OCR. Conservative
            // font-template matching provides a reviewable fallback for printed glyphs only.
            var templates=new List<Tuple<int,byte[]>>();
            foreach(string name in new[]{"Arial","Times New Roman","Microsoft YaHei","SimSun"})foreach(var style in new[]{FontStyle.Regular,FontStyle.Bold})for(int digit=0;digit<=9;digit++) {
                using(var bitmap=new Bitmap(100,100))using(var graphics=Graphics.FromImage(bitmap))using(var font=new Font(name,36,style)) {
                    graphics.Clear(Color.White);graphics.DrawString(digit.ToString(),font,Brushes.Black,15,15);
                    var pixels=new byte[10000];var box=new Blob{X=100,Y=100,R=0,B=0};
                    for(int y=0;y<100;y++)for(int x=0;x<100;x++){var color=bitmap.GetPixel(x,y);if(color.R+color.G+color.B<480){pixels[y*100+x]=1;box.X=Math.Min(box.X,x);box.Y=Math.Min(box.Y,y);box.R=Math.Max(box.R,x);box.B=Math.Max(box.B,y);}}
                    templates.Add(Tuple.Create(digit,Normalize(pixels,100,box)));
                }
            }
            var bodies=blobs.Where(b=>b.Height>=12&&b.Height>b.Width*.8&&b.Height<b.Width*7).ToList();
            if(bodies.Count==0)return "";double height=bodies.Select(b=>b.Height).OrderBy(h=>h).ElementAt(bodies.Count/2);
            var recognized=new List<Tuple<Blob,int>>();
            foreach(var blob in bodies.Where(b=>b.Height>=height*.65&&b.Height<=height*1.4)) {
                byte[] shape=Normalize(ink,imageWidth,blob);double best=1;int value=-1;
                foreach(var template in templates){int mismatch=0,union=0;for(int i=0;i<shape.Length;i++){if(shape[i]!=template.Item2[i])mismatch++;if(shape[i]!=0||template.Item2[i]!=0)union++;}double score=(double)mismatch/Math.Max(1,union);if(score<best){best=score;value=template.Item1;}}
                if(best<.44&&value>=0&&value<=7)recognized.Add(Tuple.Create(blob,value));
            }
            var lines=new List<List<Tuple<Blob,int>>>();
            foreach(var item in recognized.OrderBy(b=>b.Item1.Y)){var line=lines.FirstOrDefault(l=>Math.Abs(l[0].Item1.Y-item.Item1.Y)<height*.5);if(line==null){line=new List<Tuple<Blob,int>>();lines.Add(line);}line.Add(item);}
            return string.Join(" | ",lines.Where(l=>l.Count>=2).Select(l=>string.Join(" ",l.OrderBy(t=>t.Item1.X).Select(t=>t.Item2==0?"0":Enrich(t.Item2.ToString(),t.Item1.X,t.Item1.Y,t.Item1.Width,t.Item1.Height)))));
        }
        public string Enrich(string digit,double x,double y,double width,double height) {
            double center=x+width/2;
            var body=blobs.Where(b=>b.Height>=height*0.45&&b.Height<=height*1.15&&b.Width<=Math.Max(width*1.5,height)&&b.Cx>=x-width*.2&&b.Cx<=x+width*1.2&&b.B>=y&&b.Y<=y+height)
                .OrderBy(b=>Math.Abs(b.Cx-center)+Math.Abs(b.Y-y)).FirstOrDefault();
            if(body==null)return digit;
            double size=body.Height;
            var dots=blobs.Where(b=>b!=body&&b.Width<=size*.32&&b.Height<=size*.32&&b.Width>=2&&b.Height>=2&&b.Area>=4&&Math.Abs(b.Cx-body.Cx)<Math.Max(body.Width*.48,size*.22)).ToList();
            int above=dots.Count(b=>b.B<body.Y-size*.06&&b.Y>=body.Y-size*.85);
            int below=dots.Count(b=>b.Y>body.B+size*.06&&b.B<=body.B+size*1.2);
            int lines=0;double lastLine=-999;
            foreach(var b in blobs.Where(b=>b.Y>body.B+1&&b.Y<body.B+size*.65&&b.Height<size*.19&&b.Width>=body.Width*.8&&b.X<=body.Cx&&b.R>=body.Cx).OrderBy(b=>b.Y)) {
                if(b.Y-lastLine>Math.Max(2,size*.07)){lines++;lastLine=b.Y;}
            }
            string result=digit+new string('\'',Math.Min(2,above))+new string(',',Math.Min(2,below));
            if(lines>0)result+=":"+Math.Pow(.5,Math.Min(3,lines)).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }
    }
}
