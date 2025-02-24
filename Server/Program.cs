using Common;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        private static string connect = "server=127.0.0.1;port=3315;Database=pr4;uid=root;";
        public static List<User> Users = new List<User>();
        public static IPAddress IpAddress;
        public static int Port;
        public static int User;
        static void Main(string[] args)
        {
            LoadToDatabase();
            Console.ForegroundColor = ConsoleColor.White;
            //Users.Add(new User("dasshh", "asd", @"C:\Users\dassshhh\Desktop\Ftp_rotanova"));
            Console.Write("Введите IP адрес сервера: ");
            string sIpAdress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            //провер, что поль-ль ввел адрес и порт корректно
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAdress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Запускаю сервер");
                StartServer();
            }

            Console.Read();
        }

        private static void LoadToDatabase()
        {
            using (MySqlConnection connection = new MySqlConnection(connect))
            {
                connection.Open();
                string query = "SELECT Id, Login, Password, Src FROM Users";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader read = command.ExecuteReader())
                    {
                        while (read.Read())
                        {
                            int Id = read.GetInt32("Id");
                            string Login = read["Login"].ToString();
                            string Pass = read["Password"].ToString();
                            string Src = read["Src"].ToString();
                            Users.Add(new User(Id, Login, Pass, Src));
                        }
                    }
                }
            }
        }

        private static void LoadCommandToDatabase(int UserId, string Command)
        {
            string connect = "server=127.0.0.1;port=3315;Database=pr4;uid=root;";

            using (MySqlConnection connection = new MySqlConnection(connect))
            {
                connection.Open();
                string query = "INSERT INTO Commands ( Command, Data, UserId) VALUES ( @Command, @Data, @UserId)";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", UserId);
                    command.Parameters.AddWithValue("@Command", Command);
                    command.Parameters.AddWithValue("@Data", DateTime.Now);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static bool AutorizationUser(string login, string password, out int userId)
        {
            userId = -1;
            User user = Users.Find(x => x.login == login && x.password == password);
            if (user != null)
            {
                userId = user.id;
                return true;
            }
            return false;
        }

        public static bool AuthenUser(string login, string password)
        {
            using (MySqlConnection cn = new MySqlConnection(connect))
            {
                cn.Open();
                string query = "SELECT COUNT(*) FROM Users WHERE Login = @Login AND Password = @Password";
                using (MySqlCommand cm = new MySqlCommand(query, cn))
                {
                    cm.Parameters.AddWithValue("@Login", login);
                    cm.Parameters.AddWithValue("@Password", password);
                    int count = Convert.ToInt32(cm.ExecuteScalar());
                    return count > 0;
                }
            }
        }
        /// <summary>
        /// Получение директорий
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static List<string> GetDirectory(string src)
        {
            List<string> FolderFiles = new List<string>();
            if (Directory.Exists(src))
            {
                string[] dirs = Directory.GetDirectories(src);
                foreach (var dir in dirs)
                {
                    string NameDirectory = dir.Replace(src, "");
                    FolderFiles.Add(NameDirectory + "/");
                }
                string[] files = Directory.GetFiles(src);
                foreach (var file in files)
                {
                    string NameFile = file.Replace(src, "");
                    FolderFiles.Add(NameFile);
                }
            }

            return FolderFiles;
        }

        private static void NewHandler(Socket Handler)
        {
            try
            {
                string Data = null;
                byte[] Bytes = new byte[10485760];
                int BytesRec = Handler.Receive(Bytes);
                Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                Console.Write("Сообщение от пользователя: " + Data + "\n");
                string Reply = "";
                ViewModelSend ViewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);
                if (ViewModelSend != null)
                {
                    ViewModelMessage viewModelMessage;
                    string[] DataCommand = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                    if (DataCommand[0] == "connect")
                    {
                        string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                        if (AutorizationUser(DataMessage[1], DataMessage[2], out int UserId))
                        {
                            UserId = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).id;
                            User = UserId;
                            viewModelMessage = new ViewModelMessage("authorization", Data, UserId);
                            string nameUser = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).login;
                            string pass = Users.Find(x => x.login == DataMessage[1] && x.password == DataMessage[2]).password;
                            LoadCommandToDatabase(UserId, ViewModelSend.Message.Split(' ')[0]);
                        }
                        else
                        {
                            viewModelMessage = new ViewModelMessage("message", "Неправильный логин и пароль пользователя!", UserId);
                        }
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else if (DataCommand[0] == "cd")
                    {
                        if (ViewModelSend.Id != -1)
                        {
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            List<string> FoldersFiles = new List<string>();
                            if (DataMessage.Length == 1)
                            {
                                Users[ViewModelSend.Id - 1].temp_src = Users[ViewModelSend.Id - 1].src;
                                FoldersFiles = GetDirectory(Users[ViewModelSend.Id - 1].src);
                            }
                            else
                            {
                                string cdFolder = string.Join(" ", DataMessage.Skip(1));
                                Console.WriteLine(cdFolder);
                                if (cdFolder.Equals(Users[ViewModelSend.Id - 1].src))
                                {
                                    Users[ViewModelSend.Id - 1].temp_src = Users[ViewModelSend.Id - 1].src;
                                    FoldersFiles = GetDirectory(Users[ViewModelSend.Id - 1].temp_src);
                                }
                                else if (cdFolder.Contains(Users[ViewModelSend.Id - 1].temp_src))
                                {
                                    Users[ViewModelSend.Id - 1].temp_src = Path.Combine(Users[ViewModelSend.Id - 1].temp_src, cdFolder);
                                    FoldersFiles = GetDirectory(Users[ViewModelSend.Id - 1].temp_src);
                                }
                            }

                            if (FoldersFiles.Count == 0)
                                viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует", User);
                            else viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles), User);
                        }
                        else
                            viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться", User);

                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else if (DataCommand[0] == "get")
                    {
                        if (ViewModelSend.Id != -1)
                        {
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            string getFile = string.Join(" ", DataMessage.Skip(1));
                            string fullFilePath = Path.Combine(Users[ViewModelSend.Id - 1].temp_src, getFile);
                            Console.WriteLine($"Попытка получения доступа к файлу: {fullFilePath}");
                            if (File.Exists(fullFilePath))
                            {
                                byte[] byteFile = File.ReadAllBytes(fullFilePath);
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile), User);
                                string nameUser = Users[ViewModelSend.Id - 1].login;
                                string pass = Users[ViewModelSend.Id - 1].password;
                                var UserId = Users.Find(x => x.login == nameUser && x.password == pass).id;
                                LoadCommandToDatabase(UserId, "get");
                            }
                            else viewModelMessage = new ViewModelMessage("message", "Файл на сервере не найден!", User);
                        }
                        else viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться", User);

                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                    else
                    {
                        if (ViewModelSend.Id != -1)
                        {
                            FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                            string pathSave = Path.Combine(Users[ViewModelSend.Id - 1].temp_src, SendFileInfo.Name);
                            File.WriteAllBytes(pathSave, SendFileInfo.Data);
                            viewModelMessage = new ViewModelMessage("message", "Файл загружен", User);
                        }
                        else viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться", User);
                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                        byte[] message = Encoding.UTF8.GetBytes(Reply);
                        Handler.Send(message);
                    }
                }
            }
            catch (Exception exp)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибка на клиенте: " + exp.Message);
            }
            finally
            {
                Handler.Shutdown(SocketShutdown.Both);
                Handler.Close();
            }
        }

        public static void StartServer()
        {   //созд конечную точку, сост из айпи и порта
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
            Socket sListener = new Socket( //созд сокет для прослушки
                AddressFamily.InterNetwork, //схема адресации, использующая сокет IPv4
                SocketType.Stream, //тип сокета, двоичный код
                ProtocolType.Tcp); //протокол сокета
            sListener.Bind(endPoint); //связ сокет и кон точку
            sListener.Listen(10); //указ кол-во входящих подключений
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен");
            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    Task.Run(() => NewHandler(Handler));
                }
                catch (Exception exp)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Ошибка соединения: " + exp.Message);
                }
            }
        }
    }
}
