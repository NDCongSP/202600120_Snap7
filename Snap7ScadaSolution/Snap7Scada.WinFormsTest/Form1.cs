using Snap7ClientLib.Core;
using Snap7ClientLib.Historian;
using Snap7ClientLib.Tags;
using System.Windows.Forms;

namespace Snap7Scada.WinFormsTest
{
    public partial class Form1 : Form
    {
        PlcClient plc;
        PlcSubscriptionManager sub;
        SqliteHistorian historian;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            var manager = new PlcManager();
            manager.LoadFromConfig("tags.json");

            var plc1 = manager.GetPlc("PLC_1");

            await plc1.Reader.ReadGroupAsync(plc1.Tags);

            foreach (var t in plc1.Tags)
                listBox1.Items.Add($"{t.Name} = {t.Value}");

            plc = new PlcClient("192.168.1.100");
            plc.ConnectionStateChanged += s =>
                Invoke((Delegate)(() => lblStatus.Text = s.ToString()));

            historian = new SqliteHistorian("plc.db");

            var reader = new PlcGroupReader(plc);

            var tags = new List<PlcTag>
        {
            new() { Name="Weight", Address="DB1.DBD22", DataType=PlcDataType.Real },
            new() { Name="Run", Address="DB1.DBB2", DataType=PlcDataType.Word }
        };

            sub = new PlcSubscriptionManager(reader);
            sub.OnValueChanged += tag =>
            {
                historian.Log(tag);
                Invoke(() => listBox1.Items.Add($"{tag.Name}={tag.Value}"));
            };

            sub.Subscribe(tags, 200);
        }
    }
}
