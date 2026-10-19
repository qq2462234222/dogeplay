using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LumaKeys {
    public class PiaPage {
        public List<CatalogEntry> Entries=new List<CatalogEntry>();
        public int Page=1, PageCount=1, Total;
        public string Url, Query;
    }
    public class PiaBrowserRequiredException : Exception {
        public PiaBrowserRequiredException() : base("网站要求浏览器验证，请点“网站登录 / 下载”，在网站窗口中继续。") {}
    }
    public static class PiaStudy {
        const int MaxBytes=20000000;
        public const string Home="https://piastudy.com/";
        public const string PianoSearchPath="/searchResults/1/";
        public const string Example="https://piastudy.com/Intermediate/HeqQ2bPJ";
        public static bool ValidPath(string path) {return path!=null&&Regex.IsMatch(path,@"\AIntermediate/[A-Za-z0-9]{1,64}\z");}
        public static string PageUrl(string path) {if(!ValidPath(path))throw new FormatException("PiaStudy 曲谱链接无效。");return Home+path;}
        public static bool IsSite(Uri uri) {return uri!=null&&uri.Scheme=="https"&&uri.Host=="piastudy.com"&&uri.IsDefaultPort&&uri.UserInfo.Length==0;}
        public static string DetailPath(string url) {
            Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||!IsSite(uri))return null;
            string path=uri.AbsolutePath.TrimStart('/').TrimEnd('/');return ValidPath(path)?path:null;
        }
        public static string SearchUrl(string query,int page) {
            query=(query??"").Trim();string detail=DetailPath(query);if(detail!=null)return PageUrl(detail);
            if(query.Length==0||query.Length>150)throw new FormatException("请输入 1～150 个字符的曲名、作者，或 PiaStudy 曲谱链接。");
            if(query.StartsWith("http",StringComparison.OrdinalIgnoreCase))throw new FormatException("请填写 piastudy.com/Intermediate/ 开头的曲谱链接。");
            return Home.TrimEnd('/')+PianoSearchPath+Uri.EscapeDataString(query)+"/0/0/"+Math.Max(1,page);
        }
        static bool IsFile(Uri uri) {
            return uri!=null&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&
                (uri.Host=="i.insstudy.com"||uri.Host=="p.insstudy.com")&&uri.AbsolutePath.StartsWith("/file/psm/",StringComparison.Ordinal);
        }
        // Public requests carry no account cookies or credentials. Login and
        // authenticated downloads stay inside the site's own WebView session.
        static async Task<byte[]> Fetch(string url,bool file) {
            Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(file?!IsFile(uri):!IsSite(uri)))throw new FormatException("曲谱地址不是 PiaStudy 的可信地址。");
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            using(var timeout=new CancellationTokenSource(30000)) {
                for(int redirects=0;redirects<4;redirects++) {
                    timeout.Token.ThrowIfCancellationRequested();
                    var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;
                    request.UserAgent="LumaKeys/0.3";request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                    using(timeout.Token.Register(request.Abort)) {
                        try {
                            using(var response=(HttpWebResponse)await request.GetResponseAsync()) {
                                int status=(int)response.StatusCode;
                                if(status>=300&&status<400) {
                                    Uri next;if(!Uri.TryCreate(uri,response.Headers["Location"],out next)||(file?!IsFile(next):!IsSite(next)))throw new FormatException("网站跳转到其他地址，请在网站窗口中下载。");
                                    uri=next;continue;
                                }
                                if(response.ContentLength>MaxBytes)throw new FormatException("曲谱文件过大，请在网站下载后单独导入。");
                                using(var input=response.GetResponseStream())using(var output=new MemoryStream()) {
                                    var buffer=new byte[16384];int count;
                                    while((count=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token))>0) {
                                        if(output.Length+count>MaxBytes)throw new FormatException("曲谱文件过大。");
                                        output.Write(buffer,0,count);
                                    }
                                    return output.ToArray();
                                }
                            }
                        }catch(WebException ex) {
                            var response=ex.Response as HttpWebResponse;int status=response==null?0:(int)response.StatusCode;if(response!=null)response.Dispose();
                            if(timeout.IsCancellationRequested)throw new TimeoutException("PiaStudy 联网超过 30 秒，请重试或在网站窗口下载。");
                            if(status==401||status==403)throw new PiaBrowserRequiredException();
                            throw new WebException("PiaStudy 暂时无法访问"+(status==0?"。":"（HTTP "+status+"）。")+"请稍后重试。",ex);
                        }
                    }
                }
            }
            throw new FormatException("网站跳转次数过多，请在网站窗口中打开。");
        }
        public static async Task<PiaPage> Search(string query,int page) {
            string url=SearchUrl(query,page);string html=Encoding.UTF8.GetString(await Fetch(url,false));return ReadPage(html,url);
        }
        static object Unwrap(object value) {
            var pair=value as IList;if(pair==null||pair.Count!=2)throw new FormatException("PiaStudy 页面数据格式发生变化。");
            int type=Convert.ToInt32(pair[0]);object raw=pair[1];
            if(type==1)return ((IList)raw).Cast<object>().Select(Unwrap).ToArray();
            var dict=raw as Dictionary<string,object>;
            if(type==0&&dict!=null)return dict.ToDictionary(k=>k.Key,k=>Unwrap(k.Value));
            return raw;
        }
        static Dictionary<string,object> Props(string html,string component) {
            foreach(Match tag in Regex.Matches(html,@"<astro-island\b[^>]*>",RegexOptions.IgnoreCase)) {
                if(!Regex.IsMatch(tag.Value,"component-url=\"/assets/"+component+"[.\"]"))continue;
                var attr=Regex.Match(tag.Value,"props=\"([^\"]*)\"");if(!attr.Success)continue;
                var encoded=Store.Json.Deserialize<Dictionary<string,object>>(WebUtility.HtmlDecode(attr.Groups[1].Value));
                return encoded.ToDictionary(k=>k.Key,k=>Unwrap(k.Value));
            }
            throw new PiaBrowserRequiredException();
        }
        static Dictionary<string,object> Object(Dictionary<string,object> d,string key) {object v;return d.TryGetValue(key,out v)?v as Dictionary<string,object>:null;}
        static string Text(Dictionary<string,object> d,string key) {object v;return d.TryGetValue(key,out v)?Convert.ToString(v):"";}
        static int Number(Dictionary<string,object> d,string key,int fallback) {int v;return int.TryParse(Text(d,key),out v)?v:fallback;}
        static CatalogEntry Entry(Dictionary<string,object> d) {
            string path="Intermediate/"+Text(d,"sheetCode");string url=PageUrl(path);
            string title=Text(d,"title");if(string.IsNullOrWhiteSpace(title))title=Text(d,"songName");if(string.IsNullOrWhiteSpace(title))throw new FormatException("曲谱缺少标题。");
            string composer=string.Join(" / ",new[]{Text(d,"artist"),Text(d,"author"),Text(d,"compose")}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct());
            string sha;using(var hash=SHA1.Create())sha=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","").ToLowerInvariant();
            return new CatalogEntry{SourceId="piastudy",Path=path,Title=title,Composer=composer.Length>0?composer:"PiaStudy 制谱人",Sha=sha};
        }
        public static PiaPage ReadPage(string html,string url) {
            var result=new PiaPage{Url=url,Query=url};string detail=DetailPath(url);
            if(detail!=null) {
                var score=Object(Props(html,"ScoreCont"),"sheetData");if(score==null)throw new FormatException("页面没有曲谱数据。");
                var entry=Entry(score);if(entry.Path!=detail)throw new FormatException("页面中的曲谱编号与链接不一致。");
                result.Entries.Add(entry);result.Total=1;return result;
            }
            var props=Props(html,"SearchCont");result.Query=Text(props,"searchValue");var info=Object(props,"searchInfo");if(info==null)throw new FormatException("未找到曲谱搜索结果。");
            result.Page=Number(info,"no",1);result.Total=Number(info,"total",0);result.PageCount=Math.Max(1,Number(info,"pageCount",1));
            object raw;var records=info.TryGetValue("records",out raw)?raw as object[]:null;
            if(records!=null)foreach(var item in records) {var d=item as Dictionary<string,object>;if(d!=null)result.Entries.Add(Entry(d));}
            return result;
        }
        public static void Remember(IEnumerable<CatalogEntry> entries) {
            var merged=entries.Concat(Catalog.Load("piastudy").Entries).GroupBy(e=>e.Path).Select(g=>g.First()).Take(10000).ToList();
            if(merged.Count==0)return;
            var index=new CatalogIndex{UpdatedUtc=DateTime.UtcNow.ToString("o"),Entries=merged};Catalog.Validate(index);
            Store.Write(Path.Combine(Store.Data,"catalog","piastudy.json"),index);
        }
        public static async Task<Song> Download(CatalogEntry entry,bool allParts) {
            string url=PageUrl(entry.Path);string path=CachePath(entry);
            if(File.Exists(path))return Decorate(Importers.MusicXml(path,allParts),entry);
            string html=Encoding.UTF8.GetString(await Fetch(url,false));return await DownloadPage(html,url,allParts);
        }
        static string CachePath(CatalogEntry entry){return Path.Combine(Store.Data,"downloads","piastudy",entry.Sha+".musicxml");}
        static Song Decorate(Song song,CatalogEntry entry) {song.Title=entry.Title;song.Source="PiaStudy · "+entry.Composer;song.SourceUrl=PageUrl(entry.Path);song.License=Catalog.GetSource("piastudy").Credit;return song;}
        public static async Task<Song> DownloadPage(string html,string url,bool allParts) {
            var page=ReadPage(html,url);var entry=page.Entries.Single();
            var score=Object(Props(html,"ScoreCont"),"sheetData");
            if(!Text(score,"vip").Equals("False",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("这份曲谱需要网站确认权限，请在下方网站中登录并点击网站自己的下载按钮；下载完成后可识别导入。");
            string scoreUrl=Text(score,"scoreUrl");Uri resource;
            if(!Uri.TryCreate(scoreUrl,UriKind.Absolute,out resource)||!IsFile(resource)||!resource.AbsolutePath.EndsWith(".ibe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("此谱暂不支持直接提取音符，请使用网站自己的下载按钮，再识别导入文件。");
            byte[] bytes=await Fetch(scoreUrl,true);
            return await Task.Run(()=> {
                byte[] xml=Decode(bytes);string path=CachePath(entry);Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try {
                    File.WriteAllBytes(temp,xml);var song=Importers.MusicXml(temp,allParts); // Validate before publishing the cache.
                    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                    return Decorate(song,entry);
                }finally{if(File.Exists(temp))File.Delete(temp);}
            });
        }
        static byte[] Decode(byte[] input) {
            if(input.Length==0||input.Length>MaxBytes)throw new FormatException("网站没有返回有效曲谱。");
            var bytes=(byte[])input.Clone();var key=Encoding.ASCII.GetBytes("6VhgVZUFqQaE");int offset=bytes.Length%10;
            for(int i=0;i<bytes.Length;i++){if(offset==key.Length)offset=0;bytes[i]^=key[offset++];}
            try {
                using(var source=new MemoryStream(bytes))using(var decoder=new BrotliSharpLib.BrotliStream(source,CompressionMode.Decompress))using(var output=new MemoryStream()) {
                    byte[] buffer=new byte[16384];int count;
                    while((count=decoder.Read(buffer,0,buffer.Length))>0){if(output.Length+count>MaxBytes)throw new FormatException("解码后的曲谱过大。");output.Write(buffer,0,count);}
                    return output.ToArray();
                }
            }catch(InvalidDataException){throw new FormatException("网站曲谱格式已改变，请改用网站下载的 PDF / MIDI / MusicXML 文件导入。");}
        }
    }
}
