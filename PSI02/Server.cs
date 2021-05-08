using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSI02
{
    class Server
    {
        TcpListener _server = null;

        public Server(int port, string ip = null)
        {
            // Start server
            _server = new TcpListener(ip != null ? IPAddress.Parse(ip) : IPAddress.Any, port);
            _server.Start();

            // Start listening
            StartListener();
        }

        public void StartListener()
        {
            try
            {
                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");
                    TcpClient client = _server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    Thread t = new Thread(new ParameterizedThreadStart(HandleDeivce));
                    t.Start(client);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"{nameof(SocketException)}: {ex.Message}");
                _server.Stop();
            }
        }

        public void HandleDeivce(object obj)
        {
            TcpClient client = (TcpClient)obj;
            var stream = client.GetStream();

            byte[] bytes = new byte[256];
            string data = null;

            try
            {
                int i = stream.Read(bytes, 0, bytes.Length);

                Console.WriteLine();

                // Receive data
                data = Encoding.ASCII.GetString(bytes, 0, i);
                string responseContent = Regex.Match(data, @"(?<=(GET )).+(?=( HTTP\/1\.1))").Value;
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]: Received: {responseContent}");

                // Send data
                string message = $"<html><body>Hello from '{responseContent}'!</body></html>";
                SendMessage(stream, message);
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]: Sent: {message}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType()}: {ex.Message}");
                Console.WriteLine();
                client.Close();
            }
        }

        private void SendMessage(NetworkStream stream, string message)
        {
            var writer = new StreamWriter(stream);
            writer.Write("HTTP/1.0 200 OK");
            writer.Write(Environment.NewLine);
            writer.Write("Content-Type: text/html; charset=UTF-8");
            writer.Write(Environment.NewLine);
            writer.Write("Content-Length: " + message.Length);
            writer.Write(Environment.NewLine);
            writer.Write(Environment.NewLine);
            writer.Write(message);
            writer.Flush();
        }
    }
}
