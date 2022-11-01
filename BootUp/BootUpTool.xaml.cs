using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using InteractiveTool;

namespace BootUp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BootUpTool : Window
    {
        public BootUpTool()
        {
            this.Visibility = Visibility.Hidden;
            InitializeComponent();
            InteractionToolWindow interactionTools = new InteractionToolWindow();//show方法及hide方法均在findcurriculum计时器内
            //Page page = new Page { Content = interactionTools };
            //this.Content = page;
        }
    }
}
