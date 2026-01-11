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
        private DispatcherTimer _hideTimer;
        private bool _isVisible = false;
        private DateTime _lastPacket;

        // Colors
        private readonly SolidColorBrush ActiveColor = new SolidColorBrush(Color.FromRgb(52, 199, 89));
        private readonly SolidColorBrush InactiveColor = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        private readonly SolidColorBrush WhiteColor = Brushes.White;
        private readonly SolidColorBrush DimColor = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

        public MainWindow()
        {
            InitializeComponent();
            InitializePosition(); // Move to bottom right
            
            // Check visibility every 1 second
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _hideTimer.Tick += (s, e) => CheckTimeout();
            _hideTimer.Start();

            StartBluetooth();
        }

        private void InitializePosition()
        {
            // Calculate Bottom Right Position
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double taskbarHeight = 50; // Approximate taskbar buffer

            this.Left = screenWidth - this.Width - 20; // 20px padding from right
            this.Top = screenHeight - this.Height - taskbarHeight; // Above taskbar
        }

        private void StartBluetooth()
        {
            try
            {
                _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
                _watcher.Received += OnPacketReceived;
                _watcher.Start();
            }
            catch { /* Ignore startup errors */ }
        }

        private void CheckTimeout()
        {
            if (_isVisible && (DateTime.Now - _lastPacket).TotalSeconds > 5)
            {
                SlideOut();
            }
        }

        private void OnPacketReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            foreach (var section in args.Advertisement.ManufacturerData)
            {
                if (section.CompanyId == 0x079A)
                {
                    ParseData(section.Data);
                    break;
                }
            }
        }

        private void ParseData(IBuffer buffer)
        {
            var data = new byte[buffer.Length];
            using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(data);
            if (data.Length < 18) return;

            _lastPacket = DateTime.Now;

            // --- DECODING ---
            byte prefix = data[4];
            byte mode = data[5];

            int valL = -1, valR = -1, valC = -1;
            int raw15 = data[15] - 81;
            int raw16 = data[16] - 81;
            int raw17 = data[17] - 81;

            // Simple Logic Map
            if (prefix == 0x00 && mode == 0x01) // Left Only
            {
                valL = raw16;
            }
            else if (mode == 0x03) // Right Only
            {
                valR = raw16;
            }
            else // Both / Case
            {
                if (raw15 >= 0 && raw15 <= 100) valL = raw15;
                if (raw16 >= 0 && raw16 <= 100) valC = raw16;
                if (raw17 >= 0 && raw17 <= 100) valR = raw17;
            }

            Dispatcher.Invoke(() =>
            {
                if (!_isVisible) SlideIn();
                UpdateUI(valL, valR, valC);
            });
        }

        private void UpdateUI(int l, int r, int c)
        {
            RenderComponent(TxtLeft, IconLeft, l);
            RenderComponent(TxtRight, IconRight, r);
            RenderComponent(TxtCase, IconCase, c);
        }

        private void RenderComponent(TextBlock txt, TextBlock icon, int battery)
        {
            if (battery >= 0 && battery <= 100)
            {
                txt.Text = $"{battery}%";
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

        // --- ANIMATIONS ---
        private void SlideIn()
        {
            _isVisible = true;
            this.Show();
            
            // Slide from Right (Left offset)
            var slide = new DoubleAnimation(this.Left + 50, this.Left, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            
            this.BeginAnimation(Window.LeftProperty, slide);
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        private void SlideOut()
        {
            _isVisible = false;
            
            // Slide to Right
            var slide = new DoubleAnimation(this.Left, this.Left + 50, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            
            fade.Completed += (s, e) => this.Hide();
            
            this.BeginAnimation(Window.LeftProperty, slide);
            this.BeginAnimation(Window.OpacityProperty, fade);
        }
    }
}