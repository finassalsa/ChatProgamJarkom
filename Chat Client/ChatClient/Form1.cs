using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;
        private bool connected = false;

        public Form1()
        {
            InitializeComponent();
            btnSend.Click += BtnSend_Click;
            btnConnect.Click += BtnConnect_Click;

            // Enter juga bisa kirim pesan
            txtMessage.KeyDown += TxtMessage_KeyDown;
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (!connected)
            {
                try
                {
                    client = new TcpClient();
                    client.Connect(txtIP.Text, int.Parse(txtPort.Text));

                    NetworkStream stream = client.GetStream();
                    reader = new StreamReader(stream, Encoding.UTF8);
                    writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    // kirim username ke server
                    writer.WriteLine(txtUsername.Text);

                    connected = true;
                    lstChat.Items.Add($"[INFO] Connected to {txtIP.Text}:{txtPort.Text} as {txtUsername.Text}");

                    // mulai thread untuk baca pesan dari server
                    receiveThread = new Thread(ReceiveMessages);
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (connected && !string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                // Format pesan (server yang akan broadcast kembali)
                string formattedMsg = $"[{DateTime.Now:HH:mm}] {txtUsername.Text}: {txtMessage.Text.Trim()}";
                writer.WriteLine(formattedMsg);

                txtMessage.Clear();
            }
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                BtnSend_Click(sender, e);  // jalankan fungsi send
                e.SuppressKeyPress = true; // cegah enter bikin baris baru
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                string message;
                const string USERS_PREFIX = "[USERS]";

                while ((message = reader.ReadLine()) != null)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        if (message.StartsWith(USERS_PREFIX))
                        {
                            // gunakan Length, bukan angka literal 8
                            string payload = message.Substring(USERS_PREFIX.Length);
                            lstUsers.Items.Clear();

                            if (!string.IsNullOrWhiteSpace(payload))
                            {
                                string[] users = payload.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string u in users)
                                {
                                    lstUsers.Items.Add(u.Trim());
                                }
                            }
                        }
                        else
                        {
                            // tampilkan pesan chat / info
                            lstChat.Items.Add(message);
                        }
                    });
                }
            }
            catch
            {
                // ignore jika disconnect
            }
            finally
            {
                connected = false;
                Invoke((MethodInvoker)(() => lstChat.Items.Add("[INFO] Disconnected")));
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try
            {
                if (connected)
                {
                    writer.Close();
                    reader.Close();
                    client.Close();
                }
            }
            catch { }
        }
    }
}
