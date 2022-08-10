using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InteractiveTool
{
    /// <summary>
    /// ListenClient.xaml 的交互逻辑
    /// </summary>
    public partial class ListenClient : UserControl
    {
        private static TimeSpan slienceModeTouchTime;//静音或取消静音触摸屏触发时间戳
        private static TimeSpan applyInteractionModeTouchTime;//申请互动按键触摸屏触发时间戳
        public TimeSpan aviodclick = new TimeSpan(0, 0, 1);

        public ListenClient()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 发起http请求方法
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
        /// 后台展示到前台时所有选中状态取消
        /// </summary>
        public void AllSelectStatusCancel()
        {
            try
            {
                switch (InteractionToolWindow.showWindowStatus)
                {
                    case InteractionToolWindow.Status.ListenClient:
                        IsListenClient();
                        VoiceStatus();
                        break;
                    case InteractionToolWindow.Status.InterListenClient:
                        IsInteracting();
                        VoiceStatus();
                        break;
                }
            }
            catch (Exception ex)
            {
                switch (InteractionToolWindow.showWindowStatus)
                {
                    case InteractionToolWindow.Status.ListenClient:
                        InteractionToolWindow.logger.Error($"听讲端界面重置失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                        break;
                    case InteractionToolWindow.Status.InterListenClient:
                        InteractionToolWindow.logger.Error($"互动中模式界面重置失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                        break;
                }
                throw ex;
            }
        }

        /// <summary>
        /// 依据互动状态信息,讨论模式下互动中按键不可用
        /// </summary>
        public void IsDiscussListenClient()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"听讲端申请互动按键套路模式下禁用");
                    applyInteractionBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/apply4interactionCannotselect.png"));
                    applyInteractionBtTxt.Text = "申请互动";
                    applyInteractionBtTxt.Foreground = Brushes.Gray;
                    apply_Interaction.IsEnabled = false;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端申请互动按键讨论模式下禁用失败,异常原因:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 互动模式信息等,重置听讲端申请互动按键状态,按键可用
        /// </summary>
        public void IsListenClient()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info("听讲端申请互动按键重置为申请互动状态");
                    applyInteractionBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/apply4interactionUnselect.png"));
                    applyInteractionBtTxt.Text = "申请互动";
                    applyInteractionBtTxt.Foreground = Brushes.AliceBlue;
                    apply_Interaction.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端申请互动按键重置为申请互动状态失败,异常信息{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 听讲端设备收到websocket设备角色信息,申请互动按键状态变为互动中且按键不可点击 
        /// </summary>
        public void IsInteracting()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info("听讲端申请互动按键置为互动中状态");
                    applyInteractionBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/apply4interactionSelect.png"));
                    applyInteractionBtTxt.Text = "互动中";
                    applyInteractionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    apply_Interaction.IsEnabled = false;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端申请互动按键置为互动中状态失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 听讲端设备静音或取消静音按键重置为取消静音静音及对应状态
        /// </summary>
        public void MuteStatus()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"听讲端界面申请静音取消静音按键置为取消静音状态");
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slienceCancel.png"));
                    slienceBtTxt.Text = "取消静音";
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端界面申请静音取消静音按键置为取消静音状态失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 听讲端设备静音或取消静音按键重置为静音按键及对应状态
        /// </summary>
        public void VoiceStatus()
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"听讲端界面申请静音取消静音按键置为申请静音状态");
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "静音";
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端界面申请静音取消静音按键置为申请静音状态失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
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
                string url = @"http://" + InteractionToolWindow.IP + ":" + InteractionToolWindow.Port + "/interactionPlatform/device_api/mute_listener";

                string param = @"{""interactionId""" + ":" + "\"" + InteractionToolWindow.interactionId + "\"" + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceid + "\"" + "," + "\"" + "ctrlMute" + "\"" + ":" + ctrlmute.ToString() + "}";
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端设备静音请求失败\n 异常信息:{ex.Message}\n 异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 听讲端设备申请互动请求
        /// </summary>
        /// <param name="deviceid">听讲端设备ID,互动请求发送后,主讲端设备收到WebSocket MessageType:3的消息</param>
        public void Apply_Interaction(string deviceid)
        {
            try
            {
                JObject data = new JObject();
                JArray array = new JArray();

                string url = @"http://" + InteractionToolWindow.IP + ":" + InteractionToolWindow.Port + "/interactionPlatform/device_api/apply_interaction";

                string param = @"{""interactionId""" + ":" + "\"" + InteractionToolWindow.interactionId + "\"" + "," + "\"" + "deviceId" + "\"" + ":" + "\"" + deviceid + "\"" + "}";
                InteractionToolWindow.logger.Info($"Apply_Interaction Request Url:\r\n {url} {param}");
                IssueRequest(url, param, "POST", ref data, ref array);
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"听讲端设备申请互动失败\n 异常信息:{ex.Message}\n 异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 听讲端设备申请互动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void apply_Interaction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //时间戳规避触控屏连续触发
                if (applyInteractionModeTouchTime != null)
                {
                    TimeSpan clickTime = DateTime.Now.TimeOfDay;
                    TimeSpan dif = clickTime - applyInteractionModeTouchTime;
                    if ((dif.CompareTo(aviodclick)) < 0)
                    {
                        return;
                    }
                }

                //触摸屏事件不响应
                uint extra = InteractionToolWindow.GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"听讲端设备:{InteractionToolWindow.currentListenerDeviceId}按键鼠标请求互动");
                    Apply_Interaction(InteractionToolWindow.currentListenerDeviceId);
                    //applyInteractionBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/apply4interactionSelect.png"));
                    //applyInteractionBtTxt.Text = "互动中";
                    //applyInteractionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    //apply_Interaction.IsEnabled = false;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"申请互动请求失败,异常信息:{ex.Message.ToString()}");
                InteractionToolWindow.logger.Error($"申请互动请求失败,异常信息:{ex.Message.ToString()},异常栈:{ex.StackTrace.ToString()}");
            }
        }

        /// <summary>
        /// 听讲端设备申请互动,教学一体机触摸屏适配
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void apply_Interaction_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    applyInteractionModeTouchTime = DateTime.Now.TimeOfDay;
                    InteractionToolWindow.logger.Info($"申请互动控件触摸屏响应,touchtime:{applyInteractionModeTouchTime}");

                    InteractionToolWindow.logger.Info($"听讲端设备:{InteractionToolWindow.currentListenerDeviceId}按键触摸屏请求互动");
                    Apply_Interaction(InteractionToolWindow.currentListenerDeviceId);
                    //applyInteractionBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/apply4interactionSelect.png"));
                    //applyInteractionBtTxt.Text = "互动中";
                    //applyInteractionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    //apply_Interaction.IsEnabled = false;
                });

            }
            catch (Exception ex)
            {
                MessageBox.Show($"申请互动请求失败,异常信息:{ex.Message.ToString()}");
                InteractionToolWindow.logger.Error($"申请互动请求失败,异常信息:{ex.Message.ToString()},异常栈:{ex.StackTrace.ToString()}");
            }
        }

        /// <summary>
        /// 听讲端设备静音请求
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void slienceMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //时间戳规避触控屏连续触发
                if (slienceModeTouchTime != null)
                {
                    TimeSpan clickTime = DateTime.Now.TimeOfDay;
                    TimeSpan dif = clickTime - slienceModeTouchTime;
                    if ((dif.CompareTo(aviodclick)) < 0)
                    {
                        return;
                    }
                }

                //触摸屏事件不响应
                uint extra = InteractionToolWindow.GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info($"静音控件鼠标响应,touchtime:{slienceModeTouchTime}");
                    if (slienceBtTxt.Text == "静音")
                    {
                        InteractionToolWindow.logger.Info($"设备{InteractionToolWindow.currentListenerDeviceId}发送取消静音请求");
                        Ctrl_Interaction_Mute(InteractionToolWindow.currentListenerDeviceId, 1);
                        MuteStatus();
                    }
                    else
                    {
                        InteractionToolWindow.logger.Info($"设备{InteractionToolWindow.currentListenerDeviceId}发送静音请求");
                        Ctrl_Interaction_Mute(InteractionToolWindow.currentListenerDeviceId, 0);
                        VoiceStatus();
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"听讲端设备{InteractionToolWindow.currentListenerDeviceId}静音请求失败,异常信息:{ex.Message.ToString()}");
                InteractionToolWindow.logger.Error($"听讲端设备{InteractionToolWindow.currentListenerDeviceId}静音请求失败,异常信息:{ex.Message.ToString()},异常栈:{ex.StackTrace.ToString()}");
            }
        }

        /// <summary>
        /// 听讲端设备静音请求教学一体机触摸屏适配
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void slienceMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                slienceModeTouchTime = DateTime.Now.TimeOfDay;
                InteractionToolWindow.logger.Info($"静音控件触摸屏响应,touchtime:{slienceModeTouchTime}");

                this.Dispatcher.Invoke(() =>
                {
                    if (slienceBtTxt.Text == "静音")
                    {
                        InteractionToolWindow.logger.Info($"设备{InteractionToolWindow.currentListenerDeviceId}发送取消静音请求");
                        Ctrl_Interaction_Mute(InteractionToolWindow.currentListenerDeviceId, 1);
                        MuteStatus();
                    }
                    else
                    {
                        InteractionToolWindow.logger.Info($"设备{InteractionToolWindow.currentListenerDeviceId}发送静音请求");
                        Ctrl_Interaction_Mute(InteractionToolWindow.currentListenerDeviceId, 0);
                        VoiceStatus();
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"听讲端设备{InteractionToolWindow.currentListenerDeviceId}静音请求失败,异常信息:{ex.Message.ToString()}");
                InteractionToolWindow.logger.Error($"听讲端设备{InteractionToolWindow.currentListenerDeviceId}静音请求失败,异常信息:{ex.Message.ToString()},异常栈:{ex.StackTrace.ToString()}");
            }
        }
    }
}
