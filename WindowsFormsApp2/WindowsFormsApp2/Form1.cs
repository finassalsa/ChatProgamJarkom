using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        // ==================== VARIABEL KONEKSI DAN STATE ====================
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private bool connected = false;
        private string currentUsername;
        private ContextMenuStrip userContextMenu;
        private List<PrivateChatForm> openPrivateChats = new List<PrivateChatForm>();
        private CancellationTokenSource cancellationTokenSource;

        // ==================== VARIABEL THEME ====================
        private bool isDarkTheme = false;
        private Color lightBackColor = SystemColors.Control;
        private Color lightForeColor = SystemColors.ControlText;
        private Color lightListBackColor = SystemColors.Window;
        private Color lightListForeColor = SystemColors.WindowText;
        private Color darkBackColor = Color.FromArgb(45, 45, 48);
        private Color darkForeColor = Color.White;
        private Color darkListBackColor = Color.FromArgb(37, 37, 38);
        private Color darkListForeColor = Color.White;
        private Color darkButtonBackColor = Color.FromArgb(63, 63, 70);
        private Color darkButtonForeColor = Color.White;

        // ==================== CONSTRUCTOR DAN INISIALISASI ====================
        public Form1()
        {
            InitializeComponent();
            SetupEventHandlers();
            SetupContextMenu();
            SetupThemeToggle();
            UpdateUIState();
        }

        // ==================== SETUP EVENT HANDLERS ====================
        private void SetupEventHandlers()
        {
            btnSend.Click += BtnSend_Click;
            btnConnect.Click += BtnConnect_Click;
            txtMessage.KeyDown += TxtMessage_KeyDown;
            FormClosing += Form1_FormClosing;
            lstUsers.MouseDown += LstUsers_MouseDown;
            lstUsers.DoubleClick += LstUsers_DoubleClick;
            lstChat.DrawItem += LstChat_DrawItem;
        }

        // ==================== SETUP CONTEXT MENU UNTUK USER LIST ====================
        private void SetupContextMenu()
        {
            userContextMenu = new ContextMenuStrip();

            ToolStripMenuItem privateChatItem = new ToolStripMenuItem("Send Private Message");
            privateChatItem.Click += PrivateChatItem_Click;
            userContextMenu.Items.Add(privateChatItem);

            lstUsers.ContextMenuStrip = userContextMenu;
        }

        // ==================== SETUP THEME TOGGLE ====================
        private void SetupThemeToggle()
        {
            // Create theme toggle button
            Button btnTheme = new Button();
            btnTheme.Text = "🌙 Dark";
            btnTheme.Size = new Size(80, 23);
            btnTheme.Location = new Point(630, 12);
            btnTheme.Click += BtnTheme_Click;
            this.Controls.Add(btnTheme);
        }

        // ==================== THEME MANAGEMENT ====================
        private void BtnTheme_Click(object sender, EventArgs e)
        {
            ToggleTheme();
        }

        private void ToggleTheme()
        {
            isDarkTheme = !isDarkTheme;
            ApplyTheme(isDarkTheme);

            // Update all open private chat forms
            foreach (var privateForm in openPrivateChats)
            {
                privateForm.ApplyTheme(isDarkTheme);
            }
        }

        private void ApplyTheme(bool dark)
        {
            if (dark)
            {
                // Dark theme
                this.BackColor = darkBackColor;
                this.ForeColor = darkForeColor;

                // Apply to all controls
                ApplyDarkThemeToControl(this);

                // Update theme button
                foreach (Control control in this.Controls)
                {
                    if (control is Button btn && (btn.Text.StartsWith("🌙") || btn.Text.StartsWith("☀️")))
                    {
                        btn.Text = "☀️ Light";
                        break;
                    }
                }
            }
            else
            {
                // Light theme
                this.BackColor = lightBackColor;
                this.ForeColor = lightForeColor;

                // Apply to all controls
                ApplyLightThemeToControl(this);

                // Update theme button
                foreach (Control control in this.Controls)
                {
                    if (control is Button btn && (btn.Text.StartsWith("🌙") || btn.Text.StartsWith("☀️")))
                    {
                        btn.Text = "🌙 Dark";
                        break;
                    }
                }
            }

            // Refresh list boxes
            lstChat.Invalidate();
            lstUsers.Invalidate();
        }

        // ==================== APPLY THEME KE CONTROL ====================
        private void ApplyDarkThemeToControl(Control control)
        {
            control.BackColor = darkBackColor;
            control.ForeColor = darkForeColor;

            if (control is ListBox listBox)
            {
                listBox.BackColor = darkListBackColor;
                listBox.ForeColor = darkListForeColor;
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = darkListBackColor;
                textBox.ForeColor = darkListForeColor;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is Button button && !button.Text.StartsWith("🌙") && !button.Text.StartsWith("☀️"))
            {
                button.BackColor = darkButtonBackColor;
                button.ForeColor = darkButtonForeColor;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            }
            else if (control is Label label)
            {
                label.ForeColor = darkForeColor;
            }

            // Recursively apply to child controls
            foreach (Control childControl in control.Controls)
            {
                ApplyDarkThemeToControl(childControl);
            }
        }

        private void ApplyLightThemeToControl(Control control)
        {
            control.BackColor = lightBackColor;
            control.ForeColor = lightForeColor;

            if (control is ListBox listBox)
            {
                listBox.BackColor = lightListBackColor;
                listBox.ForeColor = lightListForeColor;
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = lightListBackColor;
                textBox.ForeColor = lightListForeColor;
                textBox.BorderStyle = BorderStyle.Fixed3D;
            }
            else if (control is Button button && !button.Text.StartsWith("🌙") && !button.Text.StartsWith("☀️"))
            {
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatStyle = FlatStyle.Standard;
            }
            else if (control is Label label)
            {
                label.ForeColor = lightForeColor;
            }

            // Recursively apply to child controls
            foreach (Control childControl in control.Controls)
            {
                ApplyLightThemeToControl(childControl);
            }
        }

        // ==================== CUSTOM DRAWING UNTUK CHAT MESSAGES ====================
        private void LstChat_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            string itemText = lstChat.Items[e.Index].ToString();
            Brush textBrush;

            // Choose color based on message type and theme
            if (itemText.Contains("[PM from"))
            {
                textBrush = isDarkTheme ? Brushes.LightBlue : Brushes.Blue;
            }
            else if (itemText.Contains("[ERROR]"))
            {
                textBrush = Brushes.Red;
            }
            else if (itemText.Contains("[INFO]") || itemText.Contains("[SYSTEM]"))
            {
                textBrush = isDarkTheme ? Brushes.LightGreen : Brushes.Green;
            }
            else
            {
                textBrush = isDarkTheme ? Brushes.White : Brushes.Black;
            }

            e.Graphics.DrawString(itemText, e.Font, textBrush, e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
        }

        // ==================== PRIVATE CHAT MANAGEMENT ====================
        private void LstUsers_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = lstUsers.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    lstUsers.SelectedIndex = index;
                }
            }
        }

        private void LstUsers_DoubleClick(object sender, EventArgs e)
        {
            StartPrivateChat();
        }

        private void PrivateChatItem_Click(object sender, EventArgs e)
        {
            StartPrivateChat();
        }

        private void StartPrivateChat()
        {
            if (lstUsers.SelectedItem != null && connected)
            {
                string targetUser = lstUsers.SelectedItem.ToString();
                if (targetUser != currentUsername)
                {
                    PrivateChatForm existingForm = openPrivateChats.Find(f => f.TargetUser == targetUser);
                    if (existingForm != null)
                    {
                        existingForm.BringToFront();
                        existingForm.Focus();
                    }
                    else
                    {
                        PrivateChatForm privateChatForm = new PrivateChatForm(targetUser, currentUsername, SendPrivateMessage, isDarkTheme);
                        privateChatForm.FormClosed += (s, args) => openPrivateChats.Remove((PrivateChatForm)s);
                        openPrivateChats.Add(privateChatForm);
                        privateChatForm.Show();
                    }
                }
                else
                {
                    MessageBox.Show("You cannot send private message to yourself!", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // ==================== FORM CLOSING DAN CLEANUP ====================
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();

            foreach (var privateForm in openPrivateChats.ToArray())
            {
                privateForm.Close();
            }
        }

        // ==================== KONEKSI KE SERVER ====================
        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            if (!connected)
            {
                await ConnectAsync();
            }
            else
            {
                Disconnect();
            }
        }

        private async Task ConnectAsync()
        {
            try
            {
                btnConnect.Text = "Connecting...";
                btnConnect.Enabled = false;
                Cursor = Cursors.WaitCursor;

                string username = txtUsername.Text.Trim();

                if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
                {
                    MessageBox.Show("Username must be at least 2 characters long", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid port number (1-65535)", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string ipAddress = txtIP.Text.Trim();
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    MessageBox.Show("Please enter a valid IP address", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                client = new TcpClient();

                var connectTask = client.ConnectAsync(ipAddress, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Connection timeout - server not responding");
                }

                await connectTask;

                NetworkStream stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                currentUsername = username;
                await writer.WriteLineAsync(currentUsername);

                var readTask = reader.ReadLineAsync();
                var readTimeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
                var readCompletedTask = await Task.WhenAny(readTask, readTimeoutTask);

                if (readCompletedTask == readTimeoutTask)
                {
                    throw new TimeoutException("Server response timeout");
                }

                string response = await readTask;
                if (response != null && response.StartsWith("[ERROR]"))
                {
                    MessageBox.Show(response, "Connection Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Disconnect();
                    return;
                }

                connected = true;
                cancellationTokenSource = new CancellationTokenSource();
                UpdateUIState();

                _ = Task.Run(() => ReceiveMessagesAsync(cancellationTokenSource.Token));

                LogToChat($"[SYSTEM] Connected to server as {currentUsername}", MessageType.System);
                LogToChat($"[SYSTEM] Type /users to see online users or /w username message for private chat", MessageType.System);
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show($"Connection timeout: {ex.Message}\n\nPlease check:\n• Server is running\n• IP address is correct\n• Port is correct",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogToChat($"[ERROR] Connection failed: {ex.Message}", MessageType.Error);
                Disconnect();
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Network error: {ex.Message}\n\nPlease check your network connection and try again.",
                    "Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogToChat($"[ERROR] Network error: {ex.Message}", MessageType.Error);
                Disconnect();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Connection Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogToChat($"[ERROR] Connection failed: {ex.Message}", MessageType.Error);
                Disconnect();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ==================== DISCONNECT DARI SERVER ====================
        private void Disconnect()
        {
            connected = false;
            cancellationTokenSource?.Cancel();

            try
            {
                writer?.Close();
                reader?.Close();
                client?.Close();
            }
            catch { }

            UpdateUIState();
            LogToChat("[SYSTEM] Disconnected from server", MessageType.System);

            foreach (var privateForm in openPrivateChats.ToArray())
            {
                privateForm.Close();
            }
        }

        // ==================== UPDATE UI STATE ====================
        private void UpdateUIState()
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)UpdateUIState);
                return;
            }

            btnConnect.Text = connected ? "Disconnect" : "Connect";
            btnConnect.Enabled = true;
            txtIP.Enabled = !connected;
            txtPort.Enabled = !connected;
            txtUsername.Enabled = !connected;
            btnSend.Enabled = connected;
            txtMessage.Enabled = connected;

            if (connected)
            {
                txtMessage.Focus();
            }

            if (!connected)
            {
                lstUsers.Items.Clear();
                lblUserCount.Text = "Online Users: 0";
            }
        }

        // ==================== SEND MESSAGE HANDLING ====================
        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
            {
                SendMessage();
                e.SuppressKeyPress = true;
            }
        }

        private async void SendMessage()
        {
            if (connected && !string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                try
                {
                    string message = txtMessage.Text.Trim();
                    await writer.WriteLineAsync(message);
                    txtMessage.Clear();
                }
                catch (Exception ex)
                {
                    LogToChat($"[ERROR] Failed to send message: {ex.Message}", MessageType.Error);
                }
            }
        }

        private async void SendPrivateMessage(string targetUser, string message)
        {
            if (connected && !string.IsNullOrWhiteSpace(message))
            {
                try
                {
                    await writer.WriteLineAsync($"/w {targetUser} {message}");
                }
                catch (Exception ex)
                {
                    LogToChat($"[ERROR] Failed to send private message: {ex.Message}", MessageType.Error);
                }
            }
        }

        // ==================== RECEIVE MESSAGES DARI SERVER ====================
        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                string message;
                while (connected && (message = await reader.ReadLineAsync()) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    ProcessReceivedMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (connected)
                {
                    LogToChat($"[ERROR] Connection lost: {ex.Message}", MessageType.Error);
                }
            }
            finally
            {
                connected = false;
                UpdateUIState();
            }
        }

        // ==================== PROCESS MESSAGE DARI SERVER ====================
        private void ProcessReceivedMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => ProcessReceivedMessage(message)));
                return;
            }

            try
            {
                if (message.StartsWith("[USERS]"))
                {
                    string payload = message.Substring("[USERS]".Length);
                    UpdateUserList(payload);
                }
                else if (message.Contains("[PM from"))
                {
                    // Handle pesan private
                    string fromUser = ExtractUserFromPrivateMessage(message);
                    if (fromUser != null && fromUser != currentUsername)
                    {
                        // Tampilkan notifikasi ringan di main chat
                        string notification = $"[{DateTime.Now:HH:mm}] [INFO] Private message from {fromUser}";
                        LogToChat(notification, MessageType.System);

                        // Kirim ke private chat window
                        PrivateChatForm privateForm = openPrivateChats.Find(f => f.TargetUser == fromUser);
                        if (privateForm != null)
                        {
                            privateForm.ReceivePrivateMessage(message);
                        }
                        else
                        {
                            // Jika window belum terbuka, buka baru
                            PrivateChatForm newPrivateForm = new PrivateChatForm(fromUser, currentUsername, SendPrivateMessage, isDarkTheme);
                            newPrivateForm.FormClosed += (s, args) => openPrivateChats.Remove((PrivateChatForm)s);
                            openPrivateChats.Add(newPrivateForm);
                            newPrivateForm.Show();
                            newPrivateForm.ReceivePrivateMessage(message);
                        }
                    }
                }
                else
                {
                    // Pesan broadcast normal
                    MessageType messageType = MessageType.Normal;
                    if (message.Contains("[ERROR]"))
                    {
                        messageType = MessageType.Error;
                    }
                    else if (message.Contains("[INFO]") || message.Contains("[SYSTEM]"))
                    {
                        messageType = MessageType.System;
                    }

                    LogToChat(message, messageType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        // ==================== UTILITY METHODS ====================
        private string ExtractUserFromPrivateMessage(string message)
        {
            try
            {
                if (message.Contains("[PM from "))
                {
                    int start = message.IndexOf("[PM from ") + "[PM from ".Length;
                    int end = message.IndexOf("]", start);
                    return message.Substring(start, end - start).Trim();
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private void UpdateUserList(string usersPayload)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => UpdateUserList(usersPayload)));
                return;
            }

            lstUsers.Items.Clear();

            if (!string.IsNullOrWhiteSpace(usersPayload))
            {
                string[] users = usersPayload.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string user in users)
                {
                    string trimmedUser = user.Trim();
                    if (trimmedUser != currentUsername)
                    {
                        lstUsers.Items.Add(trimmedUser);
                    }
                }

                int onlineCount = users.Length - 1;
                lblUserCount.Text = $"Online Users: {onlineCount}";
            }
            else
            {
                lblUserCount.Text = "Online Users: 0";
            }
        }

        private void LogToChat(string message, MessageType messageType = MessageType.Normal)
        {
            if (lstChat.InvokeRequired)
            {
                lstChat.Invoke((MethodInvoker)(() => LogToChat(message, messageType)));
                return;
            }

            lstChat.Items.Add(message);
            if (lstChat.Items.Count > 0)
            {
                lstChat.TopIndex = lstChat.Items.Count - 1;
            }

            lstChat.Refresh();
        }

        private enum MessageType
        {
            Normal,
            Private,
            Error,
            System
        }
    }

    // ==================== CLASS PRIVATE CHAT FORM ====================
    public class PrivateChatForm : Form
    {
        public string TargetUser { get; private set; }
        private string currentUser;
        private Action<string, string> sendPrivateMessage;
        private TextBox txtPrivateMessage;
        private Button btnSendPrivate;
        private ListBox lstPrivateChat;
        private bool isDarkTheme;

        // Theme colors untuk private chat
        private Color lightBackColor = SystemColors.Control;
        private Color lightForeColor = SystemColors.ControlText;
        private Color lightListBackColor = SystemColors.Window;
        private Color lightListForeColor = SystemColors.WindowText;
        private Color darkBackColor = Color.FromArgb(45, 45, 48);
        private Color darkForeColor = Color.White;
        private Color darkListBackColor = Color.FromArgb(37, 37, 38);
        private Color darkListForeColor = Color.White;
        private Color darkButtonBackColor = Color.FromArgb(63, 63, 70);
        private Color darkButtonForeColor = Color.White;

        public PrivateChatForm(string targetUser, string currentUser, Action<string, string> sendPrivateMessage, bool isDarkTheme)
        {
            this.TargetUser = targetUser;
            this.currentUser = currentUser;
            this.sendPrivateMessage = sendPrivateMessage;
            this.isDarkTheme = isDarkTheme;
            InitializeComponent();
            ApplyTheme(isDarkTheme);
        }

        // ==================== THEME MANAGEMENT UNTUK PRIVATE CHAT ====================
        public void ApplyTheme(bool dark)
        {
            isDarkTheme = dark;
            if (dark)
            {
                this.BackColor = darkBackColor;
                this.ForeColor = darkForeColor;
                lstPrivateChat.BackColor = darkListBackColor;
                lstPrivateChat.ForeColor = darkListForeColor;
                txtPrivateMessage.BackColor = darkListBackColor;
                txtPrivateMessage.ForeColor = darkListForeColor;
                btnSendPrivate.BackColor = darkButtonBackColor;
                btnSendPrivate.ForeColor = darkButtonForeColor;
                btnSendPrivate.FlatStyle = FlatStyle.Flat;
            }
            else
            {
                this.BackColor = lightBackColor;
                this.ForeColor = lightForeColor;
                lstPrivateChat.BackColor = lightListBackColor;
                lstPrivateChat.ForeColor = lightListForeColor;
                txtPrivateMessage.BackColor = lightListBackColor;
                txtPrivateMessage.ForeColor = lightListForeColor;
                btnSendPrivate.BackColor = SystemColors.Control;
                btnSendPrivate.ForeColor = SystemColors.ControlText;
                btnSendPrivate.FlatStyle = FlatStyle.Standard;
            }
            lstPrivateChat.Invalidate();
        }

        // ==================== INITIALIZE COMPONENTS PRIVATE CHAT ====================
        private void InitializeComponent()
        {
            this.Text = $"Private Chat with {TargetUser}";
            this.Size = new System.Drawing.Size(500, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblTitle = new Label();
            lblTitle.Text = $"Private Chat with {TargetUser}";
            lblTitle.Font = new Font(lblTitle.Font, FontStyle.Bold);
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 30;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(lblTitle);

            lstPrivateChat = new ListBox();
            lstPrivateChat.Dock = DockStyle.Fill;
            lstPrivateChat.Height = 280;
            lstPrivateChat.DrawMode = DrawMode.OwnerDrawVariable;
            lstPrivateChat.DrawItem += LstPrivateChat_DrawItem;
            this.Controls.Add(lstPrivateChat);

            Panel bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 60;

            txtPrivateMessage = new TextBox();
            txtPrivateMessage.Location = new System.Drawing.Point(10, 10);
            txtPrivateMessage.Width = 350;
            txtPrivateMessage.KeyDown += TxtPrivateMessage_KeyDown;
            bottomPanel.Controls.Add(txtPrivateMessage);

            btnSendPrivate = new Button();
            btnSendPrivate.Text = "Send";
            btnSendPrivate.Location = new System.Drawing.Point(370, 10);
            btnSendPrivate.Width = 80;
            btnSendPrivate.Click += BtnSendPrivate_Click;
            bottomPanel.Controls.Add(btnSendPrivate);

            this.Controls.Add(bottomPanel);

            lstPrivateChat.Items.Add($"[SYSTEM] Private chat with {TargetUser}");
            this.Activate();
            txtPrivateMessage.Focus();

            // Apply theme to title label
            if (isDarkTheme)
            {
                lblTitle.BackColor = darkBackColor;
                lblTitle.ForeColor = darkForeColor;
                bottomPanel.BackColor = darkBackColor;
            }
        }

        // ==================== CUSTOM DRAWING UNTUK PRIVATE CHAT ====================
        private void LstPrivateChat_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            string itemText = lstPrivateChat.Items[e.Index].ToString();
            Brush textBrush;

            if (itemText.Contains($"{currentUser}:"))
            {
                textBrush = isDarkTheme ? Brushes.LightBlue : Brushes.Blue;
            }
            else if (itemText.Contains($"{TargetUser}:"))
            {
                textBrush = isDarkTheme ? Brushes.LightGreen : Brushes.DarkGreen;
            }
            else if (itemText.Contains("[PM from"))
            {
                textBrush = isDarkTheme ? Brushes.Plum : Brushes.Purple;
            }
            else if (itemText.Contains("[ERROR]"))
            {
                textBrush = Brushes.Red;
            }
            else if (itemText.Contains("[INFO]") || itemText.Contains("[SYSTEM]"))
            {
                textBrush = isDarkTheme ? Brushes.LightGreen : Brushes.Green;
            }
            else
            {
                textBrush = isDarkTheme ? Brushes.White : Brushes.Black;
            }

            e.Graphics.DrawString(itemText, e.Font, textBrush, e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
        }

        // ==================== MESSAGE HANDLING UNTUK PRIVATE CHAT ====================
        public void ReceivePrivateMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => ReceivePrivateMessage(message)));
                return;
            }

            lstPrivateChat.Items.Add(message);
            lstPrivateChat.TopIndex = lstPrivateChat.Items.Count - 1;
        }

        private void BtnSendPrivate_Click(object sender, EventArgs e)
        {
            SendPrivateMessage();
        }

        private void TxtPrivateMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendPrivateMessage();
                e.SuppressKeyPress = true;
            }
        }

        private void SendPrivateMessage()
        {
            if (!string.IsNullOrWhiteSpace(txtPrivateMessage.Text))
            {
                string message = txtPrivateMessage.Text.Trim();

                string timestamp = DateTime.Now.ToString("HH:mm");
                string displayMessage = $"[{timestamp}] {currentUser}: {message}";
                lstPrivateChat.Items.Add(displayMessage);
                lstPrivateChat.TopIndex = lstPrivateChat.Items.Count - 1;

                sendPrivateMessage(TargetUser, message);

                txtPrivateMessage.Clear();
                txtPrivateMessage.Focus();
            }
        }
    }
}