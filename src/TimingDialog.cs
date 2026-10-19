using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LumaKeys {
    public partial class App {
        bool loadingTiming;
        void InitializeTimingControls() {
            LoadTimingControls();
            RoutedEventHandler changed=(s,e)=>{if(!ready||loadingTiming)return;Profile.Timing.Enabled=C<CheckBox>("TimingCheck").IsChecked==true;Save();RefreshTimingHint();};
            C<CheckBox>("TimingCheck").Checked+=changed;C<CheckBox>("TimingCheck").Unchecked+=changed;
            C<TextBox>("BpmBox").TextChanged+=(s,e)=>{if(ready)RefreshTimingHint();};
            Click("TimingButton",TimingDialog);
        }
        void LoadTimingControls() {
            loadingTiming=true;
            try{C<CheckBox>("TimingCheck").IsChecked=Profile.Timing.Enabled;}
            finally{loadingTiming=false;}
        }
        void RefreshTimingHint() {
            if(selected==null||Profile==null)return;
            if(Profile.SustainNotes){C<TextBlock>("TimingHint").Text="持续长按到谱面音符结束，保留原速；同键重复音提前短暂松开后准时重按。暂停或停止会释放按键。";return;}
            C<TextBlock>("TimingHint").Text=Profile.Timing.Enabled?
                "按原谱速度演奏，同拍和弦一起按。重复音的按住与松开时间会适应原有音符间隔。":
                "按原谱速度演奏；同键重叠仍会松开并重新按下，避免伴奏遮住后来的音。";
        }
        void TimingDialog() {
            var dialog=Dialog("原速按键优化 · "+Profile.Name,620,510);var panel=new StackPanel{Margin=new Thickness(28)};dialog.Content=panel;
            panel.Children.Add(new TextBlock{Text="保持原速，调整按键时长",FontSize=22,FontWeight=FontWeights.Bold});
            panel.Children.Add(Label("每个音仍在原定时刻按下。间隔足够时延长短按；重复音太密时压缩按住与松开的时间，让下一个音准时开始。设置按游戏保存。"));
            panel.Children.Add(Label("短音按住目标（毫秒，密集重复音会自动缩短）"));var hold=new TextBox{Text=Profile.Timing.HoldMs.ToString()};panel.Children.Add(hold);
            panel.Children.Add(Label("同键重按前松开目标（毫秒，最多占重按间隔的 40%）"));var release=new TextBox{Text=Profile.Timing.ReleaseMs.ToString()};panel.Children.Add(release);
            var presets=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,16,0,16)};panel.Children.Add(presets);
            presets.Children.Add(Button("恢复推荐值",()=>{hold.Text="60";release.Text="15";}));
            panel.Children.Add(Button("保存并启用原速优化",()=>{
                int h,r;if(!int.TryParse(hold.Text,out h)||!int.TryParse(release.Text,out r))throw new FormatException("请填写整数毫秒。");
                var timing=new GameTimingSettings{Enabled=true,HoldMs=h,ReleaseMs=r};timing.Validate();
                Profile.Timing=timing;LoadTimingControls();Save();RefreshTimingHint();dialog.Close();
            }));dialog.ShowDialog();
        }
    }
}
