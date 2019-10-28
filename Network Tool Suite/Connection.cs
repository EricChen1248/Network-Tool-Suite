using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace Network_Tool_Suite
{
    public class Connection
    {
        public bool IsServer;
        public int Port { get; }
        public IPAddress IP { get; private set; }

        private TcpClient _client;

        private TcpListener _server;

        public Connection()
        {
            Port = 45645;
            Console.ReadLine();
        }

        public void CreateServerClient()
        {
            _server = new TcpListener(IPAddress.Any, Port);
            _server.Start();
        }

        public void ConnectToServer(string ipString)
        {
            IP = IPAddress.Parse(ipString);
        }

        public void SendStream(byte[] byteArray)
        {
            _client = IsServer ? _server.AcceptTcpClient() : new TcpClient(IP.ToString(), Port);
            using (var clientStream = _client.GetStream())
            {
                var comp = Compress(byteArray);
                clientStream.Write(comp, 0, comp.Length);
            }
        }

        public byte[] ReceiveStream()
        {
            _client = IsServer ? _server.AcceptTcpClient() : new TcpClient(IP.ToString(), Port);
            var stream = _client.GetStream();
            return Decompress(stream);
        }

        private static byte[] Compress(byte[] input)
        {
            using(var compressStream = new MemoryStream())
            using(var compressor = new DeflateStream(compressStream, CompressionMode.Compress))
            {
                compressor.Write(input, 0, input.Length);
                compressor.Close();
                return compressStream.ToArray();
            }
        }

        public static byte[] Decompress(Stream data)
        {
            var output = new MemoryStream();
            using(var zipStream = new DeflateStream(data, CompressionMode.Decompress))
            {
                zipStream.CopyTo(output);
                zipStream.Close();
                return output.ToArray();
            }
        }
    }
}
