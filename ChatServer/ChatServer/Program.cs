using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatServer
{
    class Program
    {
        // ==================== VARIABEL GLOBAL SERVER ====================
        static TcpListener listener;
        static List<ClientHandler> clients = new List<ClientHandler>();
        static List<string> usernames = new List<string>();
        static readonly object clientLock = new object();
        static readonly Dictionary<string, DateTime> lastMessageTimes = new Dictionary<string, DateTime>();
        static readonly TimeSpan messageRateLimit = TimeSpan.FromMilliseconds(500);

        // ==================== MAIN METHOD - ENTRY POINT ====================
        static async Task Main(string[] args)
        {
            // Inisialisasi dan startup server
            Console.Title = "Chat Server - Port 5000";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║           CHAT SERVER v2.0           ║");
            Console.WriteLine("║       Fixed Message Routing          ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();

            try
            {
                // Start TCP listener dan main loop
                listener = new TcpListener(IPAddress.Any, 5000);
                listener.Start();
                Log("Server started on port 5000...");
                Console.WriteLine("╔══════════════════════════════════════╗");
                Console.WriteLine("║          Waiting for clients...      ║");
                Console.WriteLine("╚══════════════════════════════════════╝");

                // Main server loop - menerima koneksi client baru
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    ClientHandler handler = new ClientHandler(client);
                    _ = Task.Run(() => handler.RunAsync());
                }
            }
            catch (Exception ex)
            {
                // Error handling server
                Log($"Server error: {ex.Message}");
                Console.WriteLine($"Server error: {ex.Message}");
            }
            finally
            {
                // Cleanup resources
                listener?.Stop();
            }
        }

        // ==================== METHOD UTILITAS SERVER ====================
        private static void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(logEntry);
        }

        // ==================== MANAJEMEN RATE LIMITING ====================
        private static bool IsRateLimited(string username, out TimeSpan timeRemaining)
        {
            lock (lastMessageTimes)
            {
                if (lastMessageTimes.ContainsKey(username))
                {
                    var timeSinceLastMessage = DateTime.Now - lastMessageTimes[username];
                    if (timeSinceLastMessage < messageRateLimit)
                    {
                        timeRemaining = messageRateLimit - timeSinceLastMessage;
                        return true;
                    }
                }
                lastMessageTimes[username] = DateTime.Now;
                timeRemaining = TimeSpan.Zero;
                return false;
            }
        }

        // ==================== BROADCAST MESSAGE KE SEMUA CLIENT ====================
        private static void BroadcastMessage(string message, ClientHandler excludeClient = null)
        {
            List<ClientHandler> clientsCopy;
            lock (clientLock)
            {
                clientsCopy = new List<ClientHandler>(clients);
            }

            foreach (var client in clientsCopy)
            {
                if (client != excludeClient && client.IsConnected)
                {
                    try
                    {
                        client.Writer.WriteLine(message);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error sending to {client.Username}: {ex.Message}");
                        client.ShouldDisconnect = true;
                    }
                }
            }
            Log(message);
        }

        // ==================== KIRIM PRIVATE MESSAGE ====================
        private static void SendPrivateMessage(string fromUser, string toUser, string message)
        {
            ClientHandler targetClient = null;
            ClientHandler fromClient = null;

            // Cari client target dan pengirim
            lock (clientLock)
            {
                foreach (var client in clients)
                {
                    if (client.Username != null && client.Username.Equals(toUser, StringComparison.OrdinalIgnoreCase))
                        targetClient = client;
                    if (client.Username != null && client.Username.Equals(fromUser, StringComparison.OrdinalIgnoreCase))
                        fromClient = client;
                }
            }

            if (targetClient != null && targetClient.IsConnected)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("HH:mm");
                    string sanitizedMessage = SanitizeMessage(message);

                    // Format khusus untuk private message
                    string privateMsgToTarget = $"[{timestamp}] [PM from {fromUser}] {sanitizedMessage}";
                    targetClient.Writer.WriteLine(privateMsgToTarget);

                    Log($"PM from {fromUser} to {toUser}: {sanitizedMessage}");
                }
                catch (Exception ex)
                {
                    Log($"Error sending PM to {toUser}: {ex.Message}");
                    if (fromClient != null && fromClient.IsConnected)
                    {
                        string errorMsg = $"[{DateTime.Now:HH:mm}] [ERROR] Failed to send message to {toUser}";
                        fromClient.Writer.WriteLine(errorMsg);
                    }
                }
            }
            else if (fromClient != null && fromClient.IsConnected)
            {
                string errorMsg = $"[{DateTime.Now:HH:mm}] [ERROR] User '{toUser}' not found or offline";
                fromClient.Writer.WriteLine(errorMsg);
            }
        }

        // ==================== UPDATE DAFTAR USER ONLINE ====================
        private static void UpdateUserList()
        {
            List<string> sanitizedUsernames = new List<string>();
            foreach (string username in usernames)
            {
                sanitizedUsernames.Add(SanitizeUsername(username));
            }
            string userList = "[USERS]" + string.Join(",", sanitizedUsernames);
            BroadcastMessage(userList);
        }

        // ==================== REGISTRASI DAN UNREGISTRASI USER ====================
        private static bool RegisterUser(string username, ClientHandler client)
        {
            lock (clientLock)
            {
                foreach (string existingUser in usernames)
                {
                    if (existingUser.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
                {
                    return false;
                }

                if (!IsValidUsername(username))
                {
                    return false;
                }

                usernames.Add(username);
                clients.Add(client);
                return true;
            }
        }

        private static void UnregisterUser(string username, ClientHandler client)
        {
            lock (clientLock)
            {
                for (int i = usernames.Count - 1; i >= 0; i--)
                {
                    if (usernames[i].Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        usernames.RemoveAt(i);
                        break;
                    }
                }

                clients.Remove(client);

                lock (lastMessageTimes)
                {
                    lastMessageTimes.Remove(username);
                }
            }
        }

        // ==================== METHOD SANITASI DAN VALIDASI ====================
        private static string SanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;

            return message.Replace("\n", "\\n")
                         .Replace("\r", "\\r")
                         .Replace("\t", "\\t");
        }

        private static string SanitizeUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return string.Empty;

            return username.Replace(",", "_")
                          .Replace("\n", "_")
                          .Replace("\r", "_");
        }

        private static bool IsValidUsername(string username)
        {
            if (username.Length > 20) return false;
            if (username.Length < 2) return false;

            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                {
                    return false;
                }
            }
            return true;
        }

        // ==================== PARSING PESAN PRIVATE ====================
        private static string[] ParsePrivateMessage(string message)
        {
            try
            {
                int firstSpace = message.IndexOf(' ');
                if (firstSpace == -1) return null;

                int secondSpace = message.IndexOf(' ', firstSpace + 1);
                if (secondSpace == -1) return null;

                string targetUser = message.Substring(firstSpace + 1, secondSpace - firstSpace - 1);
                string privateMessage = message.Substring(secondSpace + 1);

                return new string[] { targetUser.Trim(), privateMessage };
            }
            catch
            {
                return null;
            }
        }

        // ==================== CLASS CLIENT HANDLER ====================
        public class ClientHandler
        {
            private TcpClient client;
            private NetworkStream stream;
            public StreamReader Reader { get; private set; }
            public StreamWriter Writer { get; private set; }
            public string Username { get; private set; }
            public bool ShouldDisconnect { get; set; } = false;
            public bool IsConnected
            {
                get
                {
                    return client != null &&
                           client.Connected &&
                           !ShouldDisconnect;
                }
            }

            // Constructor client handler
            public ClientHandler(TcpClient client)
            {
                this.client = client;
                this.stream = client.GetStream();
                this.Reader = new StreamReader(stream, Encoding.UTF8);
                this.Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                this.client.ReceiveTimeout = 30000;
                this.client.SendTimeout = 30000;
            }

            // ==================== MAIN LOOP UNTUK SETIAP CLIENT ====================
            public async Task RunAsync()
            {
                try
                {
                    // Proses login user dengan timeout
                    var usernameTask = Reader.ReadLineAsync();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                    var completedTask = await Task.WhenAny(usernameTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        await Writer.WriteLineAsync("[ERROR] Connection timeout - please try again");
                        Disconnect();
                        return;
                    }

                    Username = (await usernameTask)?.Trim();

                    // Validasi dan registrasi username
                    if (string.IsNullOrWhiteSpace(Username) || !RegisterUser(Username, this))
                    {
                        await Writer.WriteLineAsync("[ERROR] Username already taken or invalid (must be 2-20 alphanumeric characters)");
                        Disconnect();
                        return;
                    }

                    Log($"User {Username} connected from {client.Client.RemoteEndPoint}");

                    // Notifikasi user join ke semua client
                    string joinMessage = $"[{DateTime.Now:HH:mm}] [INFO] {Username} joined the chat";
                    BroadcastMessage(joinMessage);

                    UpdateUserList();

                    // Main message processing loop
                    string message;
                    while ((message = await Reader.ReadLineAsync()) != null && !ShouldDisconnect)
                    {
                        // Cek rate limiting
                        if (IsRateLimited(Username, out TimeSpan timeRemaining))
                        {
                            await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Please wait {timeRemaining.TotalSeconds:F1} seconds before sending another message");
                            continue;
                        }

                        // Handle commands dan pesan
                        if (message.StartsWith("/w "))
                        {
                            // Private message command
                            string[] parts = ParsePrivateMessage(message);
                            if (parts != null && parts.Length == 2)
                            {
                                string targetUser = parts[0];
                                string privateMessage = parts[1];

                                if (targetUser.Equals(Username, StringComparison.OrdinalIgnoreCase))
                                {
                                    await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] You cannot send private message to yourself");
                                }
                                else if (string.IsNullOrWhiteSpace(privateMessage))
                                {
                                    await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Private message cannot be empty");
                                }
                                else if (privateMessage.Length > 1000)
                                {
                                    await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Private message too long (max 1000 characters)");
                                }
                                else
                                {
                                    SendPrivateMessage(Username, targetUser, privateMessage);
                                }
                            }
                            else
                            {
                                await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Invalid private message format. Use: /w username message");
                            }
                        }
                        else if (message.Equals("/users"))
                        {
                            // Request daftar user
                            UpdateUserList();
                        }
                        else if (message.StartsWith("/"))
                        {
                            // Unknown command
                            await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Unknown command. Available commands: /w username message, /users");
                        }
                        else if (!string.IsNullOrWhiteSpace(message))
                        {
                            // Broadcast message biasa
                            if (message.Length > 2000)
                            {
                                await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Message too long (max 2000 characters)");
                                continue;
                            }

                            string sanitizedMessage = SanitizeMessage(message);
                            string formattedMsg = $"[{DateTime.Now:HH:mm}] {Username}: {sanitizedMessage}";
                            BroadcastMessage(formattedMsg);
                        }
                        else
                        {
                            await Writer.WriteLineAsync($"[{DateTime.Now:HH:mm}] [ERROR] Message cannot be empty");
                        }
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                           (socketEx.SocketErrorCode == SocketError.TimedOut ||
                                            socketEx.SocketErrorCode == SocketError.ConnectionReset))
                {
                    Log($"Client {Username} disconnected (timeout or connection reset)");
                }
                catch (Exception ex)
                {
                    Log($"Error with client {Username}: {ex.Message}");
                }
                finally
                {
                    // Cleanup dan unregistrasi client
                    if (Username != null)
                    {
                        UnregisterUser(Username, this);

                        string leaveMessage = $"[{DateTime.Now:HH:mm}] [INFO] {Username} left the chat";
                        BroadcastMessage(leaveMessage);

                        UpdateUserList();
                        Log($"User {Username} disconnected");
                    }
                    Disconnect();
                }
            }

            // ==================== DISCONNECT DAN CLEANUP ====================
            private void Disconnect()
            {
                try
                {
                    Reader?.Close();
                    Writer?.Close();
                    stream?.Close();
                    client?.Close();
                }
                catch { }
            }
        }
    }
}