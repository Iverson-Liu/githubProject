using Newtonsoft.Json.Linq;
using NLog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using WebSocketSharp;

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
        public static bool showstatus = false;//工具界面是否展示
        public static string MainDeviceId;//主讲设备ID
        public static string currentListenerDeviceId;//当前设备ID

        public static NLog.Logger logger = LogManager.GetCurrentClassLogger();

        static double top = 0;
        static double left = 0;

        DispatcherTimer timer = null;//获取课表信息计时器
        DispatcherTimer hideTimer = null;//隐藏工具栏计时器

        public static string redisconnectmessage;
        ISubscriber sub;

        //听讲端界面
        public ListenClient listener = new ListenClient();

        //板书界面
        public NewShowWindow bBoardWriting = new NewShowWindow();

        /// <summary>
        /// 工具栏展示状态枚举
        /// </summary>
        public enum Status
        {
            None = 0,
            Speaker = 1,//主讲端无板书
            BBoardWriting = 2,//主讲端带板书
            ListenClient = 3,//听讲端申请互动
            InterListenClient = 4,//听讲端互动中
            //DiscussListenClient = 5
        }

        //默认空值,获取到课表信息后更新当前状态,课程结束置空
        public static Status showWindowStatus = Status.None;

        //获取驱动事件信息
        [DllImport("user32.dll")]
        public static extern uint GetMessageExtraInfo();

        private static TimeSpan touchTime;
        public TimeSpan aviodclick = new TimeSpan(0, 0, 1);


        public InteractionToolWindow()
        {
            listener.end.Click += end_Click;
            listener.end.TouchDown += end_TouchDown;
            listener.Visibility = Visibility.Hidden;

            bBoardWriting.end.Click += end_Click;
            bBoardWriting.end.TouchDown += end_TouchDown;
            bBoardWriting.Visibility = Visibility.Hidden;
            
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

            //取消redis订阅方式,改为websocket长链接
            //RedisClient();

            //初始化时加入redis重连机制
            //while (!string.IsNullOrEmpty(redisconnectmessage))
            //{
            //    redisconnectmessage = string.Empty;
            //    RedisClient();
            //}

            //界面初始化
            InitializeComponent();
            //InitialTray();//托盘初始化
            HideToolView();

            this.Left = (0.5 * SystemParameters.WorkArea.Right) - 250;
            this.Top = SystemParameters.WorkArea.Bottom - 64 - 150;
            Url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/findCurriculum?mac=" + Mac;
            logger.Info($"FindCurriculum Url:{Url}");
            this.Visibility = Visibility.Hidden;//新界面兼容时注释掉方便开发
            //新界面兼容时注释掉方便开发
            FindCurriculum();
            //SelectLecture subView = new SelectLecture(IP, Port, Mac, interactionId);
            //subView.Top = SystemParameters.WorkArea.Bottom - 64 - 160 - subView.Height;
            //subView.Show();
        }

        /// <summary>
        /// log文件检测,用于定期日志删除(目前定义为删除15-day之前的程序日志.保证内存空间)
        /// </summary>
        public void OldLogDelete()
        {
            try
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
            catch (Exception ex)
            {
                logger.Error($"过期日志删除方法失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 初始化收起工具栏定时
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

        /// <summary>
        /// 收起工具栏定时器方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void HideDataTime_Tick(object sender, EventArgs e)
        {
            HideToolView();
        }

        /// <summary>
        /// 收起工具栏计时器启动
        /// </summary>
        public void StartHideTimer()
        {
            if (hideTimer != null && hideTimer.IsEnabled == false)
            {
                hideTimer.Start();
            }
        }

        /// <summary>
        /// 收起工具栏计时器停止
        /// </summary>
        public void StopHideTimer()
        {
            if (hideTimer != null)
            {
                hideTimer.Stop();
            }
        }

        /// <summary>
        /// 初始化获取课堂信息计时器,时间间隔为5s
        /// </summary>
        private void InitTimer()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Tick += new EventHandler(DataTime_Tick);
                timer.Interval = TimeSpan.FromSeconds(5);
            }
        }

        /// <summary>
        /// 获取课堂信息计时器方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void DataTime_Tick(object sender, EventArgs e)
        {
            FindCurriculum();
        }
        /// <summary>
        /// 获取课堂信息计时器启动
        /// </summary>
        public void StartTimer()
        {
            if (timer != null && timer.IsEnabled == false)
            {
                timer.Start();
            }
        }
        /// <summary>
        /// 获取课堂信息计时器停止(停止条件,课堂信息不为空,即开始上课)
        /// </summary>
        public void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// 确定websocket消息的频道类型
        /// </summary>
        /// <param name="js">websocket返回json格式消息</param>
        /// <returns></returns>
        public string ThisMessageType(JProperty[] js)
        {
            try
            {
                for (int i = 0; i < js.Length; i++)
                {
                    if (js[i].Name != "messageFlag")
                    {
                        continue;
                    }
                    else
                    {
                        return js[i].Value.ToString();
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error($"WebSocket Client获取消息类型失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 根据websocket相关信息处理
        /// </summary>
        /// <param name="messagetype">消息类型</param>
        /// <param name="messages">json消息</param>
        public void DealWithwsMessage(string messagetype, JProperty[] messages)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (messagetype == "2")//获取设备角色
                    {
                        for (int i = 0; i < messages.Length; i++)
                        {
                            if (messages[i].Name == "interactionId")
                            {
                                if (messages[i].Value.ToString() != interactionId)
                                {
                                    return;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            if (messages[i].Name == "deviceId")
                            {
                                if (messages[i].Value.ToString() != currentListenerDeviceId)
                                {
                                    return;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            if (messages[i].Name == "role")//role角色信息 0主讲
                            {
                                switch (showWindowStatus)
                                {
                                    case Status.Speaker:
                                        break;
                                    case Status.BBoardWriting:
                                        break;
                                    case Status.ListenClient:
                                        if (messages[i].Value.ToString() == "1")//1听讲
                                        {
                                            showWindowStatus = Status.ListenClient;
                                            logger.Info($"当前设备{currentListenerDeviceId}收到WebSocket客户端的角色信息为{messages[i].Value.ToString()},界面为听讲端模式");
                                            listener.IsListenClient();
                                        }
                                        else if (messages[i].Value.ToString() == "2")//2互动听讲
                                        {
                                            showWindowStatus = Status.InterListenClient;
                                            logger.Info($"当前设备{currentListenerDeviceId}收到WebSocket客户端的角色信息为{messages[i].Value.ToString()},界面为互动听讲端模式");
                                            listener.IsInteracting();
                                        }
                                        break;
                                    case Status.InterListenClient:
                                        if (messages[i].Value.ToString() == "1")//1听讲
                                        {
                                            showWindowStatus = Status.ListenClient;
                                            logger.Info($"当前设备{currentListenerDeviceId}收到WebSocket客户端的角色信息为{messages[i].Value.ToString()},界面为听讲端模式");
                                            listener.IsListenClient();
                                        }
                                        else if (messages[i].Value.ToString() == "2")//2互动听讲
                                        {
                                            showWindowStatus = Status.InterListenClient;
                                            logger.Info($"当前设备{currentListenerDeviceId}收到WebSocket客户端的角色信息为{messages[i].Value.ToString()},界面为互动听讲端模式");
                                            listener.IsInteracting();
                                        }
                                        break;
                                }
                            }
                        }
                    }

                    else if (messagetype == "3")//通知主讲有听讲申请互动
                    {
                        if (showWindowStatus == Status.Speaker || showWindowStatus == Status.BBoardWriting)
                        {
                            string listenerId = string.Empty;
                            string listenerName = string.Empty;
                            string wsinteractionId = string.Empty;
                            string wsmaindeviceId = string.Empty;
                            for (int i = 0; i < messages.Length; i++)
                            {
                                if (messages[i].Name == "listenerId")//听讲设备ID
                                {
                                    listenerId = messages[i].Value.ToString();
                                }
                                if (messages[i].Name == "interactionId")//互动课程ID
                                {
                                    wsinteractionId = messages[i].Value.ToString();
                                    if (messages[i].Value.ToString() != interactionId)
                                    {
                                        logger.Error($"互动课程ID不一致,当前课程ID为:{interactionId},申请设备互动ID为:{messages[i].Value}");
                                        return;
                                    }
                                }
                                if (messages[i].Name == "listenerName")//听讲设备名
                                {
                                    listenerName = messages[i].Value.ToString();
                                }
                                if (messages[i].Name == "deviceId")//主讲设备ID
                                {
                                    wsmaindeviceId = messages[i].Value.ToString();
                                }
                            }
                            if (!string.IsNullOrEmpty(listenerName) && wsmaindeviceId == MainDeviceId && wsinteractionId == interactionId)
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    if (SelectWindowsExit() != null)
                                    {
                                        SelectWindowsExit().Close();
                                    }
                                    logger.Info($"有新设备申请加入互动,互动设备名称:{listenerName}");

                                    TipTools oldtip = null;
                                    bool istop = true;
                                    if (TipWindowsExit() != null)
                                    {
                                        oldtip = TipWindowsExit() as TipTools;
                                        istop = false;
                                    }
                                    if (oldtip != null)
                                    {
                                        if (oldtip.message.Text != listenerName + "申请互动")
                                        {
                                            TipTools joinclass = new TipTools(listenerName, IP, Port, wsinteractionId, listenerId);
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
                                                oldtip.Topmost = true;
                                            }
                                            joinclass.Show();
                                            if (top >= (desktopWorkingArea.Bottom))
                                            {
                                                top = desktopWorkingArea.Bottom - 3 * joinclass.Height;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        TipTools joinclass = new TipTools(listenerName, IP, Port, wsinteractionId, listenerId);
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
                                            oldtip.Topmost = true;
                                        }
                                        joinclass.Show();
                                        if (top >= (desktopWorkingArea.Bottom))
                                        {
                                            top = desktopWorkingArea.Bottom - 3 * joinclass.Height;
                                        }
                                    }
                                });
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    else if (messagetype == "4")//互动模式
                    {
                        for (int i = 0; i < messages.Length; i++)
                        {
                            if (messages[i].Name == "deviceId")
                            {
                                if (messages[i].Value.ToString() != currentListenerDeviceId)
                                {
                                    return;
                                }
                            }

                            if (messages[i].Name == "interMode")//获取当前互动模式 1授课 2互动 3讨论 4板书
                            {
                                switch (showWindowStatus)
                                {
                                    case Status.Speaker:
                                        break;
                                    case Status.BBoardWriting:
                                        break;
                                    case Status.ListenClient:
                                        if (messages[i].Value.ToString() == "2")
                                        {
                                            showWindowStatus = Status.InterListenClient;
                                            listener.IsInteracting();
                                        }
                                        else if (messages[i].Value.ToString() == "3")
                                        {
                                            showWindowStatus = Status.ListenClient;
                                            listener.IsDiscussListenClient();
                                        }
                                        else
                                        {
                                            showWindowStatus = Status.ListenClient;
                                            listener.IsListenClient();
                                        }
                                        break;
                                    case Status.InterListenClient:
                                        if (messages[i].Value.ToString() == "2")
                                        {
                                            showWindowStatus = Status.InterListenClient;
                                            listener.IsInteracting();
                                        }
                                        else if (messages[i].Value.ToString() == "3")
                                        {
                                            showWindowStatus = Status.ListenClient;
                                            listener.IsDiscussListenClient();
                                        }
                                        else
                                        {
                                            showWindowStatus = Status.ListenClient;
                                            listener.IsListenClient();
                                        }
                                        break;
                                }
                            }
                        }
                    }

                    else if (messagetype == "10") //结束课程
                    {
                        bool ifend = false;
                        for (int i = 0; i < messages.Length; i++)
                        {
                            if (messages[i].Name == "interactionId")
                            {
                                if (messages[i].Value.ToString() != interactionId)
                                {
                                    return;
                                }
                            }
                            if (messages[i].Name == "bStart")
                            {
                                if (messages[i].Value.ToString() == "0")
                                {
                                    ifend = true;
                                }
                            }
                            if (messages[i].Name == "deviceId")
                            {
                                if (messages[i].Value.ToString() == currentListenerDeviceId && ifend)
                                {
                                    logger.Info($"WebSocket客户端收到结束当前设备{messages[i].Value.ToString()}的结束课堂信息");
                                    if (SelectWindowsExit() != null)
                                    {
                                        SelectWindowsExit().Close();
                                    }
                                    //课程结束方法
                                    if (showstatus)
                                    {
                                        HideToolView();
                                    }
                                    this.Hide();
                                    this.Visibility = Visibility.Hidden;
                                    showWindowStatus = Status.None;
                                    WebSockectClientClose();
                                    StartTimer();
                                }
                            }
                        }
                    }
                    else if (messagetype == "23")//获取设备静音状态
                    {
                        if (showWindowStatus == Status.Speaker || showWindowStatus == Status.BBoardWriting)
                        {
                            if (SelectWindowsExit() != null)
                            {
                                SelectWindowsExit().Close();
                            }
                        }
                        else
                        {
                            for (int i = 0; i < messages.Length; i++)
                            {
                                if (messages[i].Name == "deviceId")
                                {
                                    if (messages[i].Value.ToString() != currentListenerDeviceId)
                                    {
                                        return;
                                    }
                                }
                                if (messages[i].Name == "muteStatus")
                                {
                                    if (messages[i].Value.ToString() == "0")//0 正常语音 1静音
                                    {
                                        listener.VoiceStatus();
                                    }
                                    else if (messages[i].Value.ToString() == "1")
                                    {
                                        listener.MuteStatus();
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error($"WebSocket Client处理信息时异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}\r\n.WebSocket信息类型:{messagetype},WebSocket信息:{messages}");
                throw ex;
            }
        }

        /// <summary>
        /// 通讯方式从redis通讯改为websocket通讯,消息回执websocket重构
        /// </summary>
        public WebSocket ws;
        public void WebSockectClient()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    //测试用先写死23000接口,后面Port开放,使用配置文件中的数据
                    ws = new WebSocket(@"ws://" + IP + ":" + Port + "/interactionPlatform/api/websocket/" + currentListenerDeviceId);

                    string url = @"ws://" + IP + ":" + Port + "/interactionPlatform/api/websocket/" + currentListenerDeviceId;
                    logger.Info($"Websocket Url:{url}");
                    // Set the WebSocket events.
                    ws.OnOpen += (sender, e) =>
                    {
                        logger.Info("WebSocket On Open");
                    };
                    ws.OnMessage += (sender, e) =>
                     {
                         var body = !e.IsPing ? e.Data : "A ping was received.";

                         logger.Info($"WebSockect Message:{body}");
                         if (body.ToString() == "start")
                         {
                             logger.Info("WebSocket Client 已连接");
                         }

                         else if (body.ToString() != "A ping was received." && body.ToString() != "start")
                         {
                             JObject redisMessage = JObject.Parse(body);
                             IEnumerable<JProperty> jProperties = redisMessage.Properties();
                             JProperty[] messages = jProperties.ToArray();
                             string messagetype = ThisMessageType(messages);
                             if (messagetype != string.Empty)
                             {
                                 DealWithwsMessage(messagetype, messages);
                             }
                         }
                     };

                    ws.OnError += (sender, e) =>
                    {
                        logger.Error("WebSockectError:" + e.Message.ToString());
                    };

                    ws.OnClose += (sender, e) =>
                    {
                        logger.Info($"WebSocket Close Code:{e.Code} Reason:{e.Reason}");
                        if (e.Code != 1005)
                        {
                            logger.Error($"WebSocket Client 异常中断,Code:{e.Code} Reason:{e.Reason}");
                            ws.Connect();//非主动断连导致的异常中断进行重连机制规避,重连期间存在潜在的websocket信息丢失风险
                        }
                    };


                    // Connect to the server.
                    ws.Connect();

                    // Connect to the server asynchronously.
                    //ws.ConnectAsync();
                });
            }
            catch (Exception ex)
            {
                logger.Error($"WebSocket Client链接或通讯异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// websocket客户端主动断连 主动关闭码1005
        /// </summary>
        public void WebSockectClientClose()
        {
            try
            {
                if (ws != null)
                {
                    logger.Info("WebSocket Client 主动断连");
                    ws.Close();
                    ws = null;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"WebSocket Client主动断连异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        #region redis客户端代码
        /// <summary>
        /// Redis客户端链接,订阅申请互动频道,课堂结束信息频道
        /// </summary>
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
                logger.Error($"Redis客户端异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"Redis服务链接或推送信息异常,异常信息;{ex.Message}.\r\n 异常栈:{ex.StackTrace}", "Redis客户端异常提醒");
                redisconnectmessage = ex.Message.ToString();
            }
        }
        #endregion

        /// <summary>
        /// Redis客户端取消所有频道的订阅
        /// </summary>
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
                logger.Error($"配置文件读取错误\r\n 异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show("请检查配置文件\r\n" + ex.Message + "\n" + ex.StackTrace, "异常信息");
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
                        logger.Info($"Http Post Request:{url}/{param}");
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
                logger.Error($"Http请求异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                if (!string.IsNullOrEmpty(requesttime))
                {
                    logger.Error($"Http返回异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                    MessageBox.Show($"请求异常!ts:{requesttime}" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                else
                {
                    logger.Error($"服务器未响应或请求异常!异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
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
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    switch (showWindowStatus)
                    {
                        case Status.None:
                            break;
                        case Status.Speaker:
                            logger.Info("主讲端无板书模式后台展示到前台重新初始化界面");
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                            slienceBtTxt.Text = "全员静音";
                            teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                            teachingBtTxt.Foreground = Brushes.AliceBlue;
                            discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                            discussingBtTxt.Foreground = Brushes.AliceBlue;
                            interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                            interactionBtTxt.Foreground = Brushes.AliceBlue;
                            break;
                        case Status.BBoardWriting:
                            bBoardWriting.AllSelectStatusCancel();
                            break;
                        case Status.ListenClient:
                            listener.AllSelectStatusCancel();
                            break;
                        case Status.InterListenClient:
                            listener.AllSelectStatusCancel();
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error($"工具栏界面初始化异常,工具栏状态:{showWindowStatus},异常信息:{ex.Message}.\r\n,异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 获取课堂信息Http请求
        /// </summary>
        public void FindCurriculum()
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/findCurriculum?mac=" + Mac;
                //logger.Info($"FindCurriculum Url:{url}");
                IssueRequest(url, string.Empty, "GET", ref data);
                if (data != null)
                {
                    StopTimer();
                    IEnumerable<JProperty> properties = data.Properties();
                    JProperty[] list = properties.ToArray();
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].Name == "interactionId")//互动ID
                        {
                            interactionId = list[i].Value.ToString();
                        }
                        if (list[i].Name == "curriculumName")//当前课程的课程名
                        {
                            curriculumName = list[i].Value.ToString();
                        }
                        if (list[i].Name == "speakerDeviceId")//主讲设备ID
                        {
                            MainDeviceId = list[i].Value.ToString();
                        }
                        if (list[i].Name == "deviceId")//当前设备ID
                        {
                            currentListenerDeviceId = list[i].Value.ToString();
                        }
                        if (list[i].Name == "deviceRole")//设备角色
                        {
                            if (list[i].Value.ToString() == "0")//0 主讲
                            {
                                showWindowStatus = Status.Speaker;
                            }
                            else if (list[i].Value.ToString() == "1")//1 听讲
                            {
                                showWindowStatus = Status.ListenClient;

                            }
                            else if (list[i].Value.ToString() == "2")//2 互动听讲
                            {
                                showWindowStatus = Status.InterListenClient;
                            }
                        }
                        if (list[i].Name == "blackboardMode")//当前角色是否带有板书模式
                        {
                            if (showWindowStatus == Status.Speaker && list[i].Value.ToString() == "1")
                            {
                                showWindowStatus = Status.BBoardWriting;
                            }
                        }
                    }
                    if (this.Visibility == Visibility.Hidden)
                    {
                        this.Visibility = Visibility.Visible;
                        var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                        this.Left = (0.5 * desktopWorkingArea.Right) - 250;
                        this.Top = desktopWorkingArea.Bottom - 64 - 150;
                        WebSockectClient();
                        if (showstatus == false)
                        {
                            this.Visibility = Visibility.Visible;
                            this.Topmost = true;
                            AllSelectStatusCancel();
                            ShowToolView();
                        }
                        this.IsEnabled = true;

                        this.Show();
                        ReTouch();
                        //if (this.CaptureMouse() != true)
                        //{
                        //    logger.Error($"强制获取鼠标失败");
                        //}

                        //this.Focusable = true;
                        //if (this.Focus() != true)
                        //{
                        //    logger.Error($"强制获取焦  失败");
                        //}

                    }
                    logger.Info($"开始上课\n" + $"课堂信息:{curriculumName},互动ID:{interactionId},主讲设备ID:{MainDeviceId},当前设备ID:{currentListenerDeviceId},展示状态:{showWindowStatus.ToString()}");

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
                logger.Error($"获取课堂信息失败\n 异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"获取课堂信息失败\n 异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}", "异常信息");
            }
        }

        /// <summary>
        /// 更改当前教学模式
        /// </summary>
        /// <param name="interMode">互动模式 1授课,2互动,3讨论 4板书</param>
        /// <param name="interactionId">互动ID(会议ID)</param>
        /// <returns></returns>
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
                logger.Error($"设置互动模式失败,互动状态为:{interMode},异常信息:{ex.Message}.\r\n异常栈{ex.StackTrace}");
                return false;
                throw ex;
            }
        }

        /// <summary>
        /// 全员静音Http请求
        /// </summary>
        /// <param name="ctrlMute">静音状态,1静音,0取消静音</param>
        /// <param name="bSpeaker"></param>
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

                logger.Info($"发送静音指令:{ctrlMute},1:静音，0:取消");
                IssueRequest(url, param, "POST", ref data);
            }
            catch (Exception ex)
            {
                logger.Error($"全员静音或取消静音请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 结束课堂Http请求
        /// </summary>
        public void Class_End()
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/over_class";
                string param = string.Empty;
                if (showWindowStatus == Status.Speaker || showWindowStatus == Status.BBoardWriting)
                {
                    param = @"{""interactionId""" + ":" + "\"" + interactionId.ToString() + "\"" + ","
                        + "\"" + "deviceId" + "\"" + ":" + "\"" + MainDeviceId + "\"" + "}";
                }
                else
                {
                    param = @"{""interactionId""" + ":" + "\"" + interactionId.ToString() + "\"" + ","
                       + "\"" + "deviceId" + "\"" + ":" + "\"" + currentListenerDeviceId + "\"" + "}";
                }
                logger.Info($"发送课堂结束请求\n URL:{url}\n param:{param}");
                //string param = @"{""interactionId""" + ":" + "\"" + "598076" + "\"" + ","
                //   + "\"" + "deviceId" + "\"" + ":" + "\"" + 35001 + "\"" + "}";
                IssueRequest(url, param, "POST", ref data);

            }
            catch (Exception ex)
            {
                logger.Error($"结束课堂请求失败,异常信息:{ex.Message}\r\n.异常栈:{ex.StackTrace}");
                throw ex;
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
                //触摸屏事件不响应
                uint extra = GetMessageExtraInfo();
                logger.Info($"检测信息");
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }
                logger.Info("收起或展开按键鼠标响应");
                ShowOrHide();
            }
            catch (Exception ex)
            {
                logger.Error($"收起或展开功能异常,异常信息:{ex.Message},异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 展开或收起按键触摸屏适配
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fold_touchDown(object sender, TouchEventArgs e)
        {
            try
            {
                //if (this.CaptureTouch(e.TouchDevice) != true)
                //{
                //    logger.Error($"获取触摸屏设备失败");
                //}

                logger.Info("展开收起按键触摸屏响应");
                ShowOrHide();
            }
            catch (Exception ex)
            {
                logger.Error($"收起或展开功能异常,异常信息:{ex.Message},异常栈:{ex.StackTrace}");
                MessageBox.Show($"展开或收起按键功能能异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 展开并加载工具栏方法
        /// </summary>
        public void ShowToolView()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    showstatus = true;
                    expanderbd.CornerRadius = new CornerRadius(6, 0, 0, 6);//上侧圆角变左侧圆角

                    ToolView.Height = 64;
                    switch (showWindowStatus)
                    {
                        case Status.Speaker:
                            logger.Info("展开无板书主讲端");
                            ToolView.Width = 586;
                            show.Width = new GridLength(518);
                            //根据工具栏模式展开不同的工具栏
                            if (MainView.Visibility == Visibility.Hidden)
                            {
                                ToolView.ColumnDefinitions.Add(show);//增加展示列
                                MainView.SetValue(Grid.ColumnProperty, 1);
                                MainView.Visibility = Visibility.Visible;
                                ToolView.Children.Add(MainView);//增添展开栏
                            }
                            break;
                        case Status.BBoardWriting:
                            logger.Info("展开板书主讲端");
                            ToolView.Width = 654;
                            show.Width = new GridLength(586);
                            if (bBoardWriting.Visibility == Visibility.Hidden)
                            {
                                //待添加新界面
                                ToolView.ColumnDefinitions.Add(show);
                                bBoardWriting.SetValue(Grid.ColumnProperty, 1);
                                bBoardWriting.Visibility = Visibility.Visible;
                                ToolView.Children.Add(bBoardWriting);
                            }
                            break;
                        case Status.ListenClient:
                            logger.Info("展开听讲端");
                            ToolView.Width = 654;
                            show.Width = new GridLength(586);
                            if (listener.Visibility == Visibility.Hidden)
                            {
                                ToolView.ColumnDefinitions.Add(show);
                                listener.SetValue(Grid.ColumnProperty, 1);
                                listener.Visibility = Visibility.Visible;
                                ToolView.Children.Add(listener);
                            }
                            break;
                        case Status.InterListenClient:
                            logger.Info("展开互动听讲端");
                            ToolView.Width = 654;
                            show.Width = new GridLength(586);
                            if (listener.Visibility == Visibility.Hidden)
                            {
                                ToolView.ColumnDefinitions.Add(show);
                                listener.SetValue(Grid.ColumnProperty, 1);
                                listener.Visibility = Visibility.Visible;
                                ToolView.Children.Add(listener);
                            }
                            break;

                    }
                    //展开符号变为收起符号并添加分隔符
                    line.Visibility = Visibility.Visible;//分割线展示
                    expandergd.ColumnDefinitions.Add(spline);
                    expandergd.Children.Add(line);
                    expander_bg.Source = new BitmapImage(new Uri("pack://application:,,,/images/fold.png"));
                    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                    this.Left = (0.5 * desktopWorkingArea.Right) - 250;
                    MainTool.Top = desktopWorkingArea.Bottom - ToolView.Height - 150;
                    logger.Warn("工具栏展开");
                });
            }
            catch (Exception ex)
            {
                logger.Error($"展示工具栏方法异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 收起并卸载工具栏部分
        /// </summary>
        public void HideToolView()
        {
            try
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
                    //根据工具栏模式展开不同的工具栏
                    switch (showWindowStatus)
                    {
                        case Status.None:
                            if (MainView.Visibility == Visibility.Visible)
                            {
                                //后续可能根据课程信息选择不同的布局隐藏
                                MainView.Visibility = Visibility.Hidden;//隐藏功能栏布局
                                ToolView.Children.Remove(MainView);//remove展示布局
                                ToolView.ColumnDefinitions.Remove(show);//隐藏展示列
                            }
                            break;
                        case Status.Speaker:
                            if (MainView.Visibility == Visibility.Visible)
                            {
                                //后续可能根据课程信息选择不同的布局隐藏
                                MainView.Visibility = Visibility.Hidden;//隐藏功能栏布局
                                ToolView.Children.Remove(MainView);//remove展示布局
                                ToolView.ColumnDefinitions.Remove(show);//隐藏展示列
                            }
                            break;
                        case Status.BBoardWriting:
                            if (bBoardWriting.Visibility == Visibility.Visible)
                            {
                                bBoardWriting.Visibility = Visibility.Hidden;
                                ToolView.Children.Remove(bBoardWriting);
                                ToolView.ColumnDefinitions.Remove(show);
                            }
                            break;
                        case Status.ListenClient:
                            if (listener.Visibility == Visibility.Visible)
                            {
                                listener.Visibility = Visibility.Hidden;
                                ToolView.Children.Remove(listener);
                                ToolView.ColumnDefinitions.Remove(show);
                            }
                            break;
                        case Status.InterListenClient:
                            if (listener.Visibility == Visibility.Visible)
                            {
                                listener.Visibility = Visibility.Hidden;
                                ToolView.Children.Remove(listener);
                                ToolView.ColumnDefinitions.Remove(show);
                            }
                            break;
                    }
                    line.Visibility = Visibility.Hidden;//分割线隐藏
                    expandergd.Children.Remove(line);
                    expandergd.ColumnDefinitions.Remove(spline);
                    expanderbd.CornerRadius = new CornerRadius(25, 25, 0, 0);//左侧圆角变上侧圆角
                    expander_bg.Source = new BitmapImage(new Uri("pack://application:,,,/images/unfold.png"));
                    var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                    MainTool.Top = desktopWorkingArea.Bottom - ToolView.Height;
                    logger.Warn("工具栏收起");
                    StopHideTimer();
                });
            }
            catch (Exception ex)
            {
                logger.Error($"工具栏收起方法异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 收起或展开按键
        /// </summary>
        /// <returns></returns>
        public bool ShowOrHide()
        {
            try
            {
                if (showstatus)
                {
                    logger.Info("点击收起按键");
                    HideToolView();
                    return false;
                }
                else
                {
                    logger.Info("点击展开按键");
                    ShowToolView();
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"收起或展开按键功能异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 授课模式按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void teachingMode_Click(object sender, RoutedEventArgs e)
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

                logger.Info("授课模式控件鼠标操作响应");
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
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
                });
            }
            catch (Exception ex)
            {
                logger.Error($"授课模式鼠标按键响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 授课模式适配教学一体机
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void teachingMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                //if (this.CaptureTouch(e.TouchDevice) != true)
                //{
                //    logger.Error($"获取触摸屏设备失败");
                //}

                logger.Info("授课模式控件触摸屏响应");
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
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
                });
            }
            catch (Exception ex)
            {
                logger.Error($"授课模式触摸屏申请失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"授课模式申请失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "授课模式请求异常提示");
            }
        }

        /// <summary>
        /// 讨论模式按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void discussingMode_Click(object sender, RoutedEventArgs e)
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

                logger.Info("讨论模式控件鼠标操作响应");
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
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
                });
            }
            catch (Exception ex)
            {
                logger.Error($"讨论模式按键请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 讨论模式适配一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void discussingMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                //if (this.CaptureTouch(e.TouchDevice) != true)
                //{
                //    logger.Error($"获取触摸屏设备失败");
                //}

                logger.Info("讨论模式控件触摸屏响应");
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
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
                });
            }
            catch (Exception ex)
            {
                logger.Error($"讨论模式触摸屏请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"讨论模式请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "讨论模式切换异常提醒");
            }
        }

        /// <summary>
        /// 互动听讲按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionMode_Click(object sender, RoutedEventArgs e)
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

                logger.Info("互动模式控件鼠标操作响应");
                this.Dispatcher.Invoke(() =>
                {
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionSelect.png"));
                    interactionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    teachingBtTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
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
            catch (Exception ex)
            {
                logger.Error($"互动模式请求失败,异常信息;{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 互动模式适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    //if (this.CaptureTouch(e.TouchDevice) != true)
                    //{
                    //    logger.Error($"获取触摸屏设备失败");
                    //}

                    logger.Info("触摸屏操作互动模式控件");
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionSelect.png"));
                    interactionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    teachingBtTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
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
            catch (Exception ex)
            {
                logger.Error($"互动模式请求失败,异常信息;{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"互动模式请求失败,异常信息;{ex.Message}.\r\n 异常栈:{ex.StackTrace}", "更改互动状态异常提醒");
            }
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
                if (touchTime != null)
                {
                    TimeSpan clickTime = DateTime.Now.TimeOfDay;
                    TimeSpan dif = clickTime - touchTime;
                    if ((dif.CompareTo(aviodclick)) < 0)
                    {
                        logger.Warn($"全员静音按键触摸屏操作升级为鼠标操作,时间戳功能规避");
                        return;
                    }
                }

                //触摸屏事件不响应
                uint extra = GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                logger.Info("全员静音或取消静音控件鼠标操作响应");
                this.Dispatcher.Invoke(() =>
                {
                    if (slienceBtTxt.Text == "全员静音")
                    {
                        if (!string.IsNullOrEmpty(interactionId))
                        {
                            Slience_All(1, 1);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slienceCancel.png"));
                            slienceBtTxt.Text = "取消静音";
                            logger.Info("按键状态变为取消静音");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(interactionId))
                        {
                            Slience_All(0, 1);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                            slienceBtTxt.Text = "全员静音";
                            logger.Info("按键状态变为全员静音");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error($"全员静音或者取消全员静音失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 全员静音模式适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void slienceMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                //if (this.CaptureTouch(e.TouchDevice) != true)
                //{
                //    logger.Error($"获取触摸屏设备失败");
                //}

                this.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    touchTime = DateTime.Now.TimeOfDay;
                    logger.Info($"静音控件触摸屏响应,touchtime:{touchTime}");
                    if (slienceBtTxt.Text == "全员静音")
                    {
                        if (!string.IsNullOrEmpty(interactionId))
                        {
                            Slience_All(1, 1);
                            Thread.Sleep(200);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slienceCancel.png"));
                            slienceBtTxt.Text = "取消静音";
                            logger.Info("按键状态变为取消静音");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(interactionId))
                        {
                            Slience_All(0, 1);
                            Thread.Sleep(200);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                            slienceBtTxt.Text = "全员静音";
                            logger.Info("按键状态变为全员静音");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error($"全员静音或者取消全员静音失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"全员静音或取消静音请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "全员静音按键异常提醒");
            }
        }

        /// <summary>
        /// 选择课堂互动窗口是否存在
        /// </summary>
        /// <returns></returns>
        public Window SelectWindowsExit()
        {
            try
            {
                foreach (Window item in Application.Current.Windows)
                {
                    if (item is SelectLecture)
                        return item;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.Error($"选择互动教室窗口检测失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 提示窗口是否存在
        /// </summary>
        /// <returns></returns>
        public Window TipWindowsExit()
        {
            try
            {
                foreach (Window item in Application.Current.Windows)
                {
                    if (item is TipTools)
                        return item;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.Error($"申请互动教室提示窗口检测失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 结束课堂按键,根据听讲端主讲端有无板书模式区分
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void end_Click(object sender, RoutedEventArgs e)
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

                if (!string.IsNullOrEmpty(interactionId))
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                    {
                        logger.Info("结束课堂控件鼠标点击响应");
                        Class_End();
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Error($"课堂结束请求发送失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
            finally
            {
                logger.Warn("课堂结束按键点击,工具后台隐藏");
                if (SelectWindowsExit() != null)
                {
                    SelectWindowsExit().Close();
                }
                if (showstatus)
                {
                    HideToolView();
                }
                showWindowStatus = Status.None;
                WebSockectClientClose();
                this.Visibility = Visibility.Hidden;
                this.Hide();
                StartTimer();
            }
        }

        /// <summary>
        /// 结束互动教学一体机触摸屏适配
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void end_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                //if (this.CaptureTouch(e.TouchDevice) != true)
                //{
                //    logger.Error($"获取触摸屏设备失败");
                //}

                if (!string.IsNullOrEmpty(interactionId))
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                    {
                        logger.Info("结束课堂按钮触摸屏响应");
                        Class_End();
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Error($"课堂结束请求发送失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"课堂结束请求发送失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "结束课堂异常信息提醒");
            }
            finally
            {
                logger.Warn("课堂结束按键触摸屏响应,工具后台隐藏");
                if (SelectWindowsExit() != null)
                {
                    SelectWindowsExit().Close();
                }
                if (showstatus)
                {
                    HideToolView();
                }
                showWindowStatus = Status.None;
                WebSockectClientClose();
                this.Visibility = Visibility.Hidden;
                this.Hide();
                StartTimer();
            }
        }

        /// <summary>
        /// 光标在工具栏上,停止定时收起功能
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// 鼠标窗口拖动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToolView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();//窗口拖拽
        }

        /// <summary>
        /// 获取逻辑触控设备
        /// </summary>
        /// <returns></returns>
        private object GetStylusLogic()
        {
            TabletDeviceCollection devices = System.Windows.Input.Tablet.TabletDevices;

            if (devices.Count > 0)
            {
                // Get the Type of InputManager.获取输入设备管理器
                Type inputManagerType = typeof(System.Windows.Input.InputManager);

                // Call the StylusLogic method on the InputManager.Current instance.
                object stylusLogic = inputManagerType.InvokeMember("StylusLogic",
                    BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, InputManager.Current, null);

                return stylusLogic;
            }

            return null;
        }


        /// <summary>
        /// 注销触摸后重新注册
        /// </summary>
        public void ReTouch()
        {
            try
            {
                object stylusLogic = GetStylusLogic();

                if (stylusLogic == null)
                {
                    return;
                }

                Type inputManagerType = typeof(System.Windows.Input.InputManager);
                var wispLogicType = inputManagerType.Assembly.GetType("System.Windows.Input.StylusWisp.WispLogic");

                var windowInteropHelper = new WindowInteropHelper(this);
                var hwndSource = HwndSource.FromHwnd(windowInteropHelper.Handle);

                var unRegisterHwndForInputMethodInfo = wispLogicType.GetMethod("UnRegisterHwndForInput",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                unRegisterHwndForInputMethodInfo.Invoke(stylusLogic, new object[] { hwndSource });


                var registerHwndForInputMethodInfo = wispLogicType.GetMethod("RegisterHwndForInput",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                registerHwndForInputMethodInfo.Invoke(stylusLogic, new object[]
                {
                InputManager.Current,
                PresentationSource.FromVisual(this)
                });
            }
            catch (Exception ex)
            {
                logger.Error($"注销触摸屏重新注册失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }
    }
}
