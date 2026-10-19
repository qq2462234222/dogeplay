using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace LumaKeys {
    public partial class App {
        void RefreshInputAccess() {
            var target=C<ComboBox>("WindowCombo").SelectedItem as GameWindow;
            bool blocked=!testing&&target!=null&&InputAccess.RequiresElevation(target);
            C<TextBlock>("PermissionHint").Text=blocked?"游戏以管理员运行：需要以管理员身份重启dogeplay。":InputAccess.IsAdministrator?"dogeplay已以管理员身份运行。":"dogeplay当前为普通权限。";
            C<Button>("AdminButton").Visibility=InputAccess.IsAdministrator?Visibility.Collapsed:Visibility.Visible;
        }
        void RestartAsAdministrator() {
            if(player.Running){Status("请先停止演奏，再切换运行权限。");return;}
            if(InputAccess.IsAdministrator){Status("dogeplay已经以管理员身份运行。");return;}
            Save();
            try {
                var info=new ProcessStartInfo(Branding.ExecutablePath,"--elevated-restart") {UseShellExecute=true,Verb="runas",WorkingDirectory=Store.Root};
                Process.Start(info);window.Close();
            }catch(System.ComponentModel.Win32Exception ex){if(ex.NativeErrorCode==1223)Status("已取消管理员授权，当前版本仍可试听和导入曲谱。");else throw;}
        }
        bool CheckInputAccess(GameWindow target) {
            if(testing||!InputAccess.RequiresElevation(target))return true;
            string message="检测到游戏以管理员权限运行，而dogeplay是普通权限。Windows 会限制向该窗口输入。\n\n是否以管理员身份重新启动dogeplay？重启后请重新点击开始弹奏。";
            Status("权限不匹配，尚未发送按键。需要以管理员身份重启dogeplay。");
            if(MessageBox.Show(window,message,"需要匹配游戏权限",MessageBoxButton.YesNo,MessageBoxImage.Information)==MessageBoxResult.Yes)RestartAsAdministrator();
            return false;
        }
        void KeyboardTestDialog() {
            if(player.Running){Status("请先停止演奏再测试输入。");return;}
            var dialog=Dialog("键盘输入测试",640,420);var panel=new StackPanel{Margin=new Thickness(25)};dialog.Content=panel;
            panel.Children.Add(Label("点开始后，软件会向下面的测试框实际输入 asdfghj。测试不会向游戏或其他聊天窗口输入，也不会按回车发送。"));
            var receiver=new TextBox{Height=100,FontSize=26,AcceptsReturn=true,Margin=new Thickness(0,12,0,12)};InputMethod.SetIsInputMethodEnabled(receiver,false);panel.Children.Add(receiver);
            var status=Label("等待测试");panel.Children.Add(status);var button=new Button{Content="开始测试"};panel.Children.Add(button);
            var testPlayer=new Player();bool busy=false,closeAfter=false;
            dialog.Closing+=(s,e)=>{if(busy){e.Cancel=true;closeAfter=true;testPlayer.Stop();}};
            button.Click+=async(s,e)=>{
                button.IsEnabled=false;busy=true;receiver.Clear();receiver.Focus();Keyboard.Focus(receiver);status.Text="正在发送实际按键…";
                try {
                    var target=new GameWindow{Handle=new WindowInteropHelper(dialog).Handle,Title=dialog.Title};
                    await testPlayer.Start(Notation.Parse("test","1:0.4 2:0.4 3:0.4 4:0.4 5:0.4 6:0.4 7:0.4",120),new Mapping(new Settings().Profiles[0]),0,false,false,target,0);
                    await Task.Delay(100);string text=receiver.Text.ToLowerInvariant();
                    status.Text=text=="asdfghj"?"✓ 实际输入成功：asdfghj。若游戏不响应，请核对运行权限、游戏输入限制和键位。":"未收到预期文字，实际收到："+(text.Length==0?"（空）":text)+"。请保持测试框在前台后重试。";
                    Diagnostics.Log("Keyboard receiver expected=asdfghj actual="+text);
                }catch(Exception ex){status.Text=ex.Message;Diagnostics.Log(ex.ToString());}
                busy=false;button.IsEnabled=true;if(closeAfter)dialog.Close();
            };dialog.ShowDialog();
        }
    }
}
