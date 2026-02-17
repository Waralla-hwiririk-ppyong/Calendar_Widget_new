using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO; // 파일 입출력
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes; // WPF 도형
using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;

namespace Calender_Widget
{
	public class ScheduleItem : INotifyPropertyChanged
	{
		private bool _isCompleted;
		private string _content;
		private string _color;

		public string Content { get => _content; set { _content = value; OnPropertyChanged(nameof(Content)); } }
		public string Color { get => _color; set { _color = value; OnPropertyChanged(nameof(Color)); } }
		public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); } }

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public partial class MainWindow : Window, IDropTarget
	{
		private Dictionary<string, ObservableCollection<ScheduleItem>> _schedules = new Dictionary<string, ObservableCollection<ScheduleItem>>();
		private string _selectedDateKey = "";
		private DateTime _displayDate = DateTime.Now;
		private DateTime _selectedDate = DateTime.Now;
		private string _currentSelectedColor = "#FF00C6";
		private bool _isLocked = false;
		private int _editingIndex = -1;

		// 수정 포인트: System.IO.Path를 명시적으로 써서 Shapes.Path와의 충돌을 방지합니다.
		private readonly string _filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schedules.json");

		public MainWindow()
		{
			InitializeComponent();
			this.DataContext = this;
			InitializePalette();

			// 1. 저장된 데이터 불러오기
			LoadData();

			_selectedDateKey = DateTime.Now.ToString("yyyy-MM-dd");
			UpdateCalendarDisplay();
			ShowSchedule(DateTime.Now);

			ScheduleInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) AddSchedule_Click(s, e); };

			this.MouseDown += (s, e) => {
				if (e.LeftButton == MouseButtonState.Pressed && !_isLocked && !IsDescendantOfListBox(e.OriginalSource as DependencyObject))
					DragMove();
			};
		}

		#region [데이터 영구 저장 로직]
		private void SaveData()
		{
			try
			{
				// 데이터 정렬 등 필요한 처리가 있다면 여기서 수행 가능
				string json = JsonConvert.SerializeObject(_schedules, Formatting.Indented);
				File.WriteAllText(_filePath, json);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"저장 실패: {ex.Message}");
			}
		}

		private void LoadData()
		{
			try
			{
				if (File.Exists(_filePath))
				{
					string json = File.ReadAllText(_filePath);
					var loadedData = JsonConvert.DeserializeObject<Dictionary<string, ObservableCollection<ScheduleItem>>>(json);
					if (loadedData != null) _schedules = loadedData;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"로드 실패: {ex.Message}");
				_schedules = new Dictionary<string, ObservableCollection<ScheduleItem>>();
			}
		}
		#endregion

		#region [드래그 앤 드롭 구현 - 잔상 제거 버전]
		public void DragOver(IDropInfo dropInfo)
		{
			if (dropInfo.Data is ScheduleItem)
			{
				dropInfo.Effects = DragDropEffects.Move;
				// 기본 삽입 위치 표시선을 사용하고 싶지 않다면 아래 줄을 주석 처리하세요.
				dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
			}
		}

		public void Drop(IDropInfo dropInfo)
		{
			var sourceList = dropInfo.DragInfo.SourceCollection as ObservableCollection<ScheduleItem>;
			if (sourceList != null)
			{
				int oldIndex = dropInfo.DragInfo.SourceIndex;
				int newIndex = dropInfo.InsertIndex;

				if (newIndex > oldIndex) newIndex--;
				if (oldIndex == newIndex) return;

				sourceList.Move(oldIndex, newIndex);
				UpdateCalendarDisplay();
				SaveData(); // 순서 변경 후 즉시 저장
			}
		}
		#endregion

		#region [일정 관리 로직]
		private void AddSchedule_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(ScheduleInput.Text)) return;

			if (!_schedules.ContainsKey(_selectedDateKey))
				_schedules[_selectedDateKey] = new ObservableCollection<ScheduleItem>();

			var targetList = _schedules[_selectedDateKey];
			var newItem = new ScheduleItem { Content = ScheduleInput.Text, Color = _currentSelectedColor, IsCompleted = false };

			if (_editingIndex != -1 && _editingIndex < targetList.Count)
			{
				targetList.RemoveAt(_editingIndex);
				targetList.Insert(_editingIndex, newItem);
				_editingIndex = -1;
				EditNoticeText.Visibility = Visibility.Collapsed;
			}
			else { targetList.Add(newItem); }

			ScheduleInput.Clear();
			UpdateCalendarDisplay();
			SaveData(); // 추가 및 수정 시 저장
			ScheduleInput.Focus();
		}

		private void EditSchedule_Click(object sender, RoutedEventArgs e)
		{
			var item = (sender as Button)?.Tag as ScheduleItem;
			if (item != null)
			{
				_editingIndex = _schedules[_selectedDateKey].IndexOf(item);
				ScheduleInput.Text = item.Content;
				_currentSelectedColor = item.Color;
				CurrentColorIndicator.Fill = ConvertColor(item.Color);
				EditNoticeText.Visibility = Visibility.Visible;
				ScheduleInput.Focus();
				ScheduleInput.SelectAll();
			}
		}

		private void DeleteSchedule_Click(object sender, RoutedEventArgs e)
		{
			var item = (sender as Button)?.Tag as ScheduleItem;
			if (item != null && _schedules.ContainsKey(_selectedDateKey))
			{
				_schedules[_selectedDateKey].Remove(item);
				_editingIndex = -1;
				EditNoticeText.Visibility = Visibility.Collapsed;
				UpdateCalendarDisplay();
				SaveData(); // 삭제 시 저장
			}
		}

		private void Schedule_CheckChanged(object sender, RoutedEventArgs e)
		{
			UpdateCalendarDisplay();
			SaveData(); // 완료 상태 변경 시 저장
		}

		private void ShowSchedule(DateTime date)
		{
			_selectedDateKey = date.ToString("yyyy-MM-dd");
			SelectedDateText.Text = date.ToString("M월 d일 일정");
			if (!_schedules.ContainsKey(_selectedDateKey))
				_schedules[_selectedDateKey] = new ObservableCollection<ScheduleItem>();

			ScheduleListBox.ItemsSource = _schedules[_selectedDateKey];
		}
		#endregion

		#region [유틸리티 및 달력 제어]
		private void InitializePalette()
		{
			string[] colors = { "#FF00C6", "#FF5252", "#FF4081", "#E040FB", "#7C4DFF", "#536DFE", "#448AFF", "#40C4FF", "#00E2FF", "#69F0AE", "#B2FF59", "#FFD740" };
			PaletteGrid.Children.Clear();
			foreach (var colorCode in colors)
			{
				Button btn = new Button { Width = 28, Height = 28, Margin = new Thickness(5), Cursor = Cursors.Hand };
				btn.Template = CreateRoundButtonTemplate(colorCode);
				btn.Click += (s, e) => {
					_currentSelectedColor = colorCode;
					CurrentColorIndicator.Fill = ConvertColor(colorCode);
					ColorPalettePopup.IsOpen = false;
				};
				PaletteGrid.Children.Add(btn);
			}
		}

		private void ToggleLock_Click(object sender, RoutedEventArgs e)
		{
			_isLocked = !_isLocked;
			LockBtn.Content = _isLocked ? "\uE1F6" : "\uE1F7";
			LockBtn.Foreground = _isLocked ? new SolidColorBrush(Color.FromRgb(255, 85, 85)) : Brushes.Gray;
		}

		private bool IsDescendantOfListBox(DependencyObject element)
		{
			while (element != null) { if (element is ListBox) return true; element = VisualTreeHelper.GetParent(element); }
			return false;
		}

		private Brush ConvertColor(string colorCode) => (SolidColorBrush)new BrushConverter().ConvertFromString(colorCode) ?? Brushes.Gray;

		private ControlTemplate CreateRoundButtonTemplate(string color) => (ControlTemplate)System.Windows.Markup.XamlReader.Parse($@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Ellipse Fill='{color}' Stroke='White' StrokeThickness='2'/></ControlTemplate>");

		private void ColorPickerButton_Click(object sender, RoutedEventArgs e) => ColorPalettePopup.IsOpen = !ColorPalettePopup.IsOpen;

		private void UpdateCalendarDisplay() { if (DateText != null) DateText.Text = _displayDate.ToString("yyyy. MM"); GenerateCalendar(_displayDate); }

		private void PrevMonth_Click(object sender, RoutedEventArgs e) { _displayDate = _displayDate.AddMonths(-1); UpdateCalendarDisplay(); }

		private void NextMonth_Click(object sender, RoutedEventArgs e) { _displayDate = _displayDate.AddMonths(1); UpdateCalendarDisplay(); }

		private void GoToToday_Click(object sender, RoutedEventArgs e) { _displayDate = DateTime.Now; _selectedDate = DateTime.Now; ShowSchedule(DateTime.Now); UpdateCalendarDisplay(); }

		private void GenerateCalendar(DateTime targetDate)
		{
			if (CalendarGrid == null) return;
			CalendarGrid.Children.Clear();
			DateTime firstDay = new DateTime(targetDate.Year, targetDate.Month, 1);
			int startDay = (int)firstDay.DayOfWeek;

			for (int i = 0; i < startDay; i++) CalendarGrid.Children.Add(new Border());

			for (int day = 1; day <= DateTime.DaysInMonth(targetDate.Year, targetDate.Month); day++)
			{
				DateTime date = new DateTime(targetDate.Year, targetDate.Month, day);
				string key = date.ToString("yyyy-MM-dd");
				Button btn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Height = 45 };
				StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

				if (date.Date == _selectedDate.Date)
				{
					Grid g = new Grid();
					g.Children.Add(new Ellipse { Fill = new SolidColorBrush(Color.FromRgb(85, 85, 255)), Width = 28, Height = 28 });
					g.Children.Add(new TextBlock { Text = day.ToString(), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold });
					sp.Children.Add(g);
				}
				else
				{
					sp.Children.Add(new TextBlock
					{
						Text = day.ToString(),
						HorizontalAlignment = HorizontalAlignment.Center,
						Foreground = (date.Date == DateTime.Now.Date) ? new SolidColorBrush(Color.FromRgb(85, 85, 255)) : new SolidColorBrush(Color.FromRgb(80, 80, 80)),
						FontSize = 12,
						FontWeight = (date.Date == DateTime.Now.Date) ? FontWeights.Bold : FontWeights.Normal
					});
				}

				if (_schedules.ContainsKey(key) && _schedules[key].Count > 0)
				{
					StackPanel dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
					foreach (var item in _schedules[key].Take(3))
						dots.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = ConvertColor(item.Color), Margin = new Thickness(1.5) });
					sp.Children.Add(dots);
				}

				btn.Content = sp;
				btn.Click += (s, e2) => { _selectedDate = date; ShowSchedule(date); UpdateCalendarDisplay(); };
				CalendarGrid.Children.Add(btn);
			}
		}
		#endregion
	}
}