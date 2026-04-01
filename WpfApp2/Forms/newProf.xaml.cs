using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp2
{
    public partial class newProf : Page
    {
        public newProf()
        {
            InitializeComponent();
        }

        private database db = new database();
        private List<string> subjects = new List<string>();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ReturnButton.Visibility = SaveData.isNewProfile ? Visibility.Visible : Visibility.Collapsed;

            try
            {
                var query = @"SELECT DISTINCT title FROM subject ORDER BY title";
                var subjectDB = db.ExecuteQuery(query);
                if (subjectDB != null)
                {
                    var subject = subjectDB.Cast<DataRowView>()
                        .Select(row => row["title"].ToString())
                        .ToList();
                    Subject.ItemsSource = subject;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки предметов: {ex.Message}");
            }

            List<string> types = new List<string>()
            {
                "Администратор",
                "Ученик",
                "Учитель"
            };
            Types.ItemsSource = types;
            subjects.Clear();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string password = PasswordBox.Password;

            if (CheckError(password))
            {
                return;
            }

            string name = Name.Text.Trim();
            string lastName = LastName.Text.Trim();
            string patronymic = Patronymic.Text?.Trim();
            string login = Login.Text.Trim();
            string types = Types.SelectedItem.ToString();

            if (IsPersonExists(name, lastName, patronymic))
            {
                MessageBox.Show($"Пользователь {lastName} {name} {patronymic} уже существует в базе данных!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string message = $"Создать учетную запись {types.ToLower()}а {lastName} {name} {patronymic}?";
            if (types == "Учитель" && subjects.Count > 0)
            {
                message += $"\n\nДисциплины: {string.Join(", ", subjects)}";
            }

            MessageBoxResult result = MessageBox.Show(
                message,
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            string query = $@"INSERT INTO person(first_name, last_name, patronymic, login, password, rights)
                        VALUES(:first_name, :last_name, :patronymic, :login, :password, '{types}')";

            var parameters = new NpgsqlParameter[]
            {
                new NpgsqlParameter(":first_name", name),
                new NpgsqlParameter(":last_name", lastName),
                new NpgsqlParameter(":patronymic", string.IsNullOrEmpty(patronymic) ? (object)DBNull.Value : patronymic),
                new NpgsqlParameter(":login", login),
                new NpgsqlParameter(":password", password)
            };

            try
            {
                db.ExecuteNonQuery(query, parameters);

                if (types == "Учитель" && subjects.Count > 0)
                {
                    MessageBox.Show($"Учитель {lastName} {name} {patronymic} создан.\n" +
                        $"Дисциплины: {string.Join(", ", subjects)}\n\n" +
                        "Не забудьте назначить учителя на классы на странице 'Назначить'",
                        "Информация",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                ClearAll();
                MessageBox.Show("Учетная запись успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании учетной записи: {ex.Message}");
            }
        }

        private bool CheckError(string password)
        {
            Error.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(Name.Text) ||
                string.IsNullOrWhiteSpace(LastName.Text) ||
                string.IsNullOrWhiteSpace(Login.Text) ||
                string.IsNullOrWhiteSpace(password) ||
                Types.SelectedItem == null)
            {
                Error.Text = "Заполните все обязательные поля!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (Login.Text.Trim().Length < 5)
            {
                Error.Text = "Логин должен быть не менее 5 символов!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (password.Length < 8)
            {
                Error.Text = "Пароль должен быть не менее 8 символов!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (check_Space(Login.Text) || check_Space(password))
            {
                Error.Text = "Логин и пароль не должны содержать пробелы!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (check_SpecialChars(Login.Text))
            {
                Error.Text = "Логин содержит недопустимые символы!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (Types.SelectedItem.ToString() == "Учитель" && subjects.Count == 0)
            {
                Error.Text = "Выберите хотя бы одну дисциплину для учителя!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            try
            {
                string query = @"SELECT COUNT(*) FROM person WHERE login = :login";
                var parameter = new NpgsqlParameter(":login", Login.Text.Trim());

                var result = db.ExecuteQuery(query, parameter);
                if (result != null && result.Count > 0 && result[0][0] != null)
                {
                    int count = Convert.ToInt32(result[0][0]);
                    if (count > 0)
                    {
                        Error.Text = "Такой логин уже существует!";
                        Error.Visibility = Visibility.Visible;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки логина: {ex.Message}");
                return true;
            }

            return false;
        }

        private bool IsPersonExists(string firstName, string lastName, string patronymic)
        {
            try
            {
                string query = @"
                SELECT COUNT(*) 
                FROM person 
                WHERE first_name = :first_name 
                  AND last_name = :last_name 
                  AND (patronymic = :patronymic OR (patronymic IS NULL AND :patronymic IS NULL))";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter(":first_name", firstName),
                    new NpgsqlParameter(":last_name", lastName),
                    new NpgsqlParameter(":patronymic", string.IsNullOrEmpty(patronymic) ? (object)DBNull.Value : patronymic)
                };

                var result = db.ExecuteQuery(query, parameters);
                if (result != null && result.Count > 0 && result[0][0] != null)
                {
                    return Convert.ToInt32(result[0][0]) > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки пользователя: {ex.Message}");
                return false;
            }
        }

        public bool check_Space(string input) => input.Any(ch => char.IsWhiteSpace(ch));

        public bool check_SpecialChars(string input) =>
            input.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_' && ch != '-');

        private void ClearAll()
        {
            Name.Text = string.Empty;
            Login.Text = string.Empty;
            PasswordBox.Clear();
            Types.SelectedItem = null;
            LastName.Text = string.Empty;
            Patronymic.Text = string.Empty;
            Subject.SelectedItem = null;
            subjects.Clear();
            SubText.Visibility = Visibility.Collapsed;
            SubjectsPanel.Visibility = Visibility.Collapsed;
            Error.Visibility = Visibility.Collapsed;
        }

        private void Types_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Types.SelectedItem != null)
            {
                bool isTeacher = Types.SelectedItem.ToString() == "Учитель";
                SubText.Visibility = isTeacher ? Visibility.Visible : Visibility.Collapsed;
                SubjectsPanel.Visibility = isTeacher ? Visibility.Visible : Visibility.Collapsed;

                if (!isTeacher)
                {
                    subjects.Clear();
                }
            }
        }

        private void Subject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Subject.SelectedItem != null && Types.SelectedItem?.ToString() == "Учитель")
            {
                string selectedSubject = Subject.SelectedItem.ToString();
                if (!subjects.Contains(selectedSubject))
                {
                    subjects.Add(selectedSubject);
                }
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (Subject.SelectedItem != null)
            {
                string selectedSubject = Subject.SelectedItem.ToString();
                if (!subjects.Contains(selectedSubject))
                {
                    subjects.Add(selectedSubject);
                    MessageBox.Show($"Дисциплина '{selectedSubject}' добавлена в список", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Дисциплина '{selectedSubject}' уже добавлена", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Выберите дисциплину из списка", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void newSub_Click(object sender, RoutedEventArgs e)
        {
            SaveData.isNewProfile = true;
            ((MainForm)Window.GetWindow(this)).OpenNewSub();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            SaveData.isNewProfile = false;
            ((MainForm)Window.GetWindow(this)).OpenNewSub();
        }

        private void Name_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Zа-яА-Я]+$");

        private void Login_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Z0-9_-]+$");
    }
}