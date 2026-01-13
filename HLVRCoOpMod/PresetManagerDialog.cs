using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Dialog for managing presets
    /// </summary>
    public partial class PresetManagerDialog : Form
    {
        private ListBox listBoxPresets;
        private Button buttonSave;
        private Button buttonLoad;
        private Button buttonDelete;
        private Button buttonRename;
        private Button buttonClose;
        private TextBox textBoxPresetName;
        private Label labelPresetName;
        private UserInterface parentForm;

        public PresetManagerDialog(UserInterface parent)
        {
            parentForm = parent;
            InitializeComponent();
            RefreshPresetList();
        }

        private void InitializeComponent()
        {
            this.listBoxPresets = new ListBox();
            this.buttonSave = new Button();
            this.buttonLoad = new Button();
            this.buttonDelete = new Button();
            this.buttonRename = new Button();
            this.buttonClose = new Button();
            this.textBoxPresetName = new TextBox();
            this.labelPresetName = new Label();
            this.SuspendLayout();
            
            // 
            // listBoxPresets
            // 
            this.listBoxPresets.FormattingEnabled = true;
            this.listBoxPresets.ItemHeight = 15;
            this.listBoxPresets.Location = new System.Drawing.Point(12, 12);
            this.listBoxPresets.Name = "listBoxPresets";
            this.listBoxPresets.Size = new System.Drawing.Size(300, 244);
            this.listBoxPresets.TabIndex = 0;
            this.listBoxPresets.SelectedIndexChanged += ListBoxPresets_SelectedIndexChanged;
            this.listBoxPresets.DoubleClick += ListBoxPresets_DoubleClick;
            
            // 
            // labelPresetName
            // 
            this.labelPresetName.AutoSize = true;
            this.labelPresetName.Location = new System.Drawing.Point(12, 270);
            this.labelPresetName.Name = "labelPresetName";
            this.labelPresetName.Size = new System.Drawing.Size(75, 15);
            this.labelPresetName.TabIndex = 1;
            this.labelPresetName.Text = "Preset Name:";
            
            // 
            // textBoxPresetName
            // 
            this.textBoxPresetName.Location = new System.Drawing.Point(93, 267);
            this.textBoxPresetName.Name = "textBoxPresetName";
            this.textBoxPresetName.Size = new System.Drawing.Size(219, 23);
            this.textBoxPresetName.TabIndex = 2;
            
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(318, 12);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(120, 35);
            this.buttonSave.TabIndex = 3;
            this.buttonSave.Text = "Save Current";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += ButtonSave_Click;
            
            // 
            // buttonLoad
            // 
            this.buttonLoad.Enabled = false;
            this.buttonLoad.Location = new System.Drawing.Point(318, 53);
            this.buttonLoad.Name = "buttonLoad";
            this.buttonLoad.Size = new System.Drawing.Size(120, 35);
            this.buttonLoad.TabIndex = 4;
            this.buttonLoad.Text = "Load";
            this.buttonLoad.UseVisualStyleBackColor = true;
            this.buttonLoad.Click += ButtonLoad_Click;
            
            // 
            // buttonRename
            // 
            this.buttonRename.Enabled = false;
            this.buttonRename.Location = new System.Drawing.Point(318, 94);
            this.buttonRename.Name = "buttonRename";
            this.buttonRename.Size = new System.Drawing.Size(120, 35);
            this.buttonRename.TabIndex = 5;
            this.buttonRename.Text = "Rename";
            this.buttonRename.UseVisualStyleBackColor = true;
            this.buttonRename.Click += ButtonRename_Click;
            
            // 
            // buttonDelete
            // 
            this.buttonDelete.Enabled = false;
            this.buttonDelete.Location = new System.Drawing.Point(318, 135);
            this.buttonDelete.Name = "buttonDelete";
            this.buttonDelete.Size = new System.Drawing.Size(120, 35);
            this.buttonDelete.TabIndex = 6;
            this.buttonDelete.Text = "Delete";
            this.buttonDelete.UseVisualStyleBackColor = true;
            this.buttonDelete.Click += ButtonDelete_Click;
            
            // 
            // buttonClose
            // 
            this.buttonClose.Location = new System.Drawing.Point(318, 221);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(120, 35);
            this.buttonClose.TabIndex = 7;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += ButtonClose_Click;
            
            // 
            // PresetManagerDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(450, 300);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.buttonDelete);
            this.Controls.Add(this.buttonRename);
            this.Controls.Add(this.buttonLoad);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.textBoxPresetName);
            this.Controls.Add(this.labelPresetName);
            this.Controls.Add(this.listBoxPresets);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PresetManagerDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Preset Manager";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void RefreshPresetList()
        {
            listBoxPresets.Items.Clear();
            var presetNames = PresetManager.GetPresetNames();
            foreach (var name in presetNames)
            {
                listBoxPresets.Items.Add(name);
            }
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = listBoxPresets.SelectedIndex >= 0;
            buttonLoad.Enabled = hasSelection;
            buttonRename.Enabled = hasSelection;
            buttonDelete.Enabled = hasSelection;
        }

        private void ListBoxPresets_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateButtonStates();
            if (listBoxPresets.SelectedItem != null)
            {
                textBoxPresetName.Text = listBoxPresets.SelectedItem.ToString() ?? "";
            }
        }

        private void ListBoxPresets_DoubleClick(object? sender, EventArgs e)
        {
            if (listBoxPresets.SelectedIndex >= 0)
            {
                LoadSelectedPreset();
            }
        }

        private void ButtonSave_Click(object? sender, EventArgs e)
        {
            string presetName = textBoxPresetName.Text.Trim();
            
            if (string.IsNullOrEmpty(presetName))
            {
                MessageBox.Show("Please enter a preset name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if preset already exists
            var existingPreset = PresetManager.GetPreset(presetName);
            if (existingPreset != null)
            {
                var result = MessageBox.Show(
                    $"Preset '{presetName}' already exists. Do you want to overwrite it?",
                    "Preset Exists",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result != DialogResult.Yes)
                    return;
            }

            try
            {
                var preset = PresetManager.CreatePresetFromCurrentSettings(presetName);
                PresetManager.SavePreset(presetName, preset);
                RefreshPresetList();
                
                // Select the saved preset
                int index = listBoxPresets.Items.IndexOf(presetName);
                if (index >= 0)
                {
                    listBoxPresets.SelectedIndex = index;
                }
                
                parentForm.LogToOutput(parentForm.GetChannel(), $"Preset '{presetName}' saved successfully.");
                MessageBox.Show($"Preset '{presetName}' saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save preset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonLoad_Click(object? sender, EventArgs e)
        {
            LoadSelectedPreset();
        }

        private void LoadSelectedPreset()
        {
            if (listBoxPresets.SelectedItem == null)
                return;

            string presetName = listBoxPresets.SelectedItem.ToString() ?? "";
            var preset = PresetManager.GetPreset(presetName);
            
            if (preset == null)
            {
                MessageBox.Show($"Preset '{presetName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RefreshPresetList();
                return;
            }

            var result = MessageBox.Show(
                $"Load preset '{presetName}'? This will replace your current settings.",
                "Load Preset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result != DialogResult.Yes)
                return;

            try
            {
                PresetManager.ApplyPresetToSettings(preset);
                parentForm.ApplyPresetToUI(preset);
                parentForm.LogToOutput(parentForm.GetChannel(), $"Preset '{presetName}' loaded successfully.");
                MessageBox.Show($"Preset '{presetName}' loaded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load preset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonRename_Click(object? sender, EventArgs e)
        {
            if (listBoxPresets.SelectedItem == null)
                return;

            string oldName = listBoxPresets.SelectedItem.ToString() ?? "";
            string newName = textBoxPresetName.Text.Trim();
            
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Please enter a new preset name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (newName == oldName)
            {
                MessageBox.Show("The new name is the same as the current name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (PresetManager.GetPreset(newName) != null)
            {
                MessageBox.Show($"A preset named '{newName}' already exists.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (PresetManager.RenamePreset(oldName, newName))
                {
                    RefreshPresetList();
                    
                    // Select the renamed preset
                    int index = listBoxPresets.Items.IndexOf(newName);
                    if (index >= 0)
                    {
                        listBoxPresets.SelectedIndex = index;
                    }
                    
                    parentForm.LogToOutput(parentForm.GetChannel(), $"Preset '{oldName}' renamed to '{newName}'.");
                    MessageBox.Show($"Preset renamed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to rename preset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename preset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonDelete_Click(object? sender, EventArgs e)
        {
            if (listBoxPresets.SelectedItem == null)
                return;

            string presetName = listBoxPresets.SelectedItem.ToString() ?? "";
            
            var result = MessageBox.Show(
                $"Are you sure you want to delete preset '{presetName}'?",
                "Delete Preset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes)
                return;

            try
            {
                if (PresetManager.DeletePreset(presetName))
                {
                    RefreshPresetList();
                    textBoxPresetName.Text = "";
                    parentForm.LogToOutput(parentForm.GetChannel(), $"Preset '{presetName}' deleted.");
                    MessageBox.Show($"Preset '{presetName}' deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to delete preset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete preset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonClose_Click(object? sender, EventArgs e)
        {
            this.Close();
        }
    }
}
