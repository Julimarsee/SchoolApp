
using Npgsql;
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Text;

namespace WpfApp2
{
    public partial class MainForm : Window
    {
        private database db = new database();

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
            if (SaveData.currentWidth == 0)
                this.Width = 1100;

            var AllGrades = new allGrades();
            View.Content = AllGrades;
            SetActiveButton(AllGradesButton);
        }

        private void DistGradeButton_Click(object sender, RoutedEventArgs e) => OpenDist();

        public void OpenDist()
        {
            if (SaveData.role != "Ученик")
                this.Width = 1276;
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
                if (btn != null)
                    btn.IsChecked = false;
            }

            if (button != null)
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
                if (AppointButton != null)
                    AppointButton.Visibility = Visibility.Collapsed;
                if (AddButton != null)
                    AddButton.Visibility = Visibility.Collapsed;
            }
            if (SaveData.role != "Ученик") Class.Visibility = Visibility.Collapsed;
;            Name.Text = SaveData.name;
            Class.Text = SaveData.studentClass;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            this.Width = 908;
            View.Content = new Add();
            SetActiveButton(AddButton);
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            ShowInstruction();
        }

        private void ShowInstruction()
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
