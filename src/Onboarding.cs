using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LumaKeys {
    public partial class App {
        void ShowOnboarding() {
            if(onboardingOpen||windowClosed)return;
            if(player.Running||sessionActive){Status("请先停止演奏，再打开使用指南。");return;}
            var pages=new[] {
                new[]{"欢迎来到dogeplay", "把喜欢的旋律，带进游戏世界。", "01  添加曲谱    →    02  选择游戏    →    03  开始演奏", "作者：doge · 免费使用 · 仅供学习交流\n\n这份指南会带你完成第一次演奏。随时点击“一键跳过”，以后也可以从顶部“使用指南”重新打开。"},
                new[]{"先把歌曲加入曲库", "本地导入，或在线找谱。", "顶部「导入文件」 / 「在线找谱」", "推荐导入 MIDI、MusicXML 或 MXL 文件。数字简谱图片与五线谱图片 / PDF 也可以导入；识别后请对照原谱校对。多页五线谱可能需要几分钟。\n\n在线找谱中输入曲名，点击“联网搜索 PiaStudy”会进入“钢琴曲”。需要登录或下载的曲谱，请在网站窗口按网站要求操作。"},
                new[]{"让游戏用上合适的乐器", "在右侧选择游戏、模式与乐器。", "右侧「游戏方案」 → 「演奏乐器 / 模式」", "永劫无间普通模式与大师模式使用不同的键位，请与游戏中的模式保持一致。曲笛请选择长按方案。\n\n进入游戏内的乐器演奏界面，回到软件点击“刷新”并选中目标游戏窗口。可以编辑键位方案，并保存演奏设置。游戏自动适配只影响演奏按键，原谱会保留。"},
                new[]{"试听，再开始游戏演奏", "选择起点，给自己留出切回游戏的时间。", "F8 开始    ·    F9 暂停 / 继续    ·    F10 停止", "先选择歌曲，点击“原谱试听”检查曲子。开始之前可拖动进度条选择起点，也可以直接输入开始倒计时秒数。\n\n点击“游戏演奏”，倒计时结束前切回游戏。前台模式切到其他窗口会暂停，切回游戏后按 F9 继续。演奏时可用小键盘方向键调整进度。Esc 留给游戏使用。\n\n后台模式会持续向所选游戏窗口发送消息，不要求接入游戏接口，也不会抢焦点；如果游戏不响应这类后台消息，请切回前台演奏，并核对游戏窗口、键位与权限。"},
                new[]{"准备好了，开始第一首吧", "曲库、队列和更新，都在熟悉的位置。", "多选曲目管理    ·    顺序播放 / 循环    ·    自动检查更新", "在曲库按住 Ctrl 或 Shift 多选，可批量加入歌单、队列、导出或删除。顶部可以选择顺序播放、单曲循环和列表循环；点击标题、来源、BPM 或时长表头可正序 / 倒序排序。\n\n顶部“悬浮学习谱”显示简谱与游戏按键，可以独立走谱练习，也能跟随软件演奏高亮。F7 锁定 / 解锁鼠标穿透。游戏请使用无边框窗口或窗口模式。\n\n每次启动都会联网检查更新并刷新公告。启动后会弹出公告，可勾选“不再提示，直到下次更新”。公告也位于左侧作者下方；发现新版本会提示是否更新，选择“是”后下载增量更新，选择“否”则继续使用当前版本，曲库和设置都会保留。\n\n点击“开始使用”关闭指南。需要时，从顶部“使用指南”再次查看。"}
            };
            var dialog=Dialog("dogeplay · 初次使用指南",780,630);
            dialog.MinWidth=660;dialog.MinHeight=570;
            var root=new DockPanel{Margin=new Thickness(30)};dialog.Content=root;
            var header=new DockPanel{Margin=new Thickness(0,0,0,22)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var skip=new Button{Content="一键跳过",Padding=new Thickness(15,9,15,9),Margin=new Thickness(0)};
            Automation(skip,"GuideSkip");DockPanel.SetDock(skip,Dock.Right);header.Children.Add(skip);
            header.Children.Add(new TextBlock{Text="dogeplay  /  使用指南",Foreground=Brush("#7657E8"),FontSize=15,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var footer=new DockPanel{Margin=new Thickness(0,22,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);
            var previous=new Button{Content="上一步"};Automation(previous,"GuidePrevious");actions.Children.Add(previous);
            var next=new Button{Content="下一步",Background=Brush("#7657E8"),Foreground=Brush("#FFFFFF"),MinWidth=110,Margin=new Thickness(0)};
            Automation(next,"GuideNext");actions.Children.Add(next);
            var progress=new TextBlock{Foreground=Brush("#817A91"),VerticalAlignment=VerticalAlignment.Center};Automation(progress,"GuideProgress");footer.Children.Add(progress);
            var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);
            var content=new StackPanel();scroll.Content=content;
            var title=new TextBlock{FontSize=29,FontWeight=FontWeights.Bold,Foreground=Brush("#302C3D"),TextWrapping=TextWrapping.Wrap};Automation(title,"GuideTitle");content.Children.Add(title);
            var subtitle=new TextBlock{FontSize=15,Foreground=Brush("#817A91"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,23)};content.Children.Add(subtitle);
            var callout=new TextBlock{Foreground=Brush("#5B3CC4"),FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
            content.Children.Add(new Border{Background=Brush("#EDE7FF"),CornerRadius=new CornerRadius(12),Padding=new Thickness(18),Child=callout,Margin=new Thickness(0,0,0,22)});
            var body=new TextBlock{FontSize=14,LineHeight=26,Foreground=Brush("#62596F"),TextWrapping=TextWrapping.Wrap};content.Children.Add(body);
            int page=0;
            Action render=()=>{title.Text=pages[page][0];subtitle.Text=pages[page][1];callout.Text=pages[page][2];body.Text=pages[page][3];progress.Text="步骤 "+(page+1)+" / "+pages.Length;previous.IsEnabled=page>0;next.Content=page==pages.Length-1?"开始使用":"下一步";scroll.ScrollToTop();};
            skip.Click+=(s,e)=>dialog.Close();
            previous.Click+=(s,e)=>{if(page>0){page--;render();}};
            next.Click+=(s,e)=>{if(page==pages.Length-1)dialog.Close();else{page++;render();}};
            dialog.Closed+=(s,e)=>{settings.OnboardingCompleted=true;try{SaveSettings();}catch(Exception ex){Diagnostics.Log("Guide preference: "+ex);}};
            render();onboardingOpen=true;
            try{dialog.ShowDialog();}finally{onboardingOpen=false;}
        }
    }
}
