using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LumaKeys {
    public partial class App {
        // The web profile belongs only to this app. The website handles login;
        // never read cookies, localStorage, password fields, or bearer tokens.
        void OpenPiaBrowser(Window owner,string initialUrl,bool allParts,Action<PiaPage> receive,Action<Song> add) {
            var dialog=Dialog("PiaStudy · 网站登录与曲谱下载",1160,860);dialog.Owner=owner;
            var root=new DockPanel{Margin=new Thickness(18)};dialog.Content=root;
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            header.Children.Add(new TextBlock{Text="在网站中登录，下载后加入曲库",FontSize=22,FontWeight=FontWeights.Bold});
            header.Children.Add(Label("下方为 PiaStudy 官方网页。请自行选择网站提供的登录方式；登录状态保留在dogeplay的网站窗口中。"));
            var address=Label(initialUrl);header.Children.Add(address);
            var bar=new WrapPanel();header.Children.Add(bar);
            var back=new Button{Content="返回"};bar.Children.Add(back);
            var reload=new Button{Content="刷新网页"};bar.Children.Add(reload);
            var collect=new Button{Content="取回本页搜索结果"};Automation(collect,"PiaCollect");bar.Children.Add(collect);
            var direct=new Button{Content="导入当前公开曲谱",Background=Brush("#7660DB"),Foreground=Brush("#FFFFFF")};Automation(direct,"PiaImport");bar.Children.Add(direct);
            var fileButton=new Button{Content="导入网站下载文件…"};bar.Children.Add(fileButton);
            var cancelDownloads=new Button{Content="取消网站下载"};bar.Children.Add(cancelDownloads);
            var clearSession=new Button{Content="清除网站登录状态"};bar.Children.Add(clearSession);
            var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            var status=Label("正在打开网站…");bottom.Children.Add(status);
            var fallback=new WrapPanel();bottom.Children.Add(fallback);
            fallback.Children.Add(Button("用系统浏览器打开 ↗",()=>Process.Start(initialUrl)));
            fallback.Children.Add(Button("安装 / 修复浏览器组件 ↗",()=>Process.Start("https://developer.microsoft.com/microsoft-edge/webview2/#download-section")));
            var requiredFiles=new[]{"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.Wpf.dll","WebView2Loader.dll"};
            var missingFiles=requiredFiles.Where(name=>!File.Exists(Path.Combine(AppContext.BaseDirectory,name))).ToArray();
            if(missingFiles.Length>0) {
                status.Text="当前安装目录不完整，缺少："+string.Join("、",missingFiles)+"。请重新运行 dogeplay 安装包，或完整解压 ZIP；不要只复制 dogeplay.exe。也可以点下方用系统浏览器打开。";
                controlsFallback(fallback,status);
                dialog.ShowDialog();
                return;
            }
            var web=new WebView2();root.Children.Add(web);
            bool busy=false,closed=false,loading=true;int activeDownloads=0;
            var downloads=new List<CoreWebView2DownloadOperation>();
            var queue=new Queue<Tuple<string,string>>();
            Action controls=()=>{
                bool available=!closed&&web.CoreWebView2!=null;string url=available?web.CoreWebView2.Source:"";Uri uri;
                bool site=Uri.TryCreate(url,UriKind.Absolute,out uri)&&PiaStudy.IsSite(uri);
                back.IsEnabled=available&&!busy&&!loading&&web.CoreWebView2.CanGoBack;reload.IsEnabled=available&&!busy;
                collect.IsEnabled=available&&!loading&&!busy&&site&&uri.AbsolutePath.StartsWith(PiaStudy.PianoSearchPath,StringComparison.Ordinal);
                direct.IsEnabled=available&&!loading&&!busy&&PiaStudy.DetailPath(url)!=null;fileButton.IsEnabled=!busy;
                clearSession.IsEnabled=available&&!busy&&activeDownloads==0;
                cancelDownloads.IsEnabled=activeDownloads>0;
            };
            Action<bool> setBusy=value=>{busy=value;controls();};
            // Read only publicly rendered score/search props, not the whole DOM.
            Func<Task<string>> readProps=async()=>{
                string value=await web.CoreWebView2.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('astro-island[props]')).filter(e => /^\/assets\/(ScoreCont|SearchCont)\./.test(e.getAttribute('component-url') || '')).map(e => { const x=document.createElement('astro-island'); x.setAttribute('component-url',e.getAttribute('component-url')); x.setAttribute('props',e.getAttribute('props')); return x.outerHTML; }).join('')");
                return Store.Json.Deserialize<string>(value)??"";
            };
            Func<string,string,Task> importFile=async(path,sourceUrl)=>{
                if(!File.Exists(path)||new FileInfo(path).Length>20000000)throw new FormatException("文件不存在或超过 20 MB，请单独处理后导入。");
                string ext=Path.GetExtension(path).ToLowerInvariant();Song song;
                if(new[]{".musicxml",".xml",".mxl"}.Contains(ext))song=await Task.Run(()=>Importers.MusicXml(path,allParts));
                else if(ext==".mid"||ext==".midi")song=await Task.Run(()=>MidiImporter.Read(path,allParts));
                else if(new[]{".pdf",".png",".jpg",".jpeg"}.Contains(ext))song=await Importers.StaffOcr(path,allParts);
                else throw new FormatException("请选择 PDF、图片、MIDI 或 MusicXML 曲谱文件。");
                Importers.Validate(song);song.Source="PiaStudy · 网站下载";song.License=Catalog.GetSource("piastudy").Credit;
                // Do not put the user's local download path into the song metadata.
                song.SourceUrl=sourceUrl;
                // Preserve a useful filename when the source carries no title.
                if(song.Title==Path.GetFileNameWithoutExtension(path)||song.Title.StartsWith("PiaStudy-"))song.Title=Path.GetFileNameWithoutExtension(path);
                add(song);status.Text="已加入曲库："+song.Title+" · "+song.Notes.Count+" 个音符。";
            };
            bool parsing=false;
            Func<Task> drain=null;
            drain=async()=>{
                if(parsing||closed)return;
                parsing=true;
                try {
                    while(queue.Count>0&&!closed) {
                        var item=queue.Dequeue();status.Text="下载完成，正在解析曲谱；PDF / 图片识别可能需要几分钟…";
                        try{await importFile(item.Item1,item.Item2);}catch(Exception ex){status.Text="文件已保留在 "+DisplayAppPath(item.Item1)+"。导入未完成："+ex.Message;}
                    }
                }finally{parsing=false;controls();if(queue.Count>0&&!closed){Task ignored=drain();}}
            };
            back.Click+=(s,e)=>{if(web.CoreWebView2!=null&&web.CoreWebView2.CanGoBack)web.CoreWebView2.GoBack();};
            cancelDownloads.Click+=(s,e)=>{foreach(var op in downloads.ToArray())op.Cancel();};
            reload.Click+=(s,e)=>{if(web.CoreWebView2!=null)web.Reload();};
            collect.Click+=async(s,e)=>{
                if(busy)return;setBusy(true);
                try{string url=web.CoreWebView2.Source;string html=await readProps();if(url!=web.CoreWebView2.Source)throw new InvalidOperationException("网页正在跳转，请加载完成后再试。");receive(PiaStudy.ReadPage(html,url));status.Text="结果已带回曲库。";setBusy(false);dialog.Close();}
                catch(Exception ex){status.Text="读取未完成："+ex.Message;}
                finally{if(!closed)setBusy(false);}
                if(!closed)await drain();
            };
            direct.Click+=async(s,e)=>{
                if(busy)return;setBusy(true);status.Text="正在读取当前曲谱的音符与和弦…";
                try{string url=web.CoreWebView2.Source;string html=await readProps();if(url!=web.CoreWebView2.Source)throw new InvalidOperationException("网页正在跳转，请加载完成后再试。");var song=await PiaStudy.DownloadPage(html,url,allParts);add(song);status.Text="已加入曲库："+song.Title+" · "+song.Notes.Count+" 个音符。";}
                catch(Exception ex){status.Text="导入未完成："+ex.Message;}
                finally{setBusy(false);}
                await drain();
            };
            fileButton.Click+=async(s,e)=>{
                var picker=new OpenFileDialog{Filter="曲谱文件|*.pdf;*.png;*.jpg;*.jpeg;*.mid;*.midi;*.mxl;*.musicxml;*.xml",Multiselect=false};
                if(picker.ShowDialog(dialog)!=true)return;setBusy(true);status.Text="正在读取文件…";
                try{await importFile(picker.FileName,null);}catch(Exception ex){status.Text="导入未完成："+ex.Message;}finally{setBusy(false);}
                await drain();
            };
            clearSession.Click+=async(s,e)=>{
                setBusy(true);
                try{await web.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);web.CoreWebView2.Navigate(initialUrl);status.Text="已清除此网站窗口的登录和浏览数据。";}
                catch{status.Text="暂时无法清除登录状态，请在网站内退出登录。";}finally{setBusy(false);}
                await drain();
            };
            dialog.Closing+=(s,e)=>{if(busy||activeDownloads>0||parsing||queue.Count>0){e.Cancel=true;status.Text="正在下载或解析，请等待完成；可以继续下载，或点“取消网站下载”。";}};
            dialog.Closed+=(s,e)=>{closed=true;web.Dispose();};
            dialog.Loaded+=async(s,e)=>{
                try {
                    string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string legacy=Path.Combine(local,"LumaKeys","PiaStudyWebView");
                    string profile=Directory.Exists(legacy)?legacy:Path.Combine(local,"dogeplay","PiaStudyWebView");
                    var environment=await CoreWebView2Environment.CreateAsync(null,profile);if(closed)return;
                    await web.EnsureCoreWebView2Async(environment);if(closed)return;
                    web.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;
                    web.NavigationStarting+=(ns,ne)=>{loading=true;controls();};
                    web.NavigationCompleted+=(ns,ne)=>{
                        if(closed)return;loading=false;address.Text=web.CoreWebView2.Source;
                        if(!busy)status.Text=ne.IsSuccess?"可在网页中搜索、登录和下载。公开曲谱可点“导入当前公开曲谱”；网站下载的文件完成后自动导入。":"网页未加载完成，请刷新或用系统浏览器打开。";
                        controls();
                    };
                    web.CoreWebView2.DownloadStarting+=(ds,de)=>{
                        try {
                        Uri current;if(!Uri.TryCreate(web.CoreWebView2.Source,UriKind.Absolute,out current)||!PiaStudy.IsSite(current))return;
                        string name=Path.GetFileName(de.ResultFilePath);string ext=Path.GetExtension(name).ToLowerInvariant();
                        if(!new[]{".pdf",".png",".jpg",".jpeg",".mid",".midi",".mxl",".musicxml",".xml"}.Contains(ext))return;
                        if(string.IsNullOrWhiteSpace(name))return;
                        name="曲谱-"+string.Join("_",Path.GetFileNameWithoutExtension(name).Split(Path.GetInvalidFileNameChars()));
                        if(name.Length>100)name=name.Substring(0,100);name+=ext;
                        string folder=Path.Combine(Store.Data,"downloads","piastudy","website",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                        string path=Path.Combine(folder,name);de.ResultFilePath=path;de.Handled=true;
                        var op=de.DownloadOperation;string sourceUrl=PiaStudy.DetailPath(current.AbsoluteUri)!=null?current.AbsoluteUri:null;downloads.Add(op);activeDownloads++;controls();
                        status.Text="网站正在下载："+name+"；下载完成后会自动导入。";
                        bool finished=false;
                        op.BytesReceivedChanged+=(bs,be)=>{if(op.BytesReceived>20000000)op.Cancel();};
                        op.StateChanged+=async(os,oe)=>{
                            if(closed||finished||op.State==CoreWebView2DownloadState.InProgress)return;finished=true;activeDownloads--;downloads.Remove(op);controls();
                            if(op.State==CoreWebView2DownloadState.Completed){queue.Enqueue(Tuple.Create(path,sourceUrl));await drain();}
                            else status.Text="网站下载已中断或取消，请重新下载（最大 20 MB）。";
                        };
                        }catch {de.Cancel=true;status.Text="无法保存网站下载文件，请用系统浏览器下载，再通过“导入网站下载文件”选择文件。";}
                    };
                    web.CoreWebView2.Navigate(initialUrl);controls();
                }catch(Exception ex){if(!closed){Diagnostics.Log("WebView2 startup failed: "+ex);status.Text="无法启动网站窗口。请安装 / 修复 Microsoft Edge WebView2 Runtime，或用系统浏览器登录下载，再点“导入网站下载文件”。";loading=false;controls();}}
            };
            controls();dialog.Show();
        }
        // Keep the fallback controls visible even when the optional WebView2
        // assemblies are absent.  The main application remains usable and the
        // user gets an actionable Chinese explanation instead of a CLR dialog.
        static void controlsFallback(Panel panel,TextBlock status) {
            foreach(UIElement item in panel.Children)item.IsEnabled=true;
        }
    }
}
