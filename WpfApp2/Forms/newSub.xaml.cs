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
        private List<int> teacherIds = new List<int>();
        private List<string> teacherNames = new List<string>();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            teacherIds.Clear();
            teacherNames.Clear();

            OffsetLabel.Visibility = Visibility.Collapsed;
            IsOffset.Visibility = Visibility.Collapsed;

            if (SaveData.isChange)
            {
                NewSabText.Text = "Изменение дисциплины";
                Save.Content = "Изменить";
                Name.Text = SaveData.currentSub;
                LoadCurrentData();
            }

            ReturnButton.Visibility = SaveData.isNewProfile ? Visibility.Visible : Visibility.Collapsed;

            // Загрузка учителей
            var query = @"SELECT person_id, 
                          TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS teacher_name
                          FROM person p
                          WHERE rights = 'Учитель'
                          ORDER BY p.last_name, p.first_name";

            var teachersDB = db.ExecuteQuery(query);
            if (teachersDB != null)
            {
                var teachersList = new List<string>();
                foreach (DataRowView row in teachersDB)
                {
                    teachersList.Add(row["teacher_name"].ToString());
                }
                Teachers.ItemsSource = teachersList;
            }

            // Загрузка классов
            query = "SELECT class_id, class_name FROM class ORDER BY class_name";
            var classesDB = db.ExecuteQuery(query);
            if (classesDB != null)
            {
                List<string> groups = new List<string>();
                foreach (DataRowView row in classesDB)
                {
                    groups.Add(row["class_name"].ToString());
                }
                Groups.ItemsSource = groups;
            }
        }

        private void LoadCurrentData()
        {
            try
            {
                // Загружаем учителей для текущей дисциплины
                string query = @"
                    SELECT p.person_id, 
                           TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS teacher_name,
                           c.class_name
                    FROM class_subject_teacher cst
                    JOIN subject s ON cst.subject_id = s.subject_id
                    JOIN person p ON cst.teacher_id = p.person_id
                    JOIN class c ON cst.class_id = c.class_id
                    WHERE s.title = @subjectTitle";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@subjectTitle", SaveData.currentSub)
                };

                var result = db.DataQuery(query, parameters);

                if (result.Rows.Count > 0)
                {
                    // Сохраняем учителей
                    foreach (DataRow row in result.Rows)
                    {
                        int teacherId = Convert.ToInt32(row["person_id"]);
                        string teacherName = row["teacher_name"].ToString();

                        if (!teacherIds.Contains(teacherId))
                        {
                            teacherIds.Add(teacherId);
                            teacherNames.Add(teacherName);
                        }
                    }

                    // Устанавливаем класс (берем первый, так как все записи для одной дисциплины должны иметь одинаковый класс)
                    string className = result.Rows[0]["class_name"].ToString();
                    Groups.SelectedItem = className;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CheckError())
                return;

            try
            {
                if (SaveData.isChange)
                {
                    UpdateSubject();
                }
                else
                {
                    CreateSubject();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateSubject()
        {
            using (var connection = db.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string subjectTitle = Name.Text.Trim();
                        string className = Groups.SelectedItem.ToString();

                        // 1. Создаем предмет
                        string insertSubjectQuery = @"
                    INSERT INTO subject (title) 
                    VALUES (@title) 
                    RETURNING subject_id";

                        int subjectId;
                        using (var command = new NpgsqlCommand(insertSubjectQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@title", subjectTitle);
                            subjectId = Convert.ToInt32(command.ExecuteScalar());
                        }

                        // 2. Получаем class_id
                        string getClassIdQuery = "SELECT class_id FROM class WHERE class_name = @className";
                        int classId;
                        using (var command = new NpgsqlCommand(getClassIdQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@className", className);
                            var result = command.ExecuteScalar();
                            if (result == null)
                            {
                                throw new Exception("Класс не найден");
                            }
                            classId = Convert.ToInt32(result);
                        }

                        // 3. Для каждого учителя создаем связь класс-предмет-учитель
                        foreach (string teacherName in teacherNames)
                        {
                            int teacherId = GetTeacherId(teacherName);
                            if (teacherId == 0) continue;

                            string insertCstQuery = @"
                        INSERT INTO class_subject_teacher (class_id, subject_id, teacher_id)
                        VALUES (@classId, @subjectId, @teacherId)";

                            using (var command = new NpgsqlCommand(insertCstQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@classId", classId);
                                command.Parameters.AddWithValue("@subjectId", subjectId);
                                command.Parameters.AddWithValue("@teacherId", teacherId);
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        MessageBox.Show("Дисциплина успешно создана!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // Возвращаемся на предыдущую страницу
                        if (NavigationService != null && NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Ошибка при создании дисциплины: {ex.Message}");
                    }
                }
            }
        }

        private void UpdateSubject()
        {
            using (var connection = db.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string subjectTitle = Name.Text.Trim();
                        string className = Groups.SelectedItem.ToString();

                        // 1. Получаем subject_id
                        string getSubjectIdQuery = "SELECT subject_id FROM subject WHERE title = @title";
                        int subjectId;
                        using (var command = new NpgsqlCommand(getSubjectIdQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@title", SaveData.currentSub);
                            var result = command.ExecuteScalar();
                            if (result == null)
                            {
                                throw new Exception("Дисциплина не найдена");
                            }
                            subjectId = Convert.ToInt32(result);
                        }

                        // 2. Если название изменилось, обновляем
                        if (subjectTitle != SaveData.currentSub)
                        {
                            string updateSubjectQuery = "UPDATE subject SET title = @newTitle WHERE subject_id = @subjectId";
                            using (var command = new NpgsqlCommand(updateSubjectQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@newTitle", subjectTitle);
                                command.Parameters.AddWithValue("@subjectId", subjectId);
                                command.ExecuteNonQuery();
                            }
                        }

                        // 3. Получаем class_id
                        string getClassIdQuery = "SELECT class_id FROM class WHERE class_name = @className";
                        int classId;
                        using (var command = new NpgsqlCommand(getClassIdQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@className", className);
                            var result = command.ExecuteScalar();
                            if (result == null)
                            {
                                throw new Exception("Класс не найден");
                            }
                            classId = Convert.ToInt32(result);
                        }

                        // 4. Удаляем старые связи
                        string deleteCstQuery = "DELETE FROM class_subject_teacher WHERE subject_id = @subjectId";
                        using (var command = new NpgsqlCommand(deleteCstQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@subjectId", subjectId);
                            command.ExecuteNonQuery();
                        }

                        // 5. Создаем новые связи
                        foreach (string teacherName in teacherNames)
                        {
                            int teacherId = GetTeacherId(teacherName);
                            if (teacherId == 0) continue;

                            string insertCstQuery = @"
                        INSERT INTO class_subject_teacher (class_id, subject_id, teacher_id)
                        VALUES (@classId, @subjectId, @teacherId)";

                            using (var command = new NpgsqlCommand(insertCstQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@classId", classId);
                                command.Parameters.AddWithValue("@subjectId", subjectId);
                                command.Parameters.AddWithValue("@teacherId", teacherId);
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        MessageBox.Show("Дисциплина успешно обновлена!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // Возвращаемся на предыдущую страницу
                        if (NavigationService != null && NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Ошибка при обновлении дисциплины: {ex.Message}");
                    }
                }
            }
        }

        private int GetTeacherId(string fullName)
        {
            try
            {
                string[] nameParts = ParseFIO(fullName);
                if (nameParts == null) return 0;

                string query = @"
                    SELECT person_id 
                    FROM person 
                    WHERE last_name = @lastName 
                    AND first_name = @firstName 
                    AND rights = 'Учитель'";

                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("@lastName", nameParts[0]),
                    new NpgsqlParameter("@firstName", nameParts[1])
                };

                if (nameParts.Length > 2 && !string.IsNullOrEmpty(nameParts[2]))
                {
                    query += " AND patronymic = @patronymic";
                    parameters.Add(new NpgsqlParameter("@patronymic", nameParts[2]));
                }
                else
                {
                    query += " AND (patronymic IS NULL OR patronymic = '')";
                }

                var result = db.DataQuery(query, parameters.ToArray());
                if (result.Rows.Count > 0)
                {
                    return Convert.ToInt32(result.Rows[0][0]);
                }
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTeacherId Error: {ex.Message}");
                return 0;
            }
        }

        private bool CheckError()
        {
            Error.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(Name.Text))
            {
                Error.Text = "Введите название дисциплины!";
                Error.Visibility = Visibility.Visible;
                return true;
            }

            if (Groups.SelectedItem == null)
            {
                Error.Text = "Выберите класс!";
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

            if (teacherNames.Count == 0)
            {
                Error.Text = "Добавьте хотя бы одного учителя!";
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
                string query = "SELECT COUNT(*) FROM subject WHERE title = @title";
                var parameter = new NpgsqlParameter("@title", title);

                var result = db.DataQuery(query, new[] { parameter });
                if (result != null && result.Rows.Count > 0)
                {
                    int count = Convert.ToInt32(result.Rows[0][0]);
                    return count > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsSubjectExists Error: {ex.Message}");
                return true;
            }
        }

        private void ClearAll()
        {
            Teachers.SelectedItem = null;
            Groups.SelectedItem = null;
            Name.Text = string.Empty;
            teacherIds.Clear();
            teacherNames.Clear();
            Error.Visibility = Visibility.Collapsed;
        }

        private void Teachers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Не добавляем автоматически, только по кнопке Add
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (Teachers.SelectedItem != null)
            {
                string selectedTeacher = Teachers.SelectedItem.ToString();
                if (!teacherNames.Contains(selectedTeacher))
                {
                    teacherNames.Add(selectedTeacher);
                    int teacherId = GetTeacherId(selectedTeacher);
                    if (teacherId != 0)
                    {
                        teacherIds.Add(teacherId);
                    }
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