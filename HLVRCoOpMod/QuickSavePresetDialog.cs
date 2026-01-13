using System;
using System.Windows.Forms;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Simple dialog for quickly saving a preset
    /// </summary>
    public partial class QuickSavePresetDialog : Form
    {
        private TextBox textBoxPresetName;
        private Button buttonOK;
        private Button buttonCancel;
        private Label labelPresetName;

        public string PresetName { get; private set; } = "";

        public QuickSavePresetDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.textBoxPresetName = new TextBox();
            this.buttonOK = new Button();
            this.buttonCancel = new Button();
            this.labelPresetName = new Label();
            this.SuspendLayout();
            
            // 
            // labelPresetName
            // 
            this.labelPresetName.AutoSize = true;
            this.labelPresetName.Location = new System.Drawing.Point(12, 15);
            this.labelPresetName.Name = "labelPresetName";
            this.labelPresetName.Size = new System.Drawing.Size(75, 15);
            this.labelPresetName.TabIndex = 0;
            this.labelPresetName.Text = "Preset Name:";
            
            // 
            // textBoxPresetName
            // 
            this.textBoxPresetName.Location = new System.Drawing.Point(93, 12);
            this.textBoxPresetName.Name = "textBoxPresetName";
            this.textBoxPresetName.Size = new System.Drawing.Size(250, 23);
            this.textBoxPresetName.TabIndex = 1;
            this.textBoxPresetName.KeyPress += TextBoxPresetName_KeyPress;
            
            // 
            // buttonOK
            // 
            this.buttonOK.DialogResult = DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(187, 50);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 2;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += ButtonOK_Click;
            
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(268, 50);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            
            // 
            // QuickSavePresetDialog
            // 
            this.AcceptButton = this.buttonOK;
            this.CancelButton = this.buttonCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(355, 85);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.textBoxPresetName);
            this.Controls.Add(this.labelPresetName);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "QuickSavePresetDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Save Preset";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ButtonOK_Click(object? sender, EventArgs e)
        {
            PresetName = textBoxPresetName.Text.Trim();
            
            if (string.IsNullOrEmpty(PresetName))
            {
                MessageBox.Show("Please enter a preset name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Check if preset already exists
            var existingPreset = PresetManager.GetPreset(PresetName);
            if (existingPreset != null)
            {
                var result = MessageBox.Show(
                    $"Preset '{PresetName}' already exists. Do you want to overwrite it?",
                    "Preset Exists",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result != DialogResult.Yes)
                {
                    this.DialogResult = DialogResult.None;
                }
            }
        }

        private void TextBoxPresetName_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                ButtonOK_Click(sender, e);
            }
        }
    }
}
