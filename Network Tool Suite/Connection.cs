using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Network_Tool_Suite
{
    public class Connection
    {
        public int Port { get; private set; }
        public IPAddress IP { get; private set; }

        private const int PacketSize = 8096;
        private TcpClient client;

        private TcpListener server;

        public Connection()
        {
            Port = 45645;
            Console.ReadLine();
        }

        public void CreateServerClient()
        {
            server = new TcpListener(IPAddress.Any, Port);
            server.Start();
        }

        public void ConnectToServer(string ipString)
        {
            IP = IPAddress.Parse(ipString);
        }

        public void SendStream(byte[] byteArray)
        {
            client = server.AcceptTcpClient();
            var clientStream = client.GetStream();
            var comp = Compress(byteArray);
            clientStream.Write(comp, 0, comp.Length);
            clientStream.Close();
        }

        public Image ReceiveStream()
        {
            client = new TcpClient(IP.ToString(), Port);

            MemoryStream output = new MemoryStream();
            var clientStream = client.GetStream();
            using (DeflateStream dstream = new DeflateStream(clientStream, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }
            return Image.FromStream(output);
        }

        public static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
    }
}
