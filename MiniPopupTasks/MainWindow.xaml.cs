using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Timers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using SharedClasses;

namespace MiniPopupTasks
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private const string cThisAppName = "MiniPopupTasks";
		private static GUITHREADINFO guiInfo;
		private ObservableCollection<MyMenuItem> currentList = new ObservableCollection<MyMenuItem>();
		[DllImport("user32.dll", EntryPoint = "GetGUIThreadInfo")]
		private static extern bool GetGUIThreadInfo(uint tId, out GUITHREADINFO threadInfo);
		[DllImport("User32", EntryPoint = "ClientToScreen", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
		private static extern int ClientToScreen(IntPtr hWnd, [In, Out] POINT pt);
		[DllImport("user32.dll")]
		private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		public static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		private readonly string PastingListFilepath = SettingsInterop.GetFullFilePathInLocalAppdata("pastinglist.txt", cThisAppName);
		private readonly string BatchCommandsDir = System.IO.Path.GetDirectoryName(SettingsInterop.GetFullFilePathInLocalAppdata("", cThisAppName)).TrimEnd('\\')
			+ "\\BatchCommands";

		public MainWindow()
		{
			//Add commands to open/close cdrom?
			InitializeComponent();
		}

		private void TempLog(string message)
		{
			tmpwin.textbox1.Text += "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine;
			tmpwin.textbox1.ScrollToEnd();
		}

		bool rightButtonDown = false;
		int markNextRightButtonUpDoBePrevented = 0;
		UserActivityHook hook = null;
		//private Point lastMiddleClickMousePos = new Point(0, 0);
		TempLogWindow tmpwin;
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			tmpwin = new TempLogWindow();
			tmpwin.Show();
			var workAreaTempWin = System.Windows.Forms.Screen.AllScreens.Last().WorkingArea;
			tmpwin.Left = workAreaTempWin.Left + workAreaTempWin.Width / 2 - tmpwin.Width / 2;
			tmpwin.Top = workAreaTempWin.Top + workAreaTempWin.Height / 2 - tmpwin.Height / 2;
			tmpwin.WindowState = System.Windows.WindowState.Maximized;

			//mainGrid.Width = 150;//SystemParameters.WorkArea.Width;
			//mainGrid.Height = 200;//SystemParameters.WorkArea.Height;
			this.UpdateLayout();
			this.Left = SystemParameters.WorkArea.Right - this.ActualWidth;
			this.Top = SystemParameters.WorkArea.Bottom - this.ActualHeight;
			//ApplyScale(miniScale);
			//mainGrid.RenderTransformOrigin = new Point(1, 1);

			this.HideThisWindow();
			//Timer timer = new Timer(500) { Enabled = true, AutoReset = true };
			//timer.Elapsed += (sn, ev) =>
			//{
			//    this.Dispatcher.BeginInvoke((Action)delegate { ShowThisWindow(); });
			//};
			//timer.Start();
			this.FocusVisualStyle = null;

			hook = new UserActivityHook(true, true);
			hook.KeyDown += (sn, ev) =>
			{
				//if (ev.KeyCode != System.Windows.Forms.Keys.Z
				//    && ev.KeyCode != System.Windows.Forms.Keys.LWin
				//    && ev.KeyCode != System.Windows.Forms.Keys.RWin)
				lastInputKeyboardNotMouse = true;
			};
			hook.KeyUp += (sn, ev) =>
			{
				if (ev.KeyCode == System.Windows.Forms.Keys.Escape)
				{
					if (this.Visibility == System.Windows.Visibility.Visible)
						HideThisWindow();
				}
			};
			hook.OnMouseActivityWithReturnHandledState += (sn, ev) =>
			{
				try
				{
					if (ev.Button.Button == System.Windows.Forms.MouseButtons.None)
						return false;

					try
					{
						TempLog("rightButtonDown = " + rightButtonDown);
						TempLog("markNextRightButtonUpDoBePrevented = " + markNextRightButtonUpDoBePrevented);

						if (ev.Button.Button == System.Windows.Forms.MouseButtons.Left
							|| ev.Button.Button == System.Windows.Forms.MouseButtons.Middle
							|| ev.Button.Button == System.Windows.Forms.MouseButtons.Right)
						{
							if (ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.Up
								&& !listboxItems.IsMouseOver
								&& DateTime.Now.Subtract(lastShowTime) > minimumShowTimeBeforeHiding
								&& (mainGrid.ContextMenu == null || !mainGrid.ContextMenu.IsOpen)
								&& this.IsVisible
								&& (ev.Button.Button == System.Windows.Forms.MouseButtons.Left
									|| (ev.Button.Button == System.Windows.Forms.MouseButtons.Right && markNextRightButtonUpDoBePrevented == 0)))
							{
								TempLog("Hiding on miss-clicked and past minimun show time");
								this.HideThisWindow();
							}
						}

						if (ev.Button.Button == System.Windows.Forms.MouseButtons.Right)
						{
							if (ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.Up)
							{
								TempLog("Setting rightButtonDown to false");
								rightButtonDown = false;
								if (markNextRightButtonUpDoBePrevented > 0)
								{
									TempLog("Handling, decreasing markNextRightButtonUpDoBePrevented");
									markNextRightButtonUpDoBePrevented--;
									return true;
								}
							}
							else
							{
								TempLog("Setting rightButtonDown to true");
								rightButtonDown = true;
							}
						}

						lastInputKeyboardNotMouse = false;
						if (rightButtonDown
							&& ev.Button.Button == System.Windows.Forms.MouseButtons.Left
							&& ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.Up)
						{
							MouseSimulator.RightMouseButtonUp();
							ShowThisWindow();
							TempLog("Setting markNextRightButtonUpDoBePrevented to 1");
							markNextRightButtonUpDoBePrevented = 1;// 2;
						}

						/*if (ev.Button.Button == System.Windows.Forms.MouseButtons.Middle)
						{
							var currentMousePos = GetMousePosition();
							if (Point.Subtract(currentMousePos, lastMiddleClickMousePos).Length < 10D)
							{
								lastMiddleClickMousePos = currentMousePos;
								if (ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.DoubleClicked)
								{
									System.Threading.Thread.Sleep(200);
									ShowThisWindow();
								}
							}
							else
								lastMiddleClickMousePos = currentMousePos;
						}*/
					}
					finally
					{
						TempLog("-----");
					}
				}
				catch { }//Crashes here on startup for some reason

				return false;

				//if (ev.Button != null && ev.Button.Button == System.Windows.Forms.MouseButtons.Middle && ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.Up)
				//// && ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.DoubleClicked)
				//{
				//    skipCheckActiveTimerCount = 2;
				//    ShowThisWindow();
				//}
				/*if (ev.Button != null && ev.Button.Button == System.Windows.Forms.MouseButtons.Left)
				{
					if (ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.Down)
						isMouseLeftDown = true;
					else
						isMouseLeftDown = false;
				}
				if (isMouseLeftDown && ev.Button.Button == System.Windows.Forms.MouseButtons.Middle && ev.Button.ButtonState == UserActivityHook.MoreMouseButton.MoreButtonStates.Up)
				{
					//skipCheckActiveTimerCount = 2;
					System.Threading.Thread.Sleep(200);
					ShowThisWindow();
				}*/
			};

			if (!File.Exists(PastingListFilepath))
			{
				currentList.Add(new MyMenuItem("Click to define pasting items", "Click to define pasting items", delegate
				{
					File.WriteAllText(PastingListFilepath,
						"Place items here to populate in the 'pasting-list', examples:"
						+ Environment.NewLine + "Display text|The text to be pasted (note the pipe character)"
						+ Environment.NewLine + "This text will be displayed & pasted (note there is no pipe character)");
					Process.Start("notepad", PastingListFilepath).WaitForExit();
					PopulatePastinglistAndBatchcommandsFromFile();
				}));
			}
			else
				PopulatePastinglistAndBatchcommandsFromFile();
			//currentList.Add(new MyMenuItem("Hallo, hover me", "This is the full text for 'Hallo, hover me'"));
			//currentList.Add(new MyMenuItem("Item2", "Full text for 'Item2'"));
			//currentList.Add(new MyMenuItem("Item3", "Full text for 'Item3'"));
			//currentList.Add(new MyMenuItem("Item4", "Full text for 'Item4'"));

			listboxItems.ItemsSource = currentList;
			if (currentList.Count > 0) SetNewSelectedItem(currentList[0]);

			//Now checks if mouse over listbox on mouse clicks, if not hides the window
			//Timer timerCheckIfForegroundWindow = new Timer(200) { AutoReset = true, Enabled = true };
			//timerCheckIfForegroundWindow.Elapsed += delegate
			//{
			//    Dispatcher.Invoke((Action)delegate
			//    {
			//        if (this.IsVisible)
			//        {
			//            /*if (skipCheckActiveTimerCount > 0)
			//            {
			//                this.Activate();
			//                skipCheckActiveTimerCount--;
			//            }
			//            else
			//            {*/
			//            if (GetForegroundWindow() != this.Handle)
			//                if (DateTime.Now.Subtract(lastShowTime) > minimumShowTimeBeforeHiding)
			//                    HideThisWindow();
			//                else
			//                {
			//                    this.Activate();
			//                    SetForegroundWindow(this.Handle);
			//                }
			//            //}
			//        }
			//    });
			//};
			//timerCheckIfForegroundWindow.Start();
		}

		TimeSpan minimumShowTimeBeforeHiding = TimeSpan.FromMilliseconds(200);
		DateTime lastShowTime = DateTime.Now;
		private void ShowThisWindow()
		{
			var caretPos = GetPositionToPlaceWindow();
			//Console.WriteLine("Caret: " + caretPos.ToString());
			if (caretPos.x > 0 || caretPos.y > 0)
			{
				this.Left = caretPos.x;
				this.Top = caretPos.y;
			}
			else
			{
				this.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - this.ActualWidth) / 2;
				this.Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - this.ActualHeight) / 2;
			}

			lastShowTime = DateTime.Now;
			if (!this.IsVisible)
				this.Show();
			this.UpdateLayout();
			this.Activate();
			SetForegroundWindow(this.Handle);
			this.Focus();
			this.BringIntoView();
			this.Topmost = !this.Topmost;
			this.Topmost = !this.Topmost;
		}

		private void HideThisWindow()
		{
			this.Hide();
		}

		private void PopulatePastinglistAndBatchcommandsFromFile()
		{
			currentList.Clear();
			var filelines = File.ReadAllLines(PastingListFilepath);
			var pastingItems = new List<MyMenuItem>();
			foreach (string line in filelines)
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;
				string name = line.Contains('|') ? line.Substring(0, line.IndexOf('|')) : line;
				string title = line.Contains('|') ? line.Substring(line.IndexOf('|') + 1) : line;
				currentList.Add(new MyMenuItem(name, title, new Action<MyMenuItem>((item) => { SetClipboardAndPaste(item.Tooltip); })));
			}

			if (!Directory.Exists(BatchCommandsDir))
				Directory.CreateDirectory(BatchCommandsDir);
			var batchfiles = Directory.GetFiles(BatchCommandsDir, "*.bat");
			foreach (var f in batchfiles)
			{
				string filenameNoExt = System.IO.Path.GetFileNameWithoutExtension(f);
				currentList.Add(new MyMenuItem(
					filenameNoExt,
					f + Environment.NewLine + File.ReadAllText(f),
					(item) => { Process.Start(item.Tooltip.Split('\n', '\r')[0]); }));//"explorer", "/select,\"" + item.Tooltip.Split('\n', '\r')[0] + "\""); }));
			}
			var appstorun = new List<string>
			{
				"CompareCSVs",
				"GoogleEarth"
			};
			foreach (var app in appstorun)
			{
				currentList.Add(new MyMenuItem(app, "Run app named " + app,
					apptorun =>
					{
						var appfullpath = RegistryInterop.GetAppPathFromRegistry(apptorun.Name);
						if (appfullpath != null)
							Process.Start(appfullpath);
						else
							ShowNoCallbackNotificationInterop.Notify(
								err => UserMessages.ShowErrorMessage(err),
								"Cannot find exe path from registry App Paths for app '"
								+ apptorun.Name + "'" + Environment.NewLine + "(" + apptorun.Tooltip + ")",
								"App not found",
								ShowNoCallbackNotificationInterop.NotificationTypes.Error,
								10);
					}));
			}
			var appnamestokillprocess = new List<string> { "Wadiso6" };
			foreach (var killapp in appnamestokillprocess)
			{
				currentList.Add(new MyMenuItem(killapp, "Kill process for '" + killapp + "'",
					apptokill => KillAppNow(apptokill.Name)));
			}
		}

		private void KillAppNow(string appnameNotCaseSensitive)
		{
			Process[] processes = Process.GetProcesses();
			for (int i = 0; i < processes.Length; i++)
			{
				try
				{
					if (processes[i].ProcessName.Equals(appnameNotCaseSensitive, StringComparison.InvariantCultureIgnoreCase))
					{
						processes[i].Kill();
						return;
					}
				}
				catch { }
			}
			ShowNoCallbackNotificationInterop.Notify(err => UserMessages.ShowErrorMessage(err),
				"Process not found to kill with name '" + appnameNotCaseSensitive + "'",
				"Process not found",
				ShowNoCallbackNotificationInterop.NotificationTypes.Warning,
				2);
		}

		public IntPtr Handle { get { return new WindowInteropHelper(Application.Current.MainWindow).Handle; } }

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
			source.AddHook(WndProc);

			var handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
			if (!Win32Api.RegisterHotKey(handle, Win32Api.Hotkey1, Win32Api.MOD_CONTROL + Win32Api.MOD_WIN, (int)'Z'))
				MessageBox.Show(cThisAppName + " could not register hotkey Ctrl + WinKey + Z");
		}

		//private int skipCheckActiveTimerCount = 0;
		private static bool lastInputKeyboardNotMouse = true;
		bool isMouseLeftDown = false;
		void SetClipboardAndPaste(string textToPaste)
		{
			Clipboard.SetText(textToPaste);
			keybd_event(0x11, 0, 0, 0); //...CTRL key down
			keybd_event(0x56, 0, 0, 0); //...V key down
			keybd_event(0x56, 0, 0x02, 0); //...V key up
			keybd_event(0x11, 0, 0x02, 0); //...CTRL key up
		}
		void PressKey(byte keyCode)
		{
			const int KEYEVENTF_EXTENDEDKEY = 0x1;
			const int KEYEVENTF_KEYUP       = 0x2;
			keybd_event(keyCode, 0x45, KEYEVENTF_EXTENDEDKEY, 0);
			keybd_event(keyCode, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
		}

		//double currentScale;
		//private void ApplyScale(double scale)
		//{
		//    mainGrid.RenderTransform = new ScaleTransform(scale, scale);
		//    currentScale = scale;
		//}

		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == Win32Api.WM_HOTKEY)
			{
				if (wParam == new IntPtr(Win32Api.Hotkey1))
				{
					if (this.Visibility != System.Windows.Visibility.Visible)
						this.ShowThisWindow();
					else
						this.HideThisWindow();
				}
			}
			return IntPtr.Zero;
		}

		private void mainBorder_MouseEnter(object sender, MouseEventArgs e)
		{
			//ApplyScale(1);
		}

		private void mainBorder_MouseLeave(object sender, MouseEventArgs e)
		{
			//ApplyScale(miniScale);
		}

		private static POINT GetPositionToPlaceWindow()
		{
			if (lastInputKeyboardNotMouse)
			{
				var caretPosition = new POINT();
				GetCaretPosition();
				caretPosition.x = (int)guiInfo.rcCaret.Left;// +25;
				caretPosition.y = (int)guiInfo.rcCaret.Bottom;// +25;
				//Console.WriteLine("Before: " + caretPosition.ToString());
				ClientToScreen(guiInfo.hwndCaret, caretPosition);
				Console.WriteLine("HWND: " + guiInfo.hwndCaret);
				//Console.WriteLine("After: " + caretPosition.ToString());
				return caretPosition;
			}
			else
			{
				var mouse = GetMousePosition();
				return new POINT()
				{
					x = (int)mouse.X,
					y = (int)mouse.Y
				};
			}
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetCursorPos(ref Win32Point pt);

		[StructLayout(LayoutKind.Sequential)]
		internal struct Win32Point
		{
			public Int32 X;
			public Int32 Y;
		};
		public static Point GetMousePosition()
		{
			Win32Point w32Mouse = new Win32Point();
			GetCursorPos(ref w32Mouse);
			return new Point(w32Mouse.X, w32Mouse.Y);
		}

		public static void GetCaretPosition()
		{
			guiInfo = new GUITHREADINFO();
			guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
			GetGUIThreadInfo(0, out guiInfo);
			//Console.WriteLine(guiInfo.rcCaret.Left + ", " + guiInfo.rcCaret.Right);
		}

		MyMenuItem lastSelected = null;
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			HandleUpDownLeftRight(ref e);
			if (e.Key == Key.Enter)
			{
				if (lastSelected == null)
					return;
				PerformActionOfItem(lastSelected);
			}
			else if (e.Key == Key.Escape)
			{
				HideThisWindow();
			}
		}

		private void PerformActionOfItem(MyMenuItem item)
		{
			HideThisWindow();
			if (item == null || item.Action == null)
				return;
			item.Action(item);
		}

		private void HandleUpDownLeftRight(ref KeyEventArgs e)
		{
			if (e.Key == Key.Down)
			{
				if (currentList.Count == 0)
					return;

				MyMenuItem newSelectedItem = null;
				if (lastSelected == null)
					newSelectedItem = currentList[0];
				else if (currentList.IndexOf(lastSelected) < currentList.Count - 1)
					newSelectedItem = currentList[currentList.IndexOf(lastSelected) + 1];
				else
					newSelectedItem = currentList[0];

				SetNewSelectedItem(newSelectedItem);
				//statusbaritem1.Content = "Sel: " + newSelectedItem.Name;
			}
			else if (e.Key == Key.Up)
			{
				if (currentList.Count == 0)
					return;
				MyMenuItem newSelectedItem = null;
				if (lastSelected == null)
					newSelectedItem = currentList.Last();
				else if (currentList.IndexOf(lastSelected) > 0)
					newSelectedItem = currentList[currentList.IndexOf(lastSelected) - 1];
				else
					newSelectedItem = currentList.Last();

				SetNewSelectedItem(newSelectedItem);
				//statusbaritem1.Content = "Sel: " + newSelectedItem.Name;
			}
		}

		private void SetNewSelectedItem(MyMenuItem newSelectedItem)
		{
			lastSelected = newSelectedItem;
			for (int i = 0; i < currentList.Count; i++)
				currentList[i].IsSelected = currentList[i] == newSelectedItem;
		}

		private void listbox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			listboxItems.SelectedItem = null;
		}

		//private void Window_LostFocus(object sender, RoutedEventArgs e)
		//{
		//    //if (!this.IsActive)
		//    if (GetForegroundWindow() != Handle)
		//        HideThisWindow();
		//}

		//private void Window_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		//{
		//    //if (!this.IsActive)
		//    if (GetForegroundWindow() != Handle)
		//        HideThisWindow();
		//}

		private void itemBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			var border = sender as FrameworkElement;
			if (border == null) return;
			var item = border.DataContext as MyMenuItem;
			if (item == null) return;
			PerformActionOfItem(item);
		}

		private void listboxItems_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				HideThisWindow();
		}

		private void menuitemAbout_Click(object sender, RoutedEventArgs e)
		{
			AboutWindow2.ShowAboutWindow(new System.Collections.ObjectModel.ObservableCollection<DisplayItem>()
			{
				new DisplayItem("Author", "Francois Hill"),
				new DisplayItem("Icon(s) obtained from", null)

			});
		}

		private void menuitemExit_Click(object sender, RoutedEventArgs e)
		{
			tmpwin.Close();
			this.Close();
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public class POINT
	{
		public int x = 0;
		public int y = 0;
	}

	[StructLayout(LayoutKind.Sequential)]    // Required by user32.dll
	public struct RECT
	{
		public uint Left;
		public uint Top;
		public uint Right;
		public uint Bottom;
	};

	[StructLayout(LayoutKind.Sequential)]    // Required by user32.dll
	public struct GUITHREADINFO
	{
		public uint cbSize;
		public uint flags;
		public IntPtr hwndActive;
		public IntPtr hwndFocus;
		public IntPtr hwndCapture;
		public IntPtr hwndMenuOwner;
		public IntPtr hwndMoveSize;
		public IntPtr hwndCaret;
		public RECT rcCaret;
	};

	public class MyMenuItem : INotifyPropertyChanged
	{
		public string Name { get; set; }
		public string Tooltip { get; set; }
		public Action<MyMenuItem> Action;
		//public List<MyMenuItem> Subitems { get; set; }

		public MyMenuItem(string Name, string Tooltip, Action<MyMenuItem> Action)
		{
			this.Name = Name.Length > 30 ? Name.Substring(0, 30) + "..." : Name;
			this.Tooltip = Tooltip;
			this.Action = Action;
		}
		//public MyMenuItem(string Name, string Tooltip, List<MyMenuItem> Subitems)
		//{
		//    this.Name = Name;
		//    this.Tooltip = Tooltip;
		//    this.Subitems = Subitems;
		//}

		private readonly GradientStop[] SelectedGradientStops = new GradientStop[]
		{
			new GradientStop(Color.FromRgb(220, 220, 220), 0),
			new GradientStop(Color.FromRgb(255, 255, 255), 0),
			new GradientStop(Color.FromRgb(220, 220, 220), 0)
		};
		private readonly GradientStop[] unselectedGradientStops = new GradientStop[]
		{
			new GradientStop(Color.FromRgb(200, 200, 200), 0),
			new GradientStop(Color.FromRgb(210, 210, 210), 0),
			new GradientStop(Color.FromRgb(200, 200, 200), 0)
		};
		private bool _isselected;
		public bool IsSelected { get { return _isselected; } set { _isselected = value; OnPropertyChanged("DrawBrush"); } }
		public Brush DrawBrush
		{
			get
			{
				return new LinearGradientBrush(
					IsSelected
					? new GradientStopCollection(SelectedGradientStops)
					: new GradientStopCollection(unselectedGradientStops),
					new Point(0, 0),
					new Point(0, 1));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged = new PropertyChangedEventHandler(delegate { });
		public void OnPropertyChanged(string propertyName)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
