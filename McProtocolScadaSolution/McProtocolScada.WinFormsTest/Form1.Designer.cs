namespace McProtocolScada.WinFormsTest
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support.
        /// </summary>
        private void InitializeComponent()
        {
            lblStatus = new Label();
            listBox1 = new ListBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            button1 = new Button();
            _txtNewValue = new TextBox();
            _cbTagName = new ComboBox();
            label4 = new Label();
            label5 = new Label();
            label6 = new Label();
            SuspendLayout();
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(12, 9);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 15);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Status";
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(12, 181);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(823, 604);
            listBox1.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 50);
            label1.Name = "label1";
            label1.Size = new Size(38, 15);
            label1.TabIndex = 2;
            label1.Text = "label1";
            label1.Click += label1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 79);
            label2.Name = "label2";
            label2.Size = new Size(38, 15);
            label2.TabIndex = 3;
            label2.Text = "label2";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 109);
            label3.Name = "label3";
            label3.Size = new Size(38, 15);
            label3.TabIndex = 4;
            label3.Text = "label3";
            // 
            // button1
            // 
            button1.Location = new Point(547, 130);
            button1.Name = "button1";
            button1.Size = new Size(230, 23);
            button1.TabIndex = 5;
            button1.Text = "Write to PLC";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // _txtNewValue
            // 
            _txtNewValue.Location = new Point(547, 76);
            _txtNewValue.Name = "_txtNewValue";
            _txtNewValue.Size = new Size(230, 23);
            _txtNewValue.TabIndex = 6;
            // 
            // _cbTagName
            // 
            _cbTagName.FormattingEnabled = true;
            _cbTagName.Location = new Point(549, 32);
            _cbTagName.Name = "_cbTagName";
            _cbTagName.Size = new Size(217, 23);
            _cbTagName.TabIndex = 7;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(482, 35);
            label4.Name = "label4";
            label4.Size = new Size(61, 15);
            label4.TabIndex = 8;
            label4.Text = "Tag Name";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(480, 79);
            label5.Name = "label5";
            label5.Size = new Size(59, 15);
            label5.TabIndex = 9;
            label5.Text = "NewValue";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Segoe UI", 7F, FontStyle.Italic);
            label6.Location = new Point(549, 103);
            label6.Name = "label6";
            label6.Size = new Size(150, 12);
            label6.TabIndex = 10;
            label6.Text = "(According to the data type of tag)";
            // 
            // Form1
            // 
            AcceptButton = button1;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(847, 798);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(_cbTagName);
            Controls.Add(_txtNewValue);
            Controls.Add(button1);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(listBox1);
            Controls.Add(lblStatus);
            Name = "Form1";
            Text = "Mitsubishi MC Protocol SCADA Demo";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblStatus;
        private ListBox listBox1;
        private Label label1;
        private Label label2;
        private Label label3;
        private Button button1;
        private TextBox _txtNewValue;
        private ComboBox _cbTagName;
        private Label label4;
        private Label label5;
        private Label label6;
    }
}
