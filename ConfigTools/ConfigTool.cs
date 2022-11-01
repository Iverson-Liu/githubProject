using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace ConfigTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ConfigTool : Window
    {
        public ConfigTool()
        {
            //初始化
            InitializeComponent();
            InitConfig();
            PreventTouchToMousePromotion.Register(configfinish);
        }

        //获取驱动事件信息
        [DllImport("user32.dll")]
        private static extern uint GetMessageExtraInfo();

        /// <summary>
        /// 创建Xml配置文件,将配置栏中的Ip,端口,Mac地址写入config.xml配置文件中去
        /// </summary>
        public void CreateXml()
        {
            try
            {
                XmlDocument xmldoc = new XmlDocument();
                //创建Xml声明部分 <?xml version="1.0" encoding="utf-8" ?>
                XmlDeclaration declaration = xmldoc.CreateXmlDeclaration("1.0", "utf-8", null);
                XmlNode root = xmldoc.CreateElement("Configs");
                CreateNode(xmldoc, root, "ServerIp", string.Empty);
                CreateNode(xmldoc, root, "ServerPort", string.Empty);
                CreateNode(xmldoc, root, "ServerMac", string.Empty);
                xmldoc.AppendChild(root);
                xmldoc.Save(AppDomain.CurrentDomain.BaseDirectory + "config.xml");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建配置文件方法异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 创建配置文件节点
        /// </summary>
        /// <param name="xmldoc">xml配置文件</param>
        /// <param name="parentNode">父节点</param>
        /// <param name="name">节点名称</param>
        /// <param name="value">节点数据</param>
        public void CreateNode(XmlDocument xmldoc, XmlNode parentNode, string name, string value)
        {
            try
            {
                XmlNode node = xmldoc.CreateNode(XmlNodeType.Element, name, null);
                node.InnerText = value;
                parentNode.AppendChild(node);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建配置文件节点异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 写入配置文件信息
        /// </summary>
        /// <param name="node">节点名称</param>
        /// <param name="value">节点数据</param>
        public void WriteXml(string node, string value)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(AppDomain.CurrentDomain.BaseDirectory + "config.xml");
                XmlElement element = (XmlElement)xmlDoc.SelectSingleNode($"Configs/{node}");
                element.InnerText = value;
                xmlDoc.Save(AppDomain.CurrentDomain.BaseDirectory + "config.xml");
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("写XML异常:异常信息{0}.\r\n异常栈:{1}", ex.Message, ex.StackTrace), "写入配置信息异常警告");
                throw ex;
            }

        }

        /// <summary>
        /// 若配置文件存在则工具显示配置文件内容
        /// </summary>
        public void InitConfig()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "config.xml"))
                    {
                        return;
                    }
                    else
                    {
                        IP.Text = ReadXml("ServerIp");
                        Port.Text = ReadXml("ServerPort");
                        Mac.Text = ReadXml("ServerMac");
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"配置内容初始化失败,异常信息:{ex.Message}.\r\n异常栈: {ex.StackTrace}", "配置内容初始化异常警告");
                throw ex;
            }
        }

        /// <summary>
        /// 读取配置文件信息
        /// </summary>
        /// <param name="node">xml配置信息节点</param>
        /// <returns></returns>
        public string ReadXml(string node)
        {
            try
            {
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "config.xml"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(AppDomain.CurrentDomain.BaseDirectory + "config.xml");
                    XmlElement element = (XmlElement)xmlDoc.SelectSingleNode($"Configs/{node}");
                    string value = element.InnerText;
                    return value;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取配置文件异常:异常信息{ex.Message}.\r\n异常栈:{ex.StackTrace}", "配置文件读取异常警告");
                throw ex;
            }
        }

        /// <summary>
        /// 机器重启,线程启用shutdown.exe
        /// </summary>
        public void ReStart()
        {
            Process.Start("shutdown", "/r /t 0"); // 参数 /r 的意思是要重新启动计算机
        }

        private void Ip_Changed(object sender, TextChangedEventArgs e)
        {
            txtIpTip.Visibility = string.IsNullOrEmpty(IP.Text) ? Visibility.Visible : Visibility.Hidden;
        }

        private void Port_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtPortTip.Visibility = string.IsNullOrEmpty(Port.Text) ? Visibility.Visible : Visibility.Hidden;
        }

        private void Mac_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtMacTip.Visibility = string.IsNullOrEmpty(Mac.Text) ? Visibility.Visible : Visibility.Hidden;
        }

        private void txtIpTip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtIpTip.Visibility = Visibility.Hidden;
        }

        private void txtPortTip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtPortTip.Visibility = Visibility.Hidden;
        }

        private void txtMacTip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtMacTip.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// 工具栏拖拽
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        /// <summary>
        /// 最小化窗口按键,窗口最小化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Min_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 关闭按键进行窗口关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 完成配置按键功能
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigFinish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //触摸屏事件不响应
                uint extra = GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "config.xml"))
                {
                    CreateXml();
                }
                if (string.IsNullOrEmpty(IP.Text) || string.IsNullOrEmpty(Port.Text) || string.IsNullOrEmpty(Mac.Text))
                {
                    MessageBox.Show($"有配置项为空,禁止写入");
                }
                else
                {
                    WriteXml("ServerIp", IP.Text);
                    WriteXml("ServerPort", Port.Text);
                    WriteXml("ServerMac", Mac.Text);
                    //WriteRegister();
                    AutoStartUp auto = new AutoStartUp("InteractiveTool.exe");
                    auto.SetMeAutoStart();
                    MessageBoxResult result = MessageBox.Show("配置完成,需要重启生效.是否立即重启", "", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        ReStart();
                    }
                    else
                    {
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"配置工具异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "异常警告");
            }
        }

        /// <summary>
        /// 完成配置按键触控屏事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigFinish_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "config.xml"))
                {
                    CreateXml();
                }
                if (string.IsNullOrEmpty(IP.Text) || string.IsNullOrEmpty(Port.Text) || string.IsNullOrEmpty(Mac.Text))
                {
                    MessageBox.Show($"有配置项为空,禁止写入");
                }
                else
                {
                    WriteXml("ServerIp", IP.Text);
                    WriteXml("ServerPort", Port.Text);
                    WriteXml("ServerMac", Mac.Text);
                    //WriteRegister();
                    AutoStartUp auto = new AutoStartUp("InteractiveTool.exe");
                    auto.SetMeAutoStart();
                    MessageBoxResult result = MessageBox.Show("配置完成,需要重启生效.是否立即重启", "", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        ReStart();
                    }
                    else
                    {
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"配置工具配置信息失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "异常警告");
            }
        }

        /// <summary>
        /// 写注册表开机自启动 需要权限及windows版本对应,暂不考虑
        /// </summary>
        public void WriteRegister()
        {
            try
            {
                string strName = AppDomain.CurrentDomain.BaseDirectory + "InteractiveTool.exe";//获取要自动运行的应用程序名
                if (!System.IO.File.Exists(strName))//判断要自动运行的应用程序文件是否存在
                    return;
                string strnewName = strName.Substring(strName.LastIndexOf("\\") + 1);//获取应用程序文件名，不包括路径
                string RegeditPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\";
                RegistryKey hkml = Registry.LocalMachine;
                RegistryKey software = hkml.OpenSubKey(RegeditPath, RegistryKeyPermissionCheck.ReadWriteSubTree, System.Security.AccessControl.RegistryRights.FullControl);
                RegistryKey aimdir = software.OpenSubKey("Run", RegistryKeyPermissionCheck.ReadWriteSubTree, System.Security.AccessControl.RegistryRights.FullControl);
                aimdir.SetValue("AutoRun", strName, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                MessageBox.Show("写注册表异常 " + ex.Message + ex.StackTrace);
                throw ex;
            }
        }
    }
}
