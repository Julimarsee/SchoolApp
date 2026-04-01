using Npgsql;
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp2
{
    public partial class allGrades : Page
    {
        public allGrades()
        {
            InitializeComponent();
        }

        private bool isChange = true;
        private database db = new database();

        private void Grades_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "StudentName" || e.PropertyName == "ClassName")
            {
                return;
            }

            if (e.PropertyType == typeof(string))
            {
                var textColumn = new DataGridTextColumn
                {
                    Header = e.Column.Header,
                    Binding = new System.Windows.Data.Binding(e.PropertyName)
                    {
                        UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                    }
                };
                e.Column = textColumn;
            }
        }

        private void IsChange_Click(object sender, RoutedEventArgs e)
        {
            Grades.IsReadOnly = !Grades.IsReadOnly;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Данные сохранены");
        }

        private void Grades_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
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
                        string className = dataRow["Класс"].ToString();
                        string columnName = e.Column.Header.ToString();

                        if (columnName != "Ученик" && columnName != "Класс")
                        {
                            SaveGradeToDatabase(studentName, className, columnName, newValue);
                        }
                    }
                }
            }
        }

        public void SaveGradeToDatabase(string studentName, string className, string subjectTitle, string newGrade)
        {
            try
            {
                string getIdsQuery = @"
                    SELECT p.person_id, cst.cst_id
                    FROM person p
                    JOIN class_subject_teacher cst ON cst.class_id = (SELECT class_id FROM class WHERE class_name = @className)
                        AND cst.subject_id = (SELECT subject_id FROM subject WHERE title = @subjectTitle)
                    WHERE p.last_name || ' ' || p.first_name = @studentName
                        AND p.rights = 'Ученик'";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@studentName", studentName),
                    new NpgsqlParameter("@className", className),
                    new NpgsqlParameter("@subjectTitle", subjectTitle)
                };

                DataTable result = db.DataQuery(getIdsQuery, parameters);

                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    int personId = Convert.ToInt32(row["person_id"]);
                    int cstId = Convert.ToInt32(row["cst_id"]);

                    string upsertQuery = @"
                        INSERT INTO notes (note, cst_id, fk_person_id)
                        VALUES (@note, @cstId, @personId)
                        ON CONFLICT (fk_person_id, cst_id) 
                        DO UPDATE SET note = @note";

                    var upsertParams = new NpgsqlParameter[]
                    {
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (SaveData.role == "Ученик")
            {
                ButtonsStack.Visibility = Visibility.Collapsed;
                ChangeStack.Visibility = Visibility.Collapsed;
                ManageButton.Visibility = Visibility.Collapsed;
                LoadStudentData();
            }
            else if (SaveData.role == "Учитель")
            {
                ManageButton.Visibility = Visibility.Collapsed;
                LoadTeacherData();
            }
            else if (SaveData.role == "Администратор")
            {
                LoadAdminData();
            }
        }

        private void LoadAdminData()
        {
            try
            {
                string query = @"
                    SELECT 
                        p.last_name || ' ' || p.first_name as StudentName,
                        c.class_name as ClassName,
                        s.title as SubjectTitle,
                        n.note as FinalGrade
                    FROM person p
                    CROSS JOIN class c
                    CROSS JOIN subject s
                    LEFT JOIN class_subject_teacher cst ON c.class_id = cst.class_id AND s.subject_id = cst.subject_id
                    LEFT JOIN notes n ON n.cst_id = cst.cst_id AND n.fk_person_id = p.person_id
                    WHERE p.rights = 'Ученик'
                    ORDER BY c.class_name, p.last_name, p.first_name, s.title";

                DataTable dt = db.DataQuery(query);

                var resultTable = new DataTable();
                resultTable.Columns.Add("Ученик", typeof(string));
                resultTable.Columns.Add("Класс", typeof(string));

                var subjects = dt.AsEnumerable()
                    .Select(row => row["SubjectTitle"].ToString())
                    .Distinct()
                    .Where(s => !string.IsNullOrEmpty(s))
                    .OrderBy(s => s)
                    .ToList();

                foreach (var subject in subjects)
                {
                    resultTable.Columns.Add(subject, typeof(string));
                }

                var students = dt.AsEnumerable()
                    .GroupBy(row => new
                    {
                        StudentName = row["StudentName"].ToString(),
                        ClassName = row["ClassName"].ToString()
                    })
                    .OrderBy(g => g.Key.ClassName)
                    .ThenBy(g => g.Key.StudentName);

                foreach (var student in students)
                {
                    var row = resultTable.NewRow();
                    row["Ученик"] = student.Key.StudentName;
                    row["Класс"] = student.Key.ClassName;

                    foreach (var subject in subjects)
                    {
                        var grade = student.FirstOrDefault(s => s["SubjectTitle"].ToString() == subject);
                        row[subject] = grade != null ? grade["FinalGrade"].ToString() : "";
                    }

                    resultTable.Rows.Add(row);
                }

                Grades.ItemsSource = resultTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private void LoadTeacherData()
        {
            try
            {
                string query = @"
            SELECT DISTINCT
                p.last_name || ' ' || p.first_name as StudentName,
                c.class_name as ClassName,
                s.title as SubjectTitle,
                n.note as FinalGrade
            FROM person p
            JOIN class_subject_teacher cst ON cst.teacher_id = @teacherId
            JOIN class c ON cst.class_id = c.class_id
            JOIN subject s ON cst.subject_id = s.subject_id
            LEFT JOIN notes n ON n.cst_id = cst.cst_id AND n.fk_person_id = p.person_id
            WHERE p.rights = 'Ученик'
            ORDER BY ClassName, StudentName, SubjectTitle";

                var parameters = new NpgsqlParameter[]
                {
            new NpgsqlParameter("@teacherId", Convert.ToInt32(SaveData.id))
                };

                DataTable dt = db.DataQuery(query, parameters);

                var resultTable = new DataTable();
                resultTable.Columns.Add("Ученик", typeof(string));
                resultTable.Columns.Add("Класс", typeof(string));

                var subjects = dt.AsEnumerable()
                    .Select(row => row["SubjectTitle"].ToString())
                    .Distinct()
                    .Where(s => !string.IsNullOrEmpty(s))
                    .OrderBy(s => s)
                    .ToList();

                foreach (var subject in subjects)
                {
                    resultTable.Columns.Add(subject, typeof(string));
                }

                var students = dt.AsEnumerable()
                    .GroupBy(row => new
                    {
                        StudentName = row["StudentName"].ToString(),
                        ClassName = row["ClassName"].ToString()
                    })
                    .OrderBy(g => g.Key.ClassName)
                    .ThenBy(g => g.Key.StudentName);

                foreach (var student in students)
                {
                    var row = resultTable.NewRow();
                    row["Ученик"] = student.Key.StudentName;
                    row["Класс"] = student.Key.ClassName;

                    foreach (var subject in subjects)
                    {
                        var grade = student.FirstOrDefault(s => s["SubjectTitle"].ToString() == subject);
                        row[subject] = grade != null ? grade["FinalGrade"].ToString() : "";
                    }

                    resultTable.Rows.Add(row);
                }

                Grades.ItemsSource = resultTable.DefaultView;
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
            SELECT DISTINCT
                s.title as SubjectTitle,
                n.note as FinalGrade
            FROM notes n
            JOIN class_subject_teacher cst ON n.cst_id = cst.cst_id
            JOIN subject s ON cst.subject_id = s.subject_id
            WHERE n.fk_person_id = @studentId
            ORDER BY s.title";

                var parameters = new NpgsqlParameter[]
                {
            new NpgsqlParameter("@studentId", Convert.ToInt32(SaveData.id))
                };

                DataTable dt = db.DataQuery(query, parameters);

                var resultTable = new DataTable();
                resultTable.Columns.Add("Предмет", typeof(string));
                resultTable.Columns.Add("Итоговая оценка", typeof(string));

                foreach (DataRow row in dt.Rows)
                {
                    var newRow = resultTable.NewRow();
                    newRow["Предмет"] = row["SubjectTitle"].ToString();
                    newRow["Итоговая оценка"] = row["FinalGrade"].ToString();
                    resultTable.Rows.Add(newRow);
                }

                Grades.ItemsSource = resultTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private void ManageButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainForm)Window.GetWindow(this)).OpenDist();
        }
    }
}