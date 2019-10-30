using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Network_Tool_Suite
{
    public static class BitmapLib
    {
        public static int ScreenHeight;
        public static int Threads;
        
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern unsafe void CopyMemory(int* dest, int* src, int count);

        public static unsafe void ReduceAndGetDifference(Bitmap image1, Bitmap image2)
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

                var color = new byte[4];
                for (var j = 0; j < offset; j++)
                {
                    var index = i * offset + j;

                    // Manual unpacking
                    Helper.IntToByte(pPixelsA[index], color);

                    // Manual bit shift is faster than BitConverter
                    pPixelsA[index] = color[0] / 16 | 
                                          (color[1] / 16 << 8) |  
                                          (color[2] / 16 << 16) |  
                                          (color[3] << 24);

                    if (pPixelsA[index] == pPixelsB[index])
                    {
                        pPixelsA[index] = transparent;
                    }
                }

            });
            CopyMemory(pPixelsB, pPixelsA, nPixels);
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


        public static unsafe byte[] BitmapToByteCompressed(Bitmap bmp)
        {
            var bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(bounds, ImageLockMode.ReadWrite, bmp.PixelFormat);

            var nPixels = ScreenHeight * bmpData.Stride / 4;
            var pPixelsA = (int*)bmpData.Scan0.ToPointer();

            var compressed = new List<byte>[Threads];
            Parallel.For(0, Threads, i =>
            {
                compressed[i] = new List<byte>(1048576);

                var offset = nPixels / Threads;
                var start = i * offset;
                var trans = true;
                var count = 0;
                var data = new List<byte>();
                
                var color = new byte[4];
                var countByte = new byte[4];
                for (var j = 0; j < offset; j++)
                {
                    Helper.IntToByte(pPixelsA[start + j], color);
                    if (color[3] == 0) // Transparent
                    {
                        if(!trans)
                        {
                            Helper.IntToByte(count, countByte);
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
                            Helper.IntToByte(count, countByte);
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

                var transCount = Helper.ByteToInt(numCount);
                var nTransCount = Helper.ByteToInt(compressed[i].Take(4).ToArray());
                Helper.IntToByte(transCount + nTransCount, newCountArray);
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

                var count = Helper.ByteToInt(numCount);

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
    }
}
