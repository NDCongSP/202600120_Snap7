# 202600120_Snap7
Connnect to PLC Seimens via profinet protocol

# Snap7ClientLib

PLC Siemens S7-1200 / S7-1500 driver for .NET 8  
Built on top of Snap7 – optimized for SCADA / WinForms / WPF

Snap7ClientLib
│
├── Config
│   └── tags.json
│
├── Core
│   ├── PlcClient
│   ├── PlcManager (Multi PLC)
│   ├── PlcDiagnostics
│
├── Tags
│   ├── PlcTag
│   ├── PlcTagConfigLoader (JSON)
│   ├── PlcGroupReader
│   ├── PlcGroupWriter (Write only changed)
│   └── PlcSubscriptionManager

## Features

- Connect by IP or Hostname
- Auto reconnect
- Group DBRead / DBWrite (high performance)
- Full Siemens data types
- Async Read / Write
- Tag subscription (OnValueChanged)
- Deadband for Real / LReal
- WinForms ready

## Supported Data Types

Bool, Byte, Word, DWord, Int, DInt, Real, LReal, String, Char, LInt, ULInt...

## Usage

```csharp
var plc = new PlcClient("192.168.1.10");//or hostname

var reader = new PlcGroupReader(plc);

var tags = new List<PlcTag>
{
    new() { Name="Scale", Address="DB1.DBD22", DataType=PlcDataType.Real },
    new() { Name="Check", Address="DB1.DBX2.0", DataType=PlcDataType.Bool }
};

await reader.ReadGroupAsync(tags);

//Subscription
var sub = new PlcSubscriptionManager(reader);
sub.OnValueChanged += t => Console.WriteLine($"{t.Name}={t.Value}");
sub.Subscribe(tags, 200);


----------------------------------------------------------------------
Snap7 client structure:
PlcSnap7Client
│
├── PlcClient.cs          // connect / reconnect
├── PlcReader.cs          // đọc dữ liệu
├── PlcAddressParser.cs  // parse DB1.DBB30
├── PlcDataType.cs       // enum
├── PlcTag.cs            // tag model
-----------------------------------------------------------
4️⃣ Cách dùng trong WinForms (SAU KHI ADD DLL)
using PlcSnap7Client;

var plc = new PlcClient("phucthinhautomation.com");
var reader = new PlcReader(plc);

plc.Connect();

string boldMetal = reader.Read<string>(
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


------------------------------------
1️⃣ Nguyên tắc quan trọng khi đọc dữ liệu PLC bằng Snap7

Snap7 KHÔNG đọc trực tiếp theo “DataType” như SCADA
➡️ Snap7 chỉ đọc BYTE[], sau đó bạn tự chuyển kiểu

📌 Quy trình chuẩn:

PLC DB  →  DBRead()  →  byte[] buffer  →  Convert sang Byte / Word / Real / String

2️⃣ Đọc BYTE (DBB)

📍 Ví dụ: DB1.DBB10

byte[] buffer = new byte[1];

plcClient.DBRead(1, 10, 1, buffer);

byte value = buffer[0];


✔ Dùng cho:

Byte

Char

USInt

3️⃣ Đọc WORD (DBW)

📍 Ví dụ: DB1.DBW20 (2 byte)

⚠ Siemens dùng Big Endian

byte[] buffer = new byte[2];

plcClient.DBRead(1, 20, 2, buffer);

// Đảo byte cho đúng endian
ushort wordValue = (ushort)((buffer[0] << 8) | buffer[1]);


📌 Nếu là Int (signed):

short intValue = (short)((buffer[0] << 8) | buffer[1]);

4️⃣ Đọc REAL (DBD – Float 32 bit)

📍 Ví dụ: DB1.DBD30

byte[] buffer = new byte[4];

plcClient.DBRead(1, 30, 4, buffer);

// Snap7 helper
float realValue = S7.GetRealAt(buffer, 0);


✔ Khuyên dùng S7.GetRealAt()
❌ Không dùng BitConverter trực tiếp (sai endian)

5️⃣ Đọc STRING (RẤT QUAN TRỌNG) ⚠⚠⚠
📌 Cấu trúc STRING của Siemens

Ví dụ STRING[50] trong DB:

Byte	Ý nghĩa
DBB30	Max length
DBB31	Current length
DBB32 →	Dữ liệu ASCII
📍 Ví dụ DB1.DBB30 là STRING[50]
int dbNumber = 1;
int startByte = 30;
int maxLength = 50;

// +2 byte header
byte[] buffer = new byte[maxLength + 2];

plcClient.DBRead(dbNumber, startByte, buffer.Length, buffer);

// Lấy độ dài thực
int currentLength = buffer[1];

// Lấy chuỗi
string value = Encoding.ASCII.GetString(buffer, 2, currentLength);


📌 Đây chính là kiểu String bạn thấy trong hình

6️⃣ Viết STRING (để bạn dùng luôn)
string text = "AB95281,1111011303-ADSN-D167,300,3,1/6,BX2,RP";
int maxLength = 50;

byte[] buffer = new byte[maxLength + 2];
buffer[0] = (byte)maxLength;
buffer[1] = (byte)text.Length;

Encoding.ASCII.GetBytes(text, 0, text.Length, buffer, 2);

plcClient.DBWrite(1, 30, buffer.Length, buffer);
------------------------------------------------------------
5️⃣ Mapping PLC → C# (để không nhầm)
PLC	C#
Bool	bool
Byte / USInt	byte
SInt	sbyte
Char	char
Word / UInt	ushort
Int	short
DWord / UDInt	uint
DInt	int
LWord / ULInt	ulong
LInt	long
Real	float
LReal	double
String	string
------------------------------------------------------
5️⃣ CÁCH DÙNG (GIỐNG SCADA THỰC)
var tags = new List<PlcTag>
{
    new PlcTag { Name="boldMetal", Address="DB1.DBB30", DataType=PlcDataType.String, StringLength=50 },
    new PlcTag { Name="newCode", Address="DB1.DBW0", DataType=PlcDataType.Word },
    new PlcTag { Name="scaleValue", Address="DB1.DBD22", DataType=PlcDataType.Real },
    new PlcTag { Name="isCheck", Address="DB1.DBX2.0", DataType=PlcDataType.Bool }
};

var groupReader = new PlcGroupReader(plc);

groupReader.ReadGroup(tags);

// dùng
txtScale.Text = tags.First(t => t.Name=="scaleValue").Value.ToString();
---------------------------------------------------------------------
3️⃣ CÁCH DÙNG (RẤT ĐẸP – RẤT SCADA)
var writeTags = new List<PlcTag>
{
    new PlcTag
    {
        Name="newCodeMetal",
        Address="DB1.DBW0",
        DataType=PlcDataType.Word,
        Value=123
    },
    new PlcTag
    {
        Name="isCheck",
        Address="DB1.DBX2.0",
        DataType=PlcDataType.Bool,
        Value=true
    },
    new PlcTag
    {
        Name="boldScale",
        Address="DB1.DBB30",
        DataType=PlcDataType.String,
        StringLength=50,
        Value="PHUC THINH AUTOMATION"
    }
};

var writer = new PlcGroupWriter(plc);
writer.WriteGroup(writeTags);
-------------------------------------------------------------------
🧠 LƯU Ý CỰC KỲ QUAN TRỌNG

✔️ Luôn DBRead trước khi DBWrite (tránh mất bit khác)
✔️ Bool phải xử lý bit mask
✔️ String Siemens = MaxLen + CurLen + Data

------------------------------------------------------------------------
Async Connect / Read / Write Watchdog kiểm tra PLC sống Subscription tag (event OnValueChanged)
🧱 KIẾN TRÚC TỔNG THỂ
PlcClient
 ├── ConnectAsync / Disconnect
 ├── EnsureConnectedAsync
 ├── Watchdog (Timer)
 ├── PlcGroupReader (Async)
 ├── PlcGroupWriter (Async)
 └── TagSubscriptionManager
        └── OnValueChanged

✅ Cách dùng (RẤT ĐÃ)
var tags = new List<PlcTag>
{
    new PlcTag { Name="scaleValue", Address="DB1.DBD22", DataType=PlcDataType.Real },
    new PlcTag { Name="isCheck", Address="DB1.DBX2.0", DataType=PlcDataType.Bool }
};

var reader = new PlcGroupReader(plc);
var sub = new PlcSubscriptionManager(reader);

sub.OnValueChanged += tag =>
{
    Console.WriteLine($"{tag.Name} = {tag.Value}");
};

sub.Subscribe(tags, 100);

//////////////////////////////////////////////////////////////////////
🧩 SOLUTION STRUCTURE
Snap7Solution
│
├── Snap7ClientLib        (.NET 8 Class Library)
│   ├── PlcClient.cs
│   ├── PlcDataType.cs
│   ├── PlcTag.cs
│   ├── PlcAddressParser.cs
│   ├── PlcGroupReader.cs
│   ├── PlcGroupWriter.cs
│   ├── PlcSubscriptionManager.cs
│   └── Snap7ClientLib.csproj
│
└── Snap7WinformTest      (.NET 8 WinForms)
    ├── Form1.cs
    ├── Form1.Designer.cs
    └── Program.cs