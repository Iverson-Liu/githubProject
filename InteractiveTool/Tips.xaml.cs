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
using System.Windows.Threading;

namespace InteractiveTool
{
    /// <summary>
    /// Tips.xaml 的交互逻辑
    /// </summary>
    public partial class Tips : Window
    {
        string IP;
        string Port;
        string InteractionId;
        string DeviceId;
        DispatcherTimer timer = null;
     
        public Tips(string Message, string ip, string port, string interactionId, string deviceId)
        {
            this.IP = ip;
            this.Port = port;
            this.InteractionId = interactionId;
            this.DeviceId = deviceId;
            InitializeComponent();
            message.Text = Message + "申请互动";
            InitTimer();
            StartTimer();
        }
        private void InitTimer()
        {
            if (timer==null)
            {
                timer = new DispatcherTimer();
                timer.Tick += new EventHandler(DataTime_Tick);
                timer.Interval = TimeSpan.FromSeconds(15);
            }
        }
        public void DataTime_Tick(object sender, EventArgs e)
        {
            this.Close();
        }
        public void StartTimer()
        {
            if (timer!=null&&timer.IsEnabled==false)
            {
                timer.Start();
            }
        }

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
                MessageBox.Show("设置听讲端互动请求失败:" + ex.Message + "\n" + ex.StackTrace + "\n" + $"设备ID{deviceId}", "异常处理");
            }
        }

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

        private void disagree_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void agree_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Set_Interaction(DeviceId);
                Thread.Sleep(100);
                Update_interaction_info(2, InteractionId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加入互动失败\n 异常信息:{ex.Message}");
            }
            finally
            {
                this.Close();
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
