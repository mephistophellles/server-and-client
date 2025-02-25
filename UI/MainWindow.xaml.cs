using Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace UI
{

    public partial class MainWindow : Window
    {
        public static MainWindow init;
        public IPAddress ipAddress;
        public int port;
        public int userId = -1;
        public Stack<string> directoryStack = new Stack<string>();

        public MainWindow()
        {
            InitializeComponent();
            init = this;
            OpenPage(new Pages.Login());
        }
        public void OpenPage(Page page)
        {
            DoubleAnimation startAnimation = new DoubleAnimation();
            startAnimation.From = 1;
            startAnimation.To = 0;
            startAnimation.Duration = TimeSpan.FromSeconds(0.1);
            startAnimation.Completed += delegate
            {
                frame.Navigate(page);
                DoubleAnimation endAnimation = new DoubleAnimation();
                endAnimation.From = 0;
                endAnimation.To = 1;
                endAnimation.Duration = TimeSpan.FromSeconds(0.1);
                frame.BeginAnimation(OpacityProperty, endAnimation);
            };
            frame.BeginAnimation(OpacityProperty, startAnimation);
        }
        public ViewModelMessage SendCommand(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);
                    if (socket.Connected)
                    {
                        var request = new ViewModelSend(message, userId);
                        byte[] requestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                        socket.Send(requestBytes);

                        byte[] responseBytes = new byte[10485760];
                        int receivedBytes = socket.Receive(responseBytes);
                        string responseData = Encoding.UTF8.GetString(responseBytes, 0, receivedBytes);

                        return JsonConvert.DeserializeObject<ViewModelMessage>(responseData);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка соединения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }
    }
}
