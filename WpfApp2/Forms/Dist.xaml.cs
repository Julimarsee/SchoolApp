using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp2
{
    public partial class Dist : Page
    {
        public Dist()
        {
            InitializeComponent();
        }

        private bool isChange = false;
        private database db = new database();
        private string currentSubject = "";
        private string currentDate = "";
        private TextBox activeInputData = null; // Для отслеживания активного поля ввода

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (SaveData.role == "Ученик")
            {
                ChoiceStack.Visibility = Visibility.Collapsed;
                DistGrades.Margin = new Thickness(-300, 24, 20, 0);
                AddData.Visibility = Visibility.Collapsed;
                LoadStudentData();
            }
            else
            {
                LoadSubjects();
            }
        }

        private void LoadSubjects()
        {
            List<string> subjects = new List<string>();

            if (SaveData.role == "Учитель")
            {
                butStack.Visibility = Visibility.Collapsed;
                foreach (string sub in SaveData.subjects)
                    if (!string.IsNullOrEmpty(sub)) subjects.Add(sub);
            }
            else
            {
                string query = "SELECT title FROM subject ORDER BY title";
                var res = db.ExecuteQuery(query);

                foreach (DataRowView row in res)
                {
                    string title = row["title"].ToString()?.Trim();
                    if (!string.IsNullOrEmpty(title) && !subjects.Contains(title))
                        subjects.Add(title);
                }
            }

            Dists.ItemsSource = subjects;
            if (SaveData.currentSub != null && subjects.Contains(SaveData.currentSub))
                Dists.SelectedItem = SaveData.currentSub;
            else if (subjects.Count > 0)
                Dists.SelectedIndex = 0;
        }

        private void DistGrades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sub = Dists.SelectedItem;
            if (sub != null)
            {
                currentSubject = sub.ToString();
                LoadData(currentSubject);
            }
        }

        private void LoadData(string subject)
        {
            try
            {
                DataTable resultTable = new DataTable();
                resultTable.Columns.Add("Ученик", typeof(string));

                if (SaveData.role == "Ученик")
                {
                    LoadStudentData();
                    return;
                }

                string datesQuery = @"
                    SELECT DISTINCT dd.visit_date
                    FROM daily_notes dd
                    JOIN class_subject_teacher cst ON dd.cst_id = cst.cst_id
                    JOIN subject s ON cst.subject_id = s.subject_id
                    WHERE s.title = @subject
                    ORDER BY dd.visit_date";

                var dateParams = new NpgsqlParameter[] { new NpgsqlParameter("@subject", subject) };
                DataTable datesTable = db.DataQuery(datesQuery, dateParams);

                List<DateTime> dates = new List<DateTime>();
                foreach (DataRow row in datesTable.Rows)
                {
                    dates.Add(Convert.ToDateTime(row["visit_date"]));
                }

                foreach (DateTime date in dates)
                {
                    resultTable.Columns.Add(date.ToString("dd-MM-yyyy"), typeof(string));
                }
                resultTable.Columns.Add("Средняя оценка", typeof(string));

                string studentsQuery = @"
                    SELECT DISTINCT p.person_id, p.last_name || ' ' || p.first_name as StudentName
                    FROM person p
                    JOIN class_subject_teacher cst ON cst.class_id IN (
                        SELECT DISTINCT class_id FROM class_subject_teacher cst2
                        JOIN subject s2 ON cst2.subject_id = s2.subject_id
                        WHERE s2.title = @subject
                    )
                    WHERE p.rights = 'Ученик'
                    ORDER BY StudentName";

                var studentParams = new NpgsqlParameter[] { new NpgsqlParameter("@subject", subject) };
                DataTable studentsTable = db.DataQuery(studentsQuery, studentParams);

                foreach (DataRow student in studentsTable.Rows)
                {
                    DataRow newRow = resultTable.NewRow();
                    newRow["Ученик"] = student["StudentName"].ToString();
                    int personId = Convert.ToInt32(student["person_id"]);

                    double sum = 0;
                    int count = 0;

                    foreach (DateTime date in dates)
                    {
                        string gradeQuery = @"
                            SELECT dd.note
                            FROM daily_notes dd
                            JOIN class_subject_teacher cst ON dd.cst_id = cst.cst_id
                            JOIN subject s ON cst.subject_id = s.subject_id
                            WHERE s.title = @subject 
                                AND dd.fk_person_id = @personId 
                                AND dd.visit_date = @date";

                        var gradeParams = new NpgsqlParameter[]
                        {
                            new NpgsqlParameter("@subject", subject),
                            new NpgsqlParameter("@personId", personId),
                            new NpgsqlParameter("@date", date)
                        };

                        DataTable gradeTable = db.DataQuery(gradeQuery, gradeParams);
                        string grade = "";
                        if (gradeTable.Rows.Count > 0 && gradeTable.Rows[0]["note"] != DBNull.Value)
                        {
                            grade = gradeTable.Rows[0]["note"].ToString();
                            if (double.TryParse(grade, out double gradeValue))
                            {
                                sum += gradeValue;
                                count++;
                            }
                        }
                        newRow[date.ToString("dd-MM-yyyy")] = grade;
                    }

                    if (count > 0)
                        newRow["Средняя оценка"] = (sum / count).ToString("F2");
                    else
                        newRow["Средняя оценка"] = "—";

                    resultTable.Rows.Add(newRow);
                }

                DistGrades.ItemsSource = resultTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private void LoadStudentData()
        {
            try
            {
                string query = @"
                    SELECT 
                        s.title as SubjectTitle,
                        dd.visit_date as Date,
                        dd.note as Grade
                    FROM daily_notes dd
                    JOIN class_subject_teacher cst ON dd.cst_id = cst.cst_id
                    JOIN subject s ON cst.subject_id = s.subject_id
                    WHERE dd.fk_person_id = @studentId
                    ORDER BY s.title, dd.visit_date";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@studentId", Convert.ToInt32(SaveData.id))
                };

                DataTable dt = db.DataQuery(query, parameters);

                var resultTable = new DataTable();
                resultTable.Columns.Add("Предмет", typeof(string));

                var dates = dt.AsEnumerable()
                    .Select(row => Convert.ToDateTime(row["Date"]).ToString("dd-MM-yyyy"))
                    .Distinct()
                    .OrderBy(d => DateTime.ParseExact(d, "dd-MM-yyyy", null))
                    .ToList();

                foreach (var date in dates)
                {
                    resultTable.Columns.Add(date, typeof(string));
                }
                resultTable.Columns.Add("Средняя оценка", typeof(string));

                var subjects = dt.AsEnumerable()
                    .Select(row => row["SubjectTitle"].ToString())
                    .Distinct()
                    .OrderBy(s => s);

                foreach (var subject in subjects)
                {
                    DataRow newRow = resultTable.NewRow();
                    newRow["Предмет"] = subject;

                    double sum = 0;
                    int count = 0;

                    foreach (var date in dates)
                    {
                        var grade = dt.AsEnumerable()
                            .FirstOrDefault(r => r["SubjectTitle"].ToString() == subject
                                && Convert.ToDateTime(r["Date"]).ToString("dd-MM-yyyy") == date);

                        string gradeValue = grade != null ? grade["Grade"].ToString() : "";
                        newRow[date] = gradeValue;

                        if (double.TryParse(gradeValue, out double numericGrade))
                        {
                            sum += numericGrade;
                            count++;
                        }
                    }

                    if (count > 0)
                        newRow["Средняя оценка"] = (sum / count).ToString("F2");
                    else
                        newRow["Средняя оценка"] = "—";

                    resultTable.Rows.Add(newRow);
                }

                DistGrades.ItemsSource = resultTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DataStack.Visibility = Visibility.Visible;

            // Находим TextBox в шаблоне и устанавливаем фокус
            if (DataStack.HeaderTemplate != null)
            {
                var headerContent = DataStack.HeaderTemplate.LoadContent() as StackPanel;
                if (headerContent != null)
                {
                    var textBox = FindVisualChild<TextBox>(headerContent);
                    if (textBox != null)
                    {
                        activeInputData = textBox;
                        textBox.Focus();
                    }
                }
            }
        }

        private void InputData_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string text = textBox.Text;

            // Автоматическая вставка разделителей после 2 и 4 символов
            if (text.Length == 2 && !text.EndsWith("-") && text.All(char.IsDigit))
            {
                textBox.Text = text + "-";
                textBox.CaretIndex = textBox.Text.Length;
            }
            else if (text.Length == 5 && !text.EndsWith("-") && text.Count(c => c == '-') == 1)
            {
                textBox.Text = text + "-";
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private void InputData_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Key.Enter)
            {
                if (textBox.Text.Length == 10 && textBox.Text.Contains("-"))
                {
                    if (DateTime.TryParseExact(textBox.Text, "dd-MM-yyyy", null,
                        System.Globalization.DateTimeStyles.None, out DateTime newDate))
                    {
                        currentDate = textBox.Text;
                        AddNewDateColumn(currentDate);
                        DataStack.Visibility = Visibility.Collapsed;
                        textBox.Text = "";
                        activeInputData = null;
                    }
                    else
                    {
                        MessageBox.Show("Введите корректную дату в формате ДД-ММ-ГГГГ",
                            "Ошибка формата", MessageBoxButton.OK, MessageBoxImage.Warning);
                        textBox.SelectAll();
                    }
                }
                else
                {
                    MessageBox.Show("Дата должна быть в формате ДД-ММ-ГГГГ (например, 15-03-2024)",
                        "Неверный формат", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                e.Handled = true;
            }
        }

        private void AddNewDateColumn(string dateString)
        {
            try
            {
                DateTime date = DateTime.ParseExact(dateString, "dd-MM-yyyy", null);
                DataView currentView = DistGrades.ItemsSource as DataView;
                if (currentView == null) return;

                DataTable currentTable = currentView.Table;
                if (currentTable.Columns.Contains(dateString))
                {
                    MessageBox.Show("Такая дата уже существует", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                currentTable.Columns.Add(dateString, typeof(string));
                currentTable.Columns["Средняя оценка"].SetOrdinal(currentTable.Columns.Count - 1);

                // Получаем CST_ID для предмета
                string getCstIdQuery = @"
                    SELECT cst.cst_id
                    FROM class_subject_teacher cst
                    JOIN subject s ON cst.subject_id = s.subject_id
                    WHERE s.title = @subject
                    LIMIT 1";

                var cstParams = new NpgsqlParameter[] { new NpgsqlParameter("@subject", currentSubject) };
                DataTable cstResult = db.DataQuery(getCstIdQuery, cstParams);

                if (cstResult.Rows.Count == 0)
                {
                    MessageBox.Show("Не найден CST_ID для выбранного предмета", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int cstId = Convert.ToInt32(cstResult.Rows[0]["cst_id"]);

                // Проходим по всем ученикам и добавляем записи в базу данных
                int successCount = 0;
                int errorCount = 0;

                foreach (DataRow row in currentTable.Rows)
                {
                    string studentName = row["Ученик"].ToString();

                    // Получаем person_id ученика
                    string getPersonIdQuery = @"
                        SELECT person_id FROM person 
                        WHERE last_name || ' ' || first_name = @studentName
                        AND rights = 'Ученик'";

                    var personParams = new NpgsqlParameter[] {
                        new NpgsqlParameter("@studentName", studentName)
                    };
                    DataTable personResult = db.DataQuery(getPersonIdQuery, personParams);

                    if (personResult.Rows.Count > 0)
                    {
                        int personId = Convert.ToInt32(personResult.Rows[0]["person_id"]);

                        try
                        {
                            // Проверяем, существует ли уже запись
                            string checkQuery = @"
                                SELECT COUNT(*) FROM daily_notes 
                                WHERE fk_person_id = @personId 
                                AND cst_id = @cstId 
                                AND visit_date = @date";

                            var checkParams = new NpgsqlParameter[]
                            {
                                new NpgsqlParameter("@personId", personId),
                                new NpgsqlParameter("@cstId", cstId),
                                new NpgsqlParameter("@date", date)
                            };

                            DataTable checkResult = db.DataQuery(checkQuery, checkParams);
                            int exists = Convert.ToInt32(checkResult.Rows[0][0]);

                            if (exists == 0)
                            {
                                // Вставляем новую запись
                                string insertQuery = @"
                                    INSERT INTO daily_notes (visit_date, note, cst_id, fk_person_id)
                                    VALUES (@date, 'Н', @cstId, @personId)";

                                var insertParams = new NpgsqlParameter[]
                                {
                                    new NpgsqlParameter("@date", date),
                                    new NpgsqlParameter("@cstId", cstId),
                                    new NpgsqlParameter("@personId", personId)
                                };
                                db.ExecuteNonQuery(insertQuery, insertParams);
                            }

                            // Инициализируем ячейку пустой строкой
                            row[dateString] = "Н";
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            System.Diagnostics.Debug.WriteLine($"Ошибка для {studentName}: {ex.Message}");
                        }
                    }
                }

                // Обновляем отображение
                DistGrades.ItemsSource = currentTable.DefaultView;

                MessageBox.Show($"Дата {dateString} успешно добавлена\n" +
                    $"Добавлено записей: {successCount}\n" +
                    $"Ошибок: {errorCount}",
                    "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении даты: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DistGrades_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var editedElement = e.EditingElement as TextBox;
                if (editedElement != null)
                {
                    string newValue = editedElement.Text;
                    var dataRow = e.Row.Item as DataRowView;

                    if (dataRow != null)
                    {
                        string studentName = dataRow["Ученик"].ToString();
                        string columnName = e.Column.Header.ToString();

                        if (columnName != "Ученик" && columnName != "Средняя оценка")
                        {
                            SaveGradeToDatabase(studentName, currentSubject, columnName, newValue);
                        }
                    }
                }
            }
        }

        public void SaveGradeToDatabase(string studentName, string subjectTitle, string dateString, string newGrade)
        {
            try
            {
                DateTime date = DateTime.ParseExact(dateString, "dd-MM-yyyy", null);

                string getIdsQuery = @"
                    SELECT p.person_id, cst.cst_id
                    FROM person p
                    JOIN class_subject_teacher cst ON cst.subject_id = (SELECT subject_id FROM subject WHERE title = @subjectTitle)
                    WHERE p.last_name || ' ' || p.first_name = @studentName
                        AND p.rights = 'Ученик'
                    LIMIT 1";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@studentName", studentName),
                    new NpgsqlParameter("@subjectTitle", subjectTitle)
                };

                DataTable result = db.DataQuery(getIdsQuery, parameters);

                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    int personId = Convert.ToInt32(row["person_id"]);
                    int cstId = Convert.ToInt32(row["cst_id"]);

                    string upsertQuery = @"
                        INSERT INTO daily_notes (visit_date, note, cst_id, fk_person_id)
                        VALUES (@date, @note, @cstId, @personId)
                        ON CONFLICT (fk_person_id, cst_id, visit_date) 
                        DO UPDATE SET note = @note";

                    var upsertParams = new NpgsqlParameter[]
                    {
                        new NpgsqlParameter("@date", date),
                        new NpgsqlParameter("@note", newGrade),
                        new NpgsqlParameter("@cstId", cstId),
                        new NpgsqlParameter("@personId", personId)
                    };

                    db.ExecuteNonQuery(upsertQuery, upsertParams);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении оценки: {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Данные сохранены");

        private void IsChange_Click(object sender, RoutedEventArgs e) =>
            DistGrades.IsReadOnly = !DistGrades.IsReadOnly;

        private void InputData_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Разрешаем ввод только цифр
            e.Handled = !Regex.IsMatch(e.Text, @"[0-9]");

            // Запрещаем ввод более 10 символов
            if (textBox.Text.Length >= 10 && !e.Handled)
            {
                e.Handled = true;
            }
        }

        private void InputData_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && string.IsNullOrEmpty(textBox.Text))
            {
                DataStack.Visibility = Visibility.Collapsed;
                activeInputData = null;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Dists.SelectedItem == null)
            {
                MessageBox.Show("Выберите дисциплину");
                return;
            }

            var result = MessageBox.Show("Удалить выбранную дисциплину?", "Подтверждение", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string deleteQuery = "DELETE FROM subject WHERE title = @title";
                    var parameters = new NpgsqlParameter[] { new NpgsqlParameter("@title", currentSubject) };
                    db.ExecuteNonQuery(deleteQuery, parameters);

                    LoadSubjects();
                    DistGrades.ItemsSource = null;
                    MessageBox.Show("Дисциплина удалена");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}");
                }
            }
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            if (Dists.SelectedItem != null)
            {
                SaveData.isChange = true;
                SaveData.currentSub = currentSubject;
                ((MainForm)Window.GetWindow(this)).OpenNewSub();
            }
            else MessageBox.Show("Выберите дисциплину для изменения");
        }

        // Вспомогательный метод для поиска визуальных элементов
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}