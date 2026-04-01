using System;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace WpfApp2
{
    public partial class profile : Page
    {
        public profile()
        {
            InitializeComponent();
        }

        private database db = new database();
        private Autorization au = new Autorization();
        private bool changeData = false;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Name.Text = SaveData.name;
            Role.Text = SaveData.role;
            UpdateData();

            if (SaveData.role == "Учитель")
            {
                SubjectsLabel.Visibility = Visibility.Visible;
                Subjects.Visibility = Visibility.Visible;
                Subjects.Text = string.Join(", ", SaveData.subjects);
            }
        }

        private void UpdateData()
        {
            Login.Text = SaveData.login;
            string pass = SaveData.password;
            if (pass.Length < 2)
                pass = new string('*', pass.Length);
            else
            {
                char first = pass[0];
                char last = pass[pass.Length - 1];
                int middleCount = pass.Length - 2;
                pass = first + new string('*', middleCount) + last;
            }
            Password.Text = pass;
        }

        private void ChangeDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (!changeData)
            {
                Login.Visibility = Visibility.Collapsed;
                Password.Visibility = Visibility.Collapsed;
                InputLogin.Visibility = Visibility.Visible;
                InputPass.Visibility = Visibility.Visible;
                ChangeDataButton.Content = "Сохранить";
            }
            else
            {
                bool hasLoginChange = !string.IsNullOrWhiteSpace(InputLogin.Text);
                bool hasPassChange = !string.IsNullOrWhiteSpace(InputPass.Password);

                if (!hasLoginChange && !hasPassChange)
                {
                    MessageBox.Show("Введите новые данные для изменения", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (hasLoginChange && !SaveNewLogin())
                {
                    return;
                }

                if (hasPassChange)
                {
                    SaveNewPass();
                }

                UpdateData();
                Login.Visibility = Visibility.Visible;
                Password.Visibility = Visibility.Visible;
                InputLogin.Visibility = Visibility.Collapsed;
                InputPass.Visibility = Visibility.Collapsed;
                ChangeDataButton.Content = "Изменить данные";
                InputLogin.Clear();
                InputPass.Clear();
            }
            changeData = !changeData;
        }

        private bool SaveNewPass()
        {
            try
            {
                string newPass = InputPass.Password.Trim();

                if (newPass.Length < 8)
                {
                    MessageBox.Show("Пароль должен быть не менее 8 символов", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    InputPass.Clear();
                    return false;
                }

                if (au.check_Space(newPass))
                {
                    MessageBox.Show("Пароль не может содержать пробелы!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    InputPass.Clear();
                    return false;
                }

                if (newPass == SaveData.password)
                {
                    MessageBox.Show("Новый пароль не должен совпадать с текущим", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    InputPass.Clear();
                    return false;
                }

                string query = @"UPDATE person SET password = @password WHERE person_id = @personId";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@password", newPass),
                    new NpgsqlParameter("@personId", SaveData.id)
                };

                db.ExecuteNonQuery(query, parameters);
                SaveData.password = newPass;
                MessageBox.Show("Пароль успешно изменен", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении пароля: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveNewLogin()
        {
            try
            {
                string newLogin = InputLogin.Text.Trim();

                if (newLogin.Length < 5)
                {
                    MessageBox.Show("Логин должен быть не менее 5 символов", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    InputLogin.Clear();
                    return false;
                }

                if (au.check_Space(newLogin) || au.check_SpecialChars(newLogin))
                {
                    MessageBox.Show("Логин не может содержать пробелы или специальные символы!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    InputLogin.Clear();
                    return false;
                }

                if (newLogin == SaveData.login)
                {
                    MessageBox.Show("Новый логин не должен совпадать с текущим", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    InputLogin.Clear();
                    return false;
                }

                if (!IsLoginAvailable(newLogin))
                {
                    MessageBox.Show("Данный логин уже занят. Пожалуйста, выберите другой.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    InputLogin.Clear();
                    return false;
                }

                string query = @"UPDATE person SET login = @login WHERE person_id = @personId";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@login", newLogin),
                    new NpgsqlParameter("@personId", int.Parse(SaveData.id))
                };

                db.ExecuteNonQuery(query, parameters);
                SaveData.login = newLogin;
                MessageBox.Show("Логин успешно изменен", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении логина: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool IsLoginAvailable(string login)
        {
            try
            {
                string query = @"SELECT COUNT(*) FROM person WHERE login = @login AND person_id != @personId";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@login", login),
                    new NpgsqlParameter("@personId", int.Parse(SaveData.id))
                };

                var dataView = db.ExecuteQuery(query, parameters);
                if (dataView != null && dataView.Count > 0)
                {
                    var row = dataView[0];
                    return Convert.ToInt32(row[0]) == 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке логина: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void InputLogin_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ChangeDataButton_Click(sender, e);
            }
        }

        private void InputPass_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ChangeDataButton_Click(sender, e);
            }
        }
    }
}