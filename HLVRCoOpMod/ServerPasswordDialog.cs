using System;
using System.Windows.Forms;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Dialog for entering server password
    /// </summary>
    public partial class ServerPasswordDialog : Form
    {
        private TextBox textBoxPassword;
        private Button buttonOK;
        private Button buttonCancel;
        private Label labelPassword;
        
        public string Password { get; private set; } = "";
        
        public ServerPasswordDialog()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.textBoxPassword = new TextBox();
            this.buttonOK = new Button();
            this.buttonCancel = new Button();
            this.labelPassword = new Label();
            this.SuspendLayout();
            
            // 
            // labelPassword
            // 
            this.labelPassword.AutoSize = true;
            this.labelPassword.Location = new System.Drawing.Point(12, 15);
            this.labelPassword.Name = "labelPassword";
            this.labelPassword.Size = new System.Drawing.Size(90, 15);
            this.labelPassword.TabIndex = 0;
            this.labelPassword.Text = "Server Password:";
            
            // 
            // textBoxPassword
            // 
            this.textBoxPassword.Location = new System.Drawing.Point(108, 12);
            this.textBoxPassword.Name = "textBoxPassword";
            this.textBoxPassword.PasswordChar = '*';
            this.textBoxPassword.Size = new System.Drawing.Size(250, 23);
            this.textBoxPassword.TabIndex = 1;
            this.textBoxPassword.KeyPress += TextBoxPassword_KeyPress;
            
            // 
            // buttonOK
            // 
            this.buttonOK.DialogResult = DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(208, 50);
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
            this.buttonCancel.Location = new System.Drawing.Point(289, 50);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            
            // 
            // ServerPasswordDialog
            // 
            this.AcceptButton = this.buttonOK;
            this.CancelButton = this.buttonCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(376, 85);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.textBoxPassword);
            this.Controls.Add(this.labelPassword);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ServerPasswordDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Server Password";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void ButtonOK_Click(object? sender, EventArgs e)
        {
            Password = textBoxPassword.Text;
        }
        
        private void TextBoxPassword_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                ButtonOK_Click(sender, e);
            }
        }
    }
}
