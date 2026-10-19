using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading.Tasks;

namespace LumaKeys {
    public partial class App {
        void OpenCatalog() {
            var dialog=Dialog("搜索曲库 · 中文 / 动漫 / 多语种",1100,800);var root=new DockPanel{Margin=new Thickness(28)};dialog.Content=root;
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            header.Children.Add(new TextBlock{Text="搜索一首想弹的歌",FontSize=25,FontWeight=FontWeights.Bold});
            header.Children.Add(Label("本地目录即时筛选；选择 PiaStudy 可联网搜索曲名、作者，或粘贴曲谱链接。"));
            var sourceRow=new DockPanel();header.Children.Add(sourceRow);
            var refresh=new Button{Content="更新曲目目录",Margin=new Thickness(12,0,0,0)};DockPanel.SetDock(refresh,Dock.Right);sourceRow.Children.Add(refresh);
            var choices=new List<CatalogSource>{new CatalogSource{Name="全部曲库（含已搜索的 PiaStudy 曲目）"}};choices.AddRange(Catalog.Sources);
            var sourcePicker=new ComboBox{ItemsSource=choices,SelectedIndex=0,Margin=new Thickness(0,4,0,10)};Automation(sourcePicker,"CatalogSource");sourceRow.Children.Add(sourcePicker);
            var searchRow=new DockPanel();header.Children.Add(searchRow);
            var piaSearch=new Button{Content="联网搜索 PiaStudy",Margin=new Thickness(8,0,0,0)};Automation(piaSearch,"PiaSearch");DockPanel.SetDock(piaSearch,Dock.Right);searchRow.Children.Add(piaSearch);
            var search=new TextBox{ToolTip="例如：诀别书、周杰伦、Animenz，或粘贴 PiaStudy 曲谱链接"};Automation(search,"CatalogSearch");searchRow.Children.Add(search);
            var piaActions=new WrapPanel();header.Children.Add(piaActions);
            var browse=new Button{Content="网站登录 / 下载 ↗"};piaActions.Children.Add(browse);
            var previous=new Button{Content="上一页"};piaActions.Children.Add(previous);
            var next=new Button{Content="下一页"};piaActions.Children.Add(next);
            var count=Label("");header.Children.Add(count);
            var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            var credit=Label("");bottom.Children.Add(credit);
            var all=new CheckBox{Content="下载全部声部 / 图层，保留同拍和弦",IsChecked=true};Automation(all,"CatalogAllParts");bottom.Children.Add(all);
            var status=Label("可离线搜索内置目录。PiaStudy 可直接导入公开数字曲谱；需要登录的下载在网站窗口完成。");Automation(status,"CatalogStatus");bottom.Children.Add(status);
            var buttons=new WrapPanel();bottom.Children.Add(buttons);
            var download=new Button{Content="下载并加入曲库",Background=Brush("#7660DB"),Foreground=Brush("#FFFFFF")};Automation(download,"CatalogDownload");buttons.Children.Add(download);
            buttons.Children.Add(Button("Animenz / A叔公开谱 ↗",()=>Process.Start("https://animenz.wordpress.com/tag/sheet/")));
            var list=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single,CanUserAddRows=false,RowHeight=38,Background=Brush("#FFFFFF"),BorderThickness=new Thickness(0),Margin=new Thickness(0,4,0,8)};Automation(list,"CatalogResults");root.Children.Add(list);
            list.Columns.Add(new DataGridTextColumn{Header="曲名",Binding=new Binding("Title"),Width=new DataGridLength(2.2,DataGridLengthUnitType.Star)});
            list.Columns.Add(new DataGridTextColumn{Header="作者 / 制谱人",Binding=new Binding("Composer"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            list.Columns.Add(new DataGridTextColumn{Header="来源",Binding=new Binding("SourceName"),Width=new DataGridLength(1.2,DataGridLengthUnitType.Star)});
            buttons.Children.Add(Button("查看原谱与来源 ↗",()=>{var entry=list.SelectedItem as CatalogEntry;if(entry==null)return;var source=Catalog.GetSource(entry.SourceId);Process.Start(source.External?entry.DownloadUrl:"https://github.com/"+source.Repository+"/blob/"+source.Branch+"/"+string.Join("/",entry.Path.Split('/').Select(Uri.EscapeDataString)));}));
            CatalogIndex index=Catalog.LoadAll();PiaPage page=null;string pageQuery="";bool busy=false;
            Action updateButtons=()=>{
                refresh.IsEnabled=!busy&&((CatalogSource)sourcePicker.SelectedItem).Id!="piastudy";
                piaSearch.IsEnabled=!busy;browse.IsEnabled=!busy;previous.IsEnabled=!busy&&page!=null&&page.Page>1;
                next.IsEnabled=!busy&&page!=null&&page.Page<page.PageCount;download.IsEnabled=!busy&&list.SelectedItem!=null;
            };
            Action filter=()=>{
                var source=(CatalogSource)sourcePicker.SelectedItem;
                if(source.Id=="piastudy"&&page!=null&&pageQuery==search.Text.Trim()) {
                    list.ItemsSource=page.Entries;count.Text="PiaStudy · 第 "+page.Page+" / "+page.PageCount+" 页 · 本页 "+page.Entries.Count+" 份 / 网站共 "+page.Total+" 份";
                }else {
                    page=null;var items=Catalog.Search(index,search.Text,source.Id);list.ItemsSource=items;
                    count.Text="本地目录找到 "+items.Count+" 份曲谱。PiaStudy 的更多结果请点“联网搜索”。";
                }
                if(list.Items.Count>0)list.SelectedIndex=0;updateButtons();
            };
            Action<bool> setBusy=value=>{busy=value;sourcePicker.IsEnabled=!value;search.IsEnabled=!value;list.IsEnabled=!value;all.IsEnabled=!value;updateButtons();};
            Action<PiaPage> receive=result=>{
                sourcePicker.SelectedItem=choices.First(c=>c.Id=="piastudy");if(!string.IsNullOrWhiteSpace(result.Query))search.Text=result.Query;page=result;pageQuery=search.Text.Trim();
                try{PiaStudy.Remember(result.Entries);index=Catalog.LoadAll();}catch{status.Text="结果已读取，但本地目录暂时无法保存。";}
                filter();
            };
            Action<Song> add=song=>{status.Text=AddCatalogSong(song);};
            // WebView2 is an optional part of the online-search window.  A
            // portable copy can be damaged when somebody copies only the EXE,
            // so keep that failure inside this dialog instead of allowing the
            // CLR assembly-load error to escape as a Windows error box.
            Action<string> openBrowser=url=>{
                try {
                    string[] required={"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.Wpf.dll","WebView2Loader.dll"};
                    string[] missing=required.Where(name=>!File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory,name))).ToArray();
                    if(missing.Length>0) {
                        status.Text="在线搜索窗口的安装文件不完整：缺少 "+string.Join("、",missing)+"。请重新运行 dogeplay 安装包或完整解压 ZIP（不要只复制 dogeplay.exe）。";
                        return;
                    }
                    OpenPiaBrowser(dialog,url,all.IsChecked==true,receive,add);
                }catch(Exception ex) {
                    Diagnostics.Log("PiaStudy browser open failed: "+ex);
                    status.Text="无法打开在线搜索窗口。请重新安装 dogeplay；如果仍无法打开，请先安装 / 修复 Microsoft Edge WebView2 Runtime。";
                }
            };
            list.SelectionChanged+=(s,e)=>{var entry=list.SelectedItem as CatalogEntry;credit.Text=entry==null?"":Catalog.GetSource(entry.SourceId).Credit;updateButtons();};
            search.TextChanged+=(s,e)=>filter();sourcePicker.SelectionChanged+=(s,e)=>filter();filter();
            Func<int,Task> runSearch=async number=>{
                if(busy)return;string query=search.Text.Trim();string url;
                try{url=PiaStudy.SearchUrl(query,number);}catch(Exception ex){status.Text=ex.Message;return;}
                setBusy(true);status.Text="正在搜索 PiaStudy…";
                try{receive(await PiaStudy.Search(query,number));status.Text="搜索完成，可翻页选择不同编配后下载。";}
                catch(PiaBrowserRequiredException){setBusy(false);status.Text="网站需要浏览器验证。请在网站窗口完成加载后点“取回本页搜索结果”。";openBrowser(url);}
                catch(Exception ex){status.Text="PiaStudy 搜索未完成："+ex.Message;}
                finally{setBusy(false);}
            };
            piaSearch.Click+=async(s,e)=>await runSearch(1);
            previous.Click+=async(s,e)=>{if(page!=null)await runSearch(page.Page-1);};next.Click+=async(s,e)=>{if(page!=null)await runSearch(page.Page+1);};
            search.KeyDown+=async(s,e)=>{if(e.Key==Key.Enter&&!busy){e.Handled=true;await runSearch(1);}};
            browse.Click+=(s,e)=>{
                var entry=list.SelectedItem as CatalogEntry;string url=entry!=null&&entry.SourceId=="piastudy"?entry.DownloadUrl:PiaStudy.Example;
                try{if(entry==null&&search.Text.Trim().Length>0)url=PiaStudy.SearchUrl(search.Text,1);openBrowser(url);}catch(Exception ex){status.Text=ex.Message;}
            };
            dialog.Closing+=(s,e)=>{if(busy){e.Cancel=true;status.Text="正在处理曲库，请等待当前操作完成。";}};
            refresh.Click+=async(s,e)=>{
                setBusy(true);var selectedSource=(CatalogSource)sourcePicker.SelectedItem;
                var sources=selectedSource.Id==null?Catalog.Sources.Where(x=>x.Id!="piastudy").ToArray():new[]{selectedSource};var failed=new List<string>();
                try {
                    foreach(var source in sources) {status.Text="正在更新："+source.Name+"…";try{await Catalog.Refresh(source.Id);}catch(Exception ex){failed.Add(source.Name+"："+ex.Message);}}
                    index=Catalog.LoadAll();status.Text=failed.Count==0?"目录更新完成，共 "+index.Entries.Count+" 份曲谱。":"部分来源未更新，已保留原目录。"+string.Join("；",failed);
                }finally{setBusy(false);filter();}
            };
            download.Click+=async(s,e)=>{
                if(busy)return;var entry=list.SelectedItem as CatalogEntry;if(entry==null)return;bool includeAll=all.IsChecked==true;
                setBusy(true);status.Text="正在下载并解析："+entry.Title+"…";
                try{add(await Catalog.Download(entry,includeAll));}
                catch(PiaBrowserRequiredException){setBusy(false);status.Text="请在网站窗口继续下载。";openBrowser(entry.DownloadUrl);}
                catch(Exception ex){status.Text="下载未完成："+ex.Message;}
                finally{setBusy(false);}
            // Keep the catalog independent from the main window so songs can
            // be downloaded or parsed while an existing song is playing.
            };dialog.Show();
        }
        string AddCatalogSong(Song song) {
            var existing=songs.FirstOrDefault(x=>x.SourceUrl==song.SourceUrl&&x.Notes.Select(n=>n.Part).Distinct().Count()==song.Notes.Select(n=>n.Part).Distinct().Count());
            if(existing!=null){selected=existing;C<TextBox>("SearchBox").Text="";RefreshLibrary();C<ListBox>("SongList").SelectedItem=existing;return "曲库已包含："+existing.Title+"。";}
            AddSong(song);return "已加入："+song.Title+" · "+song.Notes.Select(n=>n.Part).Distinct().Count()+" 个声部 / 图层 · "+song.Notes.Count+" 个音符。";
        }
        static void Automation(FrameworkElement item,string id){System.Windows.Automation.AutomationProperties.SetAutomationId(item,id);}
    }
}
