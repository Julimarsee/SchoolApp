using Npgsql;
using System;
using System.Data;
using System.Linq;
using System.Windows;

namespace WpfApp2
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class Autorization : Window
    {
        public Autorization()
        {
            InitializeComponent();
        }

        private string login;
        private string password;
        private database db = new database();

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                Error.Visibility = Visibility.Visible;
                login = loginText.Text;
                password = parolText.Text;
                if ((check_SpecialChars(login) || check_Space(login)) || check_Space(password)) return;
                else Check_data_exists(login, password);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Check_data_exists(string login, string password)
        {
            try
            {
                IsFalse.Visibility = Visibility.Collapsed;
                IsParol.Visibility = Visibility.Collapsed;
                Err.Visibility = Visibility.Collapsed;
                IsAll.Visibility = Visibility.Collapsed;

                if (loginText.Text.Length > 0)
                    if (parolText.Text.Length > 0)
                        check_correct_data(loginText.Text, parolText.Text);
                    else
                    {
                        Err.Visibility = Visibility.Visible;
                        IsParol.Visibility = Visibility.Visible;
                    }
                else
                {
                    Err.Visibility = Visibility.Visible;
                    IsAll.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public bool check_Space(string input) => input.Any(ch => char.IsWhiteSpace(ch));
        public bool check_SpecialChars(string input) => input.Any(ch => !char.IsLetterOrDigit(ch));

        private void check_correct_data(string login, string password)
        {
            string login_from_db = "";

            string query_login = "SELECT login FROM person WHERE login = @login AND password = @password";
            var param1 = new NpgsqlParameter("@login", login);
            var param2 = new NpgsqlParameter("@password", password);
            DataView result_login = db.ExecuteQuery(query_login, param1, param2);

            if (result_login != null && result_login.Count > 0)
            {
                string dbLogin = result_login[0]["login"].ToString();
                login_from_db = dbLogin;
                SaveData.login = login_from_db;
            }
            else IsFalse.Visibility = Visibility.Visible;

            string query = "SELECT password FROM person WHERE login = @login";
            var param3 = new NpgsqlParameter("@login", login);
            DataView result = db.ExecuteQuery(query, param3);

            if (result != null && result.Count > 0)
            {
                string dbPassword = result[0]["password"].ToString();

                if (dbPassword == password)
                {
                    this.Hide();
                    MainForm window = new MainForm();
                    SaveData.password = dbPassword;
                    getData(login_from_db);
                    window.Show();
                    window.Activate();
                }
                else IsFalse.Visibility = Visibility.Visible;
            }
            else IsFalse.Visibility = Visibility.Visible;
        }

        private void getData(string login)
        {
            string role_from_db;
            string query = "SELECT rights FROM person WHERE login = @login";
            var param = new NpgsqlParameter("@login", login);
            DataView result = db.ExecuteQuery(query, param);

            if (result != null && result.Count > 0)
            {
                role_from_db = result[0]["rights"].ToString();
                SaveData.role = role_from_db;

                query = $"SELECT person_id FROM person WHERE login = '{login}'";
                result = db.ExecuteQuery(query);
                SaveData.id = result[0]["person_id"].ToString();

                // Для учителя получаем список предметов, которые он ведет
                if (role_from_db == "Учитель")
                {
                    query = @"
                        SELECT DISTINCT s.title 
                        FROM subject s
                        JOIN class_subject_teacher cst ON s.subject_id = cst.subject_id
                        WHERE cst.teacher_id = @teacherId";

                    var teacherParam = new NpgsqlParameter("@teacherId", Convert.ToInt32(SaveData.id));
                    result = db.ExecuteQuery(query, teacherParam);

                    SaveData.subjects.Clear();
                    foreach (DataRowView row in result)
                    {
                        string title = row["title"].ToString()?.Trim();
                        if (!string.IsNullOrEmpty(title) && !SaveData.subjects.Contains(title))
                            SaveData.subjects.Add(title);
                    }

                    foreach (string sub in SaveData.subjects)
                        Console.WriteLine(sub);
                }

                // Получаем ФИО пользователя
                query = "SELECT TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS full_name " +
                        $"FROM person p WHERE p.person_id = {SaveData.id}";
                result = db.ExecuteQuery(query);
                SaveData.name = result[0]["full_name"].ToString();

                Console.WriteLine(SaveData.id);
                Console.WriteLine(SaveData.name);
                Console.WriteLine(SaveData.role);
            }
        }

        private void Viewbox_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            Close();

        private void Viewbox_MouseLeftButtonDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            MessageBox.Show(@"Чтобы войти в аккаунт, введите логин и пароль от администратора в соответствующие поля, и нажмите кнопку 'Вход'");

        private void TextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (parolText.Visibility == Visibility.Visible)
            {
                parolText.Visibility = Visibility.Collapsed;
                PassText.Visibility = Visibility.Visible;
                MaskText.Text = "➖";
            }
            else
            {
                parolText.Visibility = Visibility.Visible;
                PassText.Visibility = Visibility.Collapsed;
                MaskText.Text = "👁️‍🗨️";
            }
        }

        private void PassText_PasswordChanged(object sender, RoutedEventArgs e)
        {
            parolText.Text = PassText.Password;
        }

        private void parolText_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            PassText.Password = parolText.Text;
        }
    }
}