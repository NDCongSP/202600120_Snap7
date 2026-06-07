using McProtocolClientLib.Core;
using McProtocolClientLib.Historian;
using McProtocolClientLib.Subscription;
using McProtocolClientLib.Tags;
using System.Diagnostics;

namespace McProtocolScada.WinFormsTest
{
    public partial class Form1 : Form
    {
        PlcManager _manager = new PlcManager();
        PlcRuntime _plcRuntime = null!;
        PlcClient _plc1Client = null!;
        PlcSubscriptionManager? _sub;

        private CancellationTokenSource? _readCts;
        private Task? _readTask;

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
                _sub?.Stop();
                _plc1Client?.Dispose();
            }
            catch { /* ignore on close */ }
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            try
            {
                _manager.LoadFromConfig("tags.json");

                // DEBUG: raw socket test — xác nhận C# frame có hoạt động không
                var rawResult = await PlcClient.TestRawAsync("192.168.11.3", 8000);
                MessageBox.Show(rawResult, "Raw MC Test", MessageBoxButtons.OK,
                    rawResult.StartsWith("Raw OK") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                _plcRuntime = _manager.GetPlc("PLC_1");
                _plc1Client = _plcRuntime.Client;

                // ĐĂNG KÝ SỰ KIỆN TRƯỚC KHI KẾT NỐI
                _plc1Client.StateChanged += Client_StateChanged;

                // Bind error handler của Reader → chỉ thông báo, KHÔNG crash form
                _plcRuntime.Reader.OnReadError += Reader_OnError;
                _plcRuntime.Writer.OnWriteError += Writer_OnError;

                // 1) Tạo handler subscription trước
                _sub = new PlcSubscriptionManager(_plcRuntime.Reader);
                _sub.OnValueChanged += Sub_OnValueChanged;

                // 2) Đăng ký event cho từng tag cụ thể (nếu có)
                AttachTagDebug("Step_Run");
                AttachTagDebug("PartCode");
                AttachTagDebug("PartName");

                // 3) Kết nối PLC + Watchdog (KHÔNG block UI nếu fail)
                bool connected = false;
                try
                {
                    connected = await _plc1Client.ConnectAsync();
                }
                catch (Exception ex)
                {
                    ShowErrorOnce($"Connect PLC failed: {ex.Message}");
                }
                _plc1Client.StartWatchdog(10000);

                // 4) Đọc lần đầu (đã có try/catch trong Reader → không crash)
                if (connected)
                {
                    await _plcRuntime.Reader.ReadGroupAsync(_plcRuntime.Tags);
                    foreach (var tag in _plcRuntime.Tags)
                        tag.RaiseValueChanged();

                    _plcRuntime.Tags.FirstOrDefault(x=>x.Name== "StepRun").ValueChanged += (t) =>
                    {
                        if (this.InvokeRequired)
                        {
                            label1.Invoke(()=>
                            {
                                label1.Text = $"[Event] StepRun changed: {t.LastValue} -> {t.NewValue}";
                            });
                        }
                        else
                        {
                            label1.Text = $"[Event] StepRun changed: {t.LastValue} -> {t.NewValue}";
                        }
                    };
                }

                // 5) Bắt đầu polling subscription
                _sub.Subscribe(_plcRuntime.Tags, intervalMs: 200);

                // 6) Thread phụ cập nhật UI label
                _readCts = new CancellationTokenSource();
                _readTask = Task.Run(() => TaskReadPlcAsync(_readCts.Token));

                _cbTagName.Items.AddRange(_plcRuntime.Tags.Select(x => x.Name).ToArray());
            }
            catch (Exception ex)
            {
                // Bất cứ lỗi nào trong Form_Load đều hiện thông báo, KHÔNG đóng form
                MessageBox.Show(this,
                    $"Init error:\n{ex.Message}",
                    "PLC Init",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Gắn debug-log cho 1 tag theo tên (an toàn nếu tag không tồn tại).
        /// </summary>
        private void AttachTagDebug(string tagName)
        {
            var tag = _plcRuntime.Tags.FirstOrDefault(t => t.Name == tagName);
            if (tag == null) return;

            tag.ValueChanged += (t) =>
            {
                Debug.WriteLine($"{DateTime.Now:O} [{t.Name}] {t.LastValue} -> {t.NewValue} ({t.DataType}) -> Deadband:{t.Deadband}");
            };
        }

        public async Task TaskReadPlcAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    //Action update = () =>
                    //{
                    //    var boxIdScale = _plcRuntime.Tags.FirstOrDefault(t => t.Name == "BoxIdScale");
                    //    var boxIdMetal = _plcRuntime.Tags.FirstOrDefault(t => t.Name == "BoxIdMetal");
                    //    var scaleValue = _plcRuntime.Tags.FirstOrDefault(t => t.Name == "ScaleValue");

                    //    label1.Text = $"BoxIdScale: {boxIdScale?.NewValue}";
                    //    label2.Text = $"BoxIdMetal: {boxIdMetal?.NewValue}";
                    //    label3.Text = $"ScaleValue: {scaleValue?.NewValue}";
                    //};

                    foreach (var item in _plcRuntime.Tags)
                    {
                        if (InvokeRequired)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                UpdateListBoxItem(item);
                            }));
                        }
                        else
                        {
                            UpdateListBoxItem(item);
                        }
                    }

                    if (IsHandleCreated)
                    {
                        //if (InvokeRequired) BeginInvoke(update);
                        //else update();
                    }

                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    try { await Task.Delay(500, token); } catch { break; }
                }
            }
        }

        private void Sub_OnValueChanged(PlcTag tag)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired)
                BeginInvoke(new Action(() => UpdateListBoxItem(tag)));
            else
                UpdateListBoxItem(tag);
        }

        private void UpdateListBoxItem(PlcTag tag)
        {
            string itemText = $"{DateTime.Now:O} [{tag.Name}] {tag.LastValue} -> {tag.NewValue} ({tag.DataType})";

            int foundIndex = -1;
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                if (listBox1.Items[i]!.ToString()!.Contains(tag.Name))
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

        private void Client_StateChanged(PlcConnectionState obj)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired)
                BeginInvoke((Action)(() => lblStatus.Text = obj.ToString()));
            else
                lblStatus.Text = obj.ToString();
        }

        /// <summary>
        /// Khi Reader báo lỗi (Timeout, kết nối...) → cập nhật status, log, KHÔNG đóng form.
        /// </summary>
        private void Reader_OnError(string message)
        {
            // Log debug
            Debug.WriteLine($"[ReaderError] {DateTime.Now:O} {message}");

            // Cập nhật status bar (không bật MessageBox để khỏi spam)
            if (!IsHandleCreated) return;
            Action act = () => lblStatus.Text = $"Read error: {message}";
            if (InvokeRequired) BeginInvoke(act); else act();
        }

        private void Writer_OnError(string message)
        {
            Debug.WriteLine($"[WriterError] {DateTime.Now:O} {message}");
            ShowErrorOnce($"Write error: {message}");
        }

        /// <summary>
        /// Hiện 1 MessageBox nhưng giới hạn tần suất (không spam khi PLC offline liên tục).
        /// </summary>
        private void ShowErrorOnce(string message)
        {
            if (!IsHandleCreated) return;

            // Cùng nội dung & trong vòng 5 giây thì bỏ qua
            if (message == _lastErrorMessage &&
                (DateTime.Now - _lastErrorShownAt).TotalSeconds < 5)
                return;

            _lastErrorMessage = message;
            _lastErrorShownAt = DateTime.Now;

            Action show = () =>
            {
                MessageBox.Show(this, message, "PLC",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            if (InvokeRequired) BeginInvoke(show);
            else show();
        }

        private void label1_Click(object? sender, EventArgs e) { }

        private async void button1_Click(object? sender, EventArgs e)
        {
            try
            {
                var writer = _plcRuntime?.Writer;
                if (writer == null) return;

                var tag = _plcRuntime!.Tags.FirstOrDefault(t => t.Name == _cbTagName.Text);
                if (tag == null) return;

                // 1. Chuyển đổi dữ liệu an toàn theo kiểu tag
                object newValue = tag.DataType switch
                {
                    PlcDataType.String => _txtNewValue.Text,
                    PlcDataType.Bool => _txtNewValue.Text.ToLower() == "true" || _txtNewValue.Text == "1",
                    PlcDataType.Real or PlcDataType.LReal
                        => double.TryParse(_txtNewValue.Text, out var d) ? d : 0.0,
                    _ => int.TryParse(_txtNewValue.Text, out var i) ? i : 0
                };

                // 2. Chỉ ghi tag đang chọn (tối ưu)
                tag.NewValue = newValue;
                var tagsToWrite = new List<PlcTag> { tag };

                await Task.Run(() => writer.WriteGroup(tagsToWrite));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Write PLC error:\n{ex.Message}",
                    "PLC Write", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
