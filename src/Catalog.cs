using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;

using System.Text.RegularExpressions;


namespace LumaKeys {
    public class CatalogSource {
        public string Id,Name,Repository,Branch,Prefix,Credit,IndexUrl,DownloadBase;
        public bool External;
        public string[] Extensions;
        public override string ToString(){return Name;}
    }
    public class CatalogEntry {
        public string SourceId {get;set;}
        public string Title {get;set;}
        public string Composer {get;set;}
        public string Path {get;set;}
        public string Sha {get;set;}
        public string SourceName {get{return Catalog.GetSource(SourceId).Name;}}
        public string DownloadUrl {get{var source=Catalog.GetSource(SourceId);if(SourceId=="piastudy")return PiaStudy.PageUrl(Path);if(source.External)return new Uri(new Uri(source.DownloadBase),Path).AbsoluteUri;return "https://raw.githubusercontent.com/"+source.Repository+"/"+source.Branch+"/"+string.Join("/",Path.Split('/').Select(Uri.EscapeDataString));}}
        public override string ToString(){return Title+" · "+Composer;}
    }
    public class CatalogIndex {public string UpdatedUtc {get;set;}public List<CatalogEntry> Entries {get;set;}}
    public class GitTreeItem {public string path {get;set;}public string sha {get;set;}public string type {get;set;}}
    public class GitTree {public bool truncated {get;set;}public List<GitTreeItem> tree {get;set;}}
    public static class Catalog {
        public static readonly CatalogSource[] Sources={
            new CatalogSource{Id="openscore",Name="OpenScore · 古典",Repository="OpenScore/Lieder",Branch="main",Prefix="scores/",Extensions=new[]{".mxl"},Credit="CC0-1.0"},
            new CatalogSource{Id="smap",Name="SMAP · 中文与流行",Repository="lingyunalingyun/Sky-MusaAutoPlay-SMAP-",Branch="main",Prefix="曲谱库/|Installer/sample-songs/",Extensions=new[]{".txt",".json"},Credit="社区分享；曲谱版权归原作者，见来源项目说明"},
            new CatalogSource{Id="sky",Name="SkyAutoPlayer · 多语种",Repository="StageGuard/SkyAutoPlayerScript",Branch="master",Prefix="shared_sheets/",Extensions=new[]{".txt"},Credit="社区分享；曲谱权利见来源与原作者说明"}
            ,new CatalogSource{Id="josh",Name="Josh · Animenz / A叔公开谱",External=true,IndexUrl="https://josh.agarrado.net/music/anime/",DownloadBase="https://josh.agarrado.net/music/anime/",Prefix="get/",Extensions=new[]{".pdf"},Credit="页面公开文件；请遵循页面与原作者授权说明"}
            ,new CatalogSource{Id="piastudy",Name="PiaStudy · 天天钢琴",External=true,IndexUrl=PiaStudy.Home.TrimEnd('/')+PiaStudy.PianoSearchPath,DownloadBase="https://piastudy.com/",Prefix="Intermediate/",Extensions=new[]{""},Credit="PiaStudy 页面公开曲谱；请遵循网站与制谱人授权说明"}
        };
        public static CatalogSource GetSource(string id){var source=Sources.FirstOrDefault(s=>s.Id==(string.IsNullOrEmpty(id)?"openscore":id));if(source==null)throw new FormatException("未知曲库来源。");return source;}
        static string Cache(string id){return System.IO.Path.Combine(Store.Data,"catalog",id+".json");}
        public static CatalogIndex Load(string sourceId="openscore") {
            var source=GetSource(sourceId);
            foreach(string path in new[]{Cache(source.Id),System.IO.Path.Combine(Store.Root,"assets",source.Id,"catalog.json")}) {
                if(!File.Exists(path))continue;try{var index=Store.Read<CatalogIndex>(path);Validate(index);if(index.Entries.Any(e=>GetSource(e.SourceId)!=source))throw new FormatException("目录来源不一致。");return index;}catch(Exception ex){Diagnostics.Log("Catalog cache: "+ex.Message);}
            }return new CatalogIndex{Entries=new List<CatalogEntry>()};
        }
        public static CatalogIndex LoadAll(){return new CatalogIndex{Entries=Sources.SelectMany(s=>Load(s.Id).Entries).ToList()};}
        public static void Validate(CatalogIndex index) {
            if(index==null||index.Entries==null||index.Entries.Count==0||index.Entries.Count>30000)throw new FormatException("曲库目录无效。");
            if(index.Entries.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Title)||string.IsNullOrWhiteSpace(e.Composer)||!ValidPath(e.Path,GetSource(e.SourceId))||e.Sha==null||!System.Text.RegularExpressions.Regex.IsMatch(e.Sha,"^[a-f0-9]{40}$")))throw new FormatException("曲库目录含无效条目。");
        }
        public static bool ValidPath(string path){return ValidPath(path,Sources[0]);}
        static bool ValidPath(string path,CatalogSource source){if(source.Id=="piastudy")return PiaStudy.ValidPath(path);return path!=null&&source.Prefix.Split('|').Any(prefix=>path.StartsWith(prefix,StringComparison.Ordinal))&&source.Extensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant())&&!path.Contains("\\")&&!path.Split('/').Any(s=>s==".."||s=="."||s.Length==0);}
        public static List<CatalogEntry> Search(CatalogIndex index,string query,string sourceId=null) {
            string[] words=(query??"").Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            return index.Entries.Where(e=>(sourceId==null||GetSource(e.SourceId).Id==sourceId)&&words.All(w=>(e.Title+" "+e.Composer).IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(e=>e.Title.Any(c=>c>='\u3400'&&c<='\u9fff')?0:1).ThenBy(e=>e.Title).ThenBy(e=>e.Composer).ToList();
        }
        static async Task<byte[]> Fetch(string url) {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            using(var client=new WebClient()) {
                client.Headers["User-Agent"]="LumaKeys/0.2";

                var task=client.DownloadDataTaskAsync(url);
                if(await Task.WhenAny(task,Task.Delay(45000))!=task){client.CancelAsync();throw new TimeoutException("联网超时。已内置曲库目录，可继续搜索，下载需要网络连接。");}
                return await task;
            }
        }
        public static async Task<CatalogIndex> Refresh(string sourceId="openscore") {
            var source=GetSource(sourceId);
            if(source.External)return await RefreshExternal(source);
            string json=Encoding.UTF8.GetString(await Fetch("https://api.github.com/repos/"+source.Repository+"/git/trees/"+source.Branch+"?recursive=1"));
            var tree=Store.Json.Deserialize<GitTree>(json);
            if(tree==null||tree.tree==null||tree.truncated)throw new FormatException("GitHub 返回的曲库目录不完整，保留原有目录。");
            var index=new CatalogIndex{UpdatedUtc=DateTime.UtcNow.ToString("o"),Entries=new List<CatalogEntry>()};
            var old=Load(source.Id).Entries.GroupBy(e=>e.Path).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var item in tree.tree.Where(i=>i!=null&&i.type=="blob"&&ValidPath(i.path,source))) {
                var parts=item.path.Split('/');if(source.Id=="openscore"&&parts.Length<4)continue;
                CatalogEntry previous;old.TryGetValue(item.path,out previous);
                string title=source.Id=="openscore"?parts[parts.Length-2].Replace('_',' '):System.IO.Path.GetFileNameWithoutExtension(item.path).Replace('_',' ');
                index.Entries.Add(new CatalogEntry{SourceId=source.Id,Title=title,Composer=source.Id=="openscore"?parts[1].Replace('_',' '):previous==null?"社区曲谱":previous.Composer,Path=item.path,Sha=item.sha});
            }
            Validate(index);Store.Write(Cache(source.Id),index);return index;
        }
        static async Task<CatalogIndex> RefreshExternal(CatalogSource source) {
            if(source.Id=="piastudy")throw new FormatException("PiaStudy 不提供一次性全量目录，请在搜索框输入关键词后点“联网搜索”。");
            string html=Encoding.UTF8.GetString(await Fetch(source.IndexUrl));
            var titles=System.Text.RegularExpressions.Regex.Matches(html,"<div class='songtitle'>(.*?)</div>",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var index=new CatalogIndex{UpdatedUtc=DateTime.UtcNow.ToString("o"),Entries=new List<CatalogEntry>()};
            foreach(System.Text.RegularExpressions.Match files in System.Text.RegularExpressions.Regex.Matches(html,"<div class='filelinks'>(.*?)</div>",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Singleline)) {
                var title=titles.Cast<System.Text.RegularExpressions.Match>().Where(t=>t.Index<files.Index).LastOrDefault();if(title==null)continue;
                string clean=System.Text.RegularExpressions.Regex.Replace(title.Groups[1].Value,"<[^>]+>","").Trim();
                string section=html.Substring(title.Index,files.Index-title.Index);
                string composer=section.IndexOf("Animenz",StringComparison.OrdinalIgnoreCase)>=0?"Animenz / Josh公开谱":"Josh公开谱";
                foreach(System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(files.Groups[1].Value,"href=['\"]([^'\"]+\\.pdf)['\"]",System.Text.RegularExpressions.RegexOptions.IgnoreCase)) {
                    string path=link.Groups[1].Value;string url=new Uri(new Uri(source.DownloadBase),path).AbsoluteUri;
                    string sha=BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","").ToLowerInvariant();
                    index.Entries.Add(new CatalogEntry{SourceId=source.Id,Title=clean,Composer=composer,Path=path, Sha=sha});
                }
            }
            Validate(index);Store.Write(Cache(source.Id),index);return index;
        }
        public static string GitHash(byte[] data) {
            using(var sha=SHA1.Create())using(var stream=new MemoryStream()) {
                byte[] header=Encoding.ASCII.GetBytes("blob "+data.Length+"\0");stream.Write(header,0,header.Length);stream.Write(data,0,data.Length);
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static async Task<Song> Download(CatalogEntry entry,bool allParts) {
            Validate(new CatalogIndex{Entries=new List<CatalogEntry>{entry}});
            var source=GetSource(entry.SourceId);
            if(source.Id=="piastudy")return await PiaStudy.Download(entry,allParts);
            string path=System.IO.Path.Combine(Store.Data,"downloads",source.Id,entry.Sha+System.IO.Path.GetExtension(entry.Path));
            byte[] data=File.Exists(path)?File.ReadAllBytes(path):null;
            if(data==null||(!source.External&&GitHash(data)!=entry.Sha)) {
                data=await Fetch(entry.DownloadUrl);
                if(data.Length>20000000)throw new FormatException("曲谱文件过大。");
                if(source.External&&System.IO.Path.GetExtension(entry.Path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)&&!(data.Length>4&&data[0]==0x25&&data[1]==0x50&&data[2]==0x44&&data[3]==0x46))throw new FormatException("公开链接没有返回 PDF 文件。");
                if(!source.External&&GitHash(data)!=entry.Sha)throw new FormatException("该曲谱在上游有更新，请点“更新曲目目录”后重新下载。");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));File.WriteAllBytes(path,data);
            }
            Song song;
            if(source.Id=="openscore")song=await Task.Run(()=>Importers.MusicXml(path,allParts));
            else if(source.External) {
                song=await Importers.StaffOcr(path,allParts);
            } else song=await Task.Run(()=>CommunityScores.Read(path,allParts));
            song.Title=entry.Title;song.Source=source.Name+" · "+(source.Id=="openscore"?entry.Composer:song.Source);song.License=source.Credit;song.SourceUrl=entry.DownloadUrl;return song;
        }
    }
}
