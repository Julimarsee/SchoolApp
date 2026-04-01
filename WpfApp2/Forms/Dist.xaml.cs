using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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

        public void SetData(DataTable dt)
        {
            DistGrades.ItemsSource = dt.DefaultView;
        }

        private DataTable AddAverageColumn(DataTable dt)
        {
            if (dt.Columns.Count == 0 || dt.Rows.Count == 0)
                return dt;

            DataTable newTable = dt.Clone();
            newTable.Columns.Add("Средняя оценка", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                DataRow newRow = newTable.NewRow();

                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    newRow[i] = row[i];
                }

                double sum = 0;
                int count = 0;

                foreach (DataColumn col in dt.Columns)
                {
                    if (col.ColumnName == "Ученик" || col.ColumnName == "Дисциплины")
                        continue;

                    object cellValue = row[col];
                    if (cellValue != null && cellValue != DBNull.Value)
                    {
                        if (double.TryParse(cellValue.ToString(), out double grade))
                        {
                            sum += grade;
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    double average = sum / count;
                    newRow["Средняя оценка"] = average.ToString("F2");
                }
                else
                {
                    newRow["Средняя оценка"] = "—";
                }

                newTable.Rows.Add(newRow);
            }

            return newTable;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (SaveData.role == "Ученик")
            {
                ChoiceStack.Visibility = Visibility.Collapsed;
                DistGrades.Margin = new Thickness(-300, 24, 20, 30);
                AddData.Visibility = Visibility.Collapsed;
                LoadData();
            }

            List<string> subjects = new List<string>();

            if (SaveData.role == "Учитель")
            {
                butStack.Visibility = Visibility.Collapsed;
                foreach (string sub in SaveData.subjects)
                    if (!string.IsNullOrEmpty(sub)) subjects.Add(sub);
            }
            else
            {
                string query = "SELECT title FROM subject";
                var res = db.ExecuteQuery(query);

                foreach (DataRowView row in res)
                {
                    string title = row["title"].ToString()?.Trim();
                    if (!string.IsNullOrEmpty(title) && !subjects.Contains(title))
                        subjects.Add(title);
                }
            }

            Dists.ItemsSource = subjects;
            if (SaveData.currentSub != null) Dists.SelectedItem = SaveData.currentSub;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Данные сохранены");

        private void IsChange_Click(object sender, RoutedEventArgs e) =>
            DistGrades.IsReadOnly = !DistGrades.IsReadOnly;

        private void DistGrades_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var column = e.Column as DataGridBoundColumn;
                if (column != null)
                {
                    var bindingPath = (column.Binding as Binding)?.Path.Path;
                    Console.WriteLine(bindingPath);
                    var editedValue = (e.EditingElement as TextBox)?.Text;
                    var subject = Dists.SelectedItem.ToString();
                    var row = e.Row.Item as DataRowView;

                    if (string.IsNullOrEmpty(editedValue)) editedValue = null;
                    else if (!int.TryParse(editedValue, out _)) return;

                    if (editedValue != null)
                        if (int.Parse(editedValue) > 5 || int.Parse(editedValue) < 2)
                        {
                            MessageBox.Show("Ошибка: Оценка может быть только от 2 до 5");
                            (e.EditingElement as TextBox).Text = null;
                            return;
                        }

                    if (row != null && bindingPath != "Ученик" && bindingPath != "Дисциплины" && bindingPath != "Средняя оценка")
                    {
                        string studentName = row["Ученик"].ToString();
                        string className = row["Класс"]?.ToString();
                        SaveGradeToDatabase(studentName, className, bindingPath, subject, editedValue);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (Dists.SelectedItem != null)
                                getData(Dists.SelectedItem.ToString());
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }
        }

        public void LoadData()
        {
            string person_id = SaveData.id;

            // Получаем cst_id для ученика по его предметам
            string cstIdsQuery = $@"
                SELECT DISTINCT cst_id 
                FROM class_subject_teacher cst
                JOIN class c ON cst.class_id = c.class_id
                JOIN person p ON p.person_id = {person_id}
                WHERE p.rights = 'Ученик'";

            var cstIdsResult = db.ExecuteQuery(cstIdsQuery);
            var cstIds = cstIdsResult?.Cast<DataRowView>()
                .Select(r => r["cst_id"].ToString())
                .ToList() ?? new List<string>();

            if (cstIds.Count == 0)
            {
                DataTable emptyTable = new DataTable();
                emptyTable.Columns.Add("Дисциплины", typeof(string));
                emptyTable.Columns.Add("Средняя оценка", typeof(string));
                DataRow row = emptyTable.NewRow();
                row["Дисциплины"] = "Нет дисциплин";
                row["Средняя оценка"] = "—";
                emptyTable.Rows.Add(row);
                SetData(emptyTable);
                return;
            }

            string cstIdsList = string.Join(",", cstIds);

            string datesSql = $@"
                SELECT DISTINCT datetime::date AS date
                FROM daily_notes
                WHERE cst_id IN ({cstIdsList})
                ORDER BY date";

            var datesResult = db.ExecuteQuery(datesSql);
            var dates = datesResult?.Cast<DataRowView>()
                .Select(r => ((DateTime)r["date"]).ToString("yyyy-MM-dd"))
                .ToList() ?? new List<string>();

            var pivotColumns = string.Join(",\n", dates.Select(d =>
                $@"MAX(CASE WHEN d.datetime::date = DATE '{d}' THEN d.note ELSE NULL END) AS ""{d}"""));

            var columnsToUse = string.IsNullOrEmpty(pivotColumns) ? "NULL AS 'Нет данных'" : pivotColumns;

            string query = $@"
                SELECT
                    s.title AS ""Дисциплины"",
                    {columnsToUse}
                FROM subject s
                LEFT JOIN class_subject_teacher cst ON cst.subject_id = s.subject_id
                LEFT JOIN daily_notes d 
                    ON d.cst_id = cst.cst_id
                   AND d.fk_person_id = {person_id}
                WHERE cst.cst_id IN ({cstIdsList})
                GROUP BY s.subject_id, s.title
                ORDER BY ""Дисциплины""";

            try
            {
                DataTable dt = db.DataQuery(query);
                DataTable dtWithAverage = AddAverageColumn(dt);
                SetData(dtWithAverage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void SaveGradeToDatabase(string studentName, string className, string date, string subjectTitle, string newGrade)
        {
            try
            {
                // Разбор ФИО ученика
                string[] parts = studentName.Split(' ');
                if (parts.Length < 2) return;

                string lastName = parts[0];
                string firstName = parts[1];
                string patronymic = parts.Length > 2 ? parts[2] : null;

                // Получаем person_id ученика
                string getPersonIdQuery = @"
                    SELECT person_id 
                    FROM person 
                    WHERE last_name = @last_name 
                      AND first_name = @first_name 
                      AND (patronymic = @patronymic OR (patronymic IS NULL AND @patronymic IS NULL))
                      AND rights = 'Ученик'";

                var personResult = db.ExecuteQuery(getPersonIdQuery,
                    new NpgsqlParameter("@last_name", lastName),
                    new NpgsqlParameter("@first_name", firstName),
                    new NpgsqlParameter("@patronymic", (object)patronymic ?? DBNull.Value));

                if (personResult.Count == 0)
                {
                    MessageBox.Show($"Ученик '{studentName}' не найден");
                    return;
                }

                int personId = Convert.ToInt32(personResult[0]["person_id"]);

                // Получаем cst_id
                string getCstIdQuery = @"
                    SELECT cst_id 
                    FROM class_subject_teacher cst
                    JOIN subject s ON cst.subject_id = s.subject_id
                    JOIN class c ON cst.class_id = c.class_id
                    WHERE s.title = @subjectTitle 
                      AND c.class_name = @className";

                var cstResult = db.ExecuteQuery(getCstIdQuery,
                    new NpgsqlParameter("@subjectTitle", subjectTitle),
                    new NpgsqlParameter("@className", className));

                if (cstResult.Count == 0)
                {
                    MessageBox.Show($"Предмет '{subjectTitle}' не найден в классе '{className}'");
                    return;
                }

                int cstId = Convert.ToInt32(cstResult[0]["cst_id"]);

                // Преобразование даты
                DateTime parsedDate;
                if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                {
                    // Дата в правильном формате
                }
                else if (DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                {
                    // Дата в альтернативном формате
                }
                else
                {
                    MessageBox.Show("Некорректный формат даты");
                    return;
                }

                int dbDate = parsedDate.Year * 10000 + parsedDate.Month * 100 + parsedDate.Day;

                if (newGrade == null)
                {
                    // Удаляем оценку
                    string deleteSql = @"
                        DELETE FROM daily_notes 
                        WHERE fk_person_id = @personId 
                          AND cst_id = @cstId 
                          AND datetime = @datetime";

                    db.ExecuteNonQuery(deleteSql,
                        new NpgsqlParameter("@personId", personId),
                        new NpgsqlParameter("@cstId", cstId),
                        new NpgsqlParameter("@datetime", dbDate));
                }
                else
                {
                    int gradeValue = int.Parse(newGrade);

                    // Сохраняем или обновляем оценку
                    string insertSql = @"
                        INSERT INTO daily_notes (datetime, note, cst_id, fk_person_id)
                        VALUES (@datetime, @note, @cstId, @personId)
                        ON CONFLICT (fk_person_id, cst_id, datetime) 
                        DO UPDATE SET note = EXCLUDED.note";

                    db.ExecuteNonQuery(insertSql,
                        new NpgsqlParameter("@datetime", dbDate),
                        new NpgsqlParameter("@note", gradeValue),
                        new NpgsqlParameter("@cstId", cstId),
                        new NpgsqlParameter("@personId", personId));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения оценки: {ex.Message}");
            }
        }

        private void getData(string subject)
        {
            try
            {
                SaveData.currentSub = subject;

                // Получаем subject_id
                var id_res = db.ExecuteQuery($"SELECT subject_id FROM subject WHERE title = '{subject}'");
                if (id_res?.Count == 0)
                {
                    MessageBox.Show("Предмет не найден!");
                    return;
                }

                int subject_id = Convert.ToInt32(id_res[0]["subject_id"]);

                // Получаем все классы, где преподается этот предмет
                string classesQuery = $@"
                    SELECT DISTINCT c.class_name, cst.cst_id
                    FROM class c
                    JOIN class_subject_teacher cst ON c.class_id = cst.class_id
                    WHERE cst.subject_id = {subject_id}
                    ORDER BY c.class_name";

                var classesResult = db.ExecuteQuery(classesQuery);

                if (classesResult == null || classesResult.Count == 0)
                {
                    MessageBox.Show("Нет классов, где преподается эта дисциплина!");
                    DataTable emptyTable = new DataTable();
                    emptyTable.Columns.Add("Ученик", typeof(string));
                    emptyTable.Columns.Add("Класс", typeof(string));
                    emptyTable.Columns.Add("Средняя оценка", typeof(string));
                    DataRow row = emptyTable.NewRow();
                    row["Ученик"] = "Оценок нет";
                    row["Класс"] = "—";
                    row["Средняя оценка"] = "—";
                    emptyTable.Rows.Add(row);
                    SetData(emptyTable);
                    return;
                }

                // Собираем все cst_id для этого предмета
                var cstIds = classesResult.Cast<DataRowView>()
                    .Select(r => r["cst_id"].ToString())
                    .ToList();

                string cstIdsList = string.Join(",", cstIds);

                // Получаем даты
                string datesSql = $@"
                    SELECT DISTINCT datetime::date AS date
                    FROM daily_notes
                    WHERE cst_id IN ({cstIdsList})
                    ORDER BY date";

                var datesResult = db.ExecuteQuery(datesSql);

                if (datesResult == null || datesResult.Count == 0)
                {
                    MessageBox.Show("Нет оценок по данной дисциплине!");
                    DataTable emptyTable = new DataTable();
                    emptyTable.Columns.Add("Ученик", typeof(string));
                    emptyTable.Columns.Add("Класс", typeof(string));
                    emptyTable.Columns.Add("Средняя оценка", typeof(string));
                    DataRow row = emptyTable.NewRow();
                    row["Ученик"] = "Оценок нет";
                    row["Класс"] = "—";
                    row["Средняя оценка"] = "—";
                    emptyTable.Rows.Add(row);
                    SetData(emptyTable);
                    return;
                }

                var dates = datesResult.Cast<DataRowView>()
                    .Select(r => ((DateTime)r["date"]).ToString("yyyy-MM-dd"))
                    .ToList();

                var pivotColumns = string.Join(",\n", dates.Select(d =>
                    $@"MAX(CASE WHEN d.datetime::date = DATE '{d}' THEN d.note ELSE NULL END) AS ""{d}"""));

                var columnsToUse = string.IsNullOrEmpty(pivotColumns) ? "NULL AS 'Нет данных'" : pivotColumns;

                string query = $@"
                    SELECT
                        TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS ""Ученик"",
                        c.class_name AS ""Класс"",
                        {columnsToUse}
                    FROM person p
                    LEFT JOIN daily_notes d 
                        ON d.fk_person_id = p.person_id 
                       AND d.cst_id IN ({cstIdsList})
                    LEFT JOIN class_subject_teacher cst ON cst.cst_id = d.cst_id
                    LEFT JOIN class c ON c.class_id = cst.class_id
                    WHERE p.rights = 'Ученик'
                    GROUP BY p.person_id, p.last_name, p.first_name, p.patronymic, c.class_name
                    ORDER BY c.class_name, ""Ученик""";

                DataTable dt = db.DataQuery(query);

                DataTable dtWithAverage = AddAverageColumn(dt);
                SetData(dtWithAverage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void DistGrades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sub = Dists.SelectedItem;
            if (sub != null)
            {
                getData(sub.ToString());
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e) =>
            DataStack.Visibility = Visibility.Visible;

        private void InputData_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var text = textBox.Text;

            text = Regex.Replace(text, @"[^0-9]", "");

            if (text.Length >= 2)
            {
                text = text.Insert(2, "-");
            }
            if (text.Length >= 5)
            {
                text = text.Insert(5, "-");
            }

            if (text.Length > 10)
            {
                text = text.Substring(0, 10);
            }

            textBox.Text = text;
            textBox.SelectionStart = textBox.Text.Length;
        }

        private void InputData_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"[0-9]+$");

        private void InputData_LostFocus(object sender, RoutedEventArgs e)
        {
            string newData = InputData.Text;

            if (!DateTime.TryParseExact(newData, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                MessageBox.Show("Некорректная дата! День: 01-31, Месяц: 01-12.");
                InputData.Clear();
                return;
            }

            int dbDate = parsedDate.Year * 10000 + parsedDate.Month * 100 + parsedDate.Day;

            string checkData = $@"
                SELECT datetime 
                FROM daily_notes 
                WHERE datetime = {dbDate}";

            DataView result = db.ExecuteQuery(checkData);

            if (result == null || result.Count == 0)
            {
                // Создаем запись с датой (без оценок)
                DataStack.Visibility = Visibility.Collapsed;

                if (Dists.SelectedItem != null)
                    getData(Dists.SelectedItem.ToString());
            }
            else
                MessageBox.Show("Запись этой даты уже существует, введите оценку в колонку");
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Dists.SelectedItem != null)
                {
                    var subject = Dists.SelectedItem.ToString();

                    MessageBoxResult result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить дисциплину {subject}?",
                        "Подтверждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Сначала удаляем связи в class_subject_teacher
                        string deleteRelationsQuery = $@"
                            DELETE FROM class_subject_teacher 
                            WHERE subject_id = (SELECT subject_id FROM subject WHERE title = '{subject}')";
                        db.ExecuteNonQuery(deleteRelationsQuery);

                        // Затем удаляем саму дисциплину
                        string deleteSubjectQuery = $@"DELETE FROM subject WHERE title = '{subject}'";
                        db.ExecuteNonQuery(deleteSubjectQuery);

                        MessageBox.Show($"Дисциплина {subject} успешно удалена");

                        Dists.SelectedItem = null;
                        SaveData.currentSub = null;

                        // Обновляем список предметов
                        Page_Loaded(null, null);
                    }
                }
                else
                    MessageBox.Show("Выберите дисциплину для удаления!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}");
            }
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            if (Dists.SelectedItem != null)
            {
                SaveData.isChange = true;
                ((MainForm)Window.GetWindow(this)).OpenNewSub();
            }
            else MessageBox.Show("Выберите дисциплину для изменения");
        }
    }
}