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
using System.Runtime.InteropServices;
using System.Windows.Interop;
using NLog;
namespace InteractiveTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class InteractionToolWindow : Window
    {
        public static string IP;//读取配置文件IP
        public static string Port;//读取配置文件端口
        public static string Mac;//读取配置文件MAC
        public static string Url;
        public static string interactionId;//查询当前互动ID
        public static string curriculumName;//查询当前课程名
        public static bool showstatus = false;
        public static string MainDeviceId;//主讲设备ID
        public static Logger logger = LogManager.GetCurrentClassLogger();
        public static string redisconnectmessage;

        static double top = 0;
        static double left = 0;

        DispatcherTimer timer = null;
        DispatcherTimer hideTimer = null;

        ISubscriber sub;

        public InteractionToolWindow()
        {
            //OldLogDelete();
            IP = ReadConfig("ServerIp");
            Port = ReadConfig("ServerPort");
            Mac = ReadConfig("ServerMac");
            if (!ConfigEmpty())
            {
                MessageBox.Show("配置文件中存在相关配置缺失,请配置完成后重新启动", "警告");
                logger.Error("配置文件错误,需要重启");
                this.Close();
            }

            InitTimer();
            InitHideTimer();
            this.Visibility = Visibility.Hidden;
            RedisClient();
            //初始化时加入redis重连机制
            //while (!string.IsNullOrEmpty(redisconnectmessage))
            //{
            //    redisconnectmessage = string.Empty;
            //    RedisClient();
            //}

            InitializeComponent();//界面初始化
            //InitialTray();//托盘初始化
            HideToolView();
            this.Left = (0.5 * SystemParameters.WorkArea.Right) - 250;
            this.Top = SystemParameters.WorkArea.Bottom - 64 - 150;
            FindCurriculum();
            SelectLecture subView = new SelectLecture(IP, Port, Mac, interactionId);
            subView.Top = SystemParameters.WorkArea.Bottom - 64 - 160 - subView.Height;
        }

        //log文件检测,用于定期日志删除(目前定义为删除15之前的程序日志.保证内存空间)
        public void OldLogDelete()
        {
            string oldlogMonth = DateTime.Today.AddDays(-15).ToString("yyyy-MM");
            string oldlogdate = DateTime.Today.AddDays(-15).ToString("yyyy-MM-dd");

            if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "Logs"))
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + "Logs" + "\\" + oldlogMonth;
                if (Directory.Exists(path))
                {
                    if (File.Exists($"{path}\\{oldlogdate}.log"))
                    {
                        File.Delete($"{path}\\{oldlogdate}.log");
                    }

                    string[] logfiles = Directory.GetFiles(path);
                    //将大于15天以上的日志文件全部删除
                    for (int i = 0; i < logfiles.Length; i++)
                    {
                        string filecreatedate = logfiles[i].Replace(".log", "").Replace($"{path}\\", "");
                        if (DateTime.Compare(DateTime.Parse(filecreatedate), DateTime.Parse(oldlogdate)) < 0)
                        {
                            File.Delete($"{logfiles[i]}");
                        }
                    }
                    if (Directory.GetFiles(path).Length == 0)
                    {
                        Directory.Delete(path);
                    }
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
            try
            {
                ConfigurationOptions configOptions = new ConfigurationOptions
                {
                    EndPoints =
                {
                  { IP,int.Parse("6379") }
                },
                    KeepAlive = 180,      //发送信息以保持sockets在线的间隔时间
                    Password = "zonekeyredis@2019",   //密码
                    AllowAdmin = true,     //启用被认定为是有风险的一些命令
                    ConnectRetry = 10  //链接重试
                };
                logger.Info($"redis链接信息:IP:{IP} Port:6379 密码:zonekeyredis@2019");

                ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(configOptions);
                sub = redis.GetSubscriber();
                //给客户端推送有听讲申请互动
                sub.Subscribe("listen_apply_interaction_channel", (channel, message) =>
                {
                    logger.Info("redis客户端申请加入课堂");

                    string redisInteractionId = string.Empty;//互动ID
                    string redisDeviceId = string.Empty;//主讲设备ID
                    string redisListenerId = string.Empty;//听讲设备ID
                    string redisListenerName = string.Empty;//听讲设备名称

                    JObject redisMessage = JObject.Parse(message);
                    IEnumerable<JProperty> jProperties = redisMessage.Properties();
                    logger.Info($"频道listen_apply_interaction_channel推送消息\n 消息信息:{string.Join("/", jProperties)}");
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
                    if (!string.IsNullOrEmpty(redisListenerName) && redisDeviceId == MainDeviceId && redisInteractionId == interactionId)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            logger.Info($"有新设备申请加入互动,互动设备名称:{redisListenerName}");
                            Window tip = new Window();
                            bool istop = true;
                            if (TipWindowsExit() != null)
                            {
                                tip = TipWindowsExit();
                                istop = false;
                            }
                            TipTools joinclass = new TipTools(redisListenerName, IP, Port, redisInteractionId, redisListenerId);
                            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                            if (top == 0 && left == 0)
                            {
                                top = desktopWorkingArea.Bottom - 3 * joinclass.Height;
                                left = desktopWorkingArea.Right - joinclass.Width;
                            }
                            joinclass.Top = top;
                            joinclass.Left = left;
                            top = top + joinclass.Height;
                            joinclass.Topmost = istop;
                            if (istop == false)
                            {
                                tip.Topmost = true;
                            }
                            joinclass.Show();
                            if (top >= (desktopWorkingArea.Bottom))
                            {
                                top = desktopWorkingArea.Bottom - 3 * joinclass.Height;
                            }
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
                        logger.Info($"频道:interaction_end_channel推送消息\n 消息信息:{string.Join("/", jProperties)}");
                        for (int i = 0; i < messages.Length; i++)
                        {
                            if (messages[i].Name == "interactionId")
                            {
                                redisinteractionId = messages[i].Value.ToString();
                            }
                            if (messages[i].Name == "deviceId")
                            {
                                redisdeviceId = messages[i].Value.ToString();
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
                            if (classend == true && redisinteractionId == interactionId && redisdeviceId == MainDeviceId)
                            {
                                logger.Info("redis客户端发送结束信息,工具栏后台隐藏");
                                this.Visibility = Visibility.Hidden;
                                this.Hide();
                                StartTimer();
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Error("Redis客户端异常\n" + $"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                MessageBox.Show("Redis服务链接或推送信息异常\n" + $"异常信息;{ex.Message}\n 异常栈:{ex.StackTrace}");
                redisconnectmessage = ex.Message.ToString();
            }
        }

        //取消所有订阅
        public void UnSubAllChannel()
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
                MessageBox.Show("请检查配置文件 \n" + ex.Message + "\n" + ex.StackTrace, "异常信息");
                logger.Error($"配置文件读取错误\n 错误信息:{ex.Message}\n 错误栈:{ex.StackTrace}");
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
                        logger.Info($"Request:{url}/{param}");
                    }

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
                    logger.Info(string.Join(";", properties));
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
                logger.Error($"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                if (!string.IsNullOrEmpty(requesttime))
                {
                    logger.Error($"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                    MessageBox.Show($"请求异常!ts:{requesttime}" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                else
                {
                    logger.Error($"服务器未响应或请求异常!" + Environment.NewLine + "异常信息: " + ex.Message + Environment.NewLine);
                    //MessageBox.Show("服务器未响应或请求异常!" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                throw ex;
            }
        }

        /// <summary>
        /// 后台展示到前台时,重置所有按键状态
        /// </summary>
        public void AllSelectStatusCancel()
        {
            this.Dispatcher.Invoke(() =>
            {
                logger.Info("后台展示到前台重新初始化界面");
                slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                slienceBtTxt.Text = "全员静音";
                teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                teachingBtTxt.Foreground = Brushes.AliceBlue;
                discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                discussingBtTxt.Foreground = Brushes.AliceBlue;
                interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                interactionBtTxt.Foreground = Brushes.AliceBlue;
            });
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

                IssueRequest(url, string.Empty, "GET", ref data);
                if (data != null)
                {
                    StopTimer();
                    if (this.Visibility == Visibility.Hidden)
                    {
                        this.Visibility = Visibility.Visible;
                        var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                        this.Left = (0.5 * desktopWorkingArea.Right) - 250;
                        this.Top = desktopWorkingArea.Bottom - 64 - 150;
                        this.Show();
                        if (showstatus == false)
                        {
                            this.Visibility = Visibility.Visible;
                            this.Topmost = true;
                            ShowToolView();
                            AllSelectStatusCancel();
                        }
                    }
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
                        if (list[i].Name == "speakerDeviceId")
                        {
                            MainDeviceId = list[i].Value.ToString();
                        }
                    }
                    logger.Info($"开始上课\n" + $"课堂信息:{curriculumName},互动ID:{interactionId},主讲设备ID:{MainDeviceId}");
                }
                else
                {
                    if (SelectWindowsExit() != null)
                    {
                        SelectWindowsExit().Close();
                    }
                    if (this.Visibility == Visibility.Visible)
                    {
                        logger.Warn($"当前设备无课程信息,工具栏后台隐藏");
                        this.Visibility = Visibility.Hidden;
                        this.Hide();
                    }
                    StartTimer();
                }

            }
            catch (Exception ex)
            {
                logger.Error($"获取课堂信息失败\n 异常信息:{ex.Message}\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"获取课堂信息失败\n 异常信息:{ex.Message}\n 异常栈:{ex.StackTrace}", "异常信息");
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
                logger.Error("状态设置异常\n 异常信息:" + " " + ex.Message + "\n 异常栈:" + ex.StackTrace);
                MessageBox.Show("状态设置异常\n 异常信息:" + " " + ex.Message + "\n 异常栈:" + ex.StackTrace);
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
                logger.Info($"发送课堂结束请求\n URL:{url}\n param:{param}");
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
                showstatus = true;
                ToolView.Width = 584;
                ToolView.Height = 64;
                expanderbd.CornerRadius = new CornerRadius(6, 0, 0, 6);
                if (MainView.Visibility == Visibility.Hidden)
                {
                    ToolView.ColumnDefinitions.Add(show);//增加展示列
                    //后续可能根据课程信息添加不同的布局界面 newshowwindow.xaml等文件已创建,后续根据新制定的布局界面重构,按键功能导入
                    MainView.Visibility = Visibility.Visible;

                    ToolView.Children.Add(MainView);//添加展开栏
                    line.Visibility = Visibility.Visible;//分割线展示
                    expandergd.ColumnDefinitions.Add(spline);
                    expandergd.Children.Add(line);
                    expander_bg.Source = new BitmapImage(new Uri("pack://application:,,,/images/fold.png"));
                    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                    this.Left = (0.5 * desktopWorkingArea.Right) - 250;
                    MainTool.Top = desktopWorkingArea.Bottom - ToolView.Height - 150;
                    logger.Warn("工具栏展开");
                }
            });
        }
        public void HideToolView()
        {
            this.Dispatcher.Invoke(() =>
            {
                showstatus = false;
                if (SelectWindowsExit() != null)
                {
                    SelectWindowsExit().Close();
                }
                ToolView.Width = 68;
                ToolView.Height = 50;
                if (MainView.Visibility == Visibility.Visible)
                {
                    //后续可能根据课程信息选择不同的布局隐藏
                    MainView.Visibility = Visibility.Hidden;//隐藏功能栏布局
                    ToolView.Children.Remove(MainView);//remove展示布局
                    ToolView.ColumnDefinitions.Remove(show);//隐藏展示列
                    line.Visibility = Visibility.Hidden;//分割线隐藏
                    expandergd.Children.Remove(line);
                    expandergd.ColumnDefinitions.Remove(spline);
                    expanderbd.CornerRadius = new CornerRadius(25, 25, 0, 0);
                    expander_bg.Source = new BitmapImage(new Uri("pack://application:,,,/images/unfold.png"));
                    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                    MainTool.Top = desktopWorkingArea.Bottom - ToolView.Height;
                    logger.Warn("工具栏收起");
                    StopHideTimer();
                }
            });
        }

        public bool ShowOrHide()
        {
            logger.Info("点击展开或收起按键");
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
        //适配触摸屏
        private void teachingMode_TouchDown(object sender, TouchEventArgs e)
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
        /// 适配一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void discussingMode_TouchDown(object sender, TouchEventArgs e)
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

        public void AgreeInteractionModeSelect()
        {
            this.Dispatcher.Invoke(() =>
            {
                interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionSelect.png"));
                interactionBtTxt.Foreground = Brushes.DeepSkyBlue;
                teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                teachingBtTxt.Foreground = Brushes.AliceBlue;
                discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                discussingBtTxt.Foreground = Brushes.AliceBlue;
            });
        }


        /// <summary>
        /// 互动听讲按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionMode_Click(object sender, RoutedEventArgs e)
        {

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
                    SelectWindowsExit().ShowDialog();
                }
                else
                {
                    SelectLecture subView = new SelectLecture(IP, Port, Mac, interactionId);
                    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                    subView.Top = desktopWorkingArea.Bottom - subView.Height - 64 - 160;
                    subView.ShowDialog();
                }
            });
        }
        //适配触摸屏
        private void interactionMode_TouchDown(object sender, TouchEventArgs e)
        {
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
                    SelectWindowsExit().ShowDialog();
                }
                else
                {
                    SelectLecture subView = new SelectLecture(IP, Port, Mac, interactionId);
                    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                    subView.Top = desktopWorkingArea.Bottom - subView.Height - 64 - 160;
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
                    //teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    //teachingBtTxt.Foreground = Brushes.AliceBlue;
                    //discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    //discussingBtTxt.Foreground = Brushes.AliceBlue;
                    //interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                    //interactionBtTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                logger.Error($"全员静音或者取消全员静音失败\n" + $"异常信息:{ex.Message}" + $"异常栈:{ex.StackTrace}");
                MessageBox.Show("全员静音或取消静音请求失败\n" + $"异常信息:{ex.Message}");
            }
        }
        //适配触摸屏
        private void slienceMode_TouchDown(object sender, TouchEventArgs e)
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
                    //teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    //teachingBtTxt.Foreground = Brushes.AliceBlue;
                    //discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    //discussingBtTxt.Foreground = Brushes.AliceBlue;
                    //interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                    //interactionBtTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                logger.Error($"全员静音或者取消全员静音失败\n" + $"异常信息:{ex.Message}" + $"异常栈:{ex.StackTrace}");
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
                if (item is TipTools)
                    return item;
            }
            return null;
        }

        private void end_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(interactionId))
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                    {
                        Class_End();
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Error("课堂结束请求发送失败\n" + $"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                MessageBox.Show("课堂结束请求发送失败\n" + $"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
            }
            finally
            {
                logger.Warn("课堂结束按键点击,工具后台隐藏");
                if (SelectWindowsExit() != null)
                {
                    SelectWindowsExit().Close();
                }
                this.Visibility = Visibility.Hidden;
                this.Hide();
                StartTimer();
            }
        }

        //适配触摸屏
        private void end_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(interactionId))
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                    {
                        Class_End();
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Error("课堂结束请求发送失败\n" + $"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                MessageBox.Show("课堂结束请求发送失败\n" + $"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
            }
            finally
            {
                logger.Warn("课堂结束按键点击,工具后台隐藏");
                if (SelectWindowsExit() != null)
                {
                    SelectWindowsExit().Close();
                }
                this.Visibility = Visibility.Hidden;
                this.Hide();
                StartTimer();
            }
        }

        private void ToolView_MouseEnter(object sender, MouseEventArgs e)
        {
            StopHideTimer();
        }

        private void ToolView_MouseLeave(object sender, MouseEventArgs e)
        {
            StartHideTimer();
        }

        #region 托盘图标及图标事件
        private System.Windows.Forms.NotifyIcon notifyIcon = null;
        private void InitialTray()
        {

            //设置托盘的各个属性
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            //notifyIcon.BalloonTipText = "程序开始运行";
            //notifyIcon.Text = "托盘图标";
            notifyIcon.Icon = new System.Drawing.Icon(System.Windows.Forms.Application.StartupPath + "\\configtools.ico");
            notifyIcon.Visible = true;
            //notifyIcon.ShowBalloonTip(2000);//启动时托盘栏展示2s
            notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(notifyIcon_MouseClick);

            //设置菜单项,自定义菜单项无需求
            System.Windows.Forms.MenuItem menu1 = new System.Windows.Forms.MenuItem("菜单项1");
            System.Windows.Forms.MenuItem menu2 = new System.Windows.Forms.MenuItem("菜单项2");
            System.Windows.Forms.MenuItem menu = new System.Windows.Forms.MenuItem("菜单", new System.Windows.Forms.MenuItem[] { menu1, menu2 });

            //退出菜单项
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("退出");
            exit.Click += new EventHandler(exit_Click);

            //关联托盘控件
            System.Windows.Forms.MenuItem[] childen = new System.Windows.Forms.MenuItem[] {/* menu,*/ exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);

            //窗体状态改变时候触发
            this.StateChanged += new EventHandler(SysTray_StateChanged);
        }
        ///
        /// 窗体状态改变时候触发
        ///
        ///

        ///

        private void SysTray_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Visibility = Visibility.Hidden;
            }
        }

        ///
        /// 退出选项
        ///
        ///

        ///

        private void exit_Click(object sender, EventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定要关闭吗?",
                                               "退出",
                                                MessageBoxButton.YesNo,
                                                MessageBoxImage.Question,
                                                MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                notifyIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            }
        }

        ///
        /// 鼠标单击
        ///
        ///

        ///

        private void notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (this.Visibility == Visibility.Visible)
                {
                    this.Visibility = Visibility.Hidden;
                }
                else
                {
                    this.Visibility = Visibility.Visible;
                    this.Activate();
                }
            }
        }
        #endregion

        private void ToolView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();//窗口拖拽
        }


    }
}
