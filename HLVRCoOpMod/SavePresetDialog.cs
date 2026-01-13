using System;
using System.Windows.Forms;

namespace HLVRCoOpMod
{
    public partial class SavePresetDialog : Form
    {
        private TextBox nameTextBox;
        private TextBox categoryTextBox;
        private Button okButton;
        private Button cancelButton;

        public string PresetName => nameTextBox.Text.Trim();
        public string Category => categoryTextBox.Text.Trim();

        public SavePresetDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Save Preset";
            this.Size = new System.Drawing.Size(350, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label nameLabel = new Label
            {
                Text = "Preset Name:",
                Location = new System.Drawing.Point(12, 15),
                AutoSize = true
            };

            nameTextBox = new TextBox
            {
                Location = new System.Drawing.Point(12, 35),
                Size = new System.Drawing.Size(310, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label categoryLabel = new Label
            {
                Text = "Category (optional):",
                Location = new System.Drawing.Point(12, 65),
                AutoSize = true
            };

            categoryTextBox = new TextBox
            {
                Location = new System.Drawing.Point(12, 85),
                Size = new System.Drawing.Size(310, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(166, 120),
                Size = new System.Drawing.Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(247, 120),
                Size = new System.Drawing.Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.Add(nameLabel);
            this.Controls.Add(nameTextBox);
            this.Controls.Add(categoryLabel);
            this.Controls.Add(categoryTextBox);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    MessageBox.Show("Please enter a preset name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                }
            };
        }
    }
}
