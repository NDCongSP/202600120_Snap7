using Snap7ClientLib;
using System.Windows.Forms;

namespace TestClient
{
    public partial class Form1 : Form
    {
        PlcClient plc = new PlcClient("phucthinhautomation.ddns.net");
        PlcReader reader;

        public Form1()
        {
            InitializeComponent();

            Load += Form1_Load;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {

            reader = new PlcReader(plc);

            _labStatus.Text = plc.Connect().ToString();

            textBox1.Text = reader.Read<string>(
                "DB1.DBB30",
                PlcDataType.String,
                50
            );

            float scaleValue = reader.Read<float>(
                "DB1.DBD22",
                PlcDataType.Real
            );

            ushort code = reader.Read<ushort>(
                "DB1.DBW0",
                PlcDataType.Word
            );
        }
    }
}
