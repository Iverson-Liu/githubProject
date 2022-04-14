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
        public int num;//获取设备数量(包含主讲设备,理论上比听讲教室数量多1)
        public static int classroomnum;//听讲教室数量
        public List<bool> selectStatus = new List<bool>();//选中状态
        public List<bool> slienceIf = new List<bool>();
        public List<string> deviceNames = new List<string>();//教师名
        public List<string> deviceIds = new List<string>();//设备ID
        public SelectLecture(string configIp, string configPort, string configMac, string configInteractionId)
        {
            this.interactionID = configInteractionId;
            this.IP = configIp;
            this.Port = configPort;
            this.Mac = configMac;
            InitializeComponent();
            Get_device_info();
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
                    MessageBox.Show("请求异常!" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                throw ex;
            }
        }

        /// <summary>
        /// 获取课堂信息
        /// </summary>
        public void Get_device_info()
        {
            JObject data = new JObject();
            JArray array = new JArray();

            //string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/devices_info?interactionId=" + "598076";

            string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/devices_info?interactionId=" + interactionID;
            IssueRequest(url, string.Empty, "GET", ref data, ref array);
            if (data != null)
            {
                IEnumerable<JProperty> jProperties = data.Properties();
                JProperty[] list = jProperties.ToArray();
                for (int j = 0; j < list.Length; j++)
                {
                    if (list[j].Name == "deviceId")
                    {
                        deviceIds.Add(list[j].Value.ToString());
                    }
                    if (list[j].Name == "deviceName")
                    {
                        deviceNames.Add(list[j].Value.ToString());
                    }
                }
                Add_Control(0, deviceNames[0]);
            }

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
                            else
                            {
                                ifMainDevice = false;
                            }
                        }
                        if (list[t].Name == "deviceId")
                        {
                            if (ifMainDevice)
                            {
                                MainWindow.MainDeviceId = list[t].Value.ToString();
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
                                if (!ifMainDevice)
                                {
                                    slienceIf.Add(false);
                                }
                            }
                            else
                            {
                                if (!ifMainDevice)
                                {
                                    slienceIf.Add(true);
                                }
                            }
                        }
                    }
                    if (ifMainDevice == false)
                    {
                        selectStatus.Add(false);
                        Add_Control(classroomnum, deviceNames[classroomnum]);
                        classroomnum++;
                    }
                }
            }
        }

        /// <summary>
        /// 根据课堂信息动态添加控件
        /// </summary>
        /// <param name="i"></param>
        /// <param name="deviceName"></param>
        public void Add_Control(int i, string deviceName)
        {
            this.Dispatcher.Invoke(() =>
            {
                CheckBox checkBox = new CheckBox();
                Button button = new Button();
                RowDefinition row = new RowDefinition();

                subview.RowDefinitions.Add(row);
                subview.Children.Add(checkBox);
                subview.Children.Add(button);

                button.Name = "bt_" + i;
                button.SetValue(Grid.RowProperty, i);
                button.Style = this.FindResource("OtherButtonStyle") as Style;
                button.Height = 20;
                button.Width = 25;
                button.Click += mic_Click;
                button.SetValue(Grid.ColumnProperty, 1);
                if (slienceIf[i])
                {
                    button.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/micCancel.png")));
                }
                else
                {
                    button.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/mic.png")));
                }

                checkBox.Name = "cb_" + i;
                checkBox.Click += interactionCb_Click;
                checkBox.HorizontalAlignment = HorizontalAlignment.Left;
                checkBox.VerticalAlignment = VerticalAlignment.Center;
                checkBox.Foreground = Brushes.AliceBlue;
                checkBox.Content = deviceName;
                checkBox.SetValue(Grid.RowProperty, i);
                checkBox.SetValue(Grid.ColumnProperty, 0);
                checkBox.FontSize = 18;
                checkBox.Height = 25;
            });
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
                string url = @"http://" + IP + ":" + Port + "/interactionPlatform/device_api/ctrl_interaction_mute";
                //string param = @"{""interactionId""" + ":" + "598076" + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceid + "\"" + "," + "\"" + "ctrlMute" + "\"" + ":" + ctrlmute.ToString() + "}";

                string param = @"{""interactionId""" + ":" + interactionID + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceid + "\"" + "," + "\"" + "ctrlMute" + "\"" + ":" + ctrlmute.ToString() + "}";
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"听讲端静音请求失败,设备id:{deviceid}\n" + $"异常分析:{ex.Message}");
                throw ex;
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
                this.Dispatcher.Invoke(() =>
                {
                    Button bt_mic = sender as Button;
                    for (int i = 0; i < classroomnum; i++)
                    {
                        if (bt_mic.Name == ("bt_" + i))
                        {
                            listenerId = deviceIds[i];
                            if (slienceIf[i] == false)
                            {
                                Ctrl_Interaction_Mute(deviceIds[i], 1);
                                slienceIf[i] = true;
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/micCancel.png")));

                            }
                            else
                            {
                                Ctrl_Interaction_Mute(deviceIds[i], 0);
                                slienceIf[i] = false;
                                bt_mic.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/images/mic.png")));

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


        /// <summary>
        /// 取消按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            classroomnum = 0;
            selectStatus.Clear();
            slienceIf.Clear();
            deviceNames.Clear();
            deviceIds.Clear();
            this.Close();
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
                }
                Thread.Sleep(100);
                int interMode = 2;
                Update_interaction_info(interMode, interactionID);
            }
            catch (Exception ex)
            {
                MessageBox.Show("互动请求失败\n" + "错误信息" + ex.Message);
            }
            finally
            {
                classroomnum = 0;
                selectStatus.Clear();
                slienceIf.Clear();
                deviceNames.Clear();
                deviceIds.Clear();
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

        /// <summary>
        /// 选中按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionCb_Click(object sender, RoutedEventArgs e)
        {

            CheckBox check = sender as CheckBox;
            for (int i = 0; i < classroomnum; i++)
            {
                if (check.Name == ("cb_" + i))
                {
                    if (check.IsChecked == true)
                    {
                        Set_Interaction(deviceIds[i]);
                        selectStatus[i] = true;
                    }
                    else
                    {
                        selectStatus[i] = false;
                    }
                }
            }
        }

        private void SelectView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
