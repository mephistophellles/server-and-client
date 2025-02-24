using Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace UI.Pages
{
    /// <summary>
    /// Логика взаимодействия для Login.xaml
    /// </summary>
    public partial class Login : Page
    {
        MainWindow init;
        private IPAddress IpAddress;
        private int Port;
        private int UserId = -1;
        private Stack<string> stackDir = new Stack<string>();

        public Login(MainWindow _init)
        {
            InitializeComponent();
            this.init = _init;
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IPAddress.TryParse(tbIp.Text, out IpAddress) && int.TryParse(tbPort.Text, out Port))
            {
                string login = tbLogin.Text;
                string password = tbPassword.Password;
                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Введите логин и пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    var response = CommandSend($"connect {login} {password}");

                    if (response?.Command == "authorization")
                    {
                        UserId = response.UserId;

                        if (UserId == -1)
                        {
                            MessageBox.Show("Не удалось авторизоваться. Проверьте правильность данных!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        MessageBox.Show("Подключение выполнено успешно", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        init.frame.Navigate(new Pages.DirectoryPage(init, IpAddress, Port, UserId));
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка авторизации: {response?.Data ?? "Неизвестная ошибка"}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Неправильный IP или порт", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private ViewModelMessage CommandSend(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);
                    if (socket.Connected)
                    {
                        var request = new ViewModelSend(message, UserId);
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

        public static Socket Conn(IPAddress IpAddress, int Port)
        {
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(endPoint);
                return socket;
            }
            catch (SocketException ex)
            {
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                if (socket != null && !socket.Connected)
                {
                    socket.Close();
                }
            }
            return null;
        }
    }
}
