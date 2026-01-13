using System;
using System.Linq;
using System.Windows.Forms;

namespace HLVRCoOpMod
{
    public partial class PresetManagerDialog : Form
    {
        private ListBox presetListBox;
        private Button deleteButton;
        private Button exportButton;
        private Button importButton;
        private Button closeButton;
        private Label infoLabel;

        public PresetManagerDialog()
        {
            InitializeComponent();
            LoadPresets();
        }

        private void InitializeComponent()
        {
            this.Text = "Preset Manager";
            this.Size = new System.Drawing.Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label label = new Label
            {
                Text = "Manage your configuration presets:",
                Location = new System.Drawing.Point(12, 12),
                AutoSize = true
            };

            presetListBox = new ListBox
            {
                Location = new System.Drawing.Point(12, 35),
                Size = new System.Drawing.Size(360, 280),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            presetListBox.SelectedIndexChanged += PresetListBox_SelectedIndexChanged;

            infoLabel = new Label
            {
                Location = new System.Drawing.Point(12, 320),
                Size = new System.Drawing.Size(360, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false
            };

            deleteButton = new Button
            {
                Text = "Delete",
                Location = new System.Drawing.Point(378, 35),
                Size = new System.Drawing.Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            deleteButton.Click += DeleteButton_Click;

            exportButton = new Button
            {
                Text = "Export",
                Location = new System.Drawing.Point(378, 70),
                Size = new System.Drawing.Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            exportButton.Click += ExportButton_Click;

            importButton = new Button
            {
                Text = "Import",
                Location = new System.Drawing.Point(378, 105),
                Size = new System.Drawing.Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            importButton.Click += ImportButton_Click;

            closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(378, 325),
                Size = new System.Drawing.Size(110, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.Add(label);
            this.Controls.Add(presetListBox);
            this.Controls.Add(infoLabel);
            this.Controls.Add(deleteButton);
            this.Controls.Add(exportButton);
            this.Controls.Add(importButton);
            this.Controls.Add(closeButton);

            this.CancelButton = closeButton;
        }

        private void LoadPresets()
        {
            var presets = PresetManager.GetPresets();
            presetListBox.Items.Clear();

            foreach (var preset in presets.OrderBy(p => p.Category).ThenBy(p => p.Name))
            {
                string displayText = string.IsNullOrEmpty(preset.Category)
                    ? preset.Name
                    : $"[{preset.Category}] {preset.Name}";
                presetListBox.Items.Add(new PresetListItem(displayText, preset));
            }

            if (presetListBox.Items.Count == 0)
            {
                presetListBox.Items.Add("No presets available");
                infoLabel.Text = "Create a preset by going to File > Save Preset...";
            }

            UpdateButtonStates();
        }

        private void PresetListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (presetListBox.SelectedItem is PresetListItem item)
            {
                var preset = item.Preset;
                infoLabel.Text = $"Created: {preset.CreatedDate:yyyy-MM-dd HH:mm} | Modified: {preset.LastModified:yyyy-MM-dd HH:mm}";
            }
            else
            {
                infoLabel.Text = "";
            }
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = presetListBox.SelectedItem is PresetListItem;
            deleteButton.Enabled = hasSelection;
            exportButton.Enabled = hasSelection;
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (presetListBox.SelectedItem is PresetListItem item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete preset '{item.Preset.Name}'?",
                    "Delete Preset",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    if (PresetManager.DeletePreset(item.Preset.Name))
                    {
                        LoadPresets();
                        MessageBox.Show("Preset deleted successfully.", "Delete Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete preset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            if (presetListBox.SelectedItem is PresetListItem item)
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                    dialog.FilterIndex = 1;
                    dialog.DefaultExt = "json";
                    dialog.FileName = item.Preset.Name;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        if (PresetManager.ExportPreset(item.Preset, dialog.FileName))
                        {
                            MessageBox.Show("Preset exported successfully!", "Export Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to export preset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void ImportButton_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dialog.FilterIndex = 1;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var preset = PresetManager.ImportPreset(dialog.FileName);
                    if (preset != null)
                    {
                        if (PresetManager.SavePreset(preset))
                        {
                            LoadPresets();
                            MessageBox.Show($"Preset '{preset.Name}' imported successfully!", "Import Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to import preset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to import preset. The file may be invalid.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private class PresetListItem
        {
            public string DisplayText { get; }
            public ConfigurationPreset Preset { get; }

            public PresetListItem(string displayText, ConfigurationPreset preset)
            {
                DisplayText = displayText;
                Preset = preset;
            }

            public override string ToString() => DisplayText;
        }
    }
}
