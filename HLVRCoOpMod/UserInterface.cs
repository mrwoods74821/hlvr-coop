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
using System.Text.RegularExpressions;
using System.Globalization;
using System.Net;

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

        // Log search and filtering
        private readonly List<LogEntry> logEntries = new();
        private const int MAX_LOG_ENTRIES = 10000;
        private TextBox? searchBox;
        private Button? searchButton;
        private Button? clearSearchButton;
        private CheckBox? caseSensitiveCheckBox;
        private CheckBox? regexCheckBox;
        private Button? clearLogsButton;
        private Button? exportLogsButton;
        private RichTextBox? currentLogTextBox; // Currently displayed RichTextBox
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
                                }
                            }
                        }
                    }
                }
                // Automatically select saved addons
                if (currentAddons != null)
                {
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
            }
            // Programs
            clientProgram = new ClientProgram(this);
            // Lua version of initializing
            LuaEnvironment.instance.Handle(PluginHandleType.UserInterface_Initialized);
            
            // Initialize log search UI
            InitializeLogSearchUI();
            currentLogTextBox = richTextBoxOutput;
            
            // Add preset menu items
            InitializePresetMenuItems();
        }

        private void InitializePresetMenuItems()
        {
            // Add separator
            ToolStripSeparator separator1 = new ToolStripSeparator();
            fileToolStripMenuItem.DropDownItems.Add(separator1);

            // Save Preset
            ToolStripMenuItem savePresetItem = new ToolStripMenuItem("Save Preset...");
            savePresetItem.Click += SavePresetToolStripMenuItem_Click;
            fileToolStripMenuItem.DropDownItems.Add(savePresetItem);

            // Load Preset
            ToolStripMenuItem loadPresetItem = new ToolStripMenuItem("Load Preset...");
            loadPresetItem.Click += LoadPresetToolStripMenuItem_Click;
            fileToolStripMenuItem.DropDownItems.Add(loadPresetItem);

            // Manage Presets
            ToolStripMenuItem managePresetsItem = new ToolStripMenuItem("Manage Presets...");
            managePresetsItem.Click += ManagePresetsToolStripMenuItem_Click;
            fileToolStripMenuItem.DropDownItems.Add(managePresetsItem);

            // Separator
            ToolStripSeparator separator2 = new ToolStripSeparator();
            fileToolStripMenuItem.DropDownItems.Add(separator2);

            // Export Configuration
            ToolStripMenuItem exportConfigItem = new ToolStripMenuItem("Export Configuration...");
            exportConfigItem.Click += ExportConfigurationToolStripMenuItem_Click;
            fileToolStripMenuItem.DropDownItems.Add(exportConfigItem);

            // Import Configuration
            ToolStripMenuItem importConfigItem = new ToolStripMenuItem("Import Configuration...");
            importConfigItem.Click += ImportConfigurationToolStripMenuItem_Click;
            fileToolStripMenuItem.DropDownItems.Add(importConfigItem);
        }

        private void InitializeLogSearchUI()
        {
            // Create search panel above the output tab control
            Panel searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5)
            };

            // Search textbox
            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 5, 0)
            };
            // Set placeholder text via GotFocus/LostFocus events
            searchBox.GotFocus += (s, e) =>
            {
                if (searchBox.Text == "Search logs...")
                    searchBox.Text = "";
            };
            searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBox.Text))
                    searchBox.Text = "Search logs...";
            };
            searchBox.Text = "Search logs...";
            searchBox.ForeColor = Color.Gray;
            searchBox.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Return)
                {
                    SearchLogs();
                    e.Handled = true;
                }
            };

            // Case sensitive checkbox
            caseSensitiveCheckBox = new CheckBox
            {
                Text = "Case",
                AutoSize = true,
                Dock = DockStyle.Right,
                Margin = new Padding(5, 5, 0, 5)
            };

            // Regex checkbox
            regexCheckBox = new CheckBox
            {
                Text = "Regex",
                AutoSize = true,
                Dock = DockStyle.Right,
                Margin = new Padding(5, 5, 0, 5)
            };

            // Clear search button
            clearSearchButton = new Button
            {
                Text = "Clear",
                Dock = DockStyle.Right,
                Width = 60,
                Margin = new Padding(5, 2, 0, 2)
            };
            clearSearchButton.Click += (s, e) =>
            {
                searchBox.Text = "";
                ShowAllLogs();
            };

            // Search button
            searchButton = new Button
            {
                Text = "Search",
                Dock = DockStyle.Right,
                Width = 60,
                Margin = new Padding(5, 2, 0, 2)
            };
            searchButton.Click += (s, e) => SearchLogs();

            // Clear logs button
            clearLogsButton = new Button
            {
                Text = "Clear Logs",
                Dock = DockStyle.Right,
                Width = 80,
                Margin = new Padding(5, 2, 0, 2)
            };
            clearLogsButton.Click += (s, e) => ClearLogs();

            // Export logs button
            exportLogsButton = new Button
            {
                Text = "Export",
                Dock = DockStyle.Right,
                Width = 60,
                Margin = new Padding(5, 2, 0, 2)
            };
            exportLogsButton.Click += (s, e) => ExportLogs();

            // Add controls to panel (right to left order)
            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(caseSensitiveCheckBox);
            searchPanel.Controls.Add(regexCheckBox);
            searchPanel.Controls.Add(clearSearchButton);
            searchPanel.Controls.Add(searchButton);
            searchPanel.Controls.Add(exportLogsButton);
            searchPanel.Controls.Add(clearLogsButton);

            // Add panel above tab control
            // Find the parent container and insert the panel
            Control parent = tabControlBottom.Parent;
            if (parent != null)
            {
                int tabIndex = parent.Controls.IndexOf(tabControlBottom);
                parent.Controls.Add(searchPanel);
                parent.Controls.SetChildIndex(searchPanel, tabIndex);
                searchPanel.BringToFront();
            }

            // Handle tab selection change to update current log textbox
            tabControlBottom.SelectedIndexChanged += (s, e) =>
            {
                if (tabControlBottom.SelectedTab != null && tabControlBottom.SelectedTab.Controls.Count > 0)
                {
                    if (tabControlBottom.SelectedTab.Controls[0] is RichTextBox)
                    {
                        currentLogTextBox = (RichTextBox)tabControlBottom.SelectedTab.Controls[0];
                    }
                }
                else
                {
                    currentLogTextBox = richTextBoxOutput;
                }
            };
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
                        if (ipPort.Length > 0 && !string.IsNullOrWhiteSpace(ipPort[0]))
                        {
                            // Validate IP address format
                            if (IPAddress.TryParse(ipPort[0], out _) || ipPort[0] == "localhost")
                            {
                                Settings.Default.ClientEnabled = true;
                                checkBoxClientEnabled.Checked = true;
                                Settings.Default.ServerEnabled = false;
                                checkBoxServerEnabled.Checked = false;
                                Settings.Default.ClientIpAddress = ipPort[0];
                                textBoxClientIpAddress.Text = ipPort[0];
                                if (ipPort.Length > 1 && int.TryParse(ipPort[1], out int port) && port > 0 && port <= 65535)
                                {
                                    Settings.Default.ClientPort = port;
                                    numericUpDownClientPort.Value = port;
                                }
                            }
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
                SetConnectionStatus("Inactive", Color.Red);
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
            string logText = string.Join(" ", text);
            LogEntry entry = new LogEntry(logText, Color.Black, "", "Generic");
            
            // Add to log entries list
            lock (logEntries)
            {
                logEntries.Add(entry);
                // Trim if too many entries
                if (logEntries.Count > MAX_LOG_ENTRIES)
                {
                    logEntries.RemoveAt(0);
                }
            }

            richTextBoxOutput.Select(richTextBoxOutput.TextLength, 0);
            richTextBoxOutput.SelectionColor = Color.Black;
            richTextBoxOutput.AppendText("\n" + logText);
        }

        public void LogToOutput(Channel channel, params object[] text)
        {
            string logText = channel + string.Join(" ", text);
            LogEntry entry = new LogEntry(logText, channel.GetColor(), channel.ToString(), channel.GetFriendlyName());
            
            // Add to log entries list
            lock (logEntries)
            {
                logEntries.Add(entry);
                // Trim if too many entries
                if (logEntries.Count > MAX_LOG_ENTRIES)
                {
                    logEntries.RemoveAt(0);
                }
            }

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
            richTextBoxOutput.AppendText("\n" + logText);
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
                textBox.AppendText(logText);
                newTab.Controls.Add(textBox);
                tabControlBottom.Controls.Add(newTab);
            }
            else
            {
                RichTextBox textBox = ((RichTextBox)tabControlBottom.TabPages[tabControlBottom.TabPages.IndexOfKey("tabPage" + friendlyName.Replace(" ", ""))].Controls[0]);
                textBox.Select(textBox.TextLength, 0);
                textBox.SelectionColor = channel.GetColor();
                textBox.AppendText("\n" + logText);
            }
        }

        private void SearchLogs()
        {
            if (searchBox == null || currentLogTextBox == null)
                return;

            string searchTerm = searchBox.Text;
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm == "Search logs...")
            {
                ShowAllLogs();
                return;
            }

            bool caseSensitive = caseSensitiveCheckBox?.Checked ?? false;
            bool useRegex = regexCheckBox?.Checked ?? false;

            // Get current tab's RichTextBox
            RichTextBox targetBox = currentLogTextBox;
            if (tabControlBottom.SelectedTab != null && tabControlBottom.SelectedTab.Controls.Count > 0)
            {
                if (tabControlBottom.SelectedTab.Controls[0] is RichTextBox)
                {
                    targetBox = (RichTextBox)tabControlBottom.SelectedTab.Controls[0];
                }
            }

            // Filter and highlight
            List<LogEntry> filteredEntries;
            lock (logEntries)
            {
                filteredEntries = FilterLogEntries(searchTerm, caseSensitive, useRegex);
            }

            // Rebuild RichTextBox with filtered entries
            targetBox.Clear();
            foreach (var entry in filteredEntries)
            {
                targetBox.Select(targetBox.TextLength, 0);
                targetBox.SelectionColor = entry.Color;
                targetBox.AppendText("\n" + entry.Text);
            }

            // Scroll to top
            targetBox.SelectionStart = 0;
            targetBox.ScrollToCaret();
        }

        private List<LogEntry> FilterLogEntries(string searchTerm, bool caseSensitive, bool useRegex)
        {
            List<LogEntry> results = new List<LogEntry>();

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            Regex? regex = null;

            if (useRegex)
            {
                try
                {
                    RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    regex = new Regex(searchTerm, options);
                }
                catch
                {
                    // Invalid regex, fall back to simple search
                    useRegex = false;
                }
            }

            foreach (var entry in logEntries)
            {
                bool matches = false;
                if (useRegex && regex != null)
                {
                    matches = regex.IsMatch(entry.Text);
                }
                else
                {
                    matches = entry.Text.IndexOf(searchTerm, comparison) >= 0;
                }

                if (matches)
                {
                    results.Add(entry);
                }
            }

            return results;
        }

        private void ShowAllLogs()
        {
            if (currentLogTextBox == null)
                return;

            RichTextBox targetBox = currentLogTextBox;
            if (tabControlBottom.SelectedTab != null && tabControlBottom.SelectedTab.Controls.Count > 0)
            {
                if (tabControlBottom.SelectedTab.Controls[0] is RichTextBox)
                {
                    targetBox = (RichTextBox)tabControlBottom.SelectedTab.Controls[0];
                }
            }

            // Rebuild with all entries
            targetBox.Clear();
            lock (logEntries)
            {
                foreach (var entry in logEntries)
                {
                    targetBox.Select(targetBox.TextLength, 0);
                    targetBox.SelectionColor = entry.Color;
                    targetBox.AppendText("\n" + entry.Text);
                }
            }

            targetBox.SelectionStart = targetBox.TextLength;
            targetBox.ScrollToCaret();
        }

        private void ClearLogs()
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all logs?",
                "Clear Logs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                lock (logEntries)
                {
                    logEntries.Clear();
                }

                richTextBoxOutput.Clear();
                foreach (TabPage tab in tabControlBottom.TabPages)
                {
                    if (tab.Controls.Count > 0 && tab.Controls[0] is RichTextBox)
                    {
                        ((RichTextBox)tab.Controls[0]).Clear();
                    }
                }
            }
        }

        private void ExportLogs()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dialog.FilterIndex = 1;
                dialog.DefaultExt = "txt";
                dialog.FileName = $"hlvrcoop_logs_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        List<LogEntry> entriesToExport;
                        lock (logEntries)
                        {
                            entriesToExport = new List<LogEntry>(logEntries);
                        }

                        string extension = Path.GetExtension(dialog.FileName).ToLower();
                        if (extension == ".csv")
                        {
                            ExportLogsToCsv(entriesToExport, dialog.FileName);
                        }
                        else if (extension == ".json")
                        {
                            ExportLogsToJson(entriesToExport, dialog.FileName);
                        }
                        else
                        {
                            ExportLogsToText(entriesToExport, dialog.FileName);
                        }

                        MessageBox.Show("Logs exported successfully!", "Export Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("UserInterface.ExportLogs", ex);
                        MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportLogsToText(List<LogEntry> entries, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var entry in entries)
                {
                    writer.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.Text}");
                }
            }
        }

        private void ExportLogsToCsv(List<LogEntry> entries, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Timestamp,Channel,FriendlyChannelName,Text");
                foreach (var entry in entries)
                {
                    string text = entry.Text.Replace("\"", "\"\""); // Escape quotes
                    writer.WriteLine($"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{entry.Channel}\",\"{entry.FriendlyChannelName}\",\"{text}\"");
                }
            }
        }

        private void ExportLogsToJson(List<LogEntry> entries, string filePath)
        {
            var exportData = entries.Select(e => new
            {
                timestamp = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                channel = e.Channel,
                friendlyChannelName = e.FriendlyChannelName,
                text = e.Text,
                color = ColorTranslator.ToHtml(e.Color)
            }).ToList();

            string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(filePath, json);
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

        /// <summary>
        /// Sets detailed connection status with separate indicators for WebSocket and VConsole.
        /// </summary>
        /// <param name="websocketStatus">WebSocket connection status (e.g., "Connected", "Disconnected", "Reconnecting")</param>
        /// <param name="vconsoleStatus">VConsole connection status (e.g., "Connected", "Disconnected", "Reconnecting")</param>
        /// <param name="color">Status color (Green=good, Yellow=warning, Red=error)</param>
        public void SetConnectionStatus(string websocketStatus, string vconsoleStatus, Color color)
        {
            string statusText = "";
            if (!string.IsNullOrEmpty(websocketStatus))
            {
                statusText += "WebSocket: " + websocketStatus;
            }
            if (!string.IsNullOrEmpty(vconsoleStatus))
            {
                if (!string.IsNullOrEmpty(statusText))
                    statusText += " | ";
                statusText += "VConsole: " + vconsoleStatus;
            }
            if (string.IsNullOrEmpty(statusText))
            {
                statusText = "Inactive";
                color = Color.Red;
            }
            toolStripStatusLabelConnection.ForeColor = color;
            toolStripStatusLabelConnection.Text = statusText;
        }

        /// <summary>
        /// Sets connection status with a single message (backward compatibility).
        /// </summary>
        /// <param name="status">Status message</param>
        /// <param name="color">Status color</param>
        public void SetConnectionStatus(string status, Color color)
        {
            toolStripStatusLabelConnection.ForeColor = color;
            toolStripStatusLabelConnection.Text = status;
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

        private void SavePresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new SavePresetDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var preset = ConfigurationPreset.FromCurrentSettings(dialog.PresetName, dialog.Category);
                    if (PresetManager.SavePreset(preset))
                    {
                        LogToOutput(channel, $"Preset '{preset.Name}' saved successfully");
                        MessageBox.Show($"Preset '{preset.Name}' saved successfully!", "Save Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to save preset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadPresetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new LoadPresetDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK && dialog.SelectedPreset != null)
                {
                    var preset = dialog.SelectedPreset;
                    preset.ApplyToSettings();
                    
                    // Update UI with loaded settings
                    checkBoxServerEnabled.Checked = Settings.Default.ServerEnabled;
                    checkBoxClientEnabled.Checked = Settings.Default.ClientEnabled;
                    textBoxServerPassword.Text = Settings.Default.ServerPassword;
                    textBoxServerMap.Text = Settings.Default.ServerMap;
                    numericUpDownServerPort.Value = Settings.Default.ServerPort;
                    textBoxClientIpAddress.Text = Settings.Default.ClientIpAddress;
                    numericUpDownClientPort.Value = Settings.Default.ClientPort;
                    textBoxClientUsername.Text = Settings.Default.ClientUsername;
                    textBoxClientPassword.Text = Settings.Default.ClientPassword;
                    clientPrintVConsoleToolStripMenuItem.Checked = Settings.Default.ClientPrintVconsole;
                    serverDisableUserVConsoleInputToolStripMenuItem.Checked = Settings.Default.ServerDisableUserVconsoleInput;

                    LogToOutput(channel, $"Preset '{preset.Name}' loaded successfully");
                    MessageBox.Show($"Preset '{preset.Name}' loaded successfully!", "Load Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ManagePresetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new PresetManagerDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void ExportConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dialog.FilterIndex = 1;
                dialog.DefaultExt = "json";
                dialog.FileName = $"hlvrcoop_config_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var preset = ConfigurationPreset.FromCurrentSettings("Exported Configuration", "Export");
                        if (PresetManager.ExportPreset(preset, dialog.FileName))
                        {
                            MessageBox.Show("Configuration exported successfully!", "Export Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to export configuration.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("UserInterface.ExportConfiguration", ex);
                        MessageBox.Show($"Failed to export configuration: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dialog.FilterIndex = 1;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var preset = PresetManager.ImportPreset(dialog.FileName);
                        if (preset != null)
                        {
                            var result = MessageBox.Show(
                                $"Import configuration '{preset.Name}'? This will overwrite your current settings.",
                                "Import Configuration",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (result == DialogResult.Yes)
                            {
                                preset.ApplyToSettings();
                                
                                // Update UI
                                checkBoxServerEnabled.Checked = Settings.Default.ServerEnabled;
                                checkBoxClientEnabled.Checked = Settings.Default.ClientEnabled;
                                textBoxServerPassword.Text = Settings.Default.ServerPassword;
                                textBoxServerMap.Text = Settings.Default.ServerMap;
                                numericUpDownServerPort.Value = Settings.Default.ServerPort;
                                textBoxClientIpAddress.Text = Settings.Default.ClientIpAddress;
                                numericUpDownClientPort.Value = Settings.Default.ClientPort;
                                textBoxClientUsername.Text = Settings.Default.ClientUsername;
                                textBoxClientPassword.Text = Settings.Default.ClientPassword;
                                clientPrintVConsoleToolStripMenuItem.Checked = Settings.Default.ClientPrintVconsole;
                                serverDisableUserVConsoleInputToolStripMenuItem.Checked = Settings.Default.ServerDisableUserVconsoleInput;

                                LogToOutput(channel, $"Configuration imported from '{preset.Name}'");
                                MessageBox.Show("Configuration imported successfully!", "Import Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Failed to import configuration. The file may be invalid.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("UserInterface.ImportConfiguration", ex);
                        MessageBox.Show($"Failed to import configuration: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
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
    }
}