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
                    fixed (byte* pBytes = &color[0])
                    {
                        *(int*)pBytes = pPixelsA[index];
                    }
                    //Helper.IntToByte(pPixelsA[index], color);

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
                    fixed (byte* pBytes = &color[0])
                    {
                        *(int*)pBytes = pPixelsA[start + j];
                    }
                    //Helper.IntToByte(pPixelsA[start + j], color);
                    
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
            for (int i = 0; i < Threads; i++)
            {
                results.AddRange(BitConverter.GetBytes(compressed[i].Count));
            }

            for (int i = 0; i < Threads; i++)
            {
                results.AddRange(compressed[i]);
            }

            return results.ToArray();
        }

        public static unsafe void BytesToBitmapDecompressed(byte[] bytes, Bitmap bmp)
        {
            var bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(bounds, ImageLockMode.ReadWrite, bmp.PixelFormat);
            
            var pPixelsA = (int*)bmpData.Scan0.ToPointer();
            var nPixels = ScreenHeight * bmpData.Stride / 4;
            var transparent = Color.Transparent.ToArgb();

            var size = new int[Threads];
            for (int i = 0; i < Threads; i++)
            {
                size[i] = BitConverter.ToInt32(bytes, i * 4);
            }

            Parallel.For(0, Threads, i =>
            {
                var trans = true;
                var start = Threads * 4;
                for (int j = 0; j < i; j++)
                {
                    start += size[j];
                }

                var point = 0;
                var index = nPixels / Threads * i;
                var numCount = new byte[4];
                while(point < size[i])
                {
                    numCount[0] = bytes[start + point++];
                    numCount[1] = bytes[start + point++];
                    numCount[2] = bytes[start + point++];
                    numCount[3] = bytes[start + point++];

                    var count = Helper.ByteToInt(numCount);

                    if (trans)
                    {
                        for (var j = 0; j < count; j++)
                        {
                            pPixelsA[index++] = transparent;
                        }

                        trans = false;
                    }
                    else
                    {
                        for (var j = 0; j < count; j++)
                        {
                            var r = bytes[start + point++] * 16;
                            var gb = bytes[start + point++];
                            var g = gb >> 4;
                            var b = (gb - (g << 4)) * 16;
                            pPixelsA[index++] = b | ((g * 16) << 8) | (r << 16) | (255 << 24);
                        }

                        //Color.FromArgb(255, r, g * 16, b).ToArgb();
                        trans = true;
                    }
                }
            });

            bmp.UnlockBits(bmpData);
        }
    }
}
