using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HLVRCoOpModCore;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Dialog for browsing and connecting to servers
    /// </summary>
    public partial class ServerBrowserDialog : Form
    {
        private ListView listViewServers;
        private Button buttonRefresh;
        private Button buttonConnect;
        private Button buttonClose;
        private Button buttonAddServer;
        private TextBox textBoxServerIp;
        private TextBox textBoxServerPort;
        private Label labelServerIp;
        private Label labelServerPort;
        private Label labelStatus;
        private CheckBox checkBoxShowPasswordProtected;
        private CheckBox checkBoxShowFull;
        private ServerDiscovery serverDiscovery;
        private UserInterface parentForm;
        private Dictionary<string, ServerInfo> servers = new();
        
        public ServerBrowserDialog(UserInterface parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
                
            parentForm = parent;
            serverDiscovery = ServerDiscovery.Instance; // Use singleton instance
            serverDiscovery.ServerDiscovered += ServerDiscovery_ServerDiscovered;
            serverDiscovery.ServerUpdated += ServerDiscovery_ServerUpdated;
            serverDiscovery.ServerRemoved += ServerDiscovery_ServerRemoved;
            
            InitializeComponent();
            SetupListView();
            StartDiscovery();
        }
        
        private void InitializeComponent()
        {
            this.listViewServers = new ListView();
            this.buttonRefresh = new Button();
            this.buttonConnect = new Button();
            this.buttonClose = new Button();
            this.buttonAddServer = new Button();
            this.textBoxServerIp = new TextBox();
            this.textBoxServerPort = new TextBox();
            this.labelServerIp = new Label();
            this.labelServerPort = new Label();
            this.labelStatus = new Label();
            this.checkBoxShowPasswordProtected = new CheckBox();
            this.checkBoxShowFull = new CheckBox();
            this.SuspendLayout();
            
            // 
            // listViewServers
            // 
            this.listViewServers.FullRowSelect = true;
            this.listViewServers.GridLines = true;
            this.listViewServers.Location = new System.Drawing.Point(12, 12);
            this.listViewServers.MultiSelect = false;
            this.listViewServers.Name = "listViewServers";
            this.listViewServers.Size = new System.Drawing.Size(700, 350);
            this.listViewServers.TabIndex = 0;
            this.listViewServers.UseCompatibleStateImageBehavior = false;
            this.listViewServers.View = View.Details;
            this.listViewServers.DoubleClick += ListViewServers_DoubleClick;
            this.listViewServers.SelectedIndexChanged += ListViewServers_SelectedIndexChanged;
            
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(12, 370);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(42, 15);
            this.labelStatus.TabIndex = 1;
            this.labelStatus.Text = "Status: Discovering servers...";
            
            // 
            // checkBoxShowPasswordProtected
            // 
            this.checkBoxShowPasswordProtected.AutoSize = true;
            this.checkBoxShowPasswordProtected.Checked = true;
            this.checkBoxShowPasswordProtected.CheckState = CheckState.Checked;
            this.checkBoxShowPasswordProtected.Location = new System.Drawing.Point(12, 395);
            this.checkBoxShowPasswordProtected.Name = "checkBoxShowPasswordProtected";
            this.checkBoxShowPasswordProtected.Size = new System.Drawing.Size(150, 19);
            this.checkBoxShowPasswordProtected.TabIndex = 2;
            this.checkBoxShowPasswordProtected.Text = "Show Password Protected";
            this.checkBoxShowPasswordProtected.CheckedChanged += CheckBoxShowPasswordProtected_CheckedChanged;
            
            // 
            // checkBoxShowFull
            // 
            this.checkBoxShowFull.AutoSize = true;
            this.checkBoxShowFull.Checked = true;
            this.checkBoxShowFull.CheckState = CheckState.Checked;
            this.checkBoxShowFull.Location = new System.Drawing.Point(180, 395);
            this.checkBoxShowFull.Name = "checkBoxShowFull";
            this.checkBoxShowFull.Size = new System.Drawing.Size(100, 19);
            this.checkBoxShowFull.TabIndex = 3;
            this.checkBoxShowFull.Text = "Show Full Servers";
            this.checkBoxShowFull.CheckedChanged += CheckBoxShowFull_CheckedChanged;
            
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(718, 12);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(120, 35);
            this.buttonRefresh.TabIndex = 4;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += ButtonRefresh_Click;
            
            // 
            // buttonConnect
            // 
            this.buttonConnect.Enabled = false;
            this.buttonConnect.Location = new System.Drawing.Point(718, 53);
            this.buttonConnect.Name = "buttonConnect";
            this.buttonConnect.Size = new System.Drawing.Size(120, 35);
            this.buttonConnect.TabIndex = 5;
            this.buttonConnect.Text = "Connect";
            this.buttonConnect.UseVisualStyleBackColor = true;
            this.buttonConnect.Click += ButtonConnect_Click;
            
            // 
            // buttonAddServer
            // 
            this.buttonAddServer.Location = new System.Drawing.Point(718, 94);
            this.buttonAddServer.Name = "buttonAddServer";
            this.buttonAddServer.Size = new System.Drawing.Size(120, 35);
            this.buttonAddServer.TabIndex = 6;
            this.buttonAddServer.Text = "Add Server";
            this.buttonAddServer.UseVisualStyleBackColor = true;
            this.buttonAddServer.Click += ButtonAddServer_Click;
            
            // 
            // labelServerIp
            // 
            this.labelServerIp.AutoSize = true;
            this.labelServerIp.Location = new System.Drawing.Point(300, 395);
            this.labelServerIp.Name = "labelServerIp";
            this.labelServerIp.Size = new System.Drawing.Size(58, 15);
            this.labelServerIp.TabIndex = 7;
            this.labelServerIp.Text = "IP Address:";
            
            // 
            // textBoxServerIp
            // 
            this.textBoxServerIp.Location = new System.Drawing.Point(364, 392);
            this.textBoxServerIp.Name = "textBoxServerIp";
            this.textBoxServerIp.Size = new System.Drawing.Size(150, 23);
            this.textBoxServerIp.TabIndex = 8;
            
            // 
            // labelServerPort
            // 
            this.labelServerPort.AutoSize = true;
            this.labelServerPort.Location = new System.Drawing.Point(520, 395);
            this.labelServerPort.Name = "labelServerPort";
            this.labelServerPort.Size = new System.Drawing.Size(32, 15);
            this.labelServerPort.TabIndex = 9;
            this.labelServerPort.Text = "Port:";
            
            // 
            // textBoxServerPort
            // 
            this.textBoxServerPort.Location = new System.Drawing.Point(558, 392);
            this.textBoxServerPort.Name = "textBoxServerPort";
            this.textBoxServerPort.Size = new System.Drawing.Size(80, 23);
            this.textBoxServerPort.TabIndex = 10;
            
            // 
            // buttonClose
            // 
            this.buttonClose.Location = new System.Drawing.Point(718, 327);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(120, 35);
            this.buttonClose.TabIndex = 11;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += ButtonClose_Click;
            
            // 
            // ServerBrowserDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(850, 425);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.textBoxServerPort);
            this.Controls.Add(this.labelServerPort);
            this.Controls.Add(this.textBoxServerIp);
            this.Controls.Add(this.labelServerIp);
            this.Controls.Add(this.buttonAddServer);
            this.Controls.Add(this.buttonConnect);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.checkBoxShowFull);
            this.Controls.Add(this.checkBoxShowPasswordProtected);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.listViewServers);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ServerBrowserDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Server Browser";
            this.FormClosing += ServerBrowserDialog_FormClosing;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void SetupListView()
        {
            listViewServers.Columns.Add("Server Name", 150);
            listViewServers.Columns.Add("IP Address", 120);
            listViewServers.Columns.Add("Port", 60);
            listViewServers.Columns.Add("Map", 120);
            listViewServers.Columns.Add("Players", 80);
            listViewServers.Columns.Add("Gamemode", 100);
            listViewServers.Columns.Add("Password", 70);
            listViewServers.Columns.Add("Ping", 60);
        }
        
        private void StartDiscovery()
        {
            serverDiscovery.StartDiscovery();
            UpdateStatus("Discovering servers on local network...");
        }
        
        private void ServerDiscovery_ServerDiscovered(object? sender, ServerInfo serverInfo)
        {
            if (InvokeRequired)
            {
                if (!IsDisposed && !Disposing)
                {
                    try
                    {
                        Invoke(new Action<object?, ServerInfo>(ServerDiscovery_ServerDiscovered), sender, serverInfo);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Form was disposed, ignore
                    }
                }
                return;
            }
            
            if (IsDisposed || Disposing)
                return;
            
            string key = $"{serverInfo.IpAddress}:{serverInfo.Port}";
            servers[key] = serverInfo;
            
            // Ping the server to get latency
            Task.Run(async () =>
            {
                try
                {
                    var pingedServer = await serverDiscovery.QueryServer(serverInfo.IpAddress, serverInfo.Port);
                    if (pingedServer != null)
                    {
                        if (InvokeRequired && !IsDisposed && !Disposing)
                        {
                            try
                            {
                                Invoke(new Action(() =>
                                {
                                    if (!IsDisposed && !Disposing && servers.ContainsKey(key))
                                    {
                                        servers[key].Ping = pingedServer.Ping;
                                        servers[key].PlayerCount = pingedServer.PlayerCount;
                                        servers[key].Map = pingedServer.Map;
                                        RefreshServerList();
                                    }
                                }));
                            }
                            catch (ObjectDisposedException)
                            {
                                // Form was disposed, ignore
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore ping errors
                }
            });
            
            RefreshServerList();
            UpdateStatus($"Found {servers.Count} server(s)");
        }
        
        private void ServerDiscovery_ServerUpdated(object? sender, ServerInfo serverInfo)
        {
            if (InvokeRequired)
            {
                if (!IsDisposed && !Disposing)
                {
                    try
                    {
                        Invoke(new Action<object?, ServerInfo>(ServerDiscovery_ServerUpdated), sender, serverInfo);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Form was disposed, ignore
                    }
                }
                return;
            }
            
            if (IsDisposed || Disposing)
                return;
            
            string key = $"{serverInfo.IpAddress}:{serverInfo.Port}";
            if (servers.ContainsKey(key))
            {
                servers[key] = serverInfo;
                RefreshServerList();
            }
        }
        
        private void ServerDiscovery_ServerRemoved(object? sender, string serverKey)
        {
            if (InvokeRequired)
            {
                if (!IsDisposed && !Disposing)
                {
                    try
                    {
                        Invoke(new Action<object?, string>(ServerDiscovery_ServerRemoved), sender, serverKey);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Form was disposed, ignore
                    }
                }
                return;
            }
            
            if (IsDisposed || Disposing)
                return;
            
            servers.Remove(serverKey);
            RefreshServerList();
            UpdateStatus($"Found {servers.Count} server(s)");
        }
        
        private void RefreshServerList()
        {
            if (IsDisposed || Disposing || listViewServers == null || 
                checkBoxShowPasswordProtected == null || checkBoxShowFull == null)
                return;
                
            try
            {
                listViewServers.Items.Clear();
                
                var filteredServers = servers.Values.Where(s =>
                    (checkBoxShowPasswordProtected.Checked || !s.PasswordProtected) &&
                    (checkBoxShowFull.Checked || s.PlayerCount < s.MaxPlayers)
                ).OrderBy(s => s.Ping == -1 ? int.MaxValue : s.Ping).ThenBy(s => s.ServerName);
                
                foreach (var server in filteredServers)
                {
                    var item = new ListViewItem(server.DisplayName);
                    item.Tag = server;
                    item.SubItems.Add(server.IpAddress);
                    item.SubItems.Add(server.Port.ToString());
                    item.SubItems.Add(server.Map);
                    item.SubItems.Add(server.PlayerCountDisplay);
                    item.SubItems.Add(server.Gamemode);
                    item.SubItems.Add(server.PasswordProtected ? "Yes" : "No");
                    item.SubItems.Add(server.Ping == -1 ? "?" : server.Ping + "ms");
                    
                    listViewServers.Items.Add(item);
                }
            }
            catch (ObjectDisposedException)
            {
                // Form was disposed, ignore
            }
        }
        
        private void UpdateStatus(string message)
        {
            if (IsDisposed || Disposing || labelStatus == null)
                return;
                
            try
            {
                labelStatus.Text = "Status: " + message;
            }
            catch (ObjectDisposedException)
            {
                // Form was disposed, ignore
            }
        }
        
        private void ListViewServers_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing || listViewServers == null || buttonConnect == null)
                return;
                
            buttonConnect.Enabled = listViewServers.SelectedItems.Count > 0;
        }
        
        private void ListViewServers_DoubleClick(object? sender, EventArgs e)
        {
            if (listViewServers.SelectedItems.Count > 0)
            {
                ConnectToSelectedServer();
            }
        }
        
        private void ButtonConnect_Click(object? sender, EventArgs e)
        {
            ConnectToSelectedServer();
        }
        
        private void ConnectToSelectedServer()
        {
            if (listViewServers.SelectedItems.Count == 0)
                return;
                
            var selectedItem = listViewServers.SelectedItems[0];
            var serverInfo = selectedItem.Tag as ServerInfo;
            
            if (serverInfo == null)
                return;
            
            string password = "";
            
            // If password protected, prompt for password
            if (serverInfo.PasswordProtected)
            {
                using (var passwordDialog = new ServerPasswordDialog())
                {
                    if (passwordDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        password = passwordDialog.Password;
                    }
                    else
                    {
                        return; // User cancelled password entry
                    }
                }
            }
            
            // Set client connection using the public method
            if (parentForm != null)
            {
                parentForm.SetClientConnection(serverInfo.IpAddress, serverInfo.Port, password);
                parentForm.LogToOutput(parentForm.GetChannel(), $"Connecting to server {serverInfo.DisplayName} ({serverInfo.IpAddress}:{serverInfo.Port})...");
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        
        private void ButtonRefresh_Click(object? sender, EventArgs e)
        {
            UpdateStatus("Refreshing server list...");
            servers.Clear();
            listViewServers.Items.Clear();
            
            // Restart discovery
            serverDiscovery.StopDiscovery();
            Task.Delay(500).ContinueWith(_ =>
            {
                if (IsDisposed || Disposing)
                    return;
                    
                if (InvokeRequired)
                {
                    try
                    {
                        Invoke(new Action(() => 
                        {
                            if (!IsDisposed && !Disposing)
                            {
                                StartDiscovery();
                                UpdateStatus("Discovering servers on local network...");
                            }
                        }));
                    }
                    catch (ObjectDisposedException)
                    {
                        // Form was disposed, ignore
                    }
                }
                else
                {
                    if (!IsDisposed && !Disposing)
                    {
                        StartDiscovery();
                        UpdateStatus("Discovering servers on local network...");
                    }
                }
            });
        }
        
        private void ButtonAddServer_Click(object? sender, EventArgs e)
        {
            string ip = textBoxServerIp.Text.Trim();
            string portText = textBoxServerPort.Text.Trim();
            
            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("Please enter an IP address.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            UpdateStatus($"Querying server {ip}:{port}...");
            buttonAddServer.Enabled = false;
            
            Task.Run(async () =>
            {
                try
                {
                    var serverInfo = await serverDiscovery.QueryServer(ip, port);
                    
                    if (InvokeRequired && !IsDisposed && !Disposing)
                    {
                        try
                        {
                            Invoke(new Action(() =>
                            {
                                if (!IsDisposed && !Disposing)
                                {
                                    buttonAddServer.Enabled = true;
                                    if (serverInfo != null)
                                    {
                                        string key = $"{serverInfo.IpAddress}:{serverInfo.Port}";
                                        servers[key] = serverInfo;
                                        RefreshServerList();
                                        UpdateStatus($"Added server {serverInfo.DisplayName}");
                                        textBoxServerIp.Text = "";
                                        textBoxServerPort.Text = "";
                                    }
                                    else
                                    {
                                        UpdateStatus($"Failed to query server {ip}:{port}");
                                        MessageBox.Show($"Could not connect to server {ip}:{port}. Make sure the server is running and the address is correct.", 
                                            "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    }
                                }
                            }));
                        }
                        catch (ObjectDisposedException)
                        {
                            // Form was disposed, ignore
                        }
                    }
                    else if (!InvokeRequired && !IsDisposed && !Disposing)
                    {
                        // Already on UI thread
                        buttonAddServer.Enabled = true;
                        if (serverInfo != null)
                        {
                            string key = $"{serverInfo.IpAddress}:{serverInfo.Port}";
                            servers[key] = serverInfo;
                            RefreshServerList();
                            UpdateStatus($"Added server {serverInfo.DisplayName}");
                            textBoxServerIp.Text = "";
                            textBoxServerPort.Text = "";
                        }
                        else
                        {
                            UpdateStatus($"Failed to query server {ip}:{port}");
                            MessageBox.Show($"Could not connect to server {ip}:{port}. Make sure the server is running and the address is correct.", 
                                "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                catch (Exception)
                {
                    // Re-enable button even if query fails
                    if (InvokeRequired && !IsDisposed && !Disposing)
                    {
                        try
                        {
                            Invoke(new Action(() => buttonAddServer.Enabled = true));
                        }
                        catch (ObjectDisposedException)
                        {
                            // Form was disposed, ignore
                        }
                    }
                    else if (!InvokeRequired && !IsDisposed && !Disposing)
                    {
                        buttonAddServer.Enabled = true;
                    }
                }
            });
        }
        
        private void CheckBoxShowPasswordProtected_CheckedChanged(object? sender, EventArgs e)
        {
            RefreshServerList();
        }
        
        private void CheckBoxShowFull_CheckedChanged(object? sender, EventArgs e)
        {
            RefreshServerList();
        }
        
        private void ServerBrowserDialog_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks
            serverDiscovery.ServerDiscovered -= ServerDiscovery_ServerDiscovered;
            serverDiscovery.ServerUpdated -= ServerDiscovery_ServerUpdated;
            serverDiscovery.ServerRemoved -= ServerDiscovery_ServerRemoved;
            
            // Don't stop discovery - it's a singleton and may be needed by other dialogs or the main form
            // Discovery will continue running in the background
        }
        
        private void ButtonClose_Click(object? sender, EventArgs e)
        {
            this.Close();
        }
    }
}
