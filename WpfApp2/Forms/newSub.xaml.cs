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
    public partial class newSub : Page
    {
        public newSub()
        {
            InitializeComponent();
        }

        private database db = new database();
        private List<string> teachers = new List<string>();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            teachers.Clear();

            OffsetLabel.Visibility = Visibility.Collapsed;
            IsOffset.Visibility = Visibility.Collapsed;

            if (SaveData.isChange)
            {
                NewSabText.Text = "Изменение дисциплины";
                Save.Content = "Изменить";
                Name.Text = SaveData.currentSub;
                LoadCurrentTeachers();
            }

            ReturnButton.Visibility = SaveData.isNewProfile ? Visibility.Visible : Visibility.Collapsed;

            var query = @"SELECT TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS teacher_name
            FROM person p
            WHERE rights = 'Учитель'
            ORDER BY p.last_name, p.first_name";

            var teachersDB = db.ExecuteQuery(query);
            if (teachersDB != null)
            {
                var teachersList = teachersDB.Cast<DataRowView>().Select(row => row["teacher_name"].ToString()).ToList();
                Teachers.ItemsSource = teachersList;
            }

            query = "SELECT * FROM class";
            var classesDB = db.ExecuteQuery(query);
            if (classesDB != null)
            {
                List<string> groups = classesDB.Cast<DataRowView>().Select(row => row["class_name"].ToString()).ToList();
                Groups.ItemsSource = groups;
            }

        }

        private void LoadCurrentTeachers()
        {
            try
            {
                string query = $@"
                SELECT DISTINCT TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS teacher_name
                FROM class_subject_teacher cst
                JOIN subject s ON cst.subject_id = s.subject_id
                JOIN person p ON cst.teacher_id = p.person_id
                WHERE s.title = '{SaveData.currentSub}'";

                var teachersDB = db.ExecuteQuery(query);
                if (teachersDB != null)
                {
                    teachers.Clear();
                    foreach (DataRowView row in teachersDB)
                    {
                        string teacher = row["teacher_name"].ToString();
                        if (!teachers.Contains(teacher))
                        {
                            teachers.Add(teacher);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки учителей: {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CheckError())
            {
                return;
            }

            bool isOffset = false;
            string title = Name.Text.Trim();
            string group = Groups.SelectedItem.ToString();
            string listTeacher = string.Join("\n", teachers);

            string action = SaveData.isChange ? "изменена" : "создана";
            MessageBoxResult result = MessageBox.Show(
                $"{char.ToUpper(action[0])}{action.Substring(1)} дисциплину \"{title}\"\n\nУчителя:\n{listTeacher}\n\nСпециальность: {group}",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (SaveData.isChange)
                    {
                        string deleteNotesQuery = $@"
                            DELETE FROM notes 
                            WHERE cst_id IN (
                                SELECT cst_id 
                                FROM class_subject_teacher cst
                                JOIN subject s ON cst.subject_id = s.subject_id
                                WHERE s.title = '{SaveData.currentSub}'
                            )";
                        db.ExecuteNonQuery(deleteNotesQuery);

                        string deleteDailyNotesQuery = $@"
                            DELETE FROM daily_notes 
                            WHERE cst_id IN (
                                SELECT cst_id 
                                FROM class_subject_teacher cst
                                JOIN subject s ON cst.subject_id = s.subject_id
                                WHERE s.title = '{SaveData.currentSub}'
                            )";
                        db.ExecuteNonQuery(deleteDailyNotesQuery);

                        string deleteCstQuery = $@"
                            DELETE FROM class_subject_teacher 
                            WHERE subject_id = (SELECT subject_id FROM subject WHERE title = '{SaveData.currentSub}')";
                        db.ExecuteNonQuery(deleteCstQuery);

                        string deleteSubjectQuery = $@"DELETE FROM subject WHERE title = '{SaveData.currentSub}'";
                        db.ExecuteNonQuery(deleteSubjectQuery);
                    }

                    string insertSubjectQuery = @"
                        INSERT INTO subject (title, subject_group)
                        VALUES (@title, @group)";

                    var subjectParams = new NpgsqlParameter[]
                    {
                        new NpgsqlParameter("@title", title),
                        new NpgsqlParameter("@group", group)
                    };

                    db.ExecuteNonQuery(insertSubjectQuery, subjectParams);

                    string getSubjectIdQuery = @"SELECT subject_id FROM subject WHERE title = @title";
                    var subjectIdParam = new NpgsqlParameter("@title", title);
                    var subjectResult = db.ExecuteQuery(getSubjectIdQuery, subjectIdParam);

                    if (subjectResult != null && subjectResult.Count > 0)
                    {
                        int subjectId = Convert.ToInt32(subjectResult[0]["subject_id"]);

                        if (teachers.Count > 0)
                        {
                            MessageBox.Show($"Дисциплина \"{title}\" успешно {action}!\n\n" +
                                $"Назначенные учителя: {string.Join(", ", teachers)}\n\n" +
                                "Не забудьте назначить учителей на конкретные классы на странице 'Назначить'",
                                "Информация",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }

                    ClearAll();

                    if (SaveData.isChange)
                    {
                        SaveData.isChange = false;
                        SaveData.currentSub = null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool CheckError()
        {
            Error.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(Name.Text) || Groups.SelectedItem == null)
            {
                Error.Text = "Заполните все обязательные поля!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            string title = Name.Text.Trim();

            if (title.Length < 3 || title.Length > 100)
            {
                Error.Text = "Название дисциплины должно быть от 3 до 100 символов!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (ContainsSpecialChars(title))
            {
                Error.Text = "Название содержит недопустимые символы!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (teachers.Count == 0)
            {
                Error.Text = "Выберите хотя бы одного учителя!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            foreach (string teacher in teachers)
            {
                var parts = ParseFIO(teacher);
                if (parts == null || parts.Length < 2)
                {
                    Error.Text = "Некорректное ФИО учителя!";
                    Error.Visibility = Visibility.Visible;
                    return true;
                }
            }

            if (teachers.Count != teachers.Distinct().Count())
            {
                Error.Text = "Учитель выбран более одного раза!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (!SaveData.isChange)
            {
                if (IsSubjectExists(title))
                {
                    Error.Text = "Дисциплина с таким названием уже существует!";
                    Error.Visibility = Visibility.Visible;
                    return true;
                }
            }

            return false;
        }

        private string[] ParseFIO(string fio)
        {
            if (string.IsNullOrWhiteSpace(fio))
                return null;

            var parts = fio.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return null;

            string lastName = parts[0];
            string firstName = parts[1];
            string patronymic = parts.Length > 2 ? parts[2] : null;

            return new[] { lastName, firstName, patronymic };
        }

        private bool ContainsSpecialChars(string input)
        {
            return Regex.IsMatch(input, @"[^\p{L}\p{N}\s\-\(\)\,\.]");
        }

        private bool IsSubjectExists(string title)
        {
            try
            {
                string query = @"SELECT COUNT(*) FROM subject WHERE title = :title";
                var parameter = new NpgsqlParameter("title", title);

                var result = db.ExecuteQuery(query, parameter);
                if (result != null && result.Count > 0 && result[0][0] != null)
                {
                    int count = Convert.ToInt32(result[0][0]);
                    return count > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки дисциплины: {ex.Message}");
                return true;
            }
        }

        private void ClearAll()
        {
            Teachers.SelectedItem = null;
            Groups.SelectedItem = null;
            Name.Text = string.Empty;
            teachers.Clear();
            Error.Visibility = Visibility.Collapsed;
        }

        private void Teachers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Teachers.SelectedItem != null)
            {
                string selectedTeacher = Teachers.SelectedItem.ToString();
                if (!teachers.Contains(selectedTeacher))
                {
                    teachers.Add(selectedTeacher);
                }
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (Teachers.SelectedItem != null)
            {
                string selectedTeacher = Teachers.SelectedItem.ToString();
                if (!teachers.Contains(selectedTeacher))
                {
                    teachers.Add(selectedTeacher);
                    MessageBox.Show($"Учитель '{selectedTeacher}' добавлен в список", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Учитель '{selectedTeacher}' уже в списке", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Выберите учителя из списка", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void newProf_Click(object sender, RoutedEventArgs e)
        {
            SaveData.isNewProfile = true;
            ((MainForm)Window.GetWindow(this)).OpenNewProf();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            SaveData.isNewProfile = false;
            ((MainForm)Window.GetWindow(this)).OpenNewProf();
        }

        private void Name_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Name.Text.Length > 100)
            {
                Error.Text = "Название не должно превышать 100 символов!";
                Error.Visibility = Visibility.Visible;
            }
            else if (Error.Visibility == Visibility.Visible && Name.Text.Length >= 3)
            {
                Error.Visibility = Visibility.Collapsed;
            }
        }
    }
}