using HLVRCoOpModCore;
using System.Reflection;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace HLVRCoOpMod
{
    public partial class UserInterface : Form
    {
        public ClientProgram clientProgram;
        private bool started = false;
        private DateTime startTime;
        private readonly Channel channel = new("HLVRCOOP", "HLVR Co-Op", Color.Purple);
        private readonly Random random = new();
        private Type gamemodeType = typeof(CoreGamemode);
        private readonly List<AddonInitializer> addons = new();
        private readonly List<Type> plugins = new();
        List<string>? currentAddons;
        private ToolStripComboBox? playerBodyPreferenceComboBox;
        public UserInterface()
        {
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font(Font.Name, 8.25f * 96f / CreateGraphics().DpiX, Font.Style, Font.Unit, Font.GdiCharSet, Font.GdiVerticalFont);
            InitializeComponent();
            // Status bar
            toolStripStatusLabelConnection.Text = "Inactive";
            toolStripStatusLabelConnection.ForeColor = Color.Red;
            toolStripStatusLabelVersion.Text = "Version: v" + ProductVersion.ToString();
            toolStripStatusLabelVconsolePort.Text = "VConsole Port: " + Settings.Default.VconsolePort;
            toolStripStatusLabelVconsoleProtocol.Text = "VConsole Protocol: " + Settings.Default.VconsoleProtocol;
            // Client settings
            checkBoxClientEnabled.Checked = Settings.Default.ClientEnabled;
            clientPrintVConsoleToolStripMenuItem.Checked = Settings.Default.ClientPrintVconsole;
            textBoxClientIpAddress.Text = Settings.Default.ClientIpAddress;
            textBoxClientPassword.Text = Settings.Default.ClientPassword;
            textBoxClientUsername.Text = Settings.Default.ClientUsername;
            numericUpDownClientPort.Value = Settings.Default.ClientPort;
            // Server settings
            checkBoxServerEnabled.Checked = Settings.Default.ServerEnabled;
            textBoxServerPassword.Text = Settings.Default.ServerPassword;
            textBoxServerMap.Text = Settings.Default.ServerMap;
            numericUpDownServerPort.Value = Settings.Default.ServerPort;
            serverDisableUserVConsoleInputToolStripMenuItem.Checked = Settings.Default.ServerDisableUserVconsoleInput;
            // Options
            saveOptionsOnExitToolStripMenuItem.Checked = Settings.Default.SaveOptionsOnExit;
            currentAddons = JsonConvert.DeserializeObject<List<string>>(Settings.Default.CurrentAddons);
            alwaysOnTopToolStripMenuItem.Checked = Settings.Default.AlwaysOnTop;
            TopMost = Settings.Default.AlwaysOnTop;
            // Player Body Preference (will be set when plugin menu is created)
            Application.ApplicationExit += new EventHandler(closeToolStripMenuItem_Click);
            // Load extensions
            string? appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (appDir != null)
            {
                // Load libraries
                string libraryFolderPath = Path.Combine(appDir, "libraries");
                Directory.CreateDirectory(libraryFolderPath);
                string[] libraryFiles = Directory.GetFiles(libraryFolderPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (string dll in libraryFiles)
                {
                    Assembly.LoadFrom(Path.Combine(appDir, dll));
                }
                toolStripStatusLabelLibraries.Text = "Libraries Loaded: " + libraryFiles.Length;
                // Populate gamemode menu
                string gamemodesFolderPath = Path.Combine(appDir, "gamemodes");
                Directory.CreateDirectory(gamemodesFolderPath);
                string[] files = Directory.GetFiles(gamemodesFolderPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (string dll in files)
                {
                    Assembly gamemodeAssembly = Assembly.LoadFrom(Path.Combine(appDir, dll));
                    foreach (Type type in gamemodeAssembly.GetTypes())
                    {
                        if (type.GetInterface("IGamemode") != null)
                        {
                            IGamemode? newGamemode = (IGamemode?)Activator.CreateInstance(type);
                            if (newGamemode != null)
                            {
                                if (newGamemode.Name != null)
                                {
                                    ToolStripMenuItem item = new(newGamemode.Name)
                                    {
                                        ToolTipText = newGamemode.Author + "\n" + newGamemode.Description
                                    };
                                    item.Click += SelectAddon;
                                    gamemodeToolStripMenuItem.DropDownItems.Add(item);
                                    addons.Add(new AddonInitializer(newGamemode.Name, type, AddonType.Gamemode));
                                }
                            }
                        }
                    }
                }
                // Populate plugins menu
                string pluginsFolderPath = Path.Combine(appDir, "plugins");
                Directory.CreateDirectory(pluginsFolderPath);
                string[] pluginsFiles = Directory.GetFiles(pluginsFolderPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (string dll in pluginsFiles)
                {
                    Assembly pluginAssembly = Assembly.LoadFrom(Path.Combine(appDir, dll));
                    foreach (Type type in pluginAssembly.GetTypes())
                    {
                        if (type.GetInterface("IPlugin") != null)
                        {
                            IPlugin? newPlugin = (IPlugin?)Activator.CreateInstance(type);

                            if (newPlugin != null)
                            {
                                if (newPlugin.Name != null)
                                {
                                    ToolStripMenuItem item = new(newPlugin.Name)
                                    {
                                        ToolTipText = newPlugin.Author + "\n" + newPlugin.Description
                                    };
                                    item.Click += SelectAddon;
                                    pluginsToolStripMenuItem.DropDownItems.Add(item);
                                    addons.Add(new AddonInitializer(newPlugin.Name, type, AddonType.Plugin));
                                    
                                    // Add playermodel dropdown for Player Body Sync plugin
                                    if (newPlugin.Name == "Player Body Sync")
                                    {
                                        AddPlayerBodySyncSubmenu(item);
                                    }
                                }
                            }
                        }
                    }
                }
                // Automatically select saved addons
                foreach(ToolStripItem pluginItem in pluginsToolStripMenuItem.DropDownItems)
                {
                    if (currentAddons.Contains(pluginItem.Text))
                        SelectAddon(pluginItem, new EventArgs());
                }
                foreach (ToolStripItem gamemodeItem in gamemodeToolStripMenuItem.DropDownItems)
                {
                    if (currentAddons.Contains(gamemodeItem.Text))
                    {
                        SelectAddon(gamemodeItem, new EventArgs());
                    }
                }
            }
            // Programs
            clientProgram = new ClientProgram(this);
            // Lua version of initializing
            LuaEnvironment.instance.Handle(PluginHandleType.UserInterface_Initialized);
        }
        public void UseUriScheme(Uri uri)
        {
            string[] args = uri.ToString().Replace("hlvrcoop://", "").Split("/");
            if (args.Length > 0)
                UseArgs(args);
        }
        public void UseArgs(string[] args)
        {
            if (args.Length >= 2)
            {
                switch (args[0].ToLower())
                {
                    case "connect":
                        string[] ipPort = args[1].Split(":");
                        Settings.Default.ClientEnabled = true;
                        checkBoxClientEnabled.Checked = true;
                        Settings.Default.ServerEnabled = false;
                        checkBoxServerEnabled.Checked = false;
                        Settings.Default.ClientIpAddress = ipPort[0];
                        textBoxClientIpAddress.Text = ipPort[0];
                        if (ipPort.Length > 1)
                        {
                            Settings.Default.ClientPort = int.Parse(ipPort[1]);
                            numericUpDownClientPort.Value = int.Parse(ipPort[1]);
                        }
                        if(args.Length >= 3)
                        {
                            Settings.Default.ClientPassword = args[2];
                            textBoxClientPassword.Text = args[2];
                        }
                        else
                        {
                            Settings.Default.ClientPassword = "";
                            textBoxClientPassword.Text = "";
                        }
                        break;
                }
            }
        }
        private void Save()
        {
            Settings.Default.CurrentAddons = JsonConvert.SerializeObject(currentAddons);
            Settings.Default.Save();
        }
        private void Exit(object? sender, EventArgs e)
        {
            Save();          
        }

        public void Start()
        {
            if(!started && (checkBoxServerEnabled.Checked || checkBoxClientEnabled.Checked))
            {
                // Silent listen server toggle
                if (checkBoxClientEnabled.Checked && textBoxClientIpAddress.Text == "localhost")
                    checkBoxServerEnabled.Checked = true;
                else if (checkBoxClientEnabled.Checked)
                    checkBoxServerEnabled.Checked = false;
                PluginHandler.Handle(plugins, PluginHandleType.UserInterface_PreStart, this);
                LuaEnvironment.instance.Handle(PluginHandleType.UserInterface_PreStart, this);
                started = true;
                buttonStart.Text = "Stop";
                SetStatus(Color.Green, "Active");
                startTime = DateTime.Now;
                LogToOutput(channel, "Starting at " + startTime.ToString(@"hh\:mm\:ss"));
                if (checkBoxServerEnabled.Checked)
                    ServerProgram.instance.Start(gamemodeType, plugins);
                if (checkBoxClientEnabled.Checked)
                {
                    clientProgram.Start(plugins);
                }
                PluginHandler.Handle(plugins, PluginHandleType.UserInterface_PostStart, this);
                LuaEnvironment.instance.Handle(PluginHandleType.UserInterface_PostStart, this);
            }
            else
            {
                PluginHandler.Handle(plugins, PluginHandleType.UserInterface_PreClose, this);
                LuaEnvironment.instance.Handle(PluginHandleType.UserInterface_PreClose, this);
                buttonStart.Text = "Start";
                SetStatus(Color.Red, "Inactive");
                ServerProgram.instance.Close();
                clientProgram.Close();
                started = false;
                DateTime endTime = DateTime.Now;
                LogToOutput(channel, "Stopped at", endTime.ToString(@"hh\:mm\:ss") + ", active for", (endTime - startTime).ToString(@"hh\:mm\:ss"));
                PluginHandler.Handle(plugins, PluginHandleType.UserInterface_PostClose, this);
                LuaEnvironment.instance.Handle(PluginHandleType.UserInterface_PostClose, this);
                //Save();
                //Application.Restart();
            }
            gamemodeToolStripMenuItem.Enabled = !started;
            pluginsToolStripMenuItem.Enabled = !started;
        }

        public void SelectAddon(object? sender, EventArgs e)
        {
            if (sender != null)
            {
                ToolStripMenuItem item = (ToolStripMenuItem)sender;
                foreach (AddonInitializer initializer in addons)
                {
                    if (initializer.Name == item.Text)
                    {
                        switch (initializer.AddonType)
                        {
                            case AddonType.Gamemode:
                                foreach (ToolStripMenuItem menuItem in gamemodeToolStripMenuItem.DropDownItems)
                                {
                                    menuItem.Checked = false;
                                    if (currentAddons.Contains(menuItem.Text))
                                        currentAddons.Remove(menuItem.Text);
                                }
                                gamemodeType = initializer.Type;
                                item.Checked = true;
                                if (!currentAddons.Contains(item.Text))
                                    currentAddons.Add(item.Text);
                                //LogToOutput(channel, "Gamemode changed to", item.Text);
                                break;
                            case AddonType.Plugin:
                                item.Checked = !item.Checked;
                                if (item.Checked)
                                {
                                    plugins.Add(initializer.Type);
                                    if (!currentAddons.Contains(item.Text))
                                        currentAddons.Add(item.Text);
                                    PluginHandler.Handle(initializer.Type, PluginHandleType.UserInterface_Initialized);
                                    //LogToOutput(channel, "Plugin", item.Text, "added");
                                }
                                else
                                {
                                    PluginHandler.Handle(initializer.Type, PluginHandleType.UserInterface_Exit);
                                    plugins.Remove(initializer.Type);
                                    if (currentAddons.Contains(item.Text))
                                        currentAddons.Remove(item.Text);
                                    //LogToOutput(channel, "Plugin", item.Text, "removed");
                                }
                                break;
                        }
                        break;
                    }
                }
            }
        }

        public void LogToOutputGeneric(params object[] text)
        {
            richTextBoxOutput.Select(richTextBoxOutput.TextLength, 0);
            richTextBoxOutput.SelectionColor = Color.Black;
            richTextBoxOutput.AppendText("\n" + string.Join(" ", text));
        }

        public void LogToOutput(Channel channel, params object[] text)
        {
            /*
            if(richTextBoxOutput.Lines.Length+1 >= 64)
            {
                List<string> lines = richTextBoxOutput.Lines.ToList();
                lines.RemoveAt(0);
                richTextBoxOutput.Lines = lines.ToArray();
            }
            */
            richTextBoxOutput.Select(richTextBoxOutput.TextLength, 0);
            richTextBoxOutput.SelectionColor = channel.GetColor();
            richTextBoxOutput.AppendText("\n" + channel + string.Join(" ", text));
            string friendlyName = channel.GetFriendlyName();
            if (!tabControlBottom.TabPages.ContainsKey("tabPage" + friendlyName.Replace(" ", "")))
            {
                TabPage newTab = new(friendlyName)
                {
                    Name = "tabPage" + friendlyName.Replace(" ", "")
                };
                RichTextBox textBox = new()
                {
                    TabIndex = 0,
                    HideSelection = false,
                    ReadOnly = true,
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    Size = new(752, 131),
                    Location = new(6, 6)
                };
                textBox.Select(textBox.TextLength, 0);
                textBox.SelectionColor = channel.GetColor();
                textBox.AppendText(channel + string.Join(" ", text));
                newTab.Controls.Add(textBox);
                tabControlBottom.Controls.Add(newTab);
            }
            else
            {
                RichTextBox textBox = ((RichTextBox)tabControlBottom.TabPages[tabControlBottom.TabPages.IndexOfKey("tabPage" + friendlyName.Replace(" ", ""))].Controls[0]);
                textBox.Select(textBox.TextLength, 0);
                textBox.SelectionColor = channel.GetColor();
                textBox.AppendText("\n" + channel + string.Join(" ", text));
            }
        }

        public void SetStatus(Color color, params object[] text)
        {
            toolStripStatusLabelConnection.ForeColor = color;
            toolStripStatusLabelConnection.Text = string.Join(" ", text);
        }
        public void SetStatus(Channel channel, params object[] text)
        {
            toolStripStatusLabelConnection.ForeColor = channel.GetColor();
            toolStripStatusLabelConnection.Text = channel + string.Join(" ", text);
        }

        private void DiscordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://discord.gg/X6Dp95WRR4") { UseShellExecute = true });
        }

        private void GitHubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/dayflaree/hlvr-coop") { UseShellExecute = true });
        }

        private void CheckBoxClientEnabled_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ClientEnabled = checkBoxClientEnabled.Checked;
        }

        private void ButtonCommandType_Click(object sender, EventArgs e)
        {
            buttonCommandType.Text = buttonCommandType.Text switch
            {
                "Chat" => "Command",
                "Command" => "VConsole",
                "VConsole" => "Server",
                _ => "Chat",
            };
        }

        private void ClientPrintVConsoleToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ClientPrintVconsole = clientPrintVConsoleToolStripMenuItem.Checked;
        }

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            Start();
        }
        private void TextBoxInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                string prefix = "";
                switch (buttonCommandType.Text)
                {
                    case "Command":
                        prefix = "/";
                        break;
                    case "VConsole":
                        prefix = "/vc ";
                        break;
                    case "Server":
                        ServerProgram.instance.Command(textBoxInput.Text.Split(" ").ToList());
                        LogToOutputGeneric("> " + textBoxInput.Text);
                        textBoxInput.Text = "";
                        return;
                }
                LogToOutputGeneric("> " + prefix + textBoxInput.Text);
                clientProgram.Chat(prefix + textBoxInput.Text);
                textBoxInput.Text = "";
            }
        }

        private void TextBoxClientIpAddress_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.ClientIpAddress = textBoxClientIpAddress.Text;
        }

        private void TextBoxClientPassword_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.ClientPassword = textBoxClientPassword.Text;
        }
        private void TextBoxClientUsername_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.ClientUsername = textBoxClientUsername.Text;
        }
        private void NumericUpDownClientPort_ValueChanged(object sender, EventArgs e)
        {
            Settings.Default.ClientPort = (int)numericUpDownClientPort.Value;
        }
        private void NumericUpDownServerPort_ValueChanged(object sender, EventArgs e)
        {
            Settings.Default.ServerPort = (int)numericUpDownServerPort.Value;
        }
        private void TextBoxServerPassword_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.ServerPassword = textBoxServerPassword.Text;
        }
        private void TextBoxServerMap_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.ServerMap = textBoxServerMap.Text;
        }
        private void SaveOptionsOnExitToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.SaveOptionsOnExit = saveOptionsOnExitToolStripMenuItem.Checked;
        }

        private void CheckBoxServerEnabled_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ServerEnabled = checkBoxServerEnabled.Checked;
        }

        private void ServerDisableUserVConsoleInputToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ServerDisableUserVconsoleInput = serverDisableUserVConsoleInputToolStripMenuItem.Checked;
        }

        private void SaveOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Save();
            LogToOutput(channel, "Options saved");
        }


        private void ButtonServerChangeMap_Click(object sender, EventArgs e)
        {
            ServerProgram.instance.ChangeMap(Settings.Default.ServerMap);
        }

        private void buttonServerVconsoleSend_Click(object sender, EventArgs e)
        {
            string command = textBoxServerVconsole.Text;
            ServerProgram.instance.GlobalVConsole(command);
            textBoxServerVconsole.Text = "";
        }

        private void textBoxServerVconsole_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                buttonServerVconsoleSend_Click(sender, e);
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Save();
            Application.Exit();
        }

        private void alwaysOnTopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            alwaysOnTopToolStripMenuItem.Checked = !alwaysOnTopToolStripMenuItem.Checked;
            Settings.Default.AlwaysOnTop = alwaysOnTopToolStripMenuItem.Checked;
            TopMost = alwaysOnTopToolStripMenuItem.Checked;
        }

        private void textBoxServerMap_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                ButtonServerChangeMap_Click(sender, e);
            }
        }

        private void koFiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.paypal.com/paypalme/EStaley944") { UseShellExecute = true });
        }

        /// <summary>
        /// Adds a playermodel selection dropdown submenu to the Player Body Sync plugin menu item
        /// </summary>
        private void AddPlayerBodySyncSubmenu(ToolStripMenuItem pluginMenuItem)
        {
            // Create a separator
            ToolStripSeparator separator = new();
            pluginMenuItem.DropDownItems.Add(separator);
            
            // Create label
            ToolStripLabel label = new("Playermodel:");
            pluginMenuItem.DropDownItems.Add(label);
            
            // Create ComboBox
            playerBodyPreferenceComboBox = new ToolStripComboBox
            {
                Name = "playerBodyPreferenceComboBox",
                Width = 150
            };
            playerBodyPreferenceComboBox.Items.AddRange(new object[] { "Auto", "Gordon", "Alyx" });
            
            // Load saved preference (0=Auto, 1=Gordon, 2=Alyx)
            int savedPreference = Settings.Default.PlayerBodyPreference;
            if (savedPreference >= 0 && savedPreference <= 2)
            {
                playerBodyPreferenceComboBox.SelectedIndex = savedPreference;
            }
            else
            {
                playerBodyPreferenceComboBox.SelectedIndex = 0; // Default to Auto
            }
            
            // Handle selection change
            playerBodyPreferenceComboBox.SelectedIndexChanged += PlayerBodyPreferenceComboBox_SelectedIndexChanged;
            
            pluginMenuItem.DropDownItems.Add(playerBodyPreferenceComboBox);
        }

        /// <summary>
        /// Handles when the player body preference dropdown selection changes
        /// </summary>
        private void PlayerBodyPreferenceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (playerBodyPreferenceComboBox == null) return;
            
            int preference = playerBodyPreferenceComboBox.SelectedIndex;
            
            // Save preference to settings
            Settings.Default.PlayerBodyPreference = preference;
            Settings.Default.Save();
            
            // Send command to game via VConsole (if client is connected)
            if (clientProgram != null && started && checkBoxClientEnabled.Checked)
            {
                clientProgram.SendVConsoleCommand($"convar_set player_body_preference {preference}");
            }
        }
    }
}