using Microsoft.VisualBasic.Logging;
using Snap7ClientLib.Core;
using Snap7ClientLib.Historian;
using Snap7ClientLib.Tags;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Snap7Scada.WinFormsTest
{
    public partial class Form1 : Form
    {
        PlcManager _manager = new PlcManager();
        PlcRuntime _plcRuntime;
        PlcClient _plc1Client;
        PlcSubscriptionManager _sub;
        SqliteHistorian _historian;

        private CancellationTokenSource _readModbusCts;
        private Task _readModbusTask;

        string _scaleValue = string.Empty;
        string _isCheck = string.Empty;
        string _qrMetal = string.Empty;

        public Form1()
        {
            InitializeComponent();

            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // 1.Dừng vòng lặp Modbus
            _readModbusCts?.Cancel();

            // 2. Dừng Polling của Snap7
            _sub?.Stop();

            // 3. Hủy kết nối PLC (Dừng Watchdog và ngắt TCP)
            _plc1Client.Dispose(); // Sẽ dừng watchdog và ngắt kết nối

        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            _manager.LoadFromConfig("tags.json");
            //_manager.LoadFromConfig("tags1.json");

            //tùy vào hệ thống kết nối bao nhiêu PLC để gọi kết nối đến PLC tương ứng
            _plcRuntime = _manager.GetPlc("PLC_1");
            _plc1Client = _plcRuntime.Client; // Lưu vào biến toàn cục

            // ĐĂNG KÝ SỰ KIỆN TRƯỚC KHI KẾT NỐI
            _plc1Client.StateChanged += Client_StateChanged;

            await _plcRuntime.Reader.ReadGroupAsync(_plcRuntime.Tags);

            _sub = new PlcSubscriptionManager((_plcRuntime.Reader));
            _sub.OnValueChanged += Sub_OnValueChanged;//sự kiện trả ra tất cả các tags khi có 1 tag bất kỳ thay đổi giá trị.
            //_sub.Subscribe(plc1.Tags, 200);

            foreach (var t in _plcRuntime.Tags)
                listBox1.Items.Add($"{t.Name} = {t.Value}");

            // Ví dụ đăng ký cho từng tag cụ thể trong Form_Load
            var tagScaleValue = _plcRuntime.Tags.FirstOrDefault(t => t.Name == "ScaleValue");
            if (tagScaleValue != null)
            {
                tagScaleValue.ValueChanged += (tag) =>
                {
                    _scaleValue = $"{tag.Value} (Trước đó: {tag.LastValue})";
                };
            }

            var tagIsChecck = _plcRuntime.Tags.FirstOrDefault(t => t.Name == "IsCheck");
            if (tagIsChecck != null)
            {
                tagIsChecck.ValueChanged += (tag) =>
                {
                    _isCheck = $"{tag.Value} (Trước đó: {tag.LastValue})";
                };
            }

            //// 3. ĐĂNG KÝ TỰ ĐỘNG CHO TẤT CẢ TAG
            //foreach (var tag in plcData.Tags)
            //{
            //    // Khi bất kỳ tag nào đổi giá trị, nó sẽ tự chạy vào đây
            //    tag.ValueChanged += (updatedTag) =>
            //    {
            //        UpdateTagToUI(updatedTag);
            //    };
            //}

            // Sau khi đăng ký xong hết mới bắt đầu chạy Polling
            await _plc1Client.ConnectAsync();
            _plc1Client.StartWatchdog(2000);

            // Chạy vòng lặp Subscription không chặn (Non-blocking)
            _ = Task.Run(() => _sub.SubscribeAsync(_plcRuntime.Tags, 200));

            // Duyệt qua danh sách tag của PLC để cập nhật UI lần đầu tiên
            foreach (var tag in _plcRuntime.Tags)
            {
                tag.RaiseValueChanged(); // 📣 Tự "bắn" sự kiện để UI cập nhật ngay giá trị ban đầu
            }

            //run thread đọc modbus, để đọc các giá trị cân
            _readModbusCts = new CancellationTokenSource();
            _readModbusTask = Task.Run(() => TaskReadModbusAsync(_readModbusCts.Token));
        }

        public async Task TaskReadModbusAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            listBox1.Items.Clear();
                            foreach (var t in _manager.GetPlc("PLC_1").Tags)
                                listBox1.Items.Add($"{t.Name} = {t.Value}");

                            label1.Text =$"BoxIdScale: {_plcRuntime.Tags.FirstOrDefault(t => t.Name == "BoxIdScale").Value.ToString()}";
                            label2.Text = $"BoxIdMetal: {_plcRuntime.Tags.FirstOrDefault(t => t.Name == "BoxIdMetal").Value.ToString()}";
                            label3.Text = $"ScaleValue: {_plcRuntime.Tags.FirstOrDefault(t => t.Name == "ScaleValue").Value.ToString()}";
                        }));
                    }
                    else
                    {
                        listBox1.Items.Clear();
                        foreach (var t in _manager.GetPlc("PLC_1").Tags)
                            listBox1.Items.Add($"{t.Name} = {t.Value}");

                        label1.Text = $"BoxIdScale: {_plcRuntime.Tags.FirstOrDefault(t => t.Name == "BoxIdScale").Value.ToString()}";
                        label2.Text = $"BoxIdMetal: {_plcRuntime.Tags.FirstOrDefault(t => t.Name == "BoxIdMetal").Value.ToString()}";
                        label3.Text = $"ScaleValue: {_plcRuntime.Tags.FirstOrDefault(t => t.Name == "ScaleValue").Value.ToString()}";
                    }

                    await Task.Delay(100, token); // nhịp kiểm tra, đủ nhẹ nhàng
                }
                catch (OperationCanceledException)
                {
                    // token.Cancel() => thoát vòng lặp
                    break;
                }
                catch (Exception ex)
                {
                    // Không để task chết âm thầm
                    await Task.Delay(500, token); // tạm nghỉ rồi thử lại
                }
            }
        }

        private void Sub_OnValueChanged(PlcTag obj)
        {

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() =>
                {
                    listBox1.Items.Clear();
                    foreach (var t in _manager.GetPlc("PLC_1").Tags)
                        listBox1.Items.Add($"{t.Name} = {t.Value}");
                }));
            }
            else
            {
                listBox1.Items.Clear();
                foreach (var t in _manager.GetPlc("PLC_1").Tags)
                    listBox1.Items.Add($"{t.Name} = {t.Value}");
            }

        }

        private void Client_StateChanged(PlcConnectionState obj)
        {

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() =>
                {
                    lblStatus.Text = obj.ToString();
                }));
            }
            else
            {
                lblStatus.Text = obj.ToString();
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
