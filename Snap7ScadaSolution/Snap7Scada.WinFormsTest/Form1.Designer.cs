namespace Snap7Scada.WinFormsTest
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
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pnlTop = new Panel();
            btnConnect = new Button();
            lblHost = new Label();
            lblStatus = new Label();
            lblStatusCaption = new Label();
            grid = new DataGridView();
            colName = new DataGridViewTextBoxColumn();
            colAddress = new DataGridViewTextBoxColumn();
            colType = new DataGridViewTextBoxColumn();
            colValue = new DataGridViewTextBoxColumn();
            colLast = new DataGridViewTextBoxColumn();
            colStatus = new DataGridViewTextBoxColumn();
            colUpdated = new DataGridViewTextBoxColumn();
            grpWrite = new GroupBox();
            lblTypeHint = new Label();
            btnWrite = new Button();
            txtNewValue = new TextBox();
            _cbTagName = new ComboBox();
            lblValueCaption = new Label();
            lblTagCaption = new Label();
            btnReadNow = new Button();
            pnlTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            grpWrite.SuspendLayout();
            SuspendLayout();
            //
            // pnlTop
            //
            pnlTop.BackColor = Color.FromArgb(37, 37, 38);
            pnlTop.Controls.Add(btnConnect);
            pnlTop.Controls.Add(lblHost);
            pnlTop.Controls.Add(lblStatus);
            pnlTop.Controls.Add(lblStatusCaption);
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Location = new Point(0, 0);
            pnlTop.Name = "pnlTop";
            pnlTop.Padding = new Padding(12, 0, 12, 0);
            pnlTop.Size = new Size(1004, 48);
            pnlTop.TabIndex = 0;
            //
            // btnConnect
            //
            btnConnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnConnect.BackColor = Color.FromArgb(60, 60, 62);
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            btnConnect.ForeColor = Color.White;
            btnConnect.Location = new Point(864, 8);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(128, 32);
            btnConnect.TabIndex = 3;
            btnConnect.Text = "Disconnect";
            btnConnect.UseVisualStyleBackColor = false;
            btnConnect.Click += btnConnect_Click;
            //
            // lblHost
            //
            lblHost.AutoSize = true;
            lblHost.ForeColor = Color.Gainsboro;
            lblHost.Location = new Point(360, 16);
            lblHost.Name = "lblHost";
            lblHost.Size = new Size(72, 15);
            lblHost.TabIndex = 2;
            lblHost.Text = "Host: (none)";
            //
            // lblStatus
            //
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold);
            lblStatus.ForeColor = Color.Silver;
            lblStatus.Location = new Point(72, 15);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(95, 17);
            lblStatus.TabIndex = 1;
            lblStatus.Text = "Disconnected";
            //
            // lblStatusCaption
            //
            lblStatusCaption.AutoSize = true;
            lblStatusCaption.ForeColor = Color.Gray;
            lblStatusCaption.Location = new Point(14, 16);
            lblStatusCaption.Name = "lblStatusCaption";
            lblStatusCaption.Size = new Size(45, 15);
            lblStatusCaption.TabIndex = 0;
            lblStatusCaption.Text = "Status:";
            //
            // grid
            //
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.Columns.AddRange(new DataGridViewColumn[] { colName, colAddress, colType, colValue, colLast, colStatus, colUpdated });
            grid.Dock = DockStyle.Fill;
            grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            grid.Location = new Point(0, 48);
            grid.MultiSelect = false;
            grid.Name = "grid";
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 26;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Size = new Size(1004, 425);
            grid.TabIndex = 1;
            grid.SelectionChanged += grid_SelectionChanged;
            //
            // colName
            //
            colName.FillWeight = 130F;
            colName.HeaderText = "Tag";
            colName.Name = "colName";
            colName.ReadOnly = true;
            //
            // colAddress
            //
            colAddress.FillWeight = 110F;
            colAddress.HeaderText = "Address";
            colAddress.Name = "colAddress";
            colAddress.ReadOnly = true;
            //
            // colType
            //
            colType.FillWeight = 70F;
            colType.HeaderText = "Type";
            colType.Name = "colType";
            colType.ReadOnly = true;
            //
            // colValue
            //
            colValue.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Consolas", 9.75F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleRight };
            colValue.FillWeight = 110F;
            colValue.HeaderText = "Value";
            colValue.Name = "colValue";
            colValue.ReadOnly = true;
            //
            // colLast
            //
            colLast.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Consolas", 9F), ForeColor = Color.Gray, Alignment = DataGridViewContentAlignment.MiddleRight };
            colLast.FillWeight = 100F;
            colLast.HeaderText = "Last Value";
            colLast.Name = "colLast";
            colLast.ReadOnly = true;
            //
            // colStatus
            //
            colStatus.FillWeight = 80F;
            colStatus.HeaderText = "Status";
            colStatus.Name = "colStatus";
            colStatus.ReadOnly = true;
            //
            // colUpdated
            //
            colUpdated.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.Gray, Alignment = DataGridViewContentAlignment.MiddleCenter };
            colUpdated.FillWeight = 90F;
            colUpdated.HeaderText = "Updated";
            colUpdated.Name = "colUpdated";
            colUpdated.ReadOnly = true;
            //
            // grpWrite
            //
            grpWrite.Controls.Add(lblTypeHint);
            grpWrite.Controls.Add(btnWrite);
            grpWrite.Controls.Add(txtNewValue);
            grpWrite.Controls.Add(_cbTagName);
            grpWrite.Controls.Add(lblValueCaption);
            grpWrite.Controls.Add(lblTagCaption);
            grpWrite.Controls.Add(btnReadNow);
            grpWrite.Dock = DockStyle.Bottom;
            grpWrite.Location = new Point(0, 473);
            grpWrite.Name = "grpWrite";
            grpWrite.Padding = new Padding(12, 6, 12, 6);
            grpWrite.Size = new Size(1004, 88);
            grpWrite.TabIndex = 2;
            grpWrite.TabStop = false;
            grpWrite.Text = "Write to PLC";
            //
            // lblTypeHint
            //
            lblTypeHint.AutoSize = true;
            lblTypeHint.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
            lblTypeHint.ForeColor = Color.Gray;
            lblTypeHint.Location = new Point(312, 56);
            lblTypeHint.Name = "lblTypeHint";
            lblTypeHint.Size = new Size(0, 13);
            lblTypeHint.TabIndex = 4;
            //
            // btnWrite
            //
            btnWrite.BackColor = Color.FromArgb(0, 122, 204);
            btnWrite.FlatStyle = FlatStyle.Flat;
            btnWrite.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold);
            btnWrite.ForeColor = Color.White;
            btnWrite.Location = new Point(640, 28);
            btnWrite.Name = "btnWrite";
            btnWrite.Size = new Size(140, 38);
            btnWrite.TabIndex = 3;
            btnWrite.Text = "Write  ▶";
            btnWrite.UseVisualStyleBackColor = false;
            btnWrite.Click += button1_Click;
            //
            // txtNewValue
            //
            txtNewValue.Font = new Font("Consolas", 11F);
            txtNewValue.Location = new Point(312, 30);
            txtNewValue.Name = "txtNewValue";
            txtNewValue.Size = new Size(310, 25);
            txtNewValue.TabIndex = 2;
            //
            // _cbTagName
            //
            _cbTagName.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbTagName.FormattingEnabled = true;
            _cbTagName.Location = new Point(15, 31);
            _cbTagName.Name = "_cbTagName";
            _cbTagName.Size = new Size(280, 23);
            _cbTagName.TabIndex = 1;
            _cbTagName.SelectedIndexChanged += _cbTagName_SelectedIndexChanged;
            //
            // lblValueCaption
            //
            lblValueCaption.AutoSize = true;
            lblValueCaption.ForeColor = Color.DimGray;
            lblValueCaption.Location = new Point(312, 12);
            lblValueCaption.Name = "lblValueCaption";
            lblValueCaption.Size = new Size(64, 15);
            lblValueCaption.TabIndex = 0;
            lblValueCaption.Text = "New value";
            //
            // lblTagCaption
            //
            lblTagCaption.AutoSize = true;
            lblTagCaption.ForeColor = Color.DimGray;
            lblTagCaption.Location = new Point(15, 12);
            lblTagCaption.Name = "lblTagCaption";
            lblTagCaption.Size = new Size(26, 15);
            lblTagCaption.TabIndex = 0;
            lblTagCaption.Text = "Tag";
            //
            // btnReadNow
            //
            btnReadNow.FlatStyle = FlatStyle.Flat;
            btnReadNow.Location = new Point(800, 28);
            btnReadNow.Name = "btnReadNow";
            btnReadNow.Size = new Size(140, 38);
            btnReadNow.TabIndex = 5;
            btnReadNow.Text = "Read Now  ↻";
            btnReadNow.UseVisualStyleBackColor = true;
            btnReadNow.Click += btnReadNow_Click;
            //
            // Form1
            //
            AcceptButton = btnWrite;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1004, 561);
            Controls.Add(grid);
            Controls.Add(grpWrite);
            Controls.Add(pnlTop);
            MinimumSize = new Size(820, 480);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Snap7 SCADA — Tag Monitor & Test";
            Load += Form1_Load;
            pnlTop.ResumeLayout(false);
            pnlTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)grid).EndInit();
            grpWrite.ResumeLayout(false);
            grpWrite.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlTop;
        private Label lblStatusCaption;
        private Label lblStatus;
        private Label lblHost;
        private Button btnConnect;
        private DataGridView grid;
        private DataGridViewTextBoxColumn colName;
        private DataGridViewTextBoxColumn colAddress;
        private DataGridViewTextBoxColumn colType;
        private DataGridViewTextBoxColumn colValue;
        private DataGridViewTextBoxColumn colLast;
        private DataGridViewTextBoxColumn colStatus;
        private DataGridViewTextBoxColumn colUpdated;
        private GroupBox grpWrite;
        private Label lblTagCaption;
        private ComboBox _cbTagName;
        private Label lblValueCaption;
        private TextBox txtNewValue;
        private Button btnWrite;
        private Label lblTypeHint;
        private Button btnReadNow;
    }
}
