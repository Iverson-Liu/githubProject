using System;
using System.Xml;
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
using System.Net;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace InteractiveTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        public static string IP;//读取配置文件IP
        public static string Port;//读取配置文件端口
        public static string Mac;//读取配置文件MAC
        public static string Url;
        public static string interactionId;//查询当前互动ID
        public static string curriculumName;//查询当前课程名
        public static string MainDeviceId;
        static double top = 720;
        static double left = 1580;
        DispatcherTimer timer = null;
        DispatcherTimer hideTimer = null;

        ISubscriber sub;

        public MainWindow()
        {

            IP = ReadConfig("ServerIp");
            Port = ReadConfig("ServerPort");
            Mac = ReadConfig("ServerMac");
            if (!ConfigEmpty())
            {
                MessageBox.Show("配置文件中存在相关配置缺失", "警告");
                this.Close();
            }
            InitTimer();
            InitHideTimer();

            FindCurriculum();
            RedisClient();
            InitializeComponent();
            SelectLecture subView = new SelectLecture(IP, Port, Mac, interactionId);
        }

        public void ProcessOnly()
        {
            Process[] processes = Process.GetProcesses();
            for (int i = 0; i < processes.Length; i++)
            {
                if (processes[i].ProcessName == "InteractiveTool")
                {
                    return;
                }
            }
        }
        /// <summary>
        /// 收起工具栏定时
        /// </summary>
        private void InitHideTimer()
        {
            if (hideTimer == null)
            {
                hideTimer = new DispatcherTimer();
                hideTimer.Tick += new EventHandler(HideDataTime_Tick);
                hideTimer.Interval = TimeSpan.FromSeconds(30);
            }
        }
        public void HideDataTime_Tick(object sender, EventArgs e)
        {
            HideToolView();
        }

        public void StartHideTimer()
        {
            if (hideTimer != null && hideTimer.IsEnabled == false)
            {
                hideTimer.Start();
            }
        }
        public void StopHideTimer()
        {
            if (hideTimer != null)
            {
                hideTimer.Stop();
            }
        }

        private void InitTimer()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Tick += new EventHandler(DataTime_Tick);
                timer.Interval = TimeSpan.FromSeconds(5);
            }
        }
        public void DataTime_Tick(object sender, EventArgs e)
        {
            FindCurriculum();
        }
        public void StartTimer()
        {
            if (timer != null && timer.IsEnabled == false)
            {
                timer.Start();
            }
        }
        public void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }

        public void RedisClient()
        {
            ConfigurationOptions configOptions = new ConfigurationOptions
            {
                EndPoints =
                {
                  { IP,int.Parse("6379") }
                },
                KeepAlive = 180,      //发送信息以保持sockets在线的间隔时间
                Password = "zonekeyredis@2019",   //密码
                AllowAdmin = true     //启用被认定为是有风险的一些命令
            };
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(configOptions);
            sub = redis.GetSubscriber();
            //给客户端推送有听讲申请互动

            sub.Subscribe("listen_apply_interaction_channel", (channel, message) =>
            {
                string redisInteractionId = string.Empty;
                string redisDeviceId = string.Empty;//主讲设备ID
                string redisListenerId = string.Empty;//听讲设备ID
                string redisListenerName = string.Empty;//听讲设备名称

                JObject redisMessage = JObject.Parse(message);
                IEnumerable<JProperty> jProperties = redisMessage.Properties();
                JProperty[] messages = jProperties.ToArray();
                for (int i = 0; i < messages.Length; i++)
                {
                    if (messages[i].Name == "listenerId")
                    {
                        redisListenerId = messages[i].Value.ToString();
                    }
                    if (messages[i].Name == "interactionId")
                    {
                        redisInteractionId = messages[i].Value.ToString();
                    }
                    if (messages[i].Name == "listenerName")
                    {
                        redisListenerName = messages[i].Value.ToString();
                    }
                    if (messages[i].Name == "deviceId")
                    {
                        redisDeviceId = messages[i].Value.ToString();
                    }
                }
                //if (!string.IsNullOrEmpty(redisInteractionId))
                //{
                //    this.Show();
                //}
                if (!string.IsNullOrEmpty(redisListenerName))
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        Tips joinclass = new Tips(redisListenerName, IP, Port, redisInteractionId, redisListenerId);
                        joinclass.Top = top;
                        joinclass.Left = left;
                        top = top - 30;
                        left = left - 30;
                        joinclass.Show();
                    });
                }
            });
            bool subsuccess = sub.IsConnected("listen_apply_interaction_channel");
            if (subsuccess)
            {
                sub.Subscribe("interaction_end_channel", (channel, message) =>
                {
                    string redisinteractionId = string.Empty;
                    string redisdeviceId = string.Empty;//主讲设备Id
                    bool classend = false;
                    JObject redisMessage = JObject.Parse(message);
                    IEnumerable<JProperty> jProperties = redisMessage.Properties();
                    JProperty[] messages = jProperties.ToArray();
                    for (int i = 0; i < messages.Length; i++)
                    {
                        if (messages[i].Name == "interactionId")
                        {
                            redisinteractionId = messages[i].Value.ToString();
                        }
                        if (messages[i].Name == "bStart")
                        {
                            if (messages[i].Value.ToString() == "0")
                            {
                                classend = true;
                            }
                            else
                            {
                                classend = false;
                            }
                        }
                    }
                    this.Dispatcher.Invoke(() =>
                    {
                        if (classend == true && redisinteractionId == interactionId)
                        {
                            this.Hide();
                            timer.Start();
                        }
                    });
                });
            }
        }

        public void UnSub()
        {
            sub.UnsubscribeAll();
        }
        public bool ConfigEmpty()
        {
            if (string.IsNullOrEmpty(IP) || string.IsNullOrEmpty(Port) || string.IsNullOrEmpty(Mac))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 读取配置文件
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public string ReadConfig(string node)
        {
            try
            {
                XmlDocument configfile = new XmlDocument();
                configfile.Load(AppDomain.CurrentDomain.BaseDirectory + "config.xml");
                XmlElement element = (XmlElement)configfile.SelectSingleNode($"Configs/{node}");
                string value = element.InnerText;
                return value;
            }
            catch (Exception ex)
            {
                MessageBox.Show("异常信息" + "请检查配置文件 " + ex.Message + " " + ex.StackTrace);
                throw ex;
            }
        }

        /// <summary>
        /// http请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="method"></param>
        /// <param name="unprocessedValue"></param>
        public void IssueRequest(string url, string param, string method, ref JObject unprocessedValue)
        {
            string requesttime = string.Empty;
            JObject result = new JObject();
            string exMessage = string.Empty;
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    string context = string.Empty;
                    WebRequest request = WebRequest.Create(url);
                    request.Method = method;

                    if (method.Equals(WebRequestMethods.Http.Post))
                    {
                        request.ContentType = "application/json; charset=utf-8";
                        StreamWriter strStream = new StreamWriter(request.GetRequestStream());
                        strStream.Write(param);
                        strStream.Flush();
                        strStream.Close();
                    }

                    HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        var message = String.Format("Request failed. Received HTTP {0}", response.StatusCode);
                        throw new ApplicationException(message);
                    }

                    //接收响应主体信息
                    Stream stream = response.GetResponseStream();
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            context = reader.ReadToEnd();
                        }
                    }
                    else
                    {
                        throw new Exception("ResponesStream为空值");
                    }
                    JObject json = JObject.Parse(context);

                    //得到json对应的propertyies，实际是一个<key,value>
                    //对象组成的数组，可以遍历和获得value的值

                    IEnumerable<JProperty> properties = json.Properties();
                    JProperty[] list = properties.ToArray();
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].Name == "code")
                        {
                            if (list[i].Value.ToString() != "0")
                            {
                                exMessage += $"code:{list[i].Value.ToString()}\n";
                            }
                        }
                        if (list[i].Name == "msg")
                        {
                            if (list[i].Value.ToString() != "成功")
                            {
                                exMessage += $"msg:{list[i].Value.ToString()}\n";
                            }
                        }
                        if (list[i].Name == "data")
                        {
                            if (list[i].Value.HasValues)
                            {
                                string datastr;
                                if (list[i].Value.ToString().Contains("[") || list[i].Value.ToString().Contains("]"))
                                {
                                    datastr = list[i].Value.ToString().Replace("[", "").Replace("]", "");
                                }
                                else
                                {
                                    datastr = list[i].Value.ToString();
                                }
                                result = JObject.Parse(datastr);
                            }
                        }
                        if (list[i].Name == "ts")
                        {
                            requesttime = list[i].Value.ToString();
                            if (!string.IsNullOrEmpty(exMessage))
                            {
                                throw new Exception(exMessage);
                            }
                        }
                    }
                    if (result.Count == 0)
                    {
                        result = null;
                    }
                });
                unprocessedValue = result;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(requesttime))
                {
                    MessageBox.Show($"请求异常!ts:{requesttime}" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                else
                {
                    MessageBox.Show("请求异常!" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                throw ex;
            }
        }


        /// <summary>
        /// 查询当前课表
        /// </summary>
        public void FindCurriculum()
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/findCurriculum?mac=" + Mac;
                //string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/findCurriculum?mac="+"92-0F-0C-2E-C6-39";

                IssueRequest(url, string.Empty, "GET", ref data);
                if (data != null)
                {
                    if (this.Visibility == Visibility.Hidden)
                    {
                        this.Visibility = Visibility.Visible;
                        this.Show();
                    }
                    StopTimer();
                    IEnumerable<JProperty> properties = data.Properties();
                    JProperty[] list = properties.ToArray();
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].Name == "interactionId")
                        {
                            interactionId = list[i].Value.ToString();
                        }
                        if (list[i].Name == "curriculumName")
                        {
                            curriculumName = list[i].Value.ToString();
                        }
                    }
                }
                else
                {
                    if (this.Visibility == Visibility.Visible)
                    {
                        this.Visibility = Visibility.Hidden;
                        this.Hide();
                    }
                    StartTimer();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "异常信息");
                this.Close();
            }
        }

        public bool Update_interaction_info(int interMode, string interactionId)
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/update_interaction_info";
                string param = @"{""interMode""" + ":" + interMode.ToString() + "," + "\"" + "interactionId" + "\"" + ":" + "\"" + interactionId + "\"" + "}";
                IssueRequest(url, param, "POST", ref data);
                return true;

            }
            catch (Exception ex)
            {
                MessageBox.Show("服务未启动或服务数据有问题" + " " + ex.Message + " " + ex.StackTrace);
                return false;
            }
        }

        public void Slience_All(int ctrlMute, int bSpeaker)
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/ctrl_interaction_mute";
                string param = @"{""interactionId""" + ":" + "\"" + interactionId.ToString() + "\"" + ","
                    + "\"" + "deviceId" + "\"" + ":" + "\"" + MainDeviceId + "\"" + ","
                    + "\"" + "ctrlMute" + "\"" + ":" + ctrlMute.ToString() + ","
                    + "\"" + "bSpeaker" + "\"" + ":" + bSpeaker.ToString() + "}";

                //string param = @"{""interactionId""" + ":" + "\"" + "598076" + "\"" + ","
                //    + "\"" + "deviceId" + "\"" + ":" + "\"" + "35001" + "\"" + ","
                //    + "\"" + "ctrlMute" + "\"" + ":" + ctrlMute.ToString() + ","
                //    + "\"" + "bSpeaker" + "\"" + ":" + bSpeaker.ToString() + "}";

                IssueRequest(url, param, "POST", ref data);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Class_End()
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/over_class";
                string param = @"{""interactionId""" + ":" + "\"" + interactionId.ToString() + "\"" + ","
                    + "\"" + "deviceId" + "\"" + ":" + "\"" + MainDeviceId + "\"" + "}";

                //string param = @"{""interactionId""" + ":" + "\"" + "598076" + "\"" + ","
                //   + "\"" + "deviceId" + "\"" + ":" + "\"" + 35001 + "\"" + "}";
                IssueRequest(url, param, "POST", ref data);

            }
            catch (Exception ex)
            {
                MessageBox.Show("结束互动请求失败/r/n" + "异常信息:" + ex.Message + "/r/n 异常栈" + ex.StackTrace);
            }
        }

        public void ShowToolView()
        {
            this.Dispatcher.Invoke(() =>
            {
                ToolView.Width = 750;
                MainView.Visibility = Visibility.Visible;
                ToolView.ColumnDefinitions.Add(show);
                ToolView.Children.Add(MainView);
                expander.CornerRadius = new CornerRadius(0, 0, 0, 0);
                expander_bg.Source = new BitmapImage(new Uri("pack://application:,,,/images/fold.png"));
                MainTool.Top = 880;
            });
        }
        public void HideToolView()
        {
            this.Dispatcher.Invoke(() =>
            {
                MainView.Visibility = Visibility.Hidden;
                ToolView.Children.Remove(MainView);
                ToolView.ColumnDefinitions.Remove(show);
                ToolView.Width = 80;
                expander.CornerRadius = new CornerRadius(30, 30, 0, 0);
                expander_bg.Source = new BitmapImage(new Uri("pack://application:,,,/images/unfold.png"));
                MainTool.Top = 980;
                StopHideTimer();
            });
        }
        public bool ShowOrHide()
        {

            if (MainView.Visibility == Visibility.Visible)
            {
                HideToolView();
                return false;
            }
            else
            {
                ShowToolView();
                return true;
            }
        }

        /// <summary>
        /// 展开折叠按钮处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fold_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                ShowOrHide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("异常", ex.Message + " " + ex.StackTrace);
            }

        }

        /// <summary>
        /// 授课模式按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void teachingMode_Click(object sender, RoutedEventArgs e)
        {
            int interMode = 1;
            bool result = Update_interaction_info(interMode, interactionId);
            if (!result)
            {
                MessageBox.Show("授课模式请求失败", "提示");
            }
            this.Dispatcher.Invoke(() =>
            {
                teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingSelect.png"));
                teachingBtTxt.Foreground = Brushes.DeepSkyBlue;
                discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                discussingBtTxt.Foreground = Brushes.AliceBlue;
                interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                interactionBtTxt.Foreground = Brushes.AliceBlue;
            });
        }

        /// <summary>
        /// 讨论模式按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void discussingMode_Click(object sender, RoutedEventArgs e)
        {
            int interMode = 3;
            bool result = Update_interaction_info(interMode, interactionId);
            if (!result)
            {
                MessageBox.Show("讨论模式请求失败", "提示");
            }
            this.Dispatcher.Invoke(() =>
            {
                discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingSelect.png"));
                discussingBtTxt.Foreground = Brushes.DeepSkyBlue;
                teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                teachingBtTxt.Foreground = Brushes.AliceBlue;
                interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                interactionBtTxt.Foreground = Brushes.AliceBlue;
            });
        }

        /// <summary>
        /// 互动听讲按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionMode_Click(object sender, RoutedEventArgs e)
        {
            int interMode = 2;
            bool result = Update_interaction_info(interMode, interactionId);
            if (!result)
            {
                MessageBox.Show("互动听讲请求失败", "提示");
            }
            this.Dispatcher.Invoke(() =>
            {
                interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionSelect.png"));
                interactionBtTxt.Foreground = Brushes.DeepSkyBlue;
                teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                teachingBtTxt.Foreground = Brushes.AliceBlue;
                discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                discussingBtTxt.Foreground = Brushes.AliceBlue;
                if (SelectWindowsExit() != null)
                {
                    SelectWindowsExit().Show();
                }
                else
                {
                    SelectLecture subView = new SelectLecture(IP, Port, Mac, interactionId);
                    subView.ShowDialog();
                }
            });
        }

        /// <summary>
        /// 静音按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void slienceMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (slienceBtTxt.Text == "全员静音")
                    {
                        if (!string.IsNullOrEmpty(interactionId))
                        {
                            Slience_All(1, 1);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slienceCancel.png"));
                            slienceBtTxt.Text = "取消静音";
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(interactionId))
                        {
                            Slience_All(0, 1);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                            slienceBtTxt.Text = "全员静音";
                        }
                    }
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    teachingBtTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                    interactionBtTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("全员静音或取消静音请求失败\n" + $"异常信息:{ex.Message}");
            }
        }

        /// <summary>
        /// 下拉窗口是否存在
        /// </summary>
        /// <returns></returns>
        public Window SelectWindowsExit()
        {
            foreach (Window item in Application.Current.Windows)
            {

                if (item is SelectLecture)

                    return item;
            }
            return null;
        }
        /// <summary>
        /// 提示窗口是否存在
        /// </summary>
        /// <returns></returns>
        public Window TipWindowsExit()
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item is Tips)
                    return item;
            }
            return null;
        }

        private void end_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(interactionId))
            {
                Class_End();
            }
            if (SelectWindowsExit() != null)
            {
                SelectWindowsExit().Close();
            }
            this.Hide();
            StartTimer();
        }

        private void ToolView_MouseEnter(object sender, MouseEventArgs e)
        {
            StopHideTimer();
        }

        private void ToolView_MouseLeave(object sender, MouseEventArgs e)
        {
            StartHideTimer();
        }

        private void ToolView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
