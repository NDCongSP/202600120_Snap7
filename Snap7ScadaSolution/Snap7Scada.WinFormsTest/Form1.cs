using Snap7ClientLib.Core;
using Snap7ClientLib.Tags;

namespace Snap7Scada.WinFormsTest
{
    /// <summary>
    /// Test harness for the Snap7 SCADA driver: live tag monitor (DataGridView) + write panel.
    /// </summary>
    /// <remarks>
    /// File: Snap7Scada.WinFormsTest/Form1.cs
    /// </remarks>
    public partial class Form1 : Form
    {
        PlcManager _manager = new PlcManager();
        PlcRuntime _plcRuntime;
        PlcClient _plc1Client;
        PlcSubscriptionManager _sub;

        // Map tag name -> grid row so live updates hit the right row in O(1).
        private readonly Dictionary<string, DataGridViewRow> _rowByTag = new();

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _sub?.Stop();                 // Stop polling
            _plc1Client?.Dispose();       // Stop watchdog + close TCP
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            _manager.LoadFromConfig("tags.json");

            _plcRuntime = _manager.GetPlc("PLC_1");
            _plc1Client = _plcRuntime.Client;

            // Subscribe to events BEFORE connecting.
            _plc1Client.StateChanged += Client_StateChanged;

            // Build one grid row + one combo entry per tag.
            BuildTagRows();

            // 1) Subscription manager
            _sub = new PlcSubscriptionManager(_plcRuntime.Reader);
            _sub.OnValueChanged += Sub_OnValueChanged;

            // 2) Connect + watchdog
            await _plc1Client.ConnectAsync();
            _plc1Client.StartWatchdog(2000);

            // 3) First read so the grid is populated immediately.
            await _plcRuntime.Reader.ReadGroupAsync(_plcRuntime.Tags);
            foreach (var tag in _plcRuntime.Tags)
                UpdateGridRow(tag);

            // 4) Start polling.
            _sub.Subscribe(_plcRuntime.Tags, intervalMs: 200);

            lblHost.Text = $"Host: {_plc1Client.Host}";
            if (_cbTagName.Items.Count > 0)
                _cbTagName.SelectedIndex = 0;
        }

        /// <summary>
        /// Creates one DataGridView row and one ComboBox entry per configured tag.
        /// </summary>
        private void BuildTagRows()
        {
            grid.Rows.Clear();
            _rowByTag.Clear();
            _cbTagName.Items.Clear();

            foreach (var tag in _plcRuntime.Tags)
            {
                int idx = grid.Rows.Add(tag.Name, tag.Address, tag.DataType.ToString(), "", "", "—", "");
                var row = grid.Rows[idx];
                row.Tag = tag;
                _rowByTag[tag.Name] = row;
                _cbTagName.Items.Add(tag.Name);
            }
        }

        private void Sub_OnValueChanged(PlcTag tag)
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => UpdateGridRow(tag)));
            else
                UpdateGridRow(tag);
        }

        /// <summary>
        /// Refreshes the grid row for a single tag (value, last value, status, timestamp).
        /// </summary>
        private void UpdateGridRow(PlcTag tag)
        {
            if (!_rowByTag.TryGetValue(tag.Name, out var row))
                return;

            row.Cells[colValue.Index].Value = FormatValue(tag.NewValue);
            row.Cells[colLast.Index].Value = FormatValue(tag.LastValue);
            row.Cells[colUpdated.Index].Value = DateTime.Now.ToString("HH:mm:ss.fff");

            bool ok = tag.Status == PlcConnectionState.Connected;
            var statusCell = row.Cells[colStatus.Index];
            statusCell.Value = ok ? "● OK" : "● " + tag.Status;
            statusCell.Style.ForeColor = ok ? Color.SeaGreen : Color.Firebrick;

            // Highlight a value that just changed.
            if (!Equals(tag.NewValue, tag.LastValue))
                row.Cells[colValue.Index].Style.ForeColor = Color.FromArgb(0, 122, 204);
            else
                row.Cells[colValue.Index].Style.ForeColor = Color.Black;
        }

        private static string FormatValue(object? value)
        {
            if (value == null) return "—";
            if (value is bool b) return b ? "TRUE" : "FALSE";
            return value.ToString() ?? "—";
        }

        private void Client_StateChanged(PlcConnectionState state)
        {
            if (InvokeRequired)
                BeginInvoke((Action)(() => ApplyState(state)));
            else
                ApplyState(state);
        }

        private void ApplyState(PlcConnectionState state)
        {
            lblStatus.Text = state.ToString();
            lblStatus.ForeColor = state switch
            {
                PlcConnectionState.Connected => Color.FromArgb(76, 209, 100),
                PlcConnectionState.Connecting or PlcConnectionState.Reconnecting => Color.Gold,
                PlcConnectionState.Error => Color.Tomato,
                _ => Color.Silver
            };
        }

        /// <summary>
        /// When a grid row is selected, mirror that tag into the write panel for quick testing.
        /// </summary>
        private void grid_SelectionChanged(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;
            if (grid.SelectedRows[0].Tag is not PlcTag tag) return;

            _cbTagName.SelectedItem = tag.Name;       // triggers _cbTagName_SelectedIndexChanged
        }

        /// <summary>
        /// Updates the type hint and pre-fills the textbox with the current value of the chosen tag.
        /// </summary>
        private void _cbTagName_SelectedIndexChanged(object sender, EventArgs e)
        {
            var tag = _plcRuntime?.Tags?.FirstOrDefault(t => t.Name == _cbTagName.Text);
            if (tag == null) return;

            lblTypeHint.Text = tag.DataType switch
            {
                PlcDataType.Bool => "Bool — enter: true / false / 1 / 0",
                PlcDataType.String => "String — enter text",
                PlcDataType.Real or PlcDataType.LReal => "Float — enter a decimal number (e.g. 50.5)",
                _ => $"{tag.DataType} — enter an integer"
            };
            txtNewValue.Text = FormatValue(tag.NewValue) == "—" ? "" : FormatValue(tag.NewValue);
        }

        private async void btnReadNow_Click(object sender, EventArgs e)
        {
            try
            {
                await _plcRuntime.Reader.ReadGroupAsync(_plcRuntime.Tags);
                foreach (var tag in _plcRuntime.Tags)
                    UpdateGridRow(tag);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Read PLC error: {ex.Message}", "Read",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var writer = _plcRuntime.Writer;

            var tag = _plcRuntime.Tags.FirstOrDefault(t => t.Name == _cbTagName.Text);
            if (tag == null)
            {
                MessageBox.Show("Please select a tag first.", "Write",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Convert the textbox into the tag's runtime type.
            string raw = txtNewValue.Text;
            object newValue;
            try
            {
                newValue = tag.DataType switch
                {
                    PlcDataType.String => raw,
                    PlcDataType.Bool => raw.Trim().ToLower() is "true" or "1",
                    PlcDataType.Real or PlcDataType.LReal => double.Parse(raw.Trim()),
                    _ => int.Parse(raw.Trim())
                };
            }
            catch (FormatException)
            {
                MessageBox.Show($"'{raw}' is not a valid {tag.DataType} value.", "Write",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            tag.NewValue = newValue;
            var tagsToWrite = new List<PlcTag> { tag };

            try
            {
                btnWrite.Enabled = false;
                await Task.Run(() => writer.WriteGroup(tagsToWrite));
                UpdateGridRow(tag);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Write PLC error: {ex.Message}", "Write",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnWrite.Enabled = true;
            }
        }
    }
}
