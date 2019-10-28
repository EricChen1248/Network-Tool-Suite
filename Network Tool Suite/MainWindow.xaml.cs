﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
        private static readonly int ScreenLeft = 0;
        private static readonly int ScreenTop = 0;
        private static readonly int ScreenWidth = 1920;
        private static readonly int ScreenHeight = 1080;

        private static Bitmap _bmp = new Bitmap(ScreenWidth, ScreenHeight);
        private static readonly Graphics G = Graphics.FromImage(_bmp);
        public static MemoryStream Memory = new MemoryStream();
        public DispatcherTimer Timer = new DispatcherTimer();

        private const int Threads = 8;

        public static Connection Connection;
        public MainWindow()
        {
            InitializeComponent();
            Connection = new Connection();
        }

        private void Start_Server(object sender, RoutedEventArgs e)
        {
            Title = "Server";
            Connection.CreateServerClient();
            Timer.Tick += SendScreen;
            Timer.Interval = TimeSpan.FromSeconds(0.03);
            Timer.Start();
        }

        private void Start_Client(object sender, RoutedEventArgs e)
        {
            Title = "Client";
            Connection.ConnectToServer(ipTextBox.Text);
            Timer.Tick += ShowScreen;
            Timer.Interval = TimeSpan.FromSeconds(0.03);
            Timer.Start();
        }

        private void ShowScreen(object sender, EventArgs e)
        {
            Timer.Stop();
            var oldBmp = new Bitmap(_bmp);
            var bytes = Connection.ReceiveStream();
            _bmp = new Bitmap(ScreenWidth, ScreenHeight);
            BytesToBitmapDecompressed(bytes, _bmp);
            OverlayBitmap(oldBmp, _bmp);
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

        private void SendScreen(object sender, EventArgs e)
        {
            Timer.Stop();

            var oldBmp = (Bitmap) _bmp.Clone();
            G.CopyFromScreen(ScreenLeft, ScreenTop, 0, 0, _bmp.Size);
            ReduceColor(_bmp);
            GetDifferenceImage(_bmp, oldBmp);
            Connection.SendStream(BitmapToByteCompressed(oldBmp));

            Timer.Start();
        }

        public static unsafe void GetDifferenceImage(Bitmap image1, Bitmap image2)
        {
            var bounds = new Rectangle(0, 0, image1.Width, image1.Height);
            var bmpDataA = image1.LockBits(bounds, ImageLockMode.ReadWrite, image1.PixelFormat);
            var bmpDataB = image2.LockBits(bounds, ImageLockMode.ReadWrite, image2.PixelFormat);

            var nPixels = ScreenHeight * bmpDataA.Stride / 4;
            var pPixelsA = (int*)bmpDataA.Scan0.ToPointer();
            var pPixelsB = (int*)bmpDataB.Scan0.ToPointer();
            var transparent = Color.Transparent.ToArgb();
            Parallel.For(0, Threads, i =>
            {
                var offset = nPixels / Threads;
                for (var j = 0; j < nPixels / Threads; j++)
                {
                    var index = i * offset + j;
                    if (pPixelsA[index] == pPixelsB[index])
                    {
                        pPixelsB[index] = transparent;
                    }
                    else
                    {
                        pPixelsB[index] = pPixelsA[index];
                    }
                }
            });
            image1.UnlockBits(bmpDataA);
            image2.UnlockBits(bmpDataB);
        }

        public static unsafe void OverlayBitmap(Bitmap image1, Bitmap image2)
        {
            var bounds = new Rectangle(0, 0, image1.Width, image1.Height);
            var bmpDataA = image1.LockBits(bounds, ImageLockMode.ReadWrite, image1.PixelFormat);
            var bmpDataB = image2.LockBits(bounds, ImageLockMode.ReadWrite, image2.PixelFormat);

            var nPixels = ScreenHeight * bmpDataA.Stride / 4;
            var pPixelsA = (int*)bmpDataA.Scan0.ToPointer();
            var pPixelsB = (int*)bmpDataB.Scan0.ToPointer();

            var transparent = Color.Transparent.ToArgb();

            Parallel.For(0, Threads, i =>
            {
                var offset = nPixels / Threads;
                for (var j = 0; j < offset; j++)
                {
                    var index = i * offset + j;
                    if (pPixelsB[index] == transparent)
                    {
                        pPixelsB[index] = pPixelsA[index];
                    }
                }
            });

            image1.UnlockBits(bmpDataA);
            image2.UnlockBits(bmpDataB);
        }

        public static unsafe void ReduceColor(Bitmap bmp)
        {
            var bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpDataA = bmp.LockBits(bounds, ImageLockMode.ReadWrite, bmp.PixelFormat);

            var height = ScreenHeight;
            var nPixels = height * bmpDataA.Stride / 4;
            var pPixelsA = (int*)bmpDataA.Scan0.ToPointer();

            Parallel.For(0, 8, i =>
            {
                var offset = nPixels / 8;
                var start = i * offset;
                var color = new byte[4];
                for (var j = 0; j < offset; j++)
                {
                    // Manual unpacking
                    IntToByte(pPixelsA[start + j], color);

                    // Manual bit shift is faster than BitConverter
                    pPixelsA[start + j] = color[0] / 16 | 
                                          (color[1] / 16 << 8) |  
                                          (color[2] / 16 << 16) |  
                                          (color[3] << 24);
                }
            });
            bmp.UnlockBits(bmpDataA);
        }

        public static unsafe byte[] BitmapToByteCompressed(Bitmap bmp)
        {
            var bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(bounds, ImageLockMode.ReadWrite, bmp.PixelFormat);

            var nPixels = ScreenHeight * bmpData.Stride / 4;
            var pPixelsA = (int*)bmpData.Scan0.ToPointer();

            var compressed = new List<byte>[Threads];
            Parallel.For(0, Threads, i =>
            {
                compressed[i] = new List<byte>();

                var offset = nPixels / Threads;
                var start = i * offset;
                var trans = true;
                var count = 0;
                var data = new List<byte>();
                
                var color = new byte[4];
                var countByte = new byte[4];
                for (var j = 0; j < offset; j++)
                {
                    IntToByte(pPixelsA[start + j], color);
                    if (color[3] == 0) // Transparent
                    {
                        if(!trans)
                        {
                            IntToByte(count, countByte);
                            compressed[i].AddRange(countByte);
                            compressed[i].AddRange(data);
                            trans = true;
                            count = 0;
                        }
                        ++count;
                    }
                    else
                    {
                        if (trans)
                        {
                            IntToByte(count, countByte);
                            compressed[i].AddRange(countByte);
                            data.Clear();
                            trans = false;
                            count = 0;
                        }
                        ++count;
                        data.Add(color[2]);
                        data.Add((byte)(color[1] << 4 | color[0]));
                    }
                }

                compressed[i].AddRange(BitConverter.GetBytes(count));
                if (data.Count > 0 && !trans)
                {
                    compressed[i].AddRange(data);

                    // Add a 0 to indicate 0 transparent on ending
                    compressed[i].AddRange(BitConverter.GetBytes(0));
                }

            });
            bmp.UnlockBits(bmpData);

            var results = new List<byte>();
            var numCount = new byte[4];
            var newCountArray = new byte[4];
            results.AddRange(compressed[0]);
            for (var i = 1; i < Threads; i++)
            {
                numCount[0] = results[results.Count - 4];
                numCount[1] = results[results.Count - 3];
                numCount[2] = results[results.Count - 2];
                numCount[3] = results[results.Count - 1];

                var transCount = numCount[0] | numCount[1] << 8 | numCount[2] << 16 | numCount[3] << 24;
                var nTransCount = BitConverter.ToInt32(compressed[i].Take(4).ToArray(), 0);
                IntToByte(transCount + nTransCount, newCountArray);
                results.RemoveRange(results.Count - 4, 4);

                for (var j = 0; j < 4; j++)
                {
                    compressed[i][j] = newCountArray[j];
                }
                results.AddRange(compressed[i]);
            }

            return results.ToArray();
        }

        public static unsafe void BytesToBitmapDecompressed(byte[] bytes, Bitmap bmp)
        {
            var bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(bounds, ImageLockMode.ReadWrite, bmp.PixelFormat);
            
            var pPixelsA = (int*)bmpData.Scan0.ToPointer();

            var point = 0;
            var index = 0;

            var trans = true;
            var numCount = new byte[4];
            var transparent = Color.Transparent.ToArgb();
            while(point < bytes.Length)
            {
                numCount[0] = bytes[point++];
                numCount[1] = bytes[point++];
                numCount[2] = bytes[point++];
                numCount[3] = bytes[point++];

                var count = BitConverter.ToInt32(numCount, 0);

                if (trans)
                {
                    for (var i = 0; i < count; i++)
                    {
                        pPixelsA[index++] = transparent;
                    }
                    trans = false;
                }
                else
                {
                    for (var i = 0; i < count; i++)
                    {
                        var r = bytes[point++];
                        var gb = bytes[point++];
                        var g = gb >> 4;
                        var b = gb - (g << 4);
                        pPixelsA[index++] = Color.FromArgb(255, 
                            r * 16, 
                            g * 16, 
                            b * 16).ToArgb();
                    }
                    trans = true;
                }
            }

            bmp.UnlockBits(bmpData);
        }

        public static void IntToByte(int num, byte[] bytes)
        {
            bytes[3] = (byte)(num >> 24);
            bytes[2] = (byte)(num >> 16);
            bytes[1] = (byte)(num >> 8);
            bytes[0] = (byte) num;
        }
    }
}
