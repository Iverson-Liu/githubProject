using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InteractiveTool
{
    /// <summary>
    /// NewShowWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewShowWindow : UserControl
    {
        private static TimeSpan touchTime;//静音按键触摸屏触发时间
        public TimeSpan aviodclick = new TimeSpan(0, 0, 1);
        public NewShowWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// http请求
        /// </summary>
        /// <param name="url">http Url</param>
        /// <param name="param">参数</param>
        /// <param name="method">请求方式</param>
        /// <param name="unprocessedValue">未处理值</param>
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
                        InteractionToolWindow.logger.Info($"Request:{url}/{param}");
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
                    InteractionToolWindow.logger.Info(string.Join(";", properties));
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
                InteractionToolWindow.logger.Error($"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                if (!string.IsNullOrEmpty(requesttime))
                {
                    InteractionToolWindow.logger.Error($"异常信息:{ex.Message}\n" + $"异常栈:{ex.StackTrace}");
                    MessageBox.Show($"请求异常!ts:{requesttime}" + Environment.NewLine + "异常信息:" + ex.Message + Environment.NewLine, "异常处理");
                }
                else
                {
                    InteractionToolWindow.logger.Error($"服务器未响应或请求异常!" + Environment.NewLine + "异常信息: " + ex.Message + Environment.NewLine);
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
                    InteractionToolWindow.logger.Info("板书模式后台展示到前台重新初始化界面");
                    slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                    slienceBtTxt.Text = "全员静音";
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    teachingBtTxt.Foreground = Brushes.AliceBlue;
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                    interactionBtTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"板书模式界面初始化失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// 改变互动模式请求
        /// </summary>
        /// <param name="interMode">互动状态1授课,2互动,3讨论,4 板书)</param>
        /// <param name="interactionId">互动ID</param>
        /// <returns></returns>
        public bool Update_interaction_info(int interMode, string interactionId)
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + InteractionToolWindow.IP + ":" + InteractionToolWindow.Port + "/interactionPlatform/device_api/update_interaction_info";
                string param = @"{""interMode""" + ":" + interMode.ToString() + "," + "\"" + "interactionId" + "\"" + ":" + "\"" + interactionId + "\"" + "}";
                IssueRequest(url, param, "POST", ref data);
                return true;

            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error("状态设置异常\n 异常信息:" + " " + ex.Message + "\n 异常栈:" + ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// 全员静音请求封装
        /// </summary>
        /// <param name="ctrlMute"></param>
        /// <param name="bSpeaker"></param>
        public void Slience_All(int ctrlMute, int bSpeaker)
        {
            try
            {
                JObject data = new JObject();
                string url = @"http://" + InteractionToolWindow.IP + ":" + InteractionToolWindow.Port + "/interactionPlatform/device_api/ctrl_interaction_mute";
                string param = @"{""interactionId""" + ":" + "\"" + InteractionToolWindow.interactionId.ToString() + "\"" + ","
                    + "\"" + "deviceId" + "\"" + ":" + "\"" + InteractionToolWindow.MainDeviceId + "\"" + ","
                    + "\"" + "ctrlMute" + "\"" + ":" + ctrlMute.ToString() + ","
                    + "\"" + "bSpeaker" + "\"" + ":" + bSpeaker.ToString() + "}";

                IssueRequest(url, param, "POST", ref data);
                InteractionToolWindow.logger.Info($"静音状态:{ctrlMute},1:静音，0:取消");
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"全员静音或取消静音请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
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
                uint extra = InteractionToolWindow.GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                InteractionToolWindow.logger.Info("授课模式控件鼠标操作响应");
                int interMode = 1;
                bool result = Update_interaction_info(interMode, InteractionToolWindow.interactionId);
                if (!result)
                {
                    MessageBox.Show("授课模式请求失败", "提示");
                }
                this.Dispatcher.Invoke(() =>
                {
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingSelect.png"));
                    teachingBtTxt.Foreground = Brushes.DeepSkyBlue;
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionUnselect.png"));
                    interactionBtTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"授课模式按键请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 授课模式适配触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void teachingMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                InteractionToolWindow.logger.Info("授课模式控件触摸屏响应");
                int interMode = 1;
                bool result = Update_interaction_info(interMode, InteractionToolWindow.interactionId);
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
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"授课模式触摸屏请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"授课模式触摸屏请求失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "警告");
            }
        }

        /// <summary>
        /// 板书模式按键处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bBoardWritingMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                uint extra = InteractionToolWindow.GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                InteractionToolWindow.logger.Info("板书模式控件鼠标操作响应");
                int interMode = 4;
                bool result = Update_interaction_info(interMode, InteractionToolWindow.interactionId);
                if (!result)
                {
                    MessageBox.Show("板书模式请求失败", "提示");
                }
                this.Dispatcher.Invoke(() =>
                {
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingSelect.png"));
                    bBoardWritingTxt.Foreground = Brushes.DeepSkyBlue;
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
                InteractionToolWindow.logger.Error($"板书模式申请失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 板书模式功能适配触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bBoardWritingMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                InteractionToolWindow.logger.Info("板书模式控件触控屏操作响应");
                int interMode = 4;
                bool result = Update_interaction_info(interMode, InteractionToolWindow.interactionId);
                if (!result)
                {
                    MessageBox.Show("板书模式请求失败", "提示");
                }
                this.Dispatcher.Invoke(() =>
                {
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingSelect.png"));
                    bBoardWritingTxt.Foreground = Brushes.DeepSkyBlue;
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
                InteractionToolWindow.logger.Error($"板书模式触摸屏响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"板书模式触摸屏响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "警告");
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
                uint extra = InteractionToolWindow.GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                InteractionToolWindow.logger.Info("讨论模式控件鼠标操作响应");
                int interMode = 3;
                bool result = Update_interaction_info(interMode, InteractionToolWindow.interactionId);
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
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"讨论模式鼠标按键响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 讨论模式按键适配教学一体机触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void discussingMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                InteractionToolWindow.logger.Info("讨论模式控件触摸屏响应");
                int interMode = 3;
                bool result = Update_interaction_info(interMode, InteractionToolWindow.interactionId);
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
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"讨论模式触摸屏响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}");
                MessageBox.Show($"讨论模式触摸屏响应失败,异常信息:{ex.Message}.\r\n异常栈:{ex.StackTrace}", "警告");
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
                uint extra = InteractionToolWindow.GetMessageExtraInfo();
                bool isPen = ((extra & 0xFFFFFF00) == 0xFF515700);
                bool isTouchEvent = ((extra & 0x80) == 0x80);
                if (isTouchEvent || isPen)
                {
                    return;
                }

                InteractionToolWindow.logger.Info("互动模式控件鼠标操作响应");
                this.Dispatcher.Invoke(() =>
                {
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionSelect.png"));
                    interactionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    teachingBtTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                    if (SelectWindowsExit() != null)
                    {
                        SelectWindowsExit().ShowDialog();
                    }
                    else
                    {
                        SelectLecture subView = new SelectLecture(InteractionToolWindow.IP, InteractionToolWindow.Port, InteractionToolWindow.Mac, InteractionToolWindow.interactionId);
                        var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                        subView.Top = desktopWorkingArea.Bottom - subView.Height - 64 - 160;
                        subView.ShowDialog();
                    }
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"互动模式请求失败,错误信息;{ex.Message.ToString()} 错误栈:{ex.StackTrace.ToString()}");
                MessageBox.Show($"互动模式请求失败,错误信息;{ex.Message.ToString()} 错误栈:{ex.StackTrace.ToString()}");
            }
        }

        /// <summary>
        /// 互动模式适配触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void interactionMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    InteractionToolWindow.logger.Info("触摸屏操作互动模式控件");
                    interactionBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/interactionSelect.png"));
                    interactionBtTxt.Foreground = Brushes.DeepSkyBlue;
                    teachingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/teachingUnselect.png"));
                    teachingBtTxt.Foreground = Brushes.AliceBlue;
                    discussingBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/discussingUnselect.png"));
                    discussingBtTxt.Foreground = Brushes.AliceBlue;
                    bBoardWritingBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/bboardwritingUnselect.png"));
                    bBoardWritingTxt.Foreground = Brushes.AliceBlue;
                    if (SelectWindowsExit() != null)
                    {
                        SelectWindowsExit().ShowDialog();
                    }
                    else
                    {
                        SelectLecture subView = new SelectLecture(InteractionToolWindow.IP, InteractionToolWindow.Port, InteractionToolWindow.Mac, InteractionToolWindow.interactionId);
                        var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                        subView.Top = desktopWorkingArea.Bottom - subView.Height - 64 - 160;
                        subView.ShowDialog();
                    }
                });
            }
            catch (Exception ex)
            {
                InteractionToolWindow.logger.Error($"互动模式请求失败,错误信息;{ex.Message.ToString()} 错误栈:{ex.StackTrace.ToString()}");
                MessageBox.Show($"互动模式请求失败,错误信息;{ex.Message.ToString()} 错误栈:{ex.StackTrace.ToString()}");
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

                InteractionToolWindow.logger.Info("全员静音或取消静音控件鼠标操作响应");
                this.Dispatcher.Invoke(() =>
                {
                    if (slienceBtTxt.Text == "全员静音")
                    {
                        if (!string.IsNullOrEmpty(InteractionToolWindow.interactionId))
                        {
                            Slience_All(1, 1);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slienceCancel.png"));
                            slienceBtTxt.Text = "取消静音";
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(InteractionToolWindow.interactionId))
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
                InteractionToolWindow.logger.Error($"全员静音或者取消全员静音失败\n" + $"异常信息:{ex.Message}" + $"异常栈:{ex.StackTrace}");
                MessageBox.Show("全员静音或取消静音请求失败\n" + $"异常信息:{ex.Message}");
            }
        }

        /// <summary>
        /// 全员静音适配触摸屏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void slienceMode_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                this.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    touchTime = DateTime.Now.TimeOfDay;
                    InteractionToolWindow.logger.Info($"静音控件触摸屏响应,touchtime:{touchTime}");
                    if (slienceBtTxt.Text == "全员静音")
                    {
                        if (!string.IsNullOrEmpty(InteractionToolWindow.interactionId))
                        {
                            Slience_All(1, 1);
                            Thread.Sleep(200);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slienceCancel.png"));
                            slienceBtTxt.Text = "取消静音";
                            InteractionToolWindow.logger.Info("取消静音");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(InteractionToolWindow.interactionId))
                        {
                            Slience_All(0, 1);
                            Thread.Sleep(200);
                            slienceBtBg.Source = new BitmapImage(new Uri("pack://application:,,,/images/slience.png"));
                            slienceBtTxt.Text = "全员静音";
                            InteractionToolWindow.logger.Info("全员静音");
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
                InteractionToolWindow.logger.Error($"全员静音或者取消全员静音失败\n" + $"异常信息:{ex.Message}" + $"异常栈:{ex.StackTrace}");
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
    }
}
