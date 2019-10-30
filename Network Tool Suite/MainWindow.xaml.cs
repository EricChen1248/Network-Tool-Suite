using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
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
        private static int _screenLeft;
        private static int _screenTop;
        private static readonly int ScreenWidth = 1920;
        private static readonly int ScreenHeight = 1080;

        private static Bitmap _bmp = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G = Graphics.FromImage(_bmp);

        private static readonly Bitmap Bmp2 = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G2 = Graphics.FromImage(Bmp2);
        
        public static MemoryStream Memory = new MemoryStream(8294400);
        public DispatcherTimer Timer = new DispatcherTimer();
        
        public static Connection Connection;

        public MainWindow()
        {
            InitializeComponent();
            Connection = new Connection();

            Timer.Interval = TimeSpan.FromSeconds(0.05);
            BitmapLib.ScreenHeight = ScreenHeight;
            BitmapLib.Threads = 8;

            DisplayCurrentScreen();
        }

        private void Start_Server(object sender, RoutedEventArgs e)
        {
            Title = "Server";
            Connection.CreateServerClient();
            Connection.IsServer = true;
        }

        private void Start_Client(object sender, RoutedEventArgs e)
        {
            Title = "Client";
            Connection.ConnectToServer(ipTextBox.Text);
            Connection.IsServer = false;
        }
        
        private void Start_Share(object sender, RoutedEventArgs e)
        {
            Timer.Tick += Server_Tick;
            Timer.Start();
        }
        private void Start_View(object sender, RoutedEventArgs e)
        {
            Timer.Tick += Client_Tick;
            Timer.Start();
        }
        
        private void Server_Tick(object sender, EventArgs e)
        {
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
            ShowScreen();
        }

        private void ShowScreen()
        {
            Timer.Stop();
            var oldBmp = new Bitmap(_bmp);
            var bytes = Connection.ReceiveStream();
            _bmp = new Bitmap(ScreenWidth, ScreenHeight);
            BitmapLib.BytesToBitmapDecompressed(bytes, _bmp);
            BitmapLib.OverlayBitmap(oldBmp, _bmp);
            oldBmp.Dispose();

            Memory.Position = 0;
            _bmp.Save(Memory, ImageFormat.Bmp);
            Memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = Memory;
            bitmapImage.EndInit();

            ImageViewer.Source = bitmapImage;
            Timer.Start();
        }

        private void SendScreen()
        {
            Timer.Stop();

            var oldBmp = (Bitmap) _bmp.Clone();
            G.CopyFromScreen(_screenLeft, _screenTop, 0, 0, _bmp.Size);
            BitmapLib.ReduceAndGetDifference(_bmp, oldBmp);
            Connection.SendStream(BitmapLib.BitmapToByteCompressed(oldBmp));
            
            Timer.Start();
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
        
        private void LeftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _screenLeft = (int) ((SystemParameters.VirtualScreenWidth - ScreenWidth) * e.NewValue);
            DisplayCurrentScreen();
        }

        private void TopSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _screenTop = (int) ((SystemParameters.VirtualScreenHeight - ScreenHeight) * e.NewValue);
            DisplayCurrentScreen();
        }
    }
}
