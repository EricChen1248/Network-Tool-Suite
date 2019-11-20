using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Network_Tool_Suite
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public static MainWindow Instance;

        private static int _screenLeft;
        private static int _screenTop;
        private static readonly int ScreenWidth = 1920; //1366;
        private static readonly int ScreenHeight = 1080; //768;

        private static byte[] _buffer;

        private static Bitmap _bmp = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G = Graphics.FromImage(_bmp);
        private static readonly Bitmap Bmp2 = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G2 = Graphics.FromImage(Bmp2);
        
        private static readonly MemoryStream Memory = new MemoryStream(1_000_000);
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        
        private static Connection _connection;

        private static System.Windows.Point _mouseCoords = new System.Windows.Point(0,0);
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            _connection = new Connection();

            _timer.Interval = TimeSpan.FromSeconds(0.01);
            BitmapLib.ScreenHeight = ScreenHeight;
            BitmapLib.Threads = 16;

            DisplayCurrentScreen();
        }

        private void Start_Server(object sender, RoutedEventArgs e)
        {
            Title = "Server";
            _connection.CreateServerClient();
            _connection.IsServer = true;
        }

        private void Start_Client(object sender, RoutedEventArgs e)
        {
            Title = "Client";
            _connection.ConnectToServer(ipTextBox.Text);
            _connection.IsServer = false;
        }
        
        private void Start_Share(object sender, RoutedEventArgs e)
        {
            _buffer = BitmapLib.BitmapToByteCompressed(_bmp);
            _timer.Tick += Server_Tick;
            _timer.Start();

        }
        private void Start_View(object sender, RoutedEventArgs e)
        {
            _timer.Tick += Client_Tick;
            _timer.Start();
        }
        
        private void Server_Tick(object sender, EventArgs e)
        {
            _timer.Stop();
            var frame = new DispatcherFrame();
            new Thread(() =>
            {
                SendScreen();
                frame.Continue = false;
            }).Start();
            Dispatcher.PushFrame(frame);
        }
        
        private void Client_Tick(object sender, EventArgs e)
        {
            _timer.Stop();
            ShowScreen();
        }

        private static Task t;
        private static byte[] _buffer2;
        private void ShowScreen()
        {
            if (t != null)
            {
                t.Wait();
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = Memory;
                bitmapImage.EndInit();
                ImageViewer.Source = bitmapImage;
            }
            
            t = Task.Factory.StartNew(() =>
                {
                    if (_buffer == null)
                    {
                        t = null;
                        return;
                    }

                    var b2 = _buffer;
                    var oldBmp = _bmp;
                    _bmp = new Bitmap(ScreenWidth, ScreenHeight);
                    BitmapLib.BytesToBitmapDecompressed(b2, _bmp);
                    BitmapLib.OverlayBitmap(oldBmp, _bmp);
                    oldBmp.Dispose();

                    Memory.Position = 0;
                    _bmp.Save(Memory, ImageFormat.Bmp);
                    Memory.Position = 0;
                }
            );
            
            _buffer = _connection.ReceiveStream();
            _timer.Start();
        }

        private void SendScreen()
        {
            _mouseCoords = Helper.GetMousePosition();
            t?.Wait();

            t = Task.Factory.StartNew(() =>
            {
                var tempBmp = (Bitmap) _bmp.Clone();
                G.CopyFromScreen(_screenLeft, _screenTop, 0, 0, _bmp.Size);
                G.DrawIcon(new Icon("mouse.ico"), (int) _mouseCoords.X - _screenLeft - 10,
                    (int) _mouseCoords.Y - _screenTop);
                BitmapLib.ReduceAndGetDifference(_bmp, tempBmp);
                _buffer = BitmapLib.BitmapToByteCompressed(_bmp);
            });
            _connection.SendStream(_buffer);
            t.Wait();
            _timer.Start();
        }


        private void DisplayCurrentScreen()
        {
            G2.CopyFromScreen(_screenLeft, _screenTop, 0, 0, Bmp2.Size);

            Bmp2.Save(Memory, ImageFormat.Bmp);
            Memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = Memory;
            bitmapImage.EndInit();

            ImageViewer.Source = bitmapImage;
        }
        
    }
}
