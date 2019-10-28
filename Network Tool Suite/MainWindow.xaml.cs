using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Network_Tool_Suite
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly int ScreenLeft = (int) SystemParameters.VirtualScreenLeft;
        private static readonly int ScreenTop = (int) SystemParameters.VirtualScreenTop;
        private static readonly int ScreenWidth = (int) SystemParameters.VirtualScreenWidth / 4;
        private static readonly int ScreenHeight = (int) SystemParameters.VirtualScreenHeight / 2;

        private static Bitmap _bmp = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics _g = Graphics.FromImage(_bmp);
        public static MemoryStream Memory = new MemoryStream();
        public DispatcherTimer Timer = new DispatcherTimer();

        public static Connection Connection;
        public MainWindow()
        {
            InitializeComponent();
            Connection = new Connection();
        }

        private void Start_Server(object sender, RoutedEventArgs e)
        {
            Connection.CreateServerClient();
            Timer.Tick += SendScreen;
            Timer.Interval = TimeSpan.FromSeconds(0.03);
            Timer.Start();
        }

        private void Start_Client(object sender, RoutedEventArgs e)
        {
            Connection.ConnectToServer(ipTextBox.Text);
            Timer.Tick += ShowScreen;
            Timer.Interval = TimeSpan.FromSeconds(0.03);
            Timer.Start();
        }

        private void ShowScreen(object sender, EventArgs e)
        {
            Timer.Stop();
            var oldBmp = new Bitmap(_bmp);
            _bmp = (Bitmap) Connection.ReceiveStream();

            OverlayBitmap(oldBmp, _bmp);

            Memory.Position = 0;
            _bmp.Save(Memory, ImageFormat.Png);
            Memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = Memory;
            bitmapImage.EndInit();

            ImageViewer.Source = bitmapImage;
            Timer.Start();
        }

        private void SendScreen(object sender, EventArgs e)
        {
            Timer.Stop();

            var oldBmp = (Bitmap) _bmp.Clone();
            _g.CopyFromScreen(ScreenLeft, ScreenTop, 0, 0, _bmp.Size);
            GetDifferenceImage(_bmp, oldBmp);
            oldBmp.Save(Memory, ImageFormat.Bmp);
            Connection.SendStream(Memory.ToArray());
            oldBmp.Dispose();

            Timer.Start();
        }

        public static unsafe void GetDifferenceImage(Bitmap image1, Bitmap image2)
        {
            Rectangle bounds = new Rectangle(0, 0, image1.Width, image1.Height);
            var bmpDataA = image1.LockBits(bounds, ImageLockMode.ReadWrite, image1.PixelFormat);
            var bmpDataB = image2.LockBits(bounds, ImageLockMode.ReadWrite, image2.PixelFormat);

            int height = ScreenHeight;
            int npixels = height * bmpDataA.Stride / 4;
            int* pPixelsA = (int*)bmpDataA.Scan0.ToPointer();
            int* pPixelsB = (int*)bmpDataB.Scan0.ToPointer();

            for (int i = 0; i < npixels; ++i)
            {
                if (pPixelsA[i] == pPixelsB[i])
                {
                    pPixelsB[i] = Color.Transparent.ToArgb();
                }
                else
                {
                    pPixelsB[i] = pPixelsA[i];
                }
            }

            image1.UnlockBits(bmpDataA);
            image2.UnlockBits(bmpDataB);
        }

        public static unsafe void OverlayBitmap(Bitmap image1, Bitmap image2)
        {
            Rectangle bounds = new Rectangle(0, 0, image1.Width, image1.Height);
            var bmpDataA = image1.LockBits(bounds, ImageLockMode.ReadWrite, image1.PixelFormat);
            var bmpDataB = image2.LockBits(bounds, ImageLockMode.ReadWrite, image2.PixelFormat);

            int height = ScreenHeight;
            int npixels = height * bmpDataA.Stride / 4;
            int* pPixelsA = (int*)bmpDataA.Scan0.ToPointer();
            int* pPixelsB = (int*)bmpDataB.Scan0.ToPointer();

            for (int i = 0; i < npixels; ++i)
            {
                if (pPixelsB[i] == Color.Transparent.ToArgb())
                {
                    pPixelsB[i] = pPixelsA[i];
                }
            }

            image1.UnlockBits(bmpDataA);
            image2.UnlockBits(bmpDataB);
        }
    }
}
