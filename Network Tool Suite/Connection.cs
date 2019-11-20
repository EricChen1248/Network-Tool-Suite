using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using LZ4;

namespace Network_Tool_Suite
{
    public class Connection
    {
        public bool IsServer;
        public int Port { get; }
        public IPAddress IP { get; private set; }

        private TcpClient _client;
        private NetworkStream _stream;

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
            IsServer = true;
            _client = IsServer ? _server.AcceptTcpClient() : new TcpClient(IP.ToString(), Port);
            _stream = _client.GetStream();
            
        }

        public void ConnectToServer(string ipString)
        {
            IP = IPAddress.Parse(ipString);
            IsServer = false;
            _client = IsServer ? _server.AcceptTcpClient() : new TcpClient(IP.ToString(), Port);
            _stream = _client.GetStream();
        }

        public void SendStream(byte[] byteArray)
        {
            var comp = Compress(byteArray);
            _stream.Write(BitConverter.GetBytes(comp.Length), 0, 4);
            _stream.Write(comp, 0, comp.Length);
        }

        public byte[] ReceiveStream()
        {
            var lengthByte = new byte[4];
            _stream.Read(lengthByte, 0, 4);
            var length = Helper.ByteToInt(lengthByte);
            var data = new byte[length];
            var read = 0;
            while (true)
            {
                var i = _stream.Read(data, read, length - read);
                read += i;
                if (read == length)
                {
                    break;
                }
            }

            return Decompress(new MemoryStream(data));
        }

        private static byte[] Compress(byte[] input)
        {
            using(var compressStream = new MemoryStream(input.Length))
            using(var compressor = new LZ4Stream(compressStream, CompressionMode.Compress, blockSize: 1024 * 1024 * 80))
            {
                compressor.Write(input, 0, input.Length);
                compressor.Close();
                return compressStream.ToArray();
            }
        }

        public static byte[] Decompress(Stream data)
        {
            var output = new MemoryStream();
            using(var zipStream = new LZ4Stream(data, CompressionMode.Decompress))
            {
                zipStream.CopyTo(output);
                zipStream.Close();
                return output.ToArray();
                
            }
        }
    }
}
