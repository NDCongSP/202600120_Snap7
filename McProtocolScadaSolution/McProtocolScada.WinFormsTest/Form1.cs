using McProtocolClientLib.Core;
using McProtocolClientLib.Historian;
using McProtocolClientLib.Subscription;
using McProtocolClientLib.Tags;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace McProtocolScada.WinFormsTest
{
    public partial class Form1 : Form
    {
        PlcManager _manager = new PlcManager();

        // plcName → (runtime, subscription)
        private readonly Dictionary<string, (PlcRuntime Runtime, PlcSubscriptionManager Sub)> _sessions = new();
        private readonly ConcurrentDictionary<string, string> _plcStates = new();

        private CancellationTokenSource? _readCts;
        private Task? _readTask;
        private SqliteHistorian? _historian;

        // Throttle thông báo lỗi để không spam MessageBox/log
        private DateTime _lastErrorShownAt = DateTime.MinValue;
        private string _lastErrorMessage = "";

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try
            {
                _readCts?.Cancel();
                foreach (var (_, session) in _sessions)
                {
                    session.Sub.Stop();
                    session.Runtime.Client.Dispose();
                }
            }
            catch { /* ignore on close */ }
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            try
            {
                _manager.LoadFromConfig("tags.json");
                _historian = new SqliteHistorian("plc_history.db");

                // Khởi tạo session cho mỗi PLC
                foreach (var plcName in _manager.GetAllPlcNames())
                {
                    var runtime = _manager.GetPlc(plcName);
                    _plcStates[plcName] = PlcConnectionState.Disconnected.ToString();

                    runtime.Client.StateChanged += state => Client_StateChanged(plcName, state);
                    runtime.Reader.OnReadError  += msg   => Reader_OnError(plcName, msg);
                    runtime.Writer.OnWriteError += msg   => Writer_OnError(msg);

                    AttachTagDebug(runtime, "StepRun");
                    AttachTagDebug(runtime, "Part");
                    AttachTagDebug(runtime, "PartName");

                    var sub = new PlcSubscriptionManager(runtime.Reader);
                    sub.OnValueChanged += tag => Sub_OnValueChanged(plcName, tag);
                    _sessions[plcName] = (runtime, sub);
                }

                // Kết nối tất cả PLC song song
                var connectTasks = _sessions.Select(async kv =>
                {
                    string name = kv.Key;
                    var session = kv.Value;

                    bool connected = false;
                    try { connected = await session.Runtime.Client.ConnectAsync(); }
                    catch (Exception ex) { ShowErrorOnce($"[{name}] Connect: {ex.Message}"); }

                    session.Runtime.Client.StartWatchdog(10000);

                    if (connected)
                    {
                        await session.Runtime.Reader.ReadGroupAsync(session.Runtime.Tags);
                        foreach (var tag in session.Runtime.Tags)
                            tag.RaiseValueChanged();

                        // StepRun → label1
                        var stepRunTag = session.Runtime.Tags.FirstOrDefault(x => x.Name == "StepRun");
                        if (stepRunTag != null)
                            stepRunTag.ValueChanged += (t) =>
                            {
                                if (InvokeRequired)
                                    label1.Invoke(() => label1.Text = $"[{name}] StepRun: {t.LastValue} -> {t.NewValue}");
                                else
                                    label1.Text = $"[{name}] StepRun: {t.LastValue} -> {t.NewValue}";
                            };
                    }

                    session.Sub.Subscribe(session.Runtime.Tags, intervalMs: 200);
                }).ToList();

                await Task.WhenAll(connectTasks);

                // Fill ComboBox: "PLC_1:Part", "PLC_2:Part", ...
                var tagItems = _sessions
                    .SelectMany(kv => kv.Value.Runtime.Tags.Select(t => $"{kv.Key}:{t.Name}"))
                    .ToArray();
                _cbTagName.Items.AddRange(tagItems);

                // Thread phụ cập nhật UI listBox định kỳ
                _readCts = new CancellationTokenSource();
                _readTask = Task.Run(() => TaskReadPlcAsync(_readCts.Token));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Init error:\n{ex.Message}", "PLC Init",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Gắn debug-log cho 1 tag theo tên (an toàn nếu tag không tồn tại).</summary>
        private void AttachTagDebug(PlcRuntime runtime, string tagName)
        {
            var tag = runtime.Tags.FirstOrDefault(t => t.Name == tagName);
            if (tag == null) return;
            tag.ValueChanged += (t) =>
            {
                Debug.WriteLine($"{DateTime.Now:O} [{t.Name}] {t.LastValue} -> {t.NewValue} ({t.DataType})");
            };
        }

        public async Task TaskReadPlcAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var (plcName, session) in _sessions)
                        foreach (var item in session.Runtime.Tags)
                        {
                            if (InvokeRequired)
                                BeginInvoke((Action)(() => UpdateListBoxItem(plcName, item)));
                            else
                                UpdateListBoxItem(plcName, item);
                        }

                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException) { break; }
                catch { try { await Task.Delay(500, token); } catch { break; } }
            }
        }

        private void Sub_OnValueChanged(string plcName, PlcTag tag)
        {
            _ = Task.Run(() => _historian?.Log(tag));

            if (!IsHandleCreated) return;
            if (InvokeRequired)
                BeginInvoke(new Action(() => UpdateListBoxItem(plcName, tag)));
            else
                UpdateListBoxItem(plcName, tag);
        }

        private void UpdateListBoxItem(string plcName, PlcTag tag)
        {
            string key = $"{plcName}:{tag.Name}";
            string itemText = $"{DateTime.Now:HH:mm:ss} [{key}] {tag.LastValue} -> {tag.NewValue} ({tag.DataType})";

            int foundIndex = -1;
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                if (listBox1.Items[i]!.ToString()!.Contains($"[{key}]"))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex != -1)
            {
                if (listBox1.Items[foundIndex]!.ToString() != itemText)
                    listBox1.Items[foundIndex] = itemText;
            }
            else
            {
                listBox1.Items.Add(itemText);
            }
        }

        private void Client_StateChanged(string plcName, PlcConnectionState state)
        {
            _plcStates[plcName] = state.ToString();
            if (!IsHandleCreated) return;
            var statusText = string.Join(" | ", _plcStates.Select(kv => $"{kv.Key}:{kv.Value}"));
            if (InvokeRequired)
                BeginInvoke((Action)(() => lblStatus.Text = statusText));
            else
                lblStatus.Text = statusText;
        }

        private void Reader_OnError(string plcName, string message)
        {
            Debug.WriteLine($"[{plcName}][ReaderError] {DateTime.Now:O} {message}");
            if (!IsHandleCreated) return;
            Action act = () => lblStatus.Text = $"[{plcName}] Read error: {message}";
            if (InvokeRequired) BeginInvoke(act); else act();
        }

        private void Writer_OnError(string message)
        {
            Debug.WriteLine($"[WriterError] {DateTime.Now:O} {message}");
            ShowErrorOnce($"Write error: {message}");
        }

        private void ShowErrorOnce(string message)
        {
            if (!IsHandleCreated) return;
            if (message == _lastErrorMessage &&
                (DateTime.Now - _lastErrorShownAt).TotalSeconds < 5)
                return;
            _lastErrorMessage = message;
            _lastErrorShownAt = DateTime.Now;
            Action show = () =>
            {
                MessageBox.Show(this, message, "PLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            if (InvokeRequired) BeginInvoke(show);
            else show();
        }

        private void label1_Click(object? sender, EventArgs e) { }

        private async void button1_Click(object? sender, EventArgs e)
        {
            try
            {
                var text = _cbTagName.Text;
                if (string.IsNullOrEmpty(text)) return;

                // Parse "PLC_1:Part" → plcName="PLC_1", tagName="Part"
                var parts = text.Split(':', 2);
                if (parts.Length != 2) return;
                string plcName = parts[0];
                string tagName = parts[1];

                if (!_sessions.TryGetValue(plcName, out var session)) return;
                var tag = session.Runtime.Tags.FirstOrDefault(t => t.Name == tagName);
                if (tag == null) return;

                object newValue = tag.DataType switch
                {
                    PlcDataType.String => _txtNewValue.Text,
                    PlcDataType.Bool   => _txtNewValue.Text.ToLower() == "true" || _txtNewValue.Text == "1",
                    PlcDataType.Real or PlcDataType.LReal
                        => double.TryParse(_txtNewValue.Text, out var d) ? d : 0.0,
                    _ => int.TryParse(_txtNewValue.Text, out var i) ? i : 0
                };

                tag.NewValue = newValue;
                await Task.Run(() => session.Runtime.Writer.WriteGroup(new List<PlcTag> { tag }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Write PLC error:\n{ex.Message}",
                    "PLC Write", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
