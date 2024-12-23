namespace Server
{
    public class User
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public string Src { get; set; }
        public string Temp_Src { get; set; }
        public User(string login, string password, string src)
        {
            Login = login;
            Password = password;
            Src = src;
            Temp_Src = src;
        }

    }
}
