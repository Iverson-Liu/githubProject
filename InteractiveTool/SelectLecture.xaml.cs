using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InteractiveTool
{
    /// <summary>
    /// SelectLecture.xaml 的交互逻辑
    /// </summary>
    public partial class SelectLecture : Window
    {
        string IP;
        string Port;
        string Mac;
        string interactionID;//互动id
        public static int num;//获取设备数量(包含主讲设备,理论上比听讲教室数量多1)
        public static int classroomnum = 0;//听讲教室数量
        public static List<bool> selectStatus = new List<bool>();//选中状态
        public static List<bool> slienceIf = new List<bool>();//静音状态
        public static List<string> deviceNames = new List<string>();//教师名
        public static List<string> deviceIds = new List<string>();//设备ID


        //获取驱动事件信息
        [DllImport("user32.dll")]
        private static extern uint GetMessageExtraInfo();

        public SelectLecture(string configIp, string configPort, string configMac, string configInteractionId)
        {
            if (selectStatus == null)
            {
                selectStatus = new List<bool>();
            }
            if (slienceIf == null)
            {
                slienceIf = new List<bool>();
            }
            if (deviceNames == null)
            {
                deviceNames = new List<string>();
            }
            if (deviceIds == null)
            {
                deviceIds = new List<string>();
            }
            this.interactionID = configInteractionId;
            this.IP = configIp;
            this.Port = configPort;
            this.Mac = configMac;
            this.Closing += Window_Closing;
            InitializeComponent();
            Get_device_info();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// list等对象释放,没有调用该方法时,
        /// 快速点击确定或取消后再点击互动窗口会因为上一次的列表资源没有完全释放导致本次课堂信息列表缺失
        /// </summary>
        public static void Dispose()
        {
            try
            {
                num = 0;
                classroomnum = 0;
                selectStatus.Clear();
                slienceIf.Clear();
                deviceNames.Clear();
                deviceIds.Clear();
                if (selectStatus != null)
                {
                    selectStatus = null;
                }
                if (slienceIf != null)
                {
                    slienceIf = null;
                }
                if (deviceIds != null)
                {
                    deviceIds = null;
                }
                if (deviceNames != null)
                {
                    deviceNames = null;
                }
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"选择听讲端教室子窗口资源释放失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 发起http请求方法
        /// </summary>
        /// <param name="url">http URL</param>
        /// <param name="param">json格式参数</param>
        /// <param name="method">请求方式</param>
        /// <param name="unprocessedValue">未处理json返回值</param>
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
                        InteractionToolWindow.logger.Info($"Url:{url} Param:{param}");
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
                InteractionToolWindow.logger.Error($"选择听讲端接口请求失败\n 异常信息:{ex.Message}\n 异常栈:{ex.StackTrace}");
                if (string.IsNullOrEmpty(requesttime))
                {
                    MessageBox.Show($"请求异常!ts:{requesttime}" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                else
                {
                    MessageBox.Show("Http请求异常,请检查配置信息和网络连接\n" + "异常信息:" + ex.Message + "\n" + $"异常栈:{ex.StackTrace}", "异常处理");
                }
                throw ex;
            }
        }

        /// <summary>
        /// 获取设备信息,包括设备名称,设备ID,静音状态等
        /// </summary>
        public void Get_device_info()
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();

                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/devices_info?interactionId=" + interactionID;
                InteractionToolWindow.logger.Info($"获取课堂信息请求发送,Url:{url}");
                IssueRequest(url, string.Empty, "GET", ref data, ref array);

                if (array != null)
                {
                    num = array.Count;
                    for (int i = 0; i < num; i++)
                    {
                        JToken jToken = array[i].ToString();
                        string context = jToken.ToString();
                        JObject datas = JObject.Parse(context);
                        IEnumerable<JProperty> properties = datas.Properties();
                        JProperty[] list = properties.ToArray();
                        //打印课堂信息
                        InteractionToolWindow.logger.Info($"课堂信息:{string.Join("/", properties)}");
                        bool ifMainDevice = false;
                        for (int t = 0; t < list.Length; t++)
                        {
                            if (list[t].Name == "deviceName")
                            {
                                deviceNames.Add(list[t].Value.ToString());
                            }
                            if (list[t].Name == "role")
                            {
                                if (list[t].Value.ToString() == "0")
                                {
                                    ifMainDevice = true;
                                    deviceNames.RemoveAt(i);
                                }
                                else if (list[t].Value.ToString() == "2")
                                {
                                    selectStatus.Add(true);
                                    ifMainDevice = false;
                                }
                                else
                                {
                                    selectStatus.Add(false);
                                    ifMainDevice = false;
                                }
                            }
                            if (list[t].Name == "deviceId")
                            {
                                if (ifMainDevice)
                                {
                                    InteractionToolWindow.MainDeviceId = list[t].Value.ToString();
                                }
                                else
                                {
                                    deviceIds.Add(list[t].Value.ToString());
                                }
                            }

                            if (list[t].Name == "muteStatus")
                            {
                                if (list[t].Value.ToString() == "false")
                                {
                                    if (ifMainDevice == false)
                                    {
                                        slienceIf.Add(false);
                                    }
                                }
                                else
                                {
                                    if (ifMainDevice == false)
                                    {
                                        slienceIf.Add(true);
                                    }
                                }
                            }
                        }
                        if (ifMainDevice == false)
                        {
                            if (deviceNames.Count >= 1 && selectStatus.Count >= 1)
                            {
                                Add_Control(classroomnum, deviceNames[classroomnum], selectStatus);
                                classroomnum++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"获取课堂设备信息请求失败,异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"获取听讲设备异常,互动ID为:{interactionID},异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 根据设备信息动态添加控件
        /// </summary>
        /// <param name="i">数量,对应下拉列表行数</param>
        /// <param name="deviceName">设备名称</param>
        public void Add_Control(int i, string deviceName, List<bool> selectstatus)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    CheckBox checkBox = new CheckBox();
                    Button button = new Button();
                    RowDefinition row = new RowDefinition();
                    row.Height = new GridLength(60);
                    subview.RowDefinitions.Add(row);

                    checkBox.Name = "cb_" + i;
                    checkBox.Click += interactionCb_Click;
                    //增加触摸屏响应事件
                    checkBox.TouchDown += interactionCb_TouchDown;
                    checkBox.HorizontalAlignment = HorizontalAlignment.Left;
                    checkBox.VerticalAlignment = VerticalAlignment.Top;
                    checkBox.BorderThickness = new Thickness(0);


                    checkBox.Foreground = Brushes.White;
                    checkBox.Content = deviceName;
                    checkBox.SetValue(Grid.RowProperty, i);
                    checkBox.SetValue(Grid.ColumnProperty, 0);
                    checkBox.FontSize = 16;

                    checkBox.Height = 55;

                    button.Name = "bt_" + i;
                    button.SetValue(Grid.ColumnProperty, 1);
                    button.SetValue(Grid.RowProperty, i);
                    button.Style = this.FindResource("OtherButtonStyle") as Style;
                    button.Height = 18;
                    button.Width = 21;
                    button.Click += mic_Click;
                    //增加触摸屏响应事件
                    button.TouchDown += mic_TouchDown;
                    button.IsEnabled = true;
                    button.Margin = new Thickness(0, 5, 0, 0);
                    button.HorizontalAlignment = HorizontalAlignment.Center;
                    button.VerticalAlignment = VerticalAlignment.Top;
                    if (slienceIf[i] == false)
                    {
                        button.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/mic.png")));
                    }
                    else
                    {
                        button.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/micCancel.png")));
                    }
                    if (selectstatus[i])
                    {
                        checkBox.IsChecked = true;
                        checkBox.Style = this.FindResource("CheckBoxIsCheckedStyle") as Style;
                    }
                    else
                    {
                        checkBox.IsChecked = false;
                        checkBox.Style = this.FindResource("CheckBoxNotCheckedStyle") as Style;
                    }
                    subview.Children.Add(checkBox);
                    subview.Children.Add(button);
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"选择听讲端增加设备信息失败,异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"听讲端设备信息冲突,异常信息;{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 主讲端发送听讲端静音请求
        /// </summary>
        /// <param name="deviceid">设备Id</param>
        /// <param name="ctrlmute">静音状态</param>
        public void Ctrl_Interaction_Mute(string deviceid, int ctrlmute)
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/mute_listener";

                string param = @"{""interactionId""" + ":" + "\"" + interactionID + "\"" + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceid + "\"" + "," + "\"" + "ctrlMute" + "\"" + ":" + ctrlmute.ToString() + "}";
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"主讲端单独设置听讲端设备静音失败\n 异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                //MessageBox.Show($"听讲端静音请求失败,设备id:{deviceid}\n" + $"异常分析:{ex.Message}");
                throw ex;
            }
        }

        /// <summary>
        /// 切换当前状态为互动模式
        /// </summary>
        /// <param name="interMode"></param>
        /// <param name="interactionId"></param>
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
                InteractionToolWindow.logger.Error($"更新互动状态请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                return false;
                throw ex;
            }
        }

        /// <summary>
        /// 主讲端选择听讲端设备加入互动
        /// </summary>
        /// <param name="deviceId">听讲端设备ID</param>
        public void Set_Interaction(string deviceId)
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/set_interaction";

                string param = @"{""interactionId""" + ":" + interactionID.ToString() + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceId + "\"" + "}";
                InteractionToolWindow.logger.Info($"主讲端选择听讲端互动请求: Url:{url}\n param:{param}");
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"主讲端选择听讲端请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show("设置听讲端互动请求失败:" + ex.Message + "\n" + ex.StackTrace + "\n" + $"设备ID{deviceId}", "异常处理");
            }
        }

        /// <summary>
        /// 听讲端教室选中按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionCb_Click(object sender, RoutedEventArgs e)
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
                CheckBox check = sender as CheckBox;
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info("教室选中鼠标操作响应");
                    for (int i = 0; i < classroomnum; i++)
                    {
                        if (check.Name == ("cb_" + i))
                        {
                            if (check.IsChecked == true)
                            {
                                selectStatus[i] = true;
                                check.Style = this.FindResource("CheckBoxIsCheckedStyle") as Style;
                                //subview.Children[2 * i + 1].IsEnabled = false;
                                break;
                            }
                            else
                            {
                                selectStatus[i] = false;
                                check.Style = this.FindResource("CheckBoxNotCheckedStyle") as Style;
                                //subview.Children[2 * i + 1].IsEnabled = true;
                                //先切换到授课模式,取消所有静音  (目前按照新逻辑注释掉)
                                //Update_interaction_info(1, interactionID);
                                break;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"选中互动设备或取消选中互动设备异常,异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"选中课堂错误:错误信息{ex.Message}\n" + $"错误栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 互动教室选中适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionCb_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                CheckBox check = sender as CheckBox;
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info("教室选中触摸屏操作响应");

                    for (int i = 0; i < classroomnum; i++)
                    {
                        if (check.Name == ("cb_" + i))
                        {
                            if (check.IsChecked == true)
                            {
                                selectStatus[i] = true;
                                check.Style = this.FindResource("CheckBoxIsCheckedStyle") as Style;
                                //subview.Children[2 * i + 1].IsEnabled = false;
                                break;
                            }
                            else
                            {
                                selectStatus[i] = false;
                                check.Style = this.FindResource("CheckBoxNotCheckedStyle") as Style;
                                //subview.Children[2 * i + 1].IsEnabled = true;
                                //先切换到授课模式,取消所有静音  (目前按照新逻辑注释掉)
                                //Update_interaction_info(1, interactionID);
                                break;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"选中互动设备或取消选中互动设备异常,异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"选中课堂错误:错误信息{ex.Message}\n" + $"错误栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 听讲端设备静音按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mic_Click(object sender, RoutedEventArgs e)
        {
            //触摸屏事件不响应
            uint extra = GetMessageExtraInfo();
            bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
            bool isTouchEvent = ((extra & 0x80) == 0x80);
            if (isTouchEvent || isPen)
            {
                return;
            }

            string listenerId = string.Empty;
            try
            {
                InteractionToolWindow.logger.Info("静音按键鼠标操作响应");
                Button bt_mic = sender as Button;

                this.Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < classroomnum; i++)
                    {
                        if (bt_mic.Name == ("bt_" + i))
                        {
                            listenerId = deviceIds[i];
                            if (slienceIf[i] == false)
                            {
                                /*静音或者取消静音功能放到确定按键里去做,此处之记录对应设备静音状态,确定按键中根据此处记录的状态信息去设置静音接参数
                                 */
                                //Ctrl_Interaction_Mute(deviceIds[i], 1);
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/micCancel.png")));
                                slienceIf[i] = true;
                                return;
                            }
                            else
                            {
                                //Ctrl_Interaction_Mute(deviceIds[i], 0); 教师单独静音接口
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/mic.png")));
                                slienceIf[i] = false;
                                return;
                            }
                        }
                    }
                }
                );
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"静音按键状态切换异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"静音按键状态切换异常\n 异常信息:{ex.Message} 异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 听讲端设备静音按键适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mic_TouchDown(object sender, TouchEventArgs e)
        {
            string listenerId = string.Empty;
            try
            {
                InteractionToolWindow.logger.Info("静音按键触摸屏操作响应");
                Button bt_mic = sender as Button;

                this.Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < classroomnum; i++)
                    {
                        if (bt_mic.Name == ("bt_" + i))
                        {
                            listenerId = deviceIds[i];
                            if (slienceIf[i] == false)
                            {
                                /*静音或者取消静音功能放到确定按键里去做,此处之记录对应设备静音状态,确定按键中根据此处记录的状态信息去设置静音接参数
                                 */
                                //Ctrl_Interaction_Mute(deviceIds[i], 1);
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/micCancel.png")));
                                slienceIf[i] = true;
                                return;
                            }
                            else
                            {
                                //Ctrl_Interaction_Mute(deviceIds[i], 0);
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/mic.png")));
                                slienceIf[i] = false;
                                return;
                            }
                        }
                    }
                }
                );
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"静音按键状态切换异常,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"静音按键状态切换异常\n 异常信息:{ex.Message} 异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 主界面窗口检测,获取窗口句柄
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
        /// 取消按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
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

                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info("取消按键鼠标操作响应");
                    if (InteractionWindowsExit() != null)
                    {
                        InteractionWindowsExit().Topmost = true;
                    }
                    this.Close();
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"取消按键鼠标点击失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 取消按键适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                this.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    InteractionToolWindow.logger.Info("取消按键触摸屏响应");
                    if (InteractionWindowsExit() != null)
                    {
                        InteractionWindowsExit().Topmost = true;
                    }
                    Thread.Sleep(300);
                    this.Close();
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"取消按键触摸屏响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 确认按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ok_Click(object sender, RoutedEventArgs e)
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
                InteractionToolWindow.logger.Info("确定按键鼠标操作响应");
                string selectdeviceId = string.Empty;

                for (int i = 0; i < selectStatus.Count; i++)
                {
                    if (selectStatus[i])
                    {
                        if (string.IsNullOrEmpty(selectdeviceId))
                        {
                            selectdeviceId += deviceIds[i];
                        }
                        else
                        {
                            selectdeviceId += "/" + deviceIds[i];
                        }
                    }
                }
                if (!string.IsNullOrEmpty(selectdeviceId))
                {
                    Set_Interaction(selectdeviceId);
                    Thread.Sleep(300);
                    int interMode = 2;
                    Update_interaction_info(interMode, interactionID);
                    Thread.Sleep(300);
                }

                for (int i = 0; i < deviceIds.Count; i++)
                {
                    if (slienceIf[i])
                    {
                        Ctrl_Interaction_Mute(deviceIds[i], 1);
                    }
                    else
                    {
                        Ctrl_Interaction_Mute(deviceIds[i], 0);
                    }
                }
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"主讲端选择听讲端互动,或主讲端选择听讲端静音请求失败\n 异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"互动请求失败,异常信息{ex.Message}.\r\n异常栈:{ex.StackTrace}", "确认按键响应异常提示");
            }
            finally
            {

                if (InteractionWindowsExit() != null)
                {
                    InteractionWindowsExit().Topmost = true;
                }
                this.Close();
            }
        }

        /// <summary>
        /// 确认按键教学一体机触摸屏适配
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ok_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                InteractionToolWindow.logger.Info("确定按键触摸屏操作响应");

                string selectdeviceId = string.Empty;

                for (int i = 0; i < selectStatus.Count; i++)
                {
                    if (selectStatus[i])
                    {
                        if (string.IsNullOrEmpty(selectdeviceId))
                        {
                            selectdeviceId += deviceIds[i];
                        }
                        else
                        {
                            selectdeviceId += "/" + deviceIds[i];
                        }
                    }
                }
                if (!string.IsNullOrEmpty(selectdeviceId))
                {
                    Set_Interaction(selectdeviceId);
                    Thread.Sleep(300);
                    int interMode = 2;
                    Update_interaction_info(interMode, interactionID);
                    Thread.Sleep(300);
                }

                for (int i = 0; i < deviceIds.Count; i++)
                {
                    if (slienceIf[i])
                    {
                        Ctrl_Interaction_Mute(deviceIds[i], 1);
                    }
                    else
                    {
                        Ctrl_Interaction_Mute(deviceIds[i], 0);
                    }
                }
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"主讲端选择听讲端互动,或主讲端选择听讲端静音请求失败\n 异常信息:{ex.Message}.\r\n 异常栈:{ex.StackTrace}");
                MessageBox.Show($"互动请求失败,异常信息{ex.Message}.\r\n异常栈:{ex.StackTrace}", "确认按键响应异常提示");
            }
            finally
            {
                if (InteractionWindowsExit() != null)
                {
                    InteractionWindowsExit().Topmost = true;
                }
                this.Close();
            }
        }

        /// <summary>
        /// 鼠标拖动功能
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();//子窗口拖拽
        }
    }
}
