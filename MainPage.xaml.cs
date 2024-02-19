using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Globalization;

namespace SystemLosow
{
    public partial class MainPage : ContentPage
    {
        private List<string> _classes = new List<string>();
        private string _selectedClass;
        private Dictionary<string, List<Student>> _students = new Dictionary<string, List<Student>>();
        private Random _random = new Random();
        private int _luckyNumber;
        private HashSet<int> _drawnStudents = new HashSet<int>();
        private const string ClassesFilePath = "classes.txt";

        private const string ToggleAbsenteesKey = "ToggleAbsenteesSwitchState";
        private readonly string _classesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Classes");

        public MainPage()
        {
            InitializeComponent();
            LoadClassesAndStudents();
            SetupClassPicker();
            UpdateStudentsListView();
            GenerateInitialLuckyNumber();
            ToggleAbsenteesSwitch.IsToggled = Preferences.Get(ToggleAbsenteesKey, false);
            ToggleAbsenteesSwitch.Toggled += OnToggleAbsenteesSwitchToggled;
        }

        private void OnToggleAbsenteesSwitchToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(ToggleAbsenteesKey, e.Value);
        }

        private void SetupClassPicker()
        {
            ClassPicker.ItemsSource = _classes;
            ClassPicker.SelectedIndexChanged += OnClassPickerSelectedIndexChanged;
        }

        private void OnClassPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            if (ClassPicker.SelectedIndex == -1)
            {
                _selectedClass = null;
            }
            else
            {
                _selectedClass = ClassPicker.Items[ClassPicker.SelectedIndex];
                UpdateStudentsListView();
            }
        }

        private async void OnCreateClassClicked(object sender, EventArgs e)
        {
            string className = await DisplayPromptAsync("Create Class", "Enter class name:");
            if (!string.IsNullOrWhiteSpace(className) && !_classes.Contains(className))
            {
                _classes.Add(className);
                _students.Add(className, new List<Student>());
                SaveClassesAndStudents();
                ClassPicker.ItemsSource = null;
                ClassPicker.ItemsSource = _classes;
            }
        }

        private void OnDeleteClassClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedClass))
            {
                _classes.Remove(_selectedClass);
                _students.Remove(_selectedClass);
                _selectedClass = null;
                SaveClassesAndStudents();
                UpdateStudentsListView();
                ClassPicker.ItemsSource = null;
                ClassPicker.ItemsSource = _classes;
            }
        }

        private async void OnSelectClassClicked(object sender, EventArgs e)
        {
            string result = await DisplayActionSheet("Select Class", "Cancel", null, _classes.ToArray());
            if (result != "Cancel" && _classes.Contains(result))
            {
                _selectedClass = result;
                UpdateStudentsListView();
            }
        }

        private void OnAddStudentClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(StudentNameEntry.Text) && _selectedClass != null)
            {
                var student = new Student
                {
                    Name = StudentNameEntry.Text,
                    Number = _students[_selectedClass].Count + 1
                };
                _students[_selectedClass].Add(student);
                SaveClassesAndStudents();
                StudentNameEntry.Text = string.Empty;
                UpdateStudentsListView();
            }
        }

        private void OnDeleteStudentClicked(object sender, EventArgs e)
        {
            if (!(sender is Button button)) return;
            var student = button.CommandParameter as Student;
            if (student == null || _selectedClass == null) return;

            _students[_selectedClass].Remove(student);
            SaveClassesAndStudents();
            UpdateStudentsListView();
        }

        private void OnToggleAttendanceClicked(object sender, EventArgs e)
        {
            if (!(sender is Button button)) return;
            var student = button.CommandParameter as Student;
            if (student == null || _selectedClass == null) return;

            student.IsPresent = !student.IsPresent;
            SaveClassesAndStudents();
            UpdateStudentsListView();
        }

        private void GenerateInitialLuckyNumber()
        {
            _luckyNumber = _random.Next(1, 31);
            LuckyNumberLabel.Text = $"Lucky Number: {_luckyNumber}";
        }

        private void OnLuckyNumberClicked(object sender, EventArgs e)
        {
            GenerateInitialLuckyNumber();
        }

        private void OnDrawStudentClicked(object sender, EventArgs e)
        {
            if (_selectedClass == null || !_students.ContainsKey(_selectedClass) || _students[_selectedClass].Count == 0)
            {
                DisplayAlert("Error", "No class selected or class has no students.", "OK");
                return;
            }

            foreach (var student in _students[_selectedClass])
            {
                if (student.RoundsUntilEligible > 0) student.RoundsUntilEligible--;
            }

            List<Student> eligibleStudents = _students[_selectedClass]
                .Where(s => (ToggleAbsenteesSwitch.IsToggled || s.IsPresent) && s.RoundsUntilEligible == 0)
                .ToList();

            if (eligibleStudents.Count == 0)
            {
                DisplayAlert("Info", "No eligible students to draw.", "OK");
                return;
            }

            var drawnStudent = eligibleStudents[_random.Next(eligibleStudents.Count)];
            drawnStudent.RoundsUntilEligible = 3; // kolejka na 3 rundy

            // czy ma szczesliwy
            string alertMessage = drawnStudent.Number == _luckyNumber
                ? $"{drawnStudent.Name} has been drawn BUT he has lucky number!"
                : $"{drawnStudent.Name} has been drawn.";

            DisplayAlert("Draw", alertMessage, "OK");
            SaveClassesAndStudents();
        }



        private void InitializeClassesDirectory()
        {
            if (!Directory.Exists(_classesDirectory))
            {
                Directory.CreateDirectory(_classesDirectory);
            }
        }

        private void LoadClassesAndStudents()
        {
            _classes.Clear();
            _students.Clear();
            foreach (var file in Directory.GetFiles(_classesDirectory))
            {
                var className = Path.GetFileNameWithoutExtension(file);
                _classes.Add(className);
                var studentList = new List<Student>();
                var lines = File.ReadAllLines(file);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 4)
                    {
                        studentList.Add(new Student
                        {
                            Name = parts[0],
                            IsPresent = bool.Parse(parts[1]),
                            Number = int.Parse(parts[2]),
                            RoundsUntilEligible = int.Parse(parts[3])
                        });
                    }
                }
                _students.Add(className, studentList);
            }
        }

        private void SaveClassesAndStudents()
        {
            InitializeClassesDirectory();
            foreach (var file in Directory.GetFiles(_classesDirectory))
            {
                File.Delete(file);
            }
            foreach (var className in _classes)
            {
                var filePath = Path.Combine(_classesDirectory, className + ".txt");
                var studentLines = _students[className].Select(s => $"{s.Name},{s.IsPresent},{s.Number},{s.RoundsUntilEligible}");
                File.WriteAllLines(filePath, studentLines);
            }
        }


        private void UpdateStudentsListView()
        {
            if (_selectedClass != null && _students.ContainsKey(_selectedClass))
            {
                StudentsListView.ItemsSource = null;
                StudentsListView.ItemsSource = _students[_selectedClass];
            }
            else
            {
                StudentsListView.ItemsSource = null;
            }
        }

        class Student : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private string name;
            public string Name
            {
                get => name;
                set
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }

            private bool isPresent = true;
            public bool IsPresent
            {
                get => isPresent;
                set
                {
                    isPresent = value;
                    OnPropertyChanged(nameof(IsPresent));
                }
            }

            private int number;
            public int Number
            {
                get => number;
                set
                {
                    number = value;
                    OnPropertyChanged(nameof(Number));
                }
            }

            private int roundsUntilEligible = 0;
            public int RoundsUntilEligible
            {
                get => roundsUntilEligible;
                set
                {
                    roundsUntilEligible = value;
                    OnPropertyChanged(nameof(RoundsUntilEligible));
                }
            }

            public string NumberWithPeriod => $"{Number}. ";

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value)
            {
                return Colors.Green;
            }
            return Colors.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
