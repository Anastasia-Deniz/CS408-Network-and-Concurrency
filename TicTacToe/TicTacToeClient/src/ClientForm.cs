using System.Net.Sockets;
using System.Text;

namespace TicTacToeClient
{
    public partial class ClientForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private string symbol;
        private bool connected;

        private TextBox ipTextBox;
        private TextBox portTextBox;
        private TextBox usernameTextBox;
        private Button connectButton;
        private Button disconnectButton;
        private TextBox moveTextBox;
        private Button moveButton;
        private TextBox logTextBox;
        private TextBox boardTextBox;
        private Label statusLabel;

        public ClientForm()
        {
            InitializeComponent();
            connected = false;
        }

        private void InitializeComponent()
        {
            this.Size = new Size(500, 600);
            this.Text = "TicTacToe Client";

            statusLabel = new Label
            {
                Text = "Not connected",
                ForeColor = Color.Red,
                Location = new Point(10, 10),
                AutoSize = true
            };

            Label ipLabel = new Label
            {
                Text = "IP:",
                Location = new Point(10, 40),
                AutoSize = true
            };

            ipTextBox = new TextBox
            {
                Location = new Point(100, 37),
                Size = new Size(100, 20)
            };

            Label portLabel = new Label
            {
                Text = "Port:",
                Location = new Point(10, 70),
                AutoSize = true
            };

            portTextBox = new TextBox
            {
                Location = new Point(100, 67),
                Size = new Size(100, 20)
            };

            Label usernameLabel = new Label
            {
                Text = "Username:",
                Location = new Point(10, 100),
                AutoSize = true
            };

            usernameTextBox = new TextBox
            {
                Location = new Point(100, 97),
                Size = new Size(100, 20)
            };

            connectButton = new Button
            {
                Text = "Connect",
                Location = new Point(10, 130),
                Size = new Size(90, 30)
            };
            connectButton.Click += Connect;

            disconnectButton = new Button
            {
                Text = "Disconnect",
                Location = new Point(110, 130),
                Size = new Size(90, 30),
                Enabled = false
            };
            disconnectButton.Click += Disconnect;

            Label moveLabel = new Label
            {
                Text = "Move:",
                Location = new Point(10, 170),
                AutoSize = true
            };

            moveTextBox = new TextBox
            {
                Location = new Point(100, 167),
                Size = new Size(100, 20)
            };

            moveButton = new Button
            {
                Text = "MOVE",
                Location = new Point(210, 165),
                Size = new Size(90, 30),
                Enabled = false
            };
            moveButton.Click += SendMove;

            logTextBox = new TextBox
            {
                Location = new Point(10, 210),
                Size = new Size(230, 340),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            boardTextBox = new TextBox
            {
                Location = new Point(250, 210),
                Size = new Size(230, 340),
                Multiline = true,
                ReadOnly = true
            };

            Controls.AddRange(new Control[] {
                statusLabel, ipLabel, ipTextBox, portLabel, portTextBox,
                usernameLabel, usernameTextBox, connectButton, disconnectButton,
                moveLabel, moveTextBox, moveButton, logTextBox, boardTextBox
            });
        }

        private void Connect(object sender, EventArgs e)
        {
            if (!ValidateInputs())
                return;

            try
            {
                client = new TcpClient();
                client.Connect(ipTextBox.Text, int.Parse(portTextBox.Text));
                stream = client.GetStream();

                byte[] usernameBytes = Encoding.ASCII.GetBytes(usernameTextBox.Text);
                stream.Write(usernameBytes, 0, usernameBytes.Length);

                byte[] responseBuffer = new byte[1024];
                int bytesRead = stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead);

                if (response != "Connected to the server")
                {
                    AppendToLog("Could not connect to the server: " + response);
                    client.Close();
                    return;
                }

                connected = true;
                UpdateConnectionStatus(true);
                AppendToLog("Connected to the server");

                Thread receiveThread = new Thread(ReceiveMessages);
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                AppendToLog("Connection error: " + ex.Message);
                client?.Close();
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(ipTextBox.Text) ||
                string.IsNullOrWhiteSpace(portTextBox.Text) ||
                string.IsNullOrWhiteSpace(usernameTextBox.Text))
            {
                MessageBox.Show("Please fill in all fields");
                return false;
            }

            if (!int.TryParse(portTextBox.Text, out _))
            {
                MessageBox.Show("Please enter a valid port number");
                return false;
            }

            return true;
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(UpdateConnectionStatus), isConnected);
                return;
            }

            connected = isConnected;
            statusLabel.Text = isConnected ? "Connected" : "Not connected";
            statusLabel.ForeColor = isConnected ? Color.Green : Color.Red;
            
            ipTextBox.Enabled = !isConnected;
            portTextBox.Enabled = !isConnected;
            usernameTextBox.Enabled = !isConnected;
            connectButton.Enabled = !isConnected;
            disconnectButton.Enabled = isConnected;
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];

            while (connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    ProcessServerMessage(message);
                }
                catch
                {
                    if (connected)
                    {
                        AppendToLog("Connection lost");
                        UpdateConnectionStatus(false);
                        break;
                    }
                }
            }
        }

        private void ProcessServerMessage(string message)
        {
            string[] tokens = message.Split(new[] { ' ' }, 2);
            string command = tokens[0];
            string content = tokens.Length > 1 ? tokens[1] : "";

            switch (command)
            {
                case "SYMBOL":
                    symbol = content;
                    AppendToLog($"Your symbol is: {symbol}");
                    break;

                case "BOARD":
                    UpdateBoard(content);
                    break;

                case "MESSAGE":
                    AppendToLog(content);
                    break;

                case "VALID_MOVE":
                    AppendToLog("Valid move");
                    ClearMoveInput();
                    break;

                case "INVALID_MOVE":
                    AppendToLog("Invalid move!\nTry again");
                    ClearMoveInput();
                    break;

                case "YOUR_TURN":
                    AppendToLog("It's your turn");
                    EnableMoveControls(true);
                    break;

                case "OPPONENT_TURN":
                    AppendToLog("It's opponent's turn");
                    EnableMoveControls(false);
                    break;

                case "WIN":
                    AppendToLog("You won!");
                    EnableMoveControls(false);
                    break;

                case "LOSS":
                    AppendToLog("You lost!");
                    EnableMoveControls(false);
                    break;

                case "DRAW":
                    AppendToLog("It's a draw!");
                    EnableMoveControls(false);
                    break;

                case "Disconnect":
                    Disconnect(null, null);
                    break;
            }
        }

        private void UpdateBoard(string boardData)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateBoard), boardData);
                return;
            }

            boardTextBox.Text = "Board:\r\n\r\n" + boardData;
        }

        private void EnableMoveControls(bool enable)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(EnableMoveControls), enable);
                return;
            }

            moveButton.Enabled = enable && connected;
            moveTextBox.Enabled = enable && connected;
        }

        private void ClearMoveInput()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClearMoveInput));
                return;
            }

            moveTextBox.Clear();
        }

        private void SendMove(object sender, EventArgs e)
        {
            if (!connected || !int.TryParse(moveTextBox.Text, out int move) || move < 1 || move > 9)
            {
                MessageBox.Show("Please enter a valid move (1-9)");
                return;
            }

            try
            {
                string moveMessage = $"MOVE {move}";
                byte[] moveBytes = Encoding.ASCII.GetBytes(moveMessage);
                stream.Write(moveBytes, 0, moveBytes.Length);
            }
            catch
            {
                AppendToLog("Error sending move");
            }
        }

        private void Disconnect(object sender, EventArgs e)
        {
            if (connected)
            {
                connected = false;
                try
                {
                    byte[] disconnectBytes = Encoding.ASCII.GetBytes("Disconnect");
                    stream.Write(disconnectBytes, 0, disconnectBytes.Length);
                }
                catch { }
                finally
                {
                    stream?.Close();
                    client?.Close();
                }
            }
            UpdateConnectionStatus(false);
            AppendToLog("Disconnected from server");
        }

        private void AppendToLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendToLog), message);
                return;
            }

            logTextBox.AppendText(message + Environment.NewLine);
            logTextBox.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Disconnect(null, null);
            base.OnFormClosing(e);
        }
    }
}