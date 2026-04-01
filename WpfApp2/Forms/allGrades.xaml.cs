using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfApp2
{
    /// <summary>
    /// Конвертер для зачетов (не используется в новой версии, т.к. зачеты убраны)
    /// </summary>
    public class OffsetToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // В новой версии зачетов нет, всегда возвращаем false
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public partial class allGrades : Page
    {
        public allGrades()
        {
            InitializeComponent();
        }

        private bool isChange = true;
        private database db = new database();

        public void SetData(DataTable dt, List<string> offsetColumns)
        {
            this.offsetColumns = offsetColumns ?? new List<string>();

            Grades.ItemsSource = dt.DefaultView;
            Grades.Columns.Clear();
            Grades.AutoGenerateColumns = true;
        }

        private List<string> offsetColumns = new List<string>();

        private void Grades_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string columnHeader = e.Column.Header.ToString();

            if (SaveData.role == "Учитель" && SaveData.subjects != null && SaveData.subjects.Any())
            {
                if (columnHeader != "Ученик" && columnHeader != "Класс")
                {
                    bool isTeacherSubject = SaveData.subjects.Any(sub =>
                        !string.IsNullOrEmpty(sub) && columnHeader.Contains(sub.Trim()));

                    if (!isTeacherSubject)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            if (columnHeader == "Ученик" || columnHeader == "Класс")
            {
                e.Column.IsReadOnly = true;
                return;
            }

        }

        private void IsChange_Click(object sender, RoutedEventArgs e) =>
            Grades.IsReadOnly = !Grades.IsReadOnly;

        private void SaveButton_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Данные сохранены");

        private void Grades_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            e.Cancel = true;

            var column = e.Column as DataGridBoundColumn;
            if (column == null || column.IsReadOnly) return;

            var bindingPath = (column.Binding as Binding)?.Path.Path;
            if (bindingPath == "Ученик" || bindingPath == "Класс") return;

            string newGrade = null;

            if (e.EditingElement is TextBox textBox)
            {
                newGrade = textBox.Text.Trim();
                if (string.IsNullOrEmpty(newGrade))
                {
                    newGrade = null;
                }
                else
                {
                    if (!int.TryParse(newGrade, out int grade) || grade < 2 || grade > 5)
                    {
                        MessageBox.Show("Ошибка: Оценка может быть только от 2 до 5");
                        return;
                    }
                }
            }
            else
            {
                return;
            }

            var row = e.Row.Item as DataRowView;
            if (row != null)
            {
                string studentName = row["Ученик"].ToString();
                string className = row["Класс"].ToString();
                SaveGradeToDatabase(studentName, className, bindingPath, newGrade);
            }
        }

        public void SaveGradeToDatabase(string studentName, string className, string subjectTitle, string newGrade)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(newGrade);

            string[] parts = studentName.Split(' ');
            if (parts.Length < 2) return;

            string lastName = parts[0];
            string firstName = parts[1];
            string patronymic = parts.Length > 2 ? parts[2] : null;

            string getCstIdQuery = @"
                SELECT cst.cst_id 
                FROM class_subject_teacher cst
                JOIN subject s ON cst.subject_id = s.subject_id
                JOIN class c ON cst.class_id = c.class_id
                WHERE s.title = @subjectTitle 
                  AND c.class_name = @className";

            var cstResult = db.ExecuteQuery(getCstIdQuery,
                new NpgsqlParameter("subjectTitle", subjectTitle),
                new NpgsqlParameter("className", className));

            if (cstResult.Count == 0)
            {
                MessageBox.Show($"Ошибка: Предмет '{subjectTitle}' не найден в классе '{className}'");
                return;
            }

            int cstId = Convert.ToInt32(cstResult[0]["cst_id"]);

            string getPersonIdQuery = @"
                SELECT person_id 
                FROM person 
                WHERE last_name = @last_name 
                  AND first_name = @first_name 
                  AND (patronymic = @patronymic OR (patronymic IS NULL AND @patronymic IS NULL))
                  AND rights = 'Ученик'";

            var personResult = db.ExecuteQuery(getPersonIdQuery,
                new NpgsqlParameter("last_name", lastName),
                new NpgsqlParameter("first_name", firstName),
                new NpgsqlParameter("patronymic", (object)patronymic ?? DBNull.Value));

            if (personResult.Count == 0)
            {
                MessageBox.Show($"Ошибка: Ученик '{studentName}' не найден в базе данных");
                return;
            }

            int personId = Convert.ToInt32(personResult[0]["person_id"]);

            if (isEmpty)
            {
                string deleteSql = @"
                    DELETE FROM notes 
                    WHERE fk_person_id = @personId 
                      AND cst_id = @cstId";

                db.ExecuteNonQuery(deleteSql,
                    new NpgsqlParameter("personId", personId),
                    new NpgsqlParameter("cstId", cstId));

                MessageBox.Show($"Оценка по предмету '{subjectTitle}' удалена");
            }
            else
            {
                int gradeValue = int.Parse(newGrade);

                string insertSql = @"
                    INSERT INTO notes (fk_person_id, cst_id, note)
                    VALUES (@personId, @cstId, @note)
                    ON CONFLICT (fk_person_id, cst_id)
                    DO UPDATE SET note = EXCLUDED.note";

                db.ExecuteNonQuery(insertSql,
                    new NpgsqlParameter("personId", personId),
                    new NpgsqlParameter("cstId", cstId),
                    new NpgsqlParameter("note", gradeValue));

                MessageBox.Show($"Оценка '{gradeValue}' по предмету '{subjectTitle}' сохранена");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (SaveData.role == "Ученик")
            {
                ButtonsStack.Visibility = Visibility.Collapsed;
                ChangeStack.Visibility = Visibility.Collapsed;
                ManageButton.Visibility = Visibility.Collapsed;
            }
            else if (SaveData.role == "Учитель")
            {
                ManageButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ManageButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainForm)Window.GetWindow(this)).OpenDist();
        }
    }
}