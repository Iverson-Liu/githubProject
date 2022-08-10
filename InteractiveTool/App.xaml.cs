using System;
using System.Windows;

namespace InteractiveTool
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        System.Threading.Mutex mutex;

        public App()
        {
            this.Startup += new StartupEventHandler(App_Startup);
        }

        /// <summary>
        /// 多程序运行防呆
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void App_Startup(object sender, StartupEventArgs e)
        {
            bool ret;
            mutex = new System.Threading.Mutex(true, "InteractiveTool", out ret);

            if (!ret)
            {
                MessageBox.Show("已有一个互动工具实例运行");
                Environment.Exit(0);
            }
        }
    }

}
