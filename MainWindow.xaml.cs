using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace WinPods
{
    public partial class MainWindow : Window
    {
        private BluetoothLEAdvertisementWatcher? _watcher;
        private DispatcherTimer _uiTimer;
        
        // --- SCOREBOARD ---
        private int _batL = -1;
        private int _batR = -1;
        private int _batC = -1;

        private DateTime _timeL;
        private DateTime _timeR;
        private DateTime _timeC;
        private DateTime _lastPacketGlobal; 

        private bool _isVisible = false;
        
        // Settings
        private const int DATA_TIMEOUT = 10; // 10 seconds memory
        private const int WINDOW_TIMEOUT = 5; // Close after 5 seconds of silence

        // Colors
        private readonly SolidColorBrush ActiveColor = new SolidColorBrush(Color.FromRgb(52, 199, 89));
        private readonly SolidColorBrush InactiveColor = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
        private readonly SolidColorBrush WhiteColor = Brushes.White;
        private readonly SolidColorBrush DimColor = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

        public MainWindow()
        {
            InitializeComponent();
            SetPositionBottomRight();

            // Run UI updates 10 times a second
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _uiTimer.Tick += UpdateUI;
            _uiTimer.Start();

            StartBluetooth();
        }

        private void SetPositionBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - this.Width - 20;
            this.Top = workArea.Bottom - this.Height - 10;
        }

        private void StartBluetooth()
        {
            try
            {
                _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
                _watcher.Received += OnPacketReceived;
                _watcher.Start();
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Bluetooth failed: {ex.Message}");
            }
        }

        private void OnPacketReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            foreach (var section in args.Advertisement.ManufacturerData)
            {
                if (section.CompanyId == 0x079A) // OnePlus
                {
                    ProcessData(section.Data);
                    break;
                }
            }
        }

        private void ProcessData(IBuffer buffer)
        {
            var data = new byte[buffer.Length];
            using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(data);
            if (data.Length < 18) return;

            _lastPacketGlobal = DateTime.Now;

            byte prefix = data[4];
            byte mode = data[5];
            
            // Raw Values
            int raw15 = data[15] - 81;
            int raw16 = data[16] - 81;
            int raw17 = data[17] - 81;

            // UPDATE SCOREBOARD
            // If we see a valid number, write it down and update the timestamp.
            
            if (prefix == 0x00 && mode == 0x01) // LEFT BUD ONLY
            {
                if (IsValid(raw16)) { _batL = raw16; _timeL = DateTime.Now; }
            }
            else if (mode == 0x03) // RIGHT BUD ONLY
            {
                if (IsValid(raw16)) { _batR = raw16; _timeR = DateTime.Now; }
            }
            else // STANDARD
            {
                if (IsValid(raw15)) { _batL = raw15; _timeL = DateTime.Now; }
                if (IsValid(raw17)) { _batR = raw17; _timeR = DateTime.Now; }
                if (IsValid(raw16)) { _batC = raw16; _timeC = DateTime.Now; }
            }

            if (!_isVisible) Dispatcher.Invoke(SlideIn);
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            var now = DateTime.Now;

            // Should we hide?
            if (_isVisible && (now - _lastPacketGlobal).TotalSeconds > WINDOW_TIMEOUT)
            {
                SlideOut();
                return;
            }

            // Decide what to show based on memory age
            int displayL = (now - _timeL).TotalSeconds < DATA_TIMEOUT ? _batL : -1;
            int displayR = (now - _timeR).TotalSeconds < DATA_TIMEOUT ? _batR : -1;
            int displayC = (now - _timeC).TotalSeconds < DATA_TIMEOUT ? _batC : -1;

            Render(TxtLeft, IconLeft, displayL);
            Render(TxtRight, IconRight, displayR);
            Render(TxtCase, IconCase, displayC);
        }

        private void Render(TextBlock txt, TextBlock icon, int val)
        {
            if (IsValid(val))
            {
                txt.Text = $"{val}%";
                txt.Foreground = ActiveColor;
                icon.Opacity = 1.0;
                icon.Foreground = WhiteColor;
            }
            else
            {
                txt.Text = "--";
                txt.Foreground = InactiveColor;
                icon.Opacity = 0.4;
                icon.Foreground = DimColor;
            }
        }

        private void SlideIn()
        {
            if (_isVisible) return;
            _isVisible = true;
            this.Show();

            var anim = new DoubleAnimation(this.Left + 50, this.Left, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

            this.BeginAnimation(Window.LeftProperty, anim);
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        private void SlideOut()
        {
            if (!_isVisible) return;
            _isVisible = false;

            var anim = new DoubleAnimation(this.Left, this.Left + 50, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fade.Completed += (s, e) => this.Hide();

            this.BeginAnimation(Window.LeftProperty, anim);
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        private bool IsValid(int b) => b >= 0 && b <= 100;
    }
}