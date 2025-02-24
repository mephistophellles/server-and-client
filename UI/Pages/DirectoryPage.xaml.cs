using Common;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UI.Pages
{
    public partial class DirectoryPage : Page
    {
        MainWindow init;
        private IPAddress IpAddress;
        private int Port;
        private int UserId = -1;
        private Stack<string> stackDir = new Stack<string>();

        public DirectoryPage(MainWindow _init, IPAddress iadr, int _port, int UsId)
        {
            InitializeComponent();
            this.init = _init;
            this.IpAddress = iadr;
            this.Port = _port;
            this.UserId = UsId;
            DirectoryLoad();
        }
        private void DirectoryLoad()
        {
            try
            {
                var response = CommandSend("cd");
                if (response?.Command == "cd")
                {
                    if (string.IsNullOrEmpty(response.Data))
                    {
                        MessageBox.Show("Список директорий пуст", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    List<string> directori;
                    try
                    {
                        directori = JsonConvert.DeserializeObject<List<string>>(response.Data);
                    }
                    catch (JsonException)
                    {
                        MessageBox.Show("Ошибка при получении директорий: неправильный формат данных", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    list.Items.Clear();

                    if (stackDir.Count > 0)
                    {
                        list.Items.Add("Назад");
                    }

                    foreach (var dir in directori)
                    {
                        list.Items.Add(dir);
                    }
                }
                else
                {
                    MessageBox.Show($"Ошибка загрузки директорий директорий: {response?.Data ?? "Повторите позже"}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Download(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                SendFileToServer(filePath);
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
        private void back(object sender, RoutedEventArgs e)
        {
            init.frame.Navigate(new Pages.Login(init));
        }

        public void SendFileToServer(string filePath)
        {
            try
            {
                var socket = Login.Conn(IpAddress, Port);
                if (socket == null)
                {
                    MessageBox.Show("Не удалось подключиться к серверу!");
                    return;
                }
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Указанный файл не существует.");
                    return;
                }
                FileInfo fileInfo = new FileInfo(filePath);
                FileInfoFTP fileInfoFTP = new FileInfoFTP(File.ReadAllBytes(filePath), fileInfo.Name);
                ViewModelSend viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(fileInfoFTP), UserId);
                byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                socket.Send(messageByte);
                byte[] byffer = new byte[10485760];
                int bytesReceived = socket.Receive(byffer);
                string serverResponse = Encoding.UTF8.GetString(byffer, 0, bytesReceived);
                ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
                socket.Close();
                DirectoryLoad();
                if (responseMessage.Command == "message")
                {
                    MessageBox.Show(responseMessage.Data);
                }
                else
                {
                    MessageBox.Show("Ошибка ответа от сервера!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadFile(string fileName)
        {
            string LocSavePath = GetUniqfilePath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), System.IO.Path.GetFileName(fileName));
            Console.WriteLine($"Trying to download file from server: {fileName}");
            var socket = Login.Conn(IpAddress, Port);
            if (socket == null)
            {
                MessageBox.Show("Не удалось подключиться к серверу.");
                return;
            }
            string command = $"get {fileName}";
            ViewModelSend viewModelSend = new ViewModelSend(command, UserId);
            byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
            socket.Send(messageBytes);
            byte[] byffer = new byte[10485760];
            int bytesReceived = socket.Receive(byffer);
            string serverResponse = Encoding.UTF8.GetString(byffer, 0, bytesReceived);
            ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
            socket.Close();
            if (responseMessage.Command == "file")
            {
                byte[] fileData = JsonConvert.DeserializeObject<byte[]>(responseMessage.Data);
                File.WriteAllBytes(LocSavePath, fileData);
                MessageBox.Show($"Файл скачался! Он сохранён в: {LocSavePath}");
            }
            else MessageBox.Show("Ошибка получения файла. Что-то с путем на сервере?");
        }

        private string GetUniqfilePath(string dir, string fileName)
        {
            string UnfilePath = System.IO.Path.Combine(dir, fileName);
            return UnfilePath;
        }

        private void DownFile(string fileName)
        {
            try
            {
                string LocSavePath = GetUniqfilePath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), System.IO.Path.GetFileName(fileName));
                Console.WriteLine($"Trying to download file from server: {fileName}");
                var socket = Login.Conn(IpAddress, Port);
                if (socket == null)
                {
                    MessageBox.Show("Не удалось подключиться к серверу.");
                    return;
                }
                string command = $"get {fileName}";
                ViewModelSend viewModelSend = new ViewModelSend(command, UserId);
                byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                socket.Send(messageBytes);
                byte[] byffer = new byte[10485760];
                int bytesReceived = socket.Receive(byffer);
                string serverResponse = Encoding.UTF8.GetString(byffer, 0, bytesReceived);
                ViewModelMessage responseMessage = JsonConvert.DeserializeObject<ViewModelMessage>(serverResponse);
                socket.Close();
                if (responseMessage.Command == "file")
                {
                    byte[] fileData = JsonConvert.DeserializeObject<byte[]>(responseMessage.Data);
                    File.WriteAllBytes(LocSavePath, fileData);
                    MessageBox.Show($"Файл скачался! Он сохранён в: {LocSavePath}");
                }
                else MessageBox.Show("Ошибка получения файла. Что-то с путем на сервере?");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void openFolds(object sender, MouseButtonEventArgs e)
        {
            if (list.SelectedItem == null)
                return;

            string selectedItem = list.SelectedItem.ToString();

            if (selectedItem == "Назад")
            {
                if (stackDir.Count > 0)
                {
                    string previouDirectory = stackDir.Pop();

                    var response = CommandSend($"cd {previouDirectory}");

                    if (response?.Command == "cd")
                    {
                        DirectoryLoad();
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка открытия директории: {response?.Data}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Вы в корневой директории. Обратно пути нет(", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (selectedItem.EndsWith("\\"))
            {
                stackDir.Push(selectedItem);
                var response = CommandSend($"cd {selectedItem.TrimEnd('\\')}");

                if (response?.Command == "cd")
                {
                    var items = JsonConvert.DeserializeObject<List<string>>(response.Data);
                    list.Items.Clear();
                    list.Items.Add("Назад");
                    foreach (var item in items)
                    {
                        list.Items.Add(item);
                    }
                }
                else
                {
                    MessageBox.Show($"Ошибка открытия директории: {response?.Data}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                DownFile(selectedItem);
            }
        }
    }
}
