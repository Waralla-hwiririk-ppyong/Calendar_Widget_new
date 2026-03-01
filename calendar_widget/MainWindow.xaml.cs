using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;

namespace Calender_Widget
{
	public partial class MainWindow : Window, GongSolutions.Wpf.DragDrop.IDropTarget
	{
		private Dictionary<string, ObservableCollection<ScheduleItem>> _schedules = new Dictionary<string, ObservableCollection<ScheduleItem>>();
		private string _selectedDateKey = "";
		private DateTime _displayDate = DateTime.Now;
		private DateTime _selectedDate = DateTime.Now;
		private string _currentSelectedColor = "#FF00C6";
		private bool _isLocked = false;
		private int _editingIndex = -1;
		private readonly string _filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schedules.json");

		// WinForms용 NotifyIcon은 전체 이름을 다 적어줍니다.
		private System.Windows.Forms.NotifyIcon _notifyIcon;

		public MainWindow()
		{
			InitializeComponent();
			this.DataContext = this;
			InitializePalette();
			LoadData();
			SetupTrayIcon();

			_selectedDateKey = DateTime.Now.ToString("yyyy-MM-dd");
			UpdateCalendarDisplay();
			ShowSchedule(DateTime.Now);

			ScheduleInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) AddSchedule_Click(s, e); };

			this.MouseDown += (s, e) => {
				if (e.LeftButton == MouseButtonState.Pressed && !_isLocked && !IsDescendantOfListBox(e.OriginalSource as DependencyObject))
					DragMove();
			};
		}

		private void SetupTrayIcon()
		{
			_notifyIcon = new System.Windows.Forms.NotifyIcon();
			try { _notifyIcon.Icon = new System.Drawing.Icon("icon_nb.ico"); }
			catch { _notifyIcon.Icon = System.Drawing.SystemIcons.Application; }

			_notifyIcon.Visible = true;
			_notifyIcon.Text = "Calendar Widget";

			System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
			menu.Items.Add("열기", null, (s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
			menu.Items.Add("종료", null, (s, e) => System.Windows.Application.Current.Shutdown());
			_notifyIcon.ContextMenuStrip = menu;

			_notifyIcon.DoubleClick += (s, e) => { this.Show(); this.Activate(); };
		}

		#region [데이터 및 달력 로직]
		private void SaveData()
		{
			try { File.WriteAllText(_filePath, JsonConvert.SerializeObject(_schedules, Formatting.Indented)); }
			catch { }
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
			catch { }
		}

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

				System.Windows.Controls.Button btn = new System.Windows.Controls.Button
				{
					Style = (Style)this.Resources["CalendarDayButtonStyle"],
					Height = 55,
					Cursor = System.Windows.Input.Cursors.Hand
				};

				Grid cellGrid = new Grid();
				cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
				cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });

				UIElement dayContent;
				if (date.Date == _selectedDate.Date)
				{
					Grid g = new Grid { VerticalAlignment = VerticalAlignment.Center };
					// Color 명시적 수정
					g.Children.Add(new Ellipse { Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 255)), Width = 28, Height = 28 });
					// Brushes 명시적 수정
					g.Children.Add(new TextBlock { Text = day.ToString(), Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold });
					dayContent = g;
				}
				else
				{
					dayContent = new TextBlock
					{
						Text = day.ToString(),
						HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						// Color 및 Brush 명시적 수정
						Foreground = (date.Date == DateTime.Now.Date) ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 255)) : (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#44474A"),
						FontSize = 12,
						FontWeight = (date.Date == DateTime.Now.Date) ? FontWeights.Bold : FontWeights.Normal
					};
				}
				Grid.SetRow(dayContent, 0);
				cellGrid.Children.Add(dayContent);

				if (_schedules.ContainsKey(key) && _schedules[key].Count > 0)
				{
					var daySchedules = _schedules[key];
					StackPanel indicatorPanel = new StackPanel
					{
						Orientation = System.Windows.Controls.Orientation.Horizontal,
						HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Top
					};

					if (daySchedules.Count <= 3)
					{
						foreach (var item in daySchedules)
							indicatorPanel.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = ConvertColor(item.Color), Margin = new Thickness(1.5) });
					}
					else
					{
						foreach (var item in daySchedules.Take(3))
							indicatorPanel.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = ConvertColor(item.Color), Margin = new Thickness(1.5) });

						indicatorPanel.Children.Add(new TextBlock
						{
							Text = $"+{daySchedules.Count - 3}",
							FontSize = 9,
							FontWeight = FontWeights.Bold,
							// Color 명시적 수정
							Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120)),
							Margin = new Thickness(2, -1, 0, 0)
						});
					}
					Grid.SetRow(indicatorPanel, 1);
					cellGrid.Children.Add(indicatorPanel);
				}

				btn.Content = cellGrid;
				btn.Click += (s, e) => { _selectedDate = date; ShowSchedule(date); UpdateCalendarDisplay(); };
				CalendarGrid.Children.Add(btn);
			}
		}
		#endregion

		#region [일정 기능]
		public void AddSchedule_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(ScheduleInput.Text)) return;
			if (!_schedules.ContainsKey(_selectedDateKey)) _schedules[_selectedDateKey] = new ObservableCollection<ScheduleItem>();
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

		public void EditSchedule_Click(object sender, RoutedEventArgs e)
		{
			var item = (sender as System.Windows.Controls.Button)?.Tag as ScheduleItem;
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

		public void DeleteSchedule_Click(object sender, RoutedEventArgs e)
		{
			var item = (sender as System.Windows.Controls.Button)?.Tag as ScheduleItem;
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

		public void Schedule_CheckChanged(object sender, RoutedEventArgs e)
		{
			if (this.IsLoaded) { SaveData(); UpdateCalendarDisplay(); }
		}
		#endregion

		#region [유틸리티]
		private void InitializePalette()
		{
			string[] colors = { "#FF00C6", "#FF5252", "#FF4081", "#E040FB", "#7C4DFF", "#536DFE", "#448AFF", "#40C4FF", "#00E2FF", "#69F0AE", "#B2FF59", "#FFD740" };
			PaletteGrid.Children.Clear();
			foreach (var colorCode in colors)
			{
				System.Windows.Controls.Button btn = new System.Windows.Controls.Button
				{
					Width = 28,
					Height = 28,
					Margin = new Thickness(5),
					Cursor = System.Windows.Input.Cursors.Hand
				};
				btn.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse($@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Ellipse Fill='{colorCode}' Stroke='White' StrokeThickness='2'/></ControlTemplate>");
				btn.Click += (s, e) => {
					_currentSelectedColor = colorCode;
					CurrentColorIndicator.Fill = ConvertColor(colorCode);
					ColorPalettePopup.IsOpen = false;
				};
				PaletteGrid.Children.Add(btn);
			}
		}

		private void ToggleSettings_Click(object sender, RoutedEventArgs e)
		{
			SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
				? Visibility.Collapsed
				: Visibility.Visible;
		}

		private void ToggleLock_Click(object sender, RoutedEventArgs e)
		{
			_isLocked = !_isLocked;
			LockBtn.Content = _isLocked ? "\uE1F6" : "\uE1F7";
			// Color 및 Brushes 명시적 수정
			LockBtn.Foreground = _isLocked ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 85, 85)) : System.Windows.Media.Brushes.Gray;
		}

		// Brush 타입 명시적 수정
		private System.Windows.Media.Brush ConvertColor(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex) ?? System.Windows.Media.Brushes.Gray;

		private bool IsDescendantOfListBox(DependencyObject element)
		{
			while (element != null)
			{
				if (element is System.Windows.Controls.ListBox) return true;
				element = VisualTreeHelper.GetParent(element);
			}
			return false;
		}
        // 🔹 추가됨: 우측 상단 닫기 버튼 클릭 로직
        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            // (트레이 데몬으로 유지)
            this.Hide();
        }

        // 🔹 데몬 실행을 위한 기존 추가 로직 (참고용)
        private bool _isExiting = false;

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExiting)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnClosed(e);
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e) { _displayDate = _displayDate.AddMonths(-1); UpdateCalendarDisplay(); }
		private void NextMonth_Click(object sender, RoutedEventArgs e) { _displayDate = _displayDate.AddMonths(1); UpdateCalendarDisplay(); }
		private void GoToToday_Click(object sender, RoutedEventArgs e) { _displayDate = DateTime.Now; _selectedDate = DateTime.Now; ShowSchedule(DateTime.Now); UpdateCalendarDisplay(); }
		private void ColorPickerButton_Click(object sender, RoutedEventArgs e) => ColorPalettePopup.IsOpen = !ColorPalettePopup.IsOpen;

		public void DragOver(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
		{
			dropInfo.Effects = System.Windows.DragDropEffects.Move;
			dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
		}
		public void Drop(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
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