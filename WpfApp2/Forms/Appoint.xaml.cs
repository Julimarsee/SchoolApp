using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp2
{
    /// <summary>
    /// Логика взаимодействия для Appoint.xaml
    /// </summary>
    public partial class Appoint : Page
    {
        public Appoint()
        {
            InitializeComponent();
        }

        private database db = new database();

        private void AddDist_Click(object sender, RoutedEventArgs e) =>
            ((MainForm)Window.GetWindow(this)).OpenNewSub();

        private void AddProf_Click(object sender, RoutedEventArgs e) =>
            ((MainForm)Window.GetWindow(this)).OpenNewProf();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClasses();
            LoadSubjects();
            LoadTeachers();
        }

        private void LoadClasses()
        {
            try
            {
                string classesQuery = "SELECT class_id, class_name FROM class ORDER BY class_name";
                var classesResult = db.ExecuteQuery(classesQuery);
                if (classesResult != null && classesResult.Count > 0)
                {
                    var classes = classesResult.Cast<DataRowView>()
                        .Select(row => new ClassItem
                        {
                            ClassId = Convert.ToInt32(row["class_id"]),
                            ClassName = row["class_name"].ToString()
                        })
                        .ToList();
                    Classes.ItemsSource = classes;
                    Classes.DisplayMemberPath = "ClassName";
                    Classes.SelectedValuePath = "ClassId";
                }
                else
                {
                    Classes.ItemsSource = new List<ClassItem>();
                    InfoText.Text = "Нет доступных классов. Обратитесь к администратору.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки классов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSubjects()
        {
            try
            {
                List<string> subjects = new List<string>();
                string query = "SELECT title FROM subject ORDER BY title";
                var res = db.ExecuteQuery(query);

                if (res != null && res.Count > 0)
                {
                    foreach (DataRowView row in res)
                    {
                        string title = row["title"].ToString()?.Trim();
                        if (!string.IsNullOrEmpty(title) && !subjects.Contains(title))
                            subjects.Add(title);
                    }
                }
                Subjects.ItemsSource = subjects;

                if (subjects.Count == 0)
                {
                    InfoText.Text = "Нет доступных дисциплин. Создайте новую дисциплину.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки предметов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTeachers()
        {
            try
            {
                string query = @"SELECT TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS teacher_name,
                                     p.person_id
                              FROM person p
                              WHERE p.rights = 'Учитель'
                              ORDER BY p.last_name, p.first_name";

                var teachersDB = db.ExecuteQuery(query);
                if (teachersDB != null && teachersDB.Count > 0)
                {
                    var teachers = teachersDB.Cast<DataRowView>()
                        .Select(row => new TeacherItem
                        {
                            TeacherId = Convert.ToInt32(row["person_id"]),
                            TeacherName = row["teacher_name"].ToString()
                        })
                        .ToList();
                    Teachers.ItemsSource = teachers;
                    Teachers.DisplayMemberPath = "TeacherName";
                    Teachers.SelectedValuePath = "TeacherId";
                }
                else
                {
                    Teachers.ItemsSource = new List<TeacherItem>();
                    InfoText.Text = "Нет доступных учителей. Создайте нового учителя.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки учителей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Classes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Classes.SelectedItem != null)
            {
                var selectedClass = Classes.SelectedItem as ClassItem;
                if (selectedClass != null)
                {
                    ClassError.Visibility = Visibility.Collapsed;
                    UpdateInfoText();
                }
            }
        }

        private void Subjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Subjects.SelectedItem != null)
            {
                SubjectError.Visibility = Visibility.Collapsed;
                UpdateInfoText();
            }
        }

        private void Teachers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Teachers.SelectedItem != null)
            {
                TeacherError.Visibility = Visibility.Collapsed;
                UpdateInfoText();
            }
        }

        private void UpdateInfoText()
        {
            string className = Classes.SelectedItem != null ? (Classes.SelectedItem as ClassItem)?.ClassName : "не выбран";
            string subjectName = Subjects.SelectedItem != null ? Subjects.SelectedItem.ToString() : "не выбран";
            string teacherName = Teachers.SelectedItem != null ? (Teachers.SelectedItem as TeacherItem)?.TeacherName : "не выбран";

            InfoText.Text = $"Класс: {className} | Предмет: {subjectName} | Учитель: {teacherName}";
        }

        private void AppointButton_Click(object sender, RoutedEventArgs e)
        {
            bool hasError = false;

            if (Classes.SelectedItem == null)
            {
                ClassError.Visibility = Visibility.Visible;
                hasError = true;
            }
            else
            {
                ClassError.Visibility = Visibility.Collapsed;
            }

            if (Subjects.SelectedItem == null)
            {
                SubjectError.Visibility = Visibility.Visible;
                hasError = true;
            }
            else
            {
                SubjectError.Visibility = Visibility.Collapsed;
            }

            if (Teachers.SelectedItem == null)
            {
                TeacherError.Visibility = Visibility.Visible;
                hasError = true;
            }
            else
            {
                TeacherError.Visibility = Visibility.Collapsed;
            }

            if (hasError)
            {
                MessageBox.Show("Пожалуйста, заполните все поля!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedClass = Classes.SelectedItem as ClassItem;
            var subjectTitle = Subjects.SelectedItem.ToString();
            var selectedTeacher = Teachers.SelectedItem as TeacherItem;

            if (selectedClass == null || selectedTeacher == null)
            {
                MessageBox.Show("Ошибка выбора данных!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Получаем subject_id по названию
                string getSubjectIdQuery = @"SELECT subject_id FROM subject WHERE title = @title";
                var subjectIdParam = new Npgsql.NpgsqlParameter("@title", subjectTitle);
                var subjectResult = db.ExecuteQuery(getSubjectIdQuery, subjectIdParam);

                if (subjectResult == null || subjectResult.Count == 0)
                {
                    MessageBox.Show("Дисциплина не найдена!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int subjectId = Convert.ToInt32(subjectResult[0]["subject_id"]);

                // Проверяем, существует ли уже такая связь
                string checkQuery = @"
                    SELECT COUNT(*) 
                    FROM class_subject_teacher 
                    WHERE class_id = @classId 
                      AND subject_id = @subjectId";

                var checkParams = new Npgsql.NpgsqlParameter[]
                {
                    new Npgsql.NpgsqlParameter("@classId", selectedClass.ClassId),
                    new Npgsql.NpgsqlParameter("@subjectId", subjectId)
                };

                var checkResult = db.ExecuteQuery(checkQuery, checkParams);
                if (checkResult != null && checkResult.Count > 0 && Convert.ToInt32(checkResult[0][0]) > 0)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"В классе {selectedClass.ClassName} уже назначен учитель на предмет {subjectTitle}!\n\n" +
                        "Хотите заменить существующего учителя?",
                        "Подтверждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Обновляем существующую связь
                        string updateQuery = @"
                            UPDATE class_subject_teacher 
                            SET teacher_id = @teacherId
                            WHERE class_id = @classId AND subject_id = @subjectId";

                        var updateParams = new Npgsql.NpgsqlParameter[]
                        {
                            new Npgsql.NpgsqlParameter("@classId", selectedClass.ClassId),
                            new Npgsql.NpgsqlParameter("@subjectId", subjectId),
                            new Npgsql.NpgsqlParameter("@teacherId", selectedTeacher.TeacherId)
                        };

                        db.ExecuteNonQuery(updateQuery, updateParams);
                        MessageBox.Show($"Учитель {selectedTeacher.TeacherName} успешно заменен на предмет '{subjectTitle}' в классе {selectedClass.ClassName}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                // Создаем новую связь класс-предмет-учитель
                string insertQuery = @"
                    INSERT INTO class_subject_teacher (class_id, subject_id, teacher_id)
                    VALUES (@classId, @subjectId, @teacherId)";

                var insertParams = new Npgsql.NpgsqlParameter[]
                {
                    new Npgsql.NpgsqlParameter("@classId", selectedClass.ClassId),
                    new Npgsql.NpgsqlParameter("@subjectId", subjectId),
                    new Npgsql.NpgsqlParameter("@teacherId", selectedTeacher.TeacherId)
                };

                db.ExecuteNonQuery(insertQuery, insertParams);
                MessageBox.Show($"Учитель {selectedTeacher.TeacherName} успешно назначен на предмет '{subjectTitle}' в классе {selectedClass.ClassName}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при назначении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Вспомогательные классы для хранения данных
        private class ClassItem
        {
            public int ClassId { get; set; }
            public string ClassName { get; set; }
        }

        private class TeacherItem
        {
            public int TeacherId { get; set; }
            public string TeacherName { get; set; }
        }
    }
}