using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TicTacToeServer
{
    public partial class ServerForm : Form
    {
        private TcpListener server;
        private bool isListening;
        private Dictionary<string, (TcpClient client, string symbol)> players;
        private List<string> playersReady;
        private TicTacToeGame game;
        
        private TextBox portTextBox;
        private Button startButton;
        private Button stopButton;
        private TextBox logTextBox;
        private TextBox boardTextBox;
        private Label statusLabel;
        private Label ipLabel;

        public ServerForm()
        {
            InitializeComponent();
            players = new Dictionary<string, (TcpClient, string)>();
            playersReady = new List<string>();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(500, 600);
            this.Text = "TicTacToe Server";

            statusLabel = new Label
            {
                Text = "Server is not running",
                ForeColor = Color.Red,
                Location = new Point(10, 10),
                AutoSize = true
            };

            string hostName = Dns.GetHostName();
            string ipAddress = Dns.GetHostEntry(hostName).AddressList[0].ToString();
            ipLabel = new Label
            {
                Text = "Host: " + ipAddress,
                Location = new Point(10, 40),
                AutoSize = true
            };

            Label portLabel = new Label
            {
                Text = "Port:",
                Location = new Point(10, 70),
                AutoSize = true
            };

            portTextBox = new TextBox
            {
                Location = new Point(50, 67),
                Size = new Size(100, 20)
            };

            startButton = new Button
            {
                Text = "Start Server",
                Location = new Point(10, 100),
                Size = new Size(100, 30)
            };
            startButton.Click += StartServer;

            stopButton = new Button
            {
                Text = "Stop Server",
                Location = new Point(120, 100),
                Size = new Size(100, 30),
                Enabled = false
            };
            stopButton.Click += StopServer;

            logTextBox = new TextBox
            {
                Location = new Point(10, 140),
                Size = new Size(460, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            boardTextBox = new TextBox
            {
                Location = new Point(10, 350),
                Size = new Size(460, 200),
                Multiline = true,
                ReadOnly = true
            };

            Controls.AddRange(new Control[] {
                statusLabel, ipLabel, portLabel, portTextBox,
                startButton, stopButton, logTextBox, boardTextBox
            });
        }

        private void StartServer(object sender, EventArgs e)
        {
            if (!int.TryParse(portTextBox.Text, out int port))
            {
                MessageBox.Show("Please enter a valid port number");
                return;
            }

            try
            {
                server = new TcpListener(IPAddress.Any, port);
                server.Start();
                isListening = true;

                statusLabel.Text = "Server is running";
                statusLabel.ForeColor = Color.Green;
                startButton.Enabled = false;
                stopButton.Enabled = true;

                AppendToLog($"Listening on port: {port}");

                Thread listenerThread = new Thread(AcceptClients);
                listenerThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}");
            }
        }

        private void StopServer(object sender, EventArgs e)
        {
            isListening = false;
            server?.Stop();
            
            foreach (var player in players.Values)
            {
                try
                {
                    SendMessage(player.client, "Disconnect");
                    player.client.Close();
                }
                catch { }
            }

            players.Clear();
            playersReady.Clear();

            statusLabel.Text = "Server is not running";
            statusLabel.ForeColor = Color.Red;
            startButton.Enabled = true;
            stopButton.Enabled = false;
            AppendToLog("Server stopped");
        }

        private void AcceptClients()
        {
            while (isListening)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    if (isListening)
                        AppendToLog("Error accepting client connection");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            string username = "";

            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                username = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                if (players.ContainsKey(username))
                {
                    SendMessage(client, "Username already taken");
                    client.Close();
                    return;
                }

                lock (players)
                {
                    players.Add(username, (client, ""));
                    playersReady.Add(username);
                }

                SendMessage(client, "Connected to the server");
                AppendToLog($"{username} connected");

                if (playersReady.Count == 2)
                {
                    StartGame();
                }

                while (true)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    ProcessClientMessage(username, message);
                }
            }
            catch (Exception)
            {
                if (!string.IsNullOrEmpty(username))
                {
                    lock (players)
                    {
                        players.Remove(username);
                        playersReady.Remove(username);
                    }
                    AppendToLog($"{username} disconnected");
                }
            }
        }

        private void ProcessClientMessage(string username, string message)
        {
            if (message.StartsWith("MOVE"))
            {
                if (int.TryParse(message.Substring(5), out int cell))
                {
                    var playerInfo = players[username];
                    var (valid, gameStatus) = game.MakeMove(cell, playerInfo.symbol);

                    if (valid)
                    {
                        SendMessage(players[username].client, "VALID_MOVE");
                        UpdateBoard();
                        AppendToLog($"{username} made move at cell {cell}");

                        if (gameStatus.status == "end")
                        {
                            if (gameStatus.result == "win")
                            {
                                SendMessage(players[username].client, "WIN");
                                string opponent = GetOpponentUsername(username);
                                SendMessage(players[opponent].client, "LOSS");
                                AppendToLog($"{username} won!");
                            }
                            else if (gameStatus.result == "draw")
                            {
                                BroadcastMessage("DRAW");
                                AppendToLog("Game ended in a draw");
                            }
                        }
                        else
                        {
                            string opponent = GetOpponentUsername(username);
                            if (game.Turn == playerInfo.symbol)
                            {
                                SendMessage(players[username].client, "YOUR_TURN");
                                SendMessage(players[opponent].client, "OPPONENT_TURN");
                                AppendToLog($"It's {username}'s turn");
                            }
                            else
                            {
                                SendMessage(players[username].client, "OPPONENT_TURN");
                                SendMessage(players[opponent].client, "YOUR_TURN");
                                AppendToLog($"It's {opponent}'s turn");
                            }
                        }
                    }
                    else
                    {
                        SendMessage(players[username].client, "INVALID_MOVE");
                        AppendToLog($"Invalid move by {username}");
                    }
                }
            }
        }

        private string GetOpponentUsername(string username)
        {
            return players.Keys.FirstOrDefault(k => k != username);
        }

        private void StartGame()
        {
            game = new TicTacToeGame();
            string[] symbols = { "X", "O" };

            for (int i = 0; i < 2; i++)
            {
                string username = playersReady[i];
                var playerClient = players[username].client;
                players[username] = (playerClient, symbols[i]);
                SendMessage(playerClient, $"SYMBOL {symbols[i]}");
                AppendToLog($"{username} is appointed {symbols[i]}");
            }

            BroadcastBoard();
            UpdateBoard();

            SendMessage(players[playersReady[0]].client, "YOUR_TURN");
            SendMessage(players[playersReady[1]].client, "OPPONENT_TURN");
            AppendToLog($"It's {playersReady[0]}'s turn");
        }

        private void SendMessage(TcpClient client, string message)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = Encoding.ASCII.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception)
            {
                // Handle disconnection
            }
        }

        private void BroadcastMessage(string message)
        {
            foreach (var player in players.Values)
            {
                SendMessage(player.client, message);
            }
        }

        private void BroadcastBoard()
        {
            string boardRepr = game.GetBoardRepresentation();
            foreach (var player in players.Values)
            {
                SendMessage(player.client, $"BOARD {boardRepr}");
            }
        }

        private void UpdateBoard()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateBoard));
                return;
            }
            boardTextBox.Text = "Board:\r\n\r\n" + game.GetBoardRepresentation();
        }

        private void AppendToLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendToLog), message);
                return;
            }
            logTextBox.AppendText(message + Environment.NewLine);
        }
    }
}