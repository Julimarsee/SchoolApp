using Npgsql;
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace WpfApp2
{
    /// <summary>
    /// Логика взаимодействия для Window1.xaml
    /// </summary>
    public partial class MainForm : Window
    {
        public MainForm()
        {
            InitializeComponent();
            View.Content = new allGrades();
            getData();
        }

        public void OpenNewSub() =>
            View.Content = new newSub();

        private void AllGradesButton_Click(object sender, RoutedEventArgs e) =>
            getData();

        public void getData()
        {
            if (SaveData.currentWidth == 0) this.Width = 908;

            database db = new database();
            var AllGrades = new allGrades();
            View.Content = AllGrades;
            SetActiveButton(AllGradesButton);
            string Command;

            string subjectsQuery = @"
        SELECT subject_id, title
        FROM subject
        ORDER BY title";

            DataTable subjectsTable = db.DataQuery(subjectsQuery);
            var titles = subjectsTable.AsEnumerable()
                .Select(row => new
                {
                    Title = row["title"].ToString(),
                    IsOffset = false
                }).ToList();

            var pivotColumns = string.Join(",\n", titles.Select(s =>
            {
                return $@"MAX(CASE WHEN subj.title = '{s.Title}' THEN n.note ELSE NULL END) AS ""{s.Title}""";
            }));

            if (SaveData.role == "Ученик")
            {
                // Сначала получаем класс текущего ученика
                string getStudentClassQuery = $@"
            SELECT c.class_name
            FROM person p
            JOIN class_subject_teacher cst ON cst.teacher_id = p.person_id
            JOIN class c ON cst.class_id = c.class_id
            WHERE p.person_id = {SaveData.id}
            LIMIT 1";

                var classResult = db.ExecuteQuery(getStudentClassQuery);
                string studentClass = classResult != null && classResult.Count > 0
                    ? classResult[0]["class_name"].ToString()
                    : "";

                // Если класс не найден, пытаемся найти через оценки
                if (string.IsNullOrEmpty(studentClass))
                {
                    string getClassFromNotesQuery = $@"
                SELECT DISTINCT c.class_name
                FROM notes n
                JOIN class_subject_teacher cst ON n.cst_id = cst.cst_id
                JOIN class c ON cst.class_id = c.class_id
                WHERE n.fk_person_id = {SaveData.id}
                LIMIT 1";

                    classResult = db.ExecuteQuery(getClassFromNotesQuery);
                    studentClass = classResult != null && classResult.Count > 0
                        ? classResult[0]["class_name"].ToString()
                        : "";
                }

                if (string.IsNullOrEmpty(studentClass))
                {
                    // Если класс все еще не найден, показываем только ученика
                    Command = $@"
                SELECT
                    TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS ""Ученик"",
                    '' AS ""Класс"",
                    {pivotColumns}
                FROM person p
                LEFT JOIN notes n ON n.fk_person_id = p.person_id
                LEFT JOIN subject subj ON subj.subject_id = (
                    SELECT s.subject_id 
                    FROM class_subject_teacher cst 
                    JOIN subject s ON cst.subject_id = s.subject_id 
                    WHERE cst.cst_id = n.cst_id
                )
                LEFT JOIN class_subject_teacher cst ON cst.cst_id = n.cst_id
                LEFT JOIN class c ON c.class_id = cst.class_id
                WHERE p.rights = 'Ученик'
                  AND p.person_id = {SaveData.id}
                GROUP BY p.person_id, p.last_name, p.first_name, p.patronymic, c.class_name
                ORDER BY p.last_name, p.first_name";
                }
                else
                {
                    Command = $@"
                SELECT
                    TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS ""Ученик"",
                    c.class_name AS ""Класс"",
                    {pivotColumns}
                FROM person p
                LEFT JOIN notes n ON n.fk_person_id = p.person_id
                LEFT JOIN subject subj ON subj.subject_id = (
                    SELECT s.subject_id 
                    FROM class_subject_teacher cst 
                    JOIN subject s ON cst.subject_id = s.subject_id 
                    WHERE cst.cst_id = n.cst_id
                )
                LEFT JOIN class_subject_teacher cst ON cst.cst_id = n.cst_id
                LEFT JOIN class c ON c.class_id = cst.class_id
                WHERE p.rights = 'Ученик'
                  AND c.class_name = '{studentClass}'
                GROUP BY p.person_id, p.last_name, p.first_name, p.patronymic, c.class_name
                ORDER BY c.class_name, p.last_name, p.first_name";
                }
            }
            else
            {
                Command = $@"
            SELECT
                TRIM(CONCAT(p.last_name, ' ', p.first_name, ' ', COALESCE(p.patronymic, ''))) AS ""Ученик"",
                c.class_name AS ""Класс"",
                {pivotColumns}
            FROM person p
            LEFT JOIN notes n ON n.fk_person_id = p.person_id
            LEFT JOIN subject subj ON subj.subject_id = (
                SELECT s.subject_id 
                FROM class_subject_teacher cst 
                JOIN subject s ON cst.subject_id = s.subject_id 
                WHERE cst.cst_id = n.cst_id
            )
            LEFT JOIN class_subject_teacher cst ON cst.cst_id = n.cst_id
            LEFT JOIN class c ON c.class_id = cst.class_id
            WHERE p.rights = 'Ученик'
            GROUP BY p.person_id, p.last_name, p.first_name, p.patronymic, c.class_name
            ORDER BY c.class_name, p.last_name, p.first_name";
            }

            DataTable dt = db.DataQuery(Command);
            AllGrades.SetData(dt, titles.Where(t => t.IsOffset).Select(t => t.Title).ToList());
        }

        private void DistGradeButton_Click(object sender, RoutedEventArgs e) => OpenDist();

        public void OpenDist()
        {
            if (SaveData.role != "Ученик") this.Width = 1276;
            View.Content = new Dist();
            SetActiveButton(DistGradeButton);
        }

        private void AppointButton_Click(object sender, RoutedEventArgs e)
        {
            this.Width = 908;
            View.Content = new Appoint();
            SetActiveButton(AppointButton);
        }

        private void SetActiveButton(ToggleButton button)
        {
            var buttons = new[] { AllGradesButton, DistGradeButton, AppointButton, ProfileButton, AddButton, InfoButton };
            foreach (var btn in buttons)
            {
                btn.IsChecked = false;
            }

            button.IsChecked = true;
        }

        private void Window_Closed(object sender, EventArgs e) =>
            Application.Current.Shutdown();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Width = 908;
            OpenProfile();
            SetActiveButton(ProfileButton);
        }

        public void OpenProfile() => View.Content = new profile();

        public void OpenNewProf() => View.Content = new newProf();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Role.Text = SaveData.role;
            if (SaveData.role != "Администратор")
            {
                AppointButton.Visibility = Visibility.Collapsed;
                AddButton.Visibility = Visibility.Collapsed;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            this.Width = 908;
            View.Content = new Add();
            SetActiveButton(AddButton);
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            string buttonGrade = "";
            string buttonDailyGrade = "в таблице указаны оценки по всем дисциплинам у конкретного ученика";
            string buttonAppoint = "";
            string buttonAdd = "";

            if (SaveData.role != "Ученик")
            {
                buttonGrade = "\nЧтобы изменить данные таблицы, нажмите на кнопку 'Изменить таблицу', данные сохраняются автоматически\n" +
                    "Кнопка 'Сохранить' сохраняет измененные данные";
                buttonDailyGrade = @"необходимо выбрать интересующую дисциплину с помощью 'Выберите дисциплину' и затем в таблице покажутся оценки всех учеников по дате
Чтобы изменить данные в таблице, нажмите 'Изменить таблицу', изменения сохраняются автоматически
Кнопка 'Сохранить' сохраняет данные";
            }

            if (SaveData.role == "Администратор")
            {
                buttonGrade += "\nКнопка 'Управление дисциплинами' переключает страницу на 'Успеваемость'";
                buttonDailyGrade += "\nКнопка 'Удалить' удаляет выбранную дисциплину\nКнопка 'Изменить' позволяет изменить данную дисциплину";
                buttonAppoint = @"4) На странице 'Назначить', чтобы назначить учителя дисциплине в классе, необходимо выбрать класс, дисциплину и учителя с помощью полей выбора и нажать кнопку 'Назначить'
Кнопка 'Добавить дисциплину' открывает страницу создания дисциплины, кнопка 'Новый учитель' открывает страницу создания профиля";
                buttonAdd = @"5) На странице 'Добавить' указана инструкция к созданию дисциплины или профиля
Кнопка 'Новая дисциплина' открывает страницу создания дисциплины, кнопка 'Новый профиль' открывает страницу создания профиля";
            }

            MessageBox.Show($@"Инструкция:
1) На странице 'Итоговые оценки' в таблицу выведены итоговые оценки всех учеников с указанием класса;{buttonGrade}
2) На странице 'Успеваемость' {buttonDailyGrade}
3) Чтобы открыть профиль, нажмите на значок в правом верхнем углу, на странице профиль указаны данные аккаунта и кнопка 'Изменить' позволяет поменять пароль и логин
{buttonAppoint}
{buttonAdd}
");
        }
    }
}