using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Network_Tool_Suite
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static int ScreenLeft = 0;
        private static int ScreenTop = 0;
        private static readonly int ScreenWidth = 1920;
        private static readonly int ScreenHeight = 1080;

        private static Bitmap _bmp = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G = Graphics.FromImage(_bmp);
        private static Bitmap _bmp2 = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G2 = Graphics.FromImage(_bmp2);
        public static MemoryStream Memory = new MemoryStream();
        public DispatcherTimer Timer = new DispatcherTimer();
        
        public static Connection Connection;

        public MainWindow()
        {
            InitializeComponent();
            Connection = new Connection();

            Timer.Interval = TimeSpan.FromSeconds(0.05);
            Bitmap_Lib.ScreenHeight = ScreenHeight;
            Bitmap_Lib.Threads = 8;

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
            Bitmap_Lib.BytesToBitmapDecompressed(bytes, _bmp);
            Bitmap_Lib.OverlayBitmap(oldBmp, _bmp);
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
            G.CopyFromScreen(ScreenLeft, ScreenTop, 0, 0, _bmp.Size);
            Bitmap_Lib.ReduceColor(_bmp);
            Bitmap_Lib.GetDifferenceImage(_bmp, oldBmp);
            Connection.SendStream(Bitmap_Lib.BitmapToByteCompressed(oldBmp));

            Timer.Start();
        }


        private void DisplayCurrentScreen()
        {
            G2.CopyFromScreen(ScreenLeft, ScreenTop, 0, 0, _bmp2.Size);

            _bmp2.Save(Memory, ImageFormat.Bmp);
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
            ScreenLeft = (int) ((SystemParameters.VirtualScreenWidth - ScreenWidth) * e.NewValue);
            DisplayCurrentScreen();
        }

        private void TopSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ScreenTop = (int) ((SystemParameters.VirtualScreenHeight - ScreenHeight) * e.NewValue);
            DisplayCurrentScreen();
        }
    }
}
