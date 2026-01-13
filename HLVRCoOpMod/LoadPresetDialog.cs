using System;
using System.Linq;
using System.Windows.Forms;

namespace HLVRCoOpMod
{
    public partial class LoadPresetDialog : Form
    {
        private ListBox presetListBox;
        private Button okButton;
        private Button cancelButton;
        private Label infoLabel;

        public ConfigurationPreset? SelectedPreset { get; private set; }

        public LoadPresetDialog()
        {
            InitializeComponent();
            LoadPresets();
        }

        private void InitializeComponent()
        {
            this.Text = "Load Preset";
            this.Size = new System.Drawing.Size(400, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label label = new Label
            {
                Text = "Select a preset to load:",
                Location = new System.Drawing.Point(12, 12),
                AutoSize = true
            };

            presetListBox = new ListBox
            {
                Location = new System.Drawing.Point(12, 35),
                Size = new System.Drawing.Size(360, 250),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            presetListBox.SelectedIndexChanged += PresetListBox_SelectedIndexChanged;
            presetListBox.DoubleClick += (s, e) => { if (okButton.Enabled) okButton.PerformClick(); };

            infoLabel = new Label
            {
                Location = new System.Drawing.Point(12, 290),
                Size = new System.Drawing.Size(360, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false
            };

            okButton = new Button
            {
                Text = "Load",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(228, 325),
                Size = new System.Drawing.Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            okButton.Click += OkButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(309, 325),
                Size = new System.Drawing.Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.Add(label);
            this.Controls.Add(presetListBox);
            this.Controls.Add(infoLabel);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
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
        }

        private void PresetListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (presetListBox.SelectedItem is PresetListItem item)
            {
                var preset = item.Preset;
                infoLabel.Text = $"Created: {preset.CreatedDate:yyyy-MM-dd HH:mm} | Modified: {preset.LastModified:yyyy-MM-dd HH:mm}";
                okButton.Enabled = true;
            }
            else
            {
                infoLabel.Text = "";
                okButton.Enabled = false;
            }
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            if (presetListBox.SelectedItem is PresetListItem item)
            {
                SelectedPreset = item.Preset;
            }
            else
            {
                this.DialogResult = DialogResult.None;
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
