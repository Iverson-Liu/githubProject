using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace InteractiveTool
{
    /// <summary>
    /// Tips.xaml 的交互逻辑
    /// </summary>
    public partial class TipTools : Window
    {
        string IP;
        string Port;
        string InteractionId;
        string DeviceId;
        static List<string> InteractiveDeviceId = new List<string>();//根据获取设备信息接口,动态获取正在互动中的设备
        DispatcherTimer timer = null;

        //获取驱动事件信息
        [DllImport("user32.dll")]
        private static extern uint GetMessageExtraInfo();

        ///还有一种写法,构造函数传入主窗口对象,主窗口new的时候传入this
        public TipTools(string Message, string ip, string port, string interactionId, string deviceId)
        {
            if (InteractiveDeviceId == null)
            {
                InteractiveDeviceId = new List<string>();
            }
            if (SelectWindowsExit() != null)
            {
                SelectWindowsExit().Close();
            }
            this.IP = ip;
            this.Port = port;
            this.InteractionId = interactionId;
            this.DeviceId = deviceId;
            this.Closing += Tips_Closing;
            InitializeComponent();

            message.Text = Message + "申请互动";
            InitTimer();
            StartTimer();
        }

        private void InitTimer()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Tick += new EventHandler(DataTime_Tick);
                timer.Interval = TimeSpan.FromSeconds(15);
            }
        }

        public void DataTime_Tick(object sender, EventArgs e)
        {
            InteractionToolWindow.logger.Info($"15s未处理{message.Text}请求");
            this.Close();
        }

        public void StartTimer()
        {
            if (timer != null && timer.IsEnabled == false)
            {
                timer.Start();
            }
        }

        /// <summary>
        /// 选择听讲端教书子窗口界面检测,获取窗口句柄
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
                InteractionToolWindow.logger.Error($"申请互动提示窗口检测选择听讲端教室窗口失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 获取主界面窗口句柄
        /// </summary>
        /// <returns></returns>
        public Window InteractionWindowsExit()
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item is InteractionToolWindow)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// 发起Http请求方法
        /// </summary>
        /// <param name="url">http请求URL</param>
        /// <param name="param">json格式参数</param>
        /// <param name="method">请求方法</param>
        /// <param name="unprocessedValue">未处理json数据</param>
        /// <param name="unprocessArray">未处理json格式 设备信息数组</param>
        public void IssueRequest(string url, string param, string method, ref JObject unprocessedValue, ref JArray unprocessArray)
        {
            string requesttime = string.Empty;
            JObject jvalue = new JObject();
            JArray jarr = new JArray();
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
                        InteractionToolWindow.logger.Info($"Http Request Url:{url} Param:{param}");
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
                                exMessage += $"code:{list[i].Value.ToString()}\r\n";
                            }
                        }
                        if (list[i].Name == "msg")
                        {
                            if (list[i].Value.ToString() != "成功")
                            {
                                exMessage += $"msg:{list[i].Value.ToString()}";
                            }
                        }
                        if (list[i].Name == "data")
                        {
                            if (list[i].Value.HasValues)
                            {
                                if (list[i].Value.ToString().Contains("[") || list[i].Value.ToString().Contains("]"))
                                {
                                    jarr = JArray.Parse(list[i].Value.ToString());
                                }
                                else
                                {
                                    jvalue = JObject.Parse(list[i].Value.ToString());
                                }
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
                    if (jvalue.Count == 0)
                    {
                        jvalue = null;
                    }
                    if (jarr.Count == 0)
                    {
                        jarr = null;
                    }
                });
                unprocessArray = jarr;
                unprocessedValue = jvalue;
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"申请加入课堂请求异常\n 异常信息;{ex.Message}\n 异常栈:{ex.StackTrace}");
                if (string.IsNullOrEmpty(requesttime))
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
        /// 获取当前会议设备信息,包括设备名称,设备ID,设备角色等
        /// </summary>
        public void Get_device_info()
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();

                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/devices_info?interactionId=" + InteractionId;
                IssueRequest(url, string.Empty, "GET", ref data, ref array);
                InteractionToolWindow.logger.Info($"Request:{url}");
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        JToken jToken = array[i].ToString();
                        string context = jToken.ToString();
                        JObject datas = JObject.Parse(context);
                        IEnumerable<JProperty> properties = datas.Properties();
                        JProperty[] list = properties.ToArray();
                        bool ifInteractive = false;
                        for (int t = 0; t < list.Length; t++)
                        {
                            if (list[t].Name == "role")
                            {
                                if (list[t].Value.ToString() == "2")
                                {
                                    ifInteractive = true;
                                }
                                else
                                {
                                    ifInteractive = false;
                                }
                            }
                            if (list[t].Name == "deviceId")
                            {
                                if (ifInteractive)//如果在互动中
                                {
                                    if (list[t].Value != null)//且设备信息不为空
                                    {
                                        InteractiveDeviceId.Add(list[t].Value.ToString());//添加在互动中的设备信息
                                    }
                                }
                            }
                        }
                    }
                    InteractionToolWindow.logger.Info($"互动课堂信息{string.Join("/", InteractiveDeviceId)}");
                }
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端申请加入课堂时,获取听讲端设备信息请求失败\n 异常信息:{ex.Message}\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"听讲端申请加入课堂时获取听讲设备异常\n 互动ID为:{InteractionId},异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 同意加入课堂后,设置听讲端设备互动
        /// </summary>
        /// <param name="deviceId">听讲端设备ID</param>
        public void Set_Interaction(string deviceId)
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/set_interaction";
                string param = @"{""interactionId""" + ":" + InteractionId.ToString() + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceId + "\"" + "}";
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"申请加入互动时,发送主讲端选择听讲端互动请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 设置互动状态
        /// </summary>
        /// <param name="interMode">互动模式(1授课,2互动,3讨论 4板书)</param>
        /// <param name="interactionId">互动ID</param>
        /// <returns></returns>
        public bool Update_interaction_info(int interMode, string interactionId)
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/update_interaction_info";
                string param = @"{""interMode""" + ":" + interMode.ToString() + "," + "\"" + "interactionId" + "\"" + ":" + "\"" + interactionId + "\"" + "}";
                IssueRequest(url, param, "POST", ref data, ref array);
                return true;
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"申请加入互动时,发送将当前模式切换为互动模式请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        public void Tips_Closing(object sender, System.ComponentModel.CancelEventArgs args)
        {
            Dispose();
        }

        /// <summary>
        /// 列表资源释放
        /// </summary>
        public static void Dispose()
        {
            InteractiveDeviceId.Clear();
            InteractiveDeviceId = null;
        }

        /// <summary>
        /// 拒绝加入课堂按键请求
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void disagree_Click(object sender, RoutedEventArgs e)
        {
            //触摸屏事件不响应
            uint extra = GetMessageExtraInfo();
            bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
            bool isTouchEvent = ((extra & 0x80) == 0x80);
            if (isTouchEvent || isPen)
            {
                return;
            }

            try
            {
                InteractionToolWindow.logger.Info("拒绝加入课堂鼠标操作响应");
                InteractionToolWindow.logger.Info($"拒绝{message.Text}请求");
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"拒绝加入课堂按键异常,异常信息:{ex.Message}");
            }
            finally
            {
                this.Close();
            }
        }

        /// <summary>
        /// 拒绝按键适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void disagree_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                InteractionToolWindow.logger.Info("拒绝加入课堂触摸屏操作响应");
                InteractionToolWindow.logger.Info($"拒绝{message.Text}请求");
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"拒绝加入课堂按键异常,异常信息:{ex.Message}");
            }
            finally
            {
                this.Close();
            }
        }

        /// <summary>
        /// 同意加入课堂发起对应请求
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void agree_Click(object sender, RoutedEventArgs e)
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

                InteractionToolWindow.logger.Info("同意加入课堂鼠标操作响应");
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"同意{message.Text}请求");
                    Get_device_info();
                    string deviceid = string.Empty;
                    if (InteractiveDeviceId.Count != 0 || InteractiveDeviceId != null)
                    {
                        for (int i = 0; i < InteractiveDeviceId.Count; i++)
                        {
                            if (string.IsNullOrEmpty(deviceid))
                            {
                                deviceid += InteractiveDeviceId[i];
                            }
                            else
                            {
                                deviceid += "/" + InteractiveDeviceId[i];
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(deviceid))
                    {
                        deviceid += DeviceId;
                    }
                    else
                    {
                        if (deviceid.Contains(DeviceId))
                        {
                            InteractionToolWindow.logger.Warn("当前教室已经在互动中,未发送该设备的同意互动请求");
                            return;
                        }
                        else
                        {
                            deviceid += "/" + DeviceId;
                        }
                    }
                    Set_Interaction(deviceid);
                    Thread.Sleep(200);
                    Update_interaction_info(2, InteractionId);
                    Thread.Sleep(200);

                    //理论上主窗口界面应该切换到互动模式上去,涉及子窗口与父窗口通讯,先注释掉,接口保留
                    //InteractionToolWindow maintoolwindow = InteractionWindowsExit() as InteractionToolWindow;
                    //if (maintoolwindow != null)
                    //{
                    //    maintoolwindow.listener.IsInteracting();
                    //}
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"申请加入互动失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"加入互动失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
            finally
            {
                this.Close();
            }
        }

        /// <summary>
        /// 同意按键适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void agree_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                InteractionToolWindow.logger.Info("同意加入课堂触摸屏操作响应");

                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"同意{message.Text}请求");
                    Get_device_info();
                    string deviceid = string.Empty;
                    if (InteractiveDeviceId.Count != 0 || InteractiveDeviceId != null)
                    {
                        for (int i = 0; i < InteractiveDeviceId.Count; i++)
                        {
                            if (string.IsNullOrEmpty(deviceid))
                            {
                                deviceid += InteractiveDeviceId[i];
                            }
                            else
                            {
                                deviceid += "/" + InteractiveDeviceId[i];
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(deviceid))
                    {
                        deviceid += DeviceId;
                    }
                    else
                    {
                        if (deviceid.Contains(DeviceId))
                        {
                            InteractionToolWindow.logger.Warn("当前教室已经在互动中,未发送该设备的同意互动请求");//
                            return;
                        }
                        else
                        {
                            deviceid += "/" + DeviceId;
                        }
                    }
                    Set_Interaction(deviceid);
                    Thread.Sleep(200);
                    Update_interaction_info(2, InteractionId);
                    Thread.Sleep(200);

                    //理论上主窗口界面应该切换到互动模式上去,涉及子窗口与父窗口通讯,先注释掉,接口保留
                    //InteractionToolWindow maintoolwindow = InteractionWindowsExit() as InteractionToolWindow;
                    //if (maintoolwindow != null)
                    //{
                    //    maintoolwindow.listener.IsInteracting();
                    //}
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"申请加入互动失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"加入互动失败,异常信息:{ex.Message}\r\n异常栈:{ex.StackTrace}");
            }
            finally
            {
                this.Close();
            }
        }

        /// <summary>
        /// 提示窗口,窗口拖动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();//提示窗口拖拽
        }

    }
}
