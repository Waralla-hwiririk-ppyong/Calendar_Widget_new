using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;

namespace Calender_Widget
{
	public partial class MainWindow : Window, IDropTarget
	{
		private Dictionary<string, ObservableCollection<ScheduleItem>> _schedules = new Dictionary<string, ObservableCollection<ScheduleItem>>();
		private string _selectedDateKey = "";
		private DateTime _displayDate = DateTime.Now;
		private DateTime _selectedDate = DateTime.Now;
		private string _currentSelectedColor = "#FF00C6";
		private bool _isLocked = false;
		private int _editingIndex = -1;

		// Path 충돌 방지를 위해 System.IO.Path 명시
		private readonly string _filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schedules.json");

		public MainWindow()
		{
			InitializeComponent();
			this.DataContext = this;
			InitializePalette();
			LoadData();

			_selectedDateKey = DateTime.Now.ToString("yyyy-MM-dd");
			UpdateCalendarDisplay();
			ShowSchedule(DateTime.Now);

			// 엔터키 입력 처리
			ScheduleInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) AddSchedule_Click(s, e); };

			// 윈도우 드래그 (잠금 상태 아닐 때만)
			this.MouseDown += (s, e) => {
				if (e.LeftButton == MouseButtonState.Pressed && !_isLocked && !IsDescendantOfListBox(e.OriginalSource as DependencyObject))
					DragMove();
			};
		}

		#region [데이터 관리]
		private void SaveData()
		{
			try { File.WriteAllText(_filePath, JsonConvert.SerializeObject(_schedules, Formatting.Indented)); }
			catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"저장 실패: {ex.Message}"); }
		}

		private void LoadData()
		{
			try
			{
				if (File.Exists(_filePath))
				{
					var data = JsonConvert.DeserializeObject<Dictionary<string, ObservableCollection<ScheduleItem>>>(File.ReadAllText(_filePath));
					if (data != null) _schedules = data;
				}
			}
			catch { _schedules = new Dictionary<string, ObservableCollection<ScheduleItem>>(); }
		}
		#endregion

		#region [달력 생성]
		private void UpdateCalendarDisplay()
		{
			if (DateText != null) DateText.Text = _displayDate.ToString("yyyy. MM");
			GenerateCalendar(_displayDate);
		}

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

				Button btn = new Button
				{
					Style = (Style)this.Resources["CalendarDayButtonStyle"],
					Height = 55,
					Cursor = Cursors.Hand
				};

				Grid cellGrid = new Grid();
				cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // 숫자 영역
				cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) }); // 점 영역

				UIElement dayContent;
				if (date.Date == _selectedDate.Date)
				{
					Grid g = new Grid { VerticalAlignment = VerticalAlignment.Center };
					g.Children.Add(new Ellipse { Fill = new SolidColorBrush(Color.FromRgb(85, 85, 255)), Width = 28, Height = 28 });
					g.Children.Add(new TextBlock { Text = day.ToString(), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold });
					dayContent = g;
				}
				else
				{
					dayContent = new TextBlock
					{
						Text = day.ToString(),
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						Foreground = (date.Date == DateTime.Now.Date) ? new SolidColorBrush(Color.FromRgb(85, 85, 255)) : (Brush)new BrushConverter().ConvertFromString("#44474A"),
						FontSize = 12,
						FontWeight = (date.Date == DateTime.Now.Date) ? FontWeights.Bold : FontWeights.Normal
					};
				}
				Grid.SetRow(dayContent, 0);
				cellGrid.Children.Add(dayContent);

				if (_schedules.ContainsKey(key) && _schedules[key].Count > 0)
				{
					StackPanel dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
					foreach (var item in _schedules[key].Take(3))
						dots.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = ConvertColor(item.Color), Margin = new Thickness(1.5) });
					Grid.SetRow(dots, 1);
					cellGrid.Children.Add(dots);
				}

				btn.Content = cellGrid;
				btn.Click += (s, e2) => { _selectedDate = date; ShowSchedule(date); UpdateCalendarDisplay(); };
				CalendarGrid.Children.Add(btn);
			}
		}
		#endregion

		#region [일정 기능]
		private void AddSchedule_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(ScheduleInput.Text)) return;

			if (!_schedules.ContainsKey(_selectedDateKey))
				_schedules[_selectedDateKey] = new ObservableCollection<ScheduleItem>();

			var targetList = _schedules[_selectedDateKey];

			if (_editingIndex != -1 && _editingIndex < targetList.Count)
			{
				targetList[_editingIndex].Content = ScheduleInput.Text;
				targetList[_editingIndex].Color = _currentSelectedColor;
				_editingIndex = -1;
				EditNoticeText.Visibility = Visibility.Collapsed;
			}
			else
			{
				targetList.Add(new ScheduleItem { Content = ScheduleInput.Text, Color = _currentSelectedColor, IsCompleted = false });
			}

			ScheduleInput.Clear();
			UpdateCalendarDisplay();
			SaveData();
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
			}
		}

		private void DeleteSchedule_Click(object sender, RoutedEventArgs e)
		{
			var item = (sender as Button)?.Tag as ScheduleItem;
			if (item != null)
			{
				_schedules[_selectedDateKey].Remove(item);
				_editingIndex = -1;
				EditNoticeText.Visibility = Visibility.Collapsed;
				UpdateCalendarDisplay();
				SaveData();
			}
		}

		private void ShowSchedule(DateTime date)
		{
			_selectedDateKey = date.ToString("yyyy-MM-dd");
			SelectedDateText.Text = date.ToString("M월 d일 일정");
			if (!_schedules.ContainsKey(_selectedDateKey)) _schedules[_selectedDateKey] = new ObservableCollection<ScheduleItem>();
			ScheduleListBox.ItemsSource = _schedules[_selectedDateKey];
		}

		private void Schedule_CheckChanged(object sender, RoutedEventArgs e) { SaveData(); UpdateCalendarDisplay(); }
		#endregion

		#region [유틸리티]
		private void InitializePalette()
		{
			string[] colors = { "#FF00C6", "#FF5252", "#FF4081", "#E040FB", "#7C4DFF", "#536DFE", "#448AFF", "#40C4FF", "#00E2FF", "#69F0AE", "#B2FF59", "#FFD740" };
			PaletteGrid.Children.Clear();
			foreach (var colorCode in colors)
			{
				Button btn = new Button { Width = 28, Height = 28, Margin = new Thickness(5), Cursor = Cursors.Hand };
				btn.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse($@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Ellipse Fill='{colorCode}' Stroke='White' StrokeThickness='2'/></ControlTemplate>");
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
			LockBtn.Content = _isLocked ? "\uE1F6" : "\uE1F7"; // MDL2 Assets 잠금 아이콘
			LockBtn.Foreground = _isLocked ? new SolidColorBrush(Color.FromRgb(255, 85, 85)) : Brushes.Gray;
		}

		private Brush ConvertColor(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex) ?? Brushes.Gray;

		private bool IsDescendantOfListBox(DependencyObject element)
		{
			while (element != null) { if (element is ListBox) return true; element = VisualTreeHelper.GetParent(element); }
			return false;
		}

		private void PrevMonth_Click(object sender, RoutedEventArgs e) { _displayDate = _displayDate.AddMonths(-1); UpdateCalendarDisplay(); }
		private void NextMonth_Click(object sender, RoutedEventArgs e) { _displayDate = _displayDate.AddMonths(1); UpdateCalendarDisplay(); }
		private void GoToToday_Click(object sender, RoutedEventArgs e) { _displayDate = DateTime.Now; _selectedDate = DateTime.Now; ShowSchedule(DateTime.Now); UpdateCalendarDisplay(); }
		private void ColorPickerButton_Click(object sender, RoutedEventArgs e) => ColorPalettePopup.IsOpen = !ColorPalettePopup.IsOpen;

		// 드래그 앤 드롭 구현
		public void DragOver(IDropInfo dropInfo) { dropInfo.Effects = DragDropEffects.Move; dropInfo.DropTargetAdorner = DropTargetAdorners.Insert; }
		public void Drop(IDropInfo dropInfo)
		{
			var list = dropInfo.DragInfo.SourceCollection as ObservableCollection<ScheduleItem>;
			if (list != null)
			{
				int oldIdx = dropInfo.DragInfo.SourceIndex;
				int newIdx = dropInfo.InsertIndex;
				if (newIdx > oldIdx) newIdx--;
				list.Move(oldIdx, newIdx);
				SaveData();
			}
		}
		#endregion
	}

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
}