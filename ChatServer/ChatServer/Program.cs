using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    class Program
    {
        static TcpListener listener;
        static List<ClientHandler> clients = new List<ClientHandler>();
        static List<string> usernames = new List<string>();

        static void Main(string[] args)
        {
            listener = new TcpListener(IPAddress.Any, 5000); // port default
            listener.Start();
            Console.WriteLine("Server started on port 5000...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                ClientHandler handler = new ClientHandler(client);
                Thread t = new Thread(handler.Run);
                t.Start();
            }
        }

        public static void BroadcastMessage(string message)
        {
            lock (clients)
            {
                foreach (var c in clients)
                {
                    try
                    {
                        c.Writer.WriteLine(message);
                    }
                    catch { }
                }
            }
            Console.WriteLine(message);
        }

        public static void UpdateUserList()
        {
            string userList = "[USERS]" + string.Join(",", usernames);
            BroadcastMessage(userList);
        }

        class ClientHandler
        {
            private TcpClient client;
            public StreamReader Reader { get; private set; }
            public StreamWriter Writer { get; private set; }
            private string username;

            public ClientHandler(TcpClient client)
            {
                this.client = client;
            }

            public void Run()
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    Reader = new StreamReader(stream, Encoding.UTF8);
                    Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    // baca username pertama kali
                    username = Reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        client.Close();
                        return;
                    }

                    lock (clients)
                    {
                        clients.Add(this);
                        usernames.Add(username);
                    }

                    // broadcast join info
                    BroadcastMessage($"[INFO] {username} memasuki server");

                    // update daftar user
                    UpdateUserList();

                    // loop terima pesan chat
                    string message;
                    while ((message = Reader.ReadLine()) != null)
                    {
                        BroadcastMessage(message);
                    }
                }
                catch { }
                finally
                {
                    lock (clients)
                    {
                        clients.Remove(this);
                        usernames.Remove(username);
                    }

                    BroadcastMessage($"[INFO] {username} meninggalkan server");
                    UpdateUserList();
                    client.Close();
                }
            }
        }
    }
}
