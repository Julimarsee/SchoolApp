using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp2
{
    /// <summary>
    /// Логика взаимодействия для Add.xaml
    /// </summary>
    public partial class Add : Page
    {
        public Add()
        {
            InitializeComponent();
        }

        private void AddDist_Click(object sender, RoutedEventArgs e) =>
            ((MainForm)Window.GetWindow(this)).OpenNewSub();

        private void AddProf_Click(object sender, RoutedEventArgs e) =>
            ((MainForm)Window.GetWindow(this)).OpenNewProf();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Обновляем текст инструкции для новой базы данных
            InstructionText.Text = @"Инструкция по добавлению:

1. ДОБАВЛЕНИЕ ДИСЦИПЛИНЫ:
   • Нажмите кнопку 'Новая дисциплина'
   • Заполните название дисциплины
   • Выберите специальность
   • Выберите учителей, которые могут вести эту дисциплину
   • Нажмите 'Сохранить'
   • После создания дисциплины, назначьте ее на классы на странице 'Назначить'

2. ДОБАВЛЕНИЕ УЧИТЕЛЯ:
   • Нажмите кнопку 'Новый учитель'
   • Заполните ФИО учителя
   • Придумайте логин и пароль (логин не менее 5 символов, пароль не менее 8)
   • Выберите тип учетной записи 'Учитель'
   • Выберите дисциплины, которые будет вести учитель
   • Нажмите 'Сохранить'
   • После создания учителя, назначьте его на классы на странице 'Назначить'

3. ДОБАВЛЕНИЕ УЧЕНИКА:
   • Нажмите кнопку 'Новый учитель' (создание нового профиля)
   • Заполните ФИО ученика
   • Придумайте логин и пароль
   • Выберите тип учетной записи 'Ученик'
   • Нажмите 'Сохранить'

4. ДОБАВЛЕНИЕ КЛАССА:
   • Для добавления нового класса необходимо обратиться к администратору базы данных
   • Классы создаются через SQL запрос: INSERT INTO class (class_name, year) VALUES ('5А', 2024)

5. НАЗНАЧЕНИЕ УЧИТЕЛЯ НА ПРЕДМЕТ В КЛАССЕ:
   • Перейдите на страницу 'Назначить'
   • Выберите класс, дисциплину и учителя
   • Нажмите 'Назначить'

Примечания:
- Дисциплина может преподаваться в нескольких классах разными учителями
- Один учитель может вести несколько дисциплин в разных классах
- В одном классе на одну дисциплину может быть назначен только один учитель
- Для работы с оценками необходимо сначала назначить учителя на предмет в классе";
        }
    }
}