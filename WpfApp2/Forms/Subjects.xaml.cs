using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp2
{
    public partial class Subjects : Page
    {
        private database db = new database();
        private DataTable currentDataTable;
        private bool isSaving = false;

        public Subjects()
        {
            InitializeComponent();
            SetPermissionsBasedOnRole();
        }

        private void SetPermissionsBasedOnRole()
        {
            if (SaveData.role == "Администратор")
            {
                DataGrid.CanUserAddRows = true;
                DataGrid.CanUserDeleteRows = true;
                SaveButton.Visibility = Visibility.Visible;
            }
            else
            {
                DataGrid.CanUserAddRows = false;
                DataGrid.CanUserDeleteRows = false;
                SaveButton.Visibility = Visibility.Collapsed;
                DataGrid.IsReadOnly = true;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSubjectsData();
                if (SaveData.role == "Администратор")
                {
                    ToolTip = "Режим редактирования: вы можете изменять, добавлять и удалять записи";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке страницы: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataTypeComboBox.SelectedItem == null) return;

            ComboBoxItem selectedItem = (ComboBoxItem)DataTypeComboBox.SelectedItem;
            string selectedValue = selectedItem.Content.ToString();

            try
            {
                switch (selectedValue)
                {
                    case "Предметы":
                        LoadSubjectsData();
                        break;
                    case "Учителя":
                        LoadTeachersData();
                        break;
                    case "Ученики":
                        LoadStudentsData();
                        break;
                    case "Классы":
                        LoadClassesData();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSubjectsData()
        {
            try
            {
                string query = "SELECT subject_id AS \"ID\", title AS \"Название предмета\" FROM subject ORDER BY title;";
                DataTable dataTable = db.DataQuery(query);
                currentDataTable = dataTable;
                DataGrid.ItemsSource = currentDataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных предметов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTeachersData()
        {
            try
            {
                string query = @"SELECT person_id AS ""ID"", 
                                        first_name AS ""Имя"", 
                                        last_name AS ""Фамилия"", 
                                        patronymic AS ""Отчество"", 
                                        login AS ""Логин"", 
                                        rights AS ""Права"" 
                                 FROM person 
                                 WHERE rights = 'Учитель' 
                                 ORDER BY last_name, first_name;";
                DataTable dataTable = db.DataQuery(query);
                currentDataTable = dataTable;
                DataGrid.ItemsSource = currentDataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных учителей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStudentsData()
        {
            try
            {
                string query = @"SELECT person_id AS ""ID"", 
                                        first_name AS ""Имя"", 
                                        last_name AS ""Фамилия"", 
                                        patronymic AS ""Отчество"", 
                                        login AS ""Логин"", 
                                        rights AS ""Права"" 
                                 FROM person 
                                 WHERE rights = 'Ученик' 
                                 ORDER BY last_name, first_name;";
                DataTable dataTable = db.DataQuery(query);
                currentDataTable = dataTable;
                DataGrid.ItemsSource = currentDataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных учеников: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadClassesData()
        {
            try
            {
                string query = "SELECT class_id AS \"ID\", class_name AS \"Название класса\" FROM class ORDER BY class_name;";
                DataTable dataTable = db.DataQuery(query);
                currentDataTable = dataTable;
                DataGrid.ItemsSource = currentDataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных классов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "ID")
            {
                e.Column.Width = 60;
                e.Column.IsReadOnly = true;
            }
            else if (e.PropertyName == "Название предмета" || e.PropertyName == "Название класса")
            {
                e.Column.Width = 200;
            }
            else if (e.PropertyName == "Имя" || e.PropertyName == "Фамилия" || e.PropertyName == "Отчество")
            {
                e.Column.Width = 120;
            }
            else if (e.PropertyName == "Логин")
            {
                e.Column.Width = 150;
            }
            else if (e.PropertyName == "Права")
            {
                e.Column.Width = 100;
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (SaveData.role != "Администратор")
            {
                e.Cancel = true;
                MessageBox.Show("У вас нет прав на редактирование данных", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (isSaving) return;

            if (SaveData.role == "Администратор" && e.EditAction == DataGridEditAction.Commit)
            {
                isSaving = true;
                try
                {
                    SaveButton_Click(sender, null);
                }
                finally
                {
                    isSaving = false;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SaveData.role != "Администратор")
            {
                MessageBox.Show("У вас нет прав на сохранение данных", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (currentDataTable == null)
                {
                    MessageBox.Show("Нет данных для сохранения", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DataTable changes = currentDataTable.GetChanges();
                if (changes == null || changes.Rows.Count == 0)
                {
                    return;
                }

                ComboBoxItem selectedItem = (ComboBoxItem)DataTypeComboBox.SelectedItem;
                string selectedValue = selectedItem.Content.ToString();

                bool success = SaveChangesToDatabase(selectedValue, changes);

                if (success)
                {
                    MessageBox.Show("Данные успешно сохранены!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    switch (selectedValue)
                    {
                        case "Предметы":
                            LoadSubjectsData();
                            break;
                        case "Учителя":
                            LoadTeachersData();
                            break;
                        case "Ученики":
                            LoadStudentsData();
                            break;
                        case "Классы":
                            LoadClassesData();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SaveChangesToDatabase(string dataType, DataTable changes)
        {
            switch (dataType)
            {
                case "Предметы":
                    return SaveSubjects(changes);
                case "Учителя":
                    return SaveTeachers(changes);
                case "Ученики":
                    return SaveStudents(changes);
                case "Классы":
                    return SaveClasses(changes);
                default:
                    return false;
            }
        }

        private bool SaveSubjects(DataTable changes)
        {
            try
            {
                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string title = row["Название предмета"].ToString();

                            string query = "UPDATE subject SET title = @title WHERE subject_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@title", title),
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                    else if (row.RowState == DataRowState.Added)
                    {
                        if (row["Название предмета"] != DBNull.Value &&
                            !string.IsNullOrWhiteSpace(row["Название предмета"].ToString()))
                        {
                            string title = row["Название предмета"].ToString();
                            string query = "INSERT INTO subject (title) VALUES (@title);";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@title", title));
                        }
                    }
                    else if (row.RowState == DataRowState.Deleted)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string query = "DELETE FROM subject WHERE subject_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                }

                currentDataTable.AcceptChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении предметов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveTeachers(DataTable changes)
        {
            try
            {
                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string firstName = row["Имя"].ToString();
                            string lastName = row["Фамилия"].ToString();
                            string patronymic = row["Отчество"] != DBNull.Value ? row["Отчество"].ToString() : "";
                            string login = row["Логин"].ToString();
                            string rights = row["Права"].ToString();

                            string query = @"UPDATE person 
                                            SET first_name = @firstName, 
                                                last_name = @lastName, 
                                                patronymic = @patronymic, 
                                                login = @login, 
                                                rights = @rights 
                                            WHERE person_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@firstName", firstName),
                                new Npgsql.NpgsqlParameter("@lastName", lastName),
                                new Npgsql.NpgsqlParameter("@patronymic", patronymic),
                                new Npgsql.NpgsqlParameter("@login", login),
                                new Npgsql.NpgsqlParameter("@rights", rights),
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                    else if (row.RowState == DataRowState.Added)
                    {
                        if (row["Имя"] != DBNull.Value && row["Фамилия"] != DBNull.Value &&
                            row["Логин"] != DBNull.Value && row["Права"] != DBNull.Value &&
                            !string.IsNullOrWhiteSpace(row["Имя"].ToString()) &&
                            !string.IsNullOrWhiteSpace(row["Фамилия"].ToString()) &&
                            !string.IsNullOrWhiteSpace(row["Логин"].ToString()) &&
                            !string.IsNullOrWhiteSpace(row["Права"].ToString()))
                        {
                            string firstName = row["Имя"].ToString();
                            string lastName = row["Фамилия"].ToString();
                            string patronymic = row["Отчество"] != DBNull.Value ? row["Отчество"].ToString() : "";
                            string login = row["Логин"].ToString();
                            string rights = row["Права"].ToString();
                            string defaultPassword = "password123";

                            string query = @"INSERT INTO person (first_name, last_name, patronymic, login, password, rights) 
                                            VALUES (@firstName, @lastName, @patronymic, @login, @password, @rights);";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@firstName", firstName),
                                new Npgsql.NpgsqlParameter("@lastName", lastName),
                                new Npgsql.NpgsqlParameter("@patronymic", patronymic),
                                new Npgsql.NpgsqlParameter("@login", login),
                                new Npgsql.NpgsqlParameter("@password", defaultPassword),
                                new Npgsql.NpgsqlParameter("@rights", rights));
                        }
                    }
                    else if (row.RowState == DataRowState.Deleted)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string query = "DELETE FROM person WHERE person_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                }

                currentDataTable.AcceptChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении учителей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveStudents(DataTable changes)
        {
            try
            {
                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string firstName = row["Имя"].ToString();
                            string lastName = row["Фамилия"].ToString();
                            string patronymic = row["Отчество"] != DBNull.Value ? row["Отчество"].ToString() : "";
                            string login = row["Логин"].ToString();
                            string rights = row["Права"].ToString();

                            string query = @"UPDATE person 
                                            SET first_name = @firstName, 
                                                last_name = @lastName, 
                                                patronymic = @patronymic, 
                                                login = @login, 
                                                rights = @rights 
                                            WHERE person_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@firstName", firstName),
                                new Npgsql.NpgsqlParameter("@lastName", lastName),
                                new Npgsql.NpgsqlParameter("@patronymic", patronymic),
                                new Npgsql.NpgsqlParameter("@login", login),
                                new Npgsql.NpgsqlParameter("@rights", rights),
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                    else if (row.RowState == DataRowState.Added)
                    {
                        if (row["Имя"] != DBNull.Value && row["Фамилия"] != DBNull.Value &&
                            row["Логин"] != DBNull.Value && row["Права"] != DBNull.Value &&
                            !string.IsNullOrWhiteSpace(row["Имя"].ToString()) &&
                            !string.IsNullOrWhiteSpace(row["Фамилия"].ToString()) &&
                            !string.IsNullOrWhiteSpace(row["Логин"].ToString()) &&
                            !string.IsNullOrWhiteSpace(row["Права"].ToString()))
                        {
                            string firstName = row["Имя"].ToString();
                            string lastName = row["Фамилия"].ToString();
                            string patronymic = row["Отчество"] != DBNull.Value ? row["Отчество"].ToString() : "";
                            string login = row["Логин"].ToString();
                            string rights = row["Права"].ToString();
                            string defaultPassword = "password123";

                            string query = @"INSERT INTO person (first_name, last_name, patronymic, login, password, rights) 
                                            VALUES (@firstName, @lastName, @patronymic, @login, @password, @rights);";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@firstName", firstName),
                                new Npgsql.NpgsqlParameter("@lastName", lastName),
                                new Npgsql.NpgsqlParameter("@patronymic", patronymic),
                                new Npgsql.NpgsqlParameter("@login", login),
                                new Npgsql.NpgsqlParameter("@password", defaultPassword),
                                new Npgsql.NpgsqlParameter("@rights", rights));
                        }
                    }
                    else if (row.RowState == DataRowState.Deleted)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string query = "DELETE FROM person WHERE person_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                }

                currentDataTable.AcceptChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении учеников: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveClasses(DataTable changes)
        {
            try
            {
                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string className = row["Название класса"].ToString();

                            string query = "UPDATE class SET class_name = @className WHERE class_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@className", className),
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                    else if (row.RowState == DataRowState.Added)
                    {
                        if (row["Название класса"] != DBNull.Value &&
                            !string.IsNullOrWhiteSpace(row["Название класса"].ToString()))
                        {
                            string className = row["Название класса"].ToString();
                            string query = "INSERT INTO class (class_name) VALUES (@className);";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@className", className));
                        }
                    }
                    else if (row.RowState == DataRowState.Deleted)
                    {
                        if (row["ID", DataRowVersion.Original] != DBNull.Value)
                        {
                            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
                            string query = "DELETE FROM class WHERE class_id = @id;";
                            db.ExecuteNonQuery(query,
                                new Npgsql.NpgsqlParameter("@id", id));
                        }
                    }
                }

                currentDataTable.AcceptChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении классов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentDataTable == null) return;

                string searchText = SearchText.Text.Trim();

                if (string.IsNullOrEmpty(searchText))
                {
                    DataGrid.ItemsSource = currentDataTable.DefaultView;
                    return;
                }

                DataView filteredView = currentDataTable.DefaultView;
                string filterExpression = BuildFilterExpression(searchText);

                if (!string.IsNullOrEmpty(filterExpression))
                {
                    filteredView.RowFilter = filterExpression;
                    DataGrid.ItemsSource = filteredView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildFilterExpression(string searchText)
        {
            ComboBoxItem selectedItem = (ComboBoxItem)DataTypeComboBox.SelectedItem;
            string selectedValue = selectedItem.Content.ToString();

            string[] searchWords = searchText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            switch (selectedValue)
            {
                case "Предметы":
                    return $"CONVERT([Название предмета], System.String) LIKE '%{searchText}%'";

                case "Учителя":
                case "Ученики":
                    return $"CONVERT([Имя], System.String) LIKE '%{searchText}%' OR " +
                           $"CONVERT([Фамилия], System.String) LIKE '%{searchText}%' OR " +
                           $"CONVERT([Отчество], System.String) LIKE '%{searchText}%' OR " +
                           $"CONVERT([Логин], System.String) LIKE '%{searchText}%'";

                case "Классы":
                    return $"CONVERT([Название класса], System.String) LIKE '%{searchText}%'";

                default:
                    return "";
            }
        }
    }
}