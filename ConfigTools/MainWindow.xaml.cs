using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Xml;

namespace ConfigTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            //初始化
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "config.xml"))
            {
                CreateXml();
            }
            if (string.IsNullOrEmpty(IP.Text) || string.IsNullOrEmpty(Port.Text) || string.IsNullOrEmpty(Mac.Text))
            {
                MessageBox.Show("有配置项为空,禁止写入");
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
                MessageBox.Show("创建XML异常 " + ex.Message + ex.StackTrace);
                throw;
            }
        }

        public void CreateNode(XmlDocument xmldoc, XmlNode parentNode, string name, string value)
        {
            XmlNode node = xmldoc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value;
            parentNode.AppendChild(node);
        }

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
                MessageBox.Show("写XML异常: " + ex.Message + ex.StackTrace);
                throw;
            }

        }
        public string ReadXml(string node)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(AppDomain.CurrentDomain.BaseDirectory + "config.xml");
                XmlElement element = (XmlElement)xmlDoc.SelectSingleNode($"Configs/{node}");
                string value = element.InnerText;

                return value;
            }
            catch (Exception ex)
            {
                MessageBox.Show("读XML异常: " + ex.Message + ex.StackTrace);
                throw;
            }
        }

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

        private void ConfigView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
                throw;
            }

        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
