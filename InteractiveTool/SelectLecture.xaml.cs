using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
        public static void Dispose()
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
        /// <summary>
        /// http请求方法
        /// </summary>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="method"></param>
        /// <param name="unprocessedValue"></param>
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
        /// 获取课堂信息
        /// </summary>
        public void Get_device_info()
        {
            try
            {

                JObject data = new JObject();
                JArray array = new JArray();

                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/devices_info?interactionId=" + interactionID;
                IssueRequest(url, string.Empty, "GET", ref data, ref array);
                //if (data != null)
                //{
                //    IEnumerable<JProperty> jProperties = data.Properties();
                //    JProperty[] list = jProperties.ToArray();
                //    for (int j = 0; j < list.Length; j++)
                //    {
                //        if (list[j].Name == "deviceId")
                //        {
                //            deviceIds.Add(list[j].Value.ToString());
                //        }
                //        if (list[j].Name == "deviceName")
                //        {
                //            deviceNames.Add(list[j].Value.ToString());
                //        }
                //    }
                //    Add_Control(0, deviceNames[0]);
                //}

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
                MessageBox.Show($"获取听讲设备异常,互动ID为:{interactionID},异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 根据课堂信息动态添加控件
        /// </summary>
        /// <param name="i"></param>
        /// <param name="deviceName"></param>
        public void Add_Control(int i, string deviceName, List<bool> selectstatus)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    CheckBox checkBox = new CheckBox();
                    Button button = new Button();
                    RowDefinition row = new RowDefinition();
                    row.Height = new GridLength(35);
                    subview.RowDefinitions.Add(row);

                    checkBox.Name = "cb_" + i;
                    checkBox.Click += interactionCb_Click;
                    checkBox.HorizontalAlignment = HorizontalAlignment.Left;
                    checkBox.VerticalAlignment = VerticalAlignment.Center;
                    checkBox.BorderThickness = new Thickness(0);


                    checkBox.Foreground = Brushes.White;
                    checkBox.Content = deviceName;
                    checkBox.SetValue(Grid.RowProperty, i);
                    checkBox.SetValue(Grid.ColumnProperty, 0);
                    checkBox.FontSize = 16;
                    checkBox.Height = 20;

                    button.Name = "bt_" + i;
                    button.SetValue(Grid.ColumnProperty, 1);
                    button.SetValue(Grid.RowProperty, i);
                    button.Style = this.FindResource("OtherButtonStyle") as Style;
                    button.Height = 18;
                    button.Width = 21;
                    button.Click += mic_Click;
                    button.IsEnabled = true;
                    button.HorizontalAlignment = HorizontalAlignment.Left;
                    button.VerticalAlignment = VerticalAlignment.Center;
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
                MessageBox.Show("听讲端设备信息冲突\n" + $"异常信息;{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 主讲端选择听讲端静音
        /// </summary>
        /// <param name="deviceid"></param>
        /// <param name="ctrlmute"></param>
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
                //MessageBox.Show($"听讲端静音请求失败,设备id:{deviceid}\n" + $"异常分析:{ex.Message}");
                throw ex;
            }
        }
        /// <summary>
        /// 选中按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionCb_Click(object sender, RoutedEventArgs e)
        {
            CheckBox check = sender as CheckBox;
            try
            {
                this.Dispatcher.Invoke(() =>
                {
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
                MessageBox.Show($"选中课堂错误:错误信息{ex.Message}\n" + $"错误栈:{ex.StackTrace}");
            }
        }


        /// <summary>
        /// 静音按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mic_Click(object sender, RoutedEventArgs e)
        {
            string listenerId = string.Empty;
            try
            {

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
                                Ctrl_Interaction_Mute(deviceIds[i], 1);
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/micCancel.png")));
                                slienceIf[i] = true;
                                return;
                            }
                            else
                            {
                                Ctrl_Interaction_Mute(deviceIds[i], 0);
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
                MessageBox.Show("静音或取消静音请求发送失败\n" + $"设备ID:{listenerId}");
            }
        }

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
            this.Dispatcher.Invoke(() =>
            {

                if (InteractionWindowsExit() != null)
                {
                    InteractionWindowsExit().Topmost = true;
                }
                this.Close();
            });
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
                MessageBox.Show("切换当前模式为互动模式请求失败\n" + "错误信息:" + ex.Message + "\n 错误栈:" + ex.StackTrace, "异常");
                return false;
            }
        }
        /// <summary>
        /// 确认按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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
                MessageBox.Show("互动请求失败\n" + "错误信息" + ex.Message);
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
        /// 主讲端发起互动
        /// </summary>
        /// <param name="deviceId"></param>
        public void Set_Interaction(string deviceId)
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/set_interaction";

                //string param = @"{""interactionId""" + ":" + "598076" + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceId + "\"" + "}";

                string param = @"{""interactionId""" + ":" + interactionID.ToString() + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceId + "\"" + "}";
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置听讲端互动请求失败:" + ex.Message + "\n" + ex.StackTrace + "\n" + $"设备ID{deviceId}", "异常处理");
            }
        }

        private void SelectView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
