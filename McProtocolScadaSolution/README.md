# McProtocolScada — Mitsubishi MC Protocol SCADA Driver

Thư viện kết nối PLC **Mitsubishi** (Q / L / iQ-R / FX series) qua **MC Protocol**
(SLMP – Seamless Message Protocol) cho .NET 8 / .NET Standard 2.0.

> Phiên bản này là **bản port 1-1 từ `Snap7ScadaSolution`** (Siemens S7 Profinet) sang
> Mitsubishi MC Protocol. Cấu trúc, namespace, lớp, behavior giữ nguyên — chỉ thay đổi
> tầng giao tiếp PLC từ `Sharp7` (S7Comm) sang `HslCommunication` (MC Protocol).

```
McProtocolScadaSolution
│
├── McProtocolScada.Core         (.NET Standard 2.0 – Class Library)
│   ├── Config
│   │   └── PlcConfigLoader.cs
│   ├── Core
│   │   ├── PlcClient.cs            ← MelsecMcNet / MelsecMcAsciiNet / A1E / iQ-R
│   │   ├── PlcConnectionState.cs
│   │   └── PlcManager.cs           (multi-PLC SCADA)
│   ├── Diagnostics
│   │   └── PlcDiagnostics.cs
│   ├── Historian
│   │   └── SqliteHistorian.cs
│   ├── IO
│   │   ├── PlcGroupReader.cs       (batch read theo Word/Bit device)
│   │   └── PlcGroupWriter.cs       (Read-Modify-Write trên Word device)
│   ├── Subscription
│   │   └── PlcSubscriptionManager.cs (polling + OnValueChanged + deadband)
│   └── Tags
│       ├── PlcAddressParser.cs     (D100 / D100.5 / M50 / X1A / Y20 / ZR1000 / B1F0)
│       ├── PlcDataType.cs
│       ├── PlcDeviceCode.cs
│       └── PlcTag.cs
│
└── McProtocolScada.WinFormsTest (.NET 8 WinForms demo)
    ├── Form1.cs / .Designer.cs
    ├── Program.cs
    └── tags.json
```

## Features

- Kết nối qua IP / hostname (cả hostname động kiểu DDNS đều OK)
- Hỗ trợ nhiều khung MC Protocol: **QnA-3E Binary**, **QnA-3E ASCII**, **A1E Binary**, **A1E ASCII**, **iQ-R Binary (4E)**
- Auto reconnect + Watchdog
- Group Read / Write theo device (giảm số lượt request – chuẩn SCADA)
- Hỗ trợ đầy đủ word device: **D, W, R, ZR, SD, SW**
- Hỗ trợ đầy đủ bit device : **M, X, Y, B, F, L, S, SM, SB**
- Bit-in-word: **D100.5**, **W10.A** (Read-Modify-Write tự động)
- Async Read / Write
- Tag subscription (event `OnValueChanged`)
- Deadband cho `Real` / `LReal`
- Scaling per tag (Gain × Raw + Offset, làm tròn `NumDecimal`)
- WinForms ready, Sqlite Historian sẵn

## Supported Data Types

`Bool, Byte, Word, DWord, Int, DInt, UInt, UDInt, Real, LReal, String, Char, LInt, ULInt, LWord`

## Mapping địa chỉ Mitsubishi

| PLC          | Mô tả                          | Ví dụ      | Radix offset |
|--------------|--------------------------------|------------|--------------|
| **D**        | Data register (word)           | `D100`     | DEC          |
| **W**        | Link register (word)           | `W30`      | DEC          |
| **R / ZR**   | File register / Extended FR    | `R100`, `ZR1000` | DEC    |
| **SD / SW**  | Special data / link register   | `SD0`      | DEC          |
| **M**        | Internal relay (bit)           | `M50`      | DEC          |
| **L / F / S**| Latch / Annunciator / Step     | `L100`     | DEC          |
| **SM**       | Special internal relay         | `SM400`    | DEC          |
| **X / Y**    | Input / Output (bit)           | `X1A`, `Y20` | **HEX**    |
| **B / SB**   | Link relay (bit)               | `B1F0`     | **HEX**      |
| **D100.5**   | Bit thứ 5 trong word D100      | (chỉ Bool) | DEC          |

## Mapping kiểu PLC → C#

| PLC                 | C#       | Bytes |
|---------------------|----------|-------|
| Bool                | bool     | 1 bit |
| Byte / USInt        | byte     | 1     |
| SInt                | sbyte    | 1     |
| Char                | char     | 1     |
| Word / UInt         | ushort   | 2     |
| Int                 | short    | 2     |
| DWord / UDInt       | uint     | 4     |
| DInt                | int      | 4     |
| LWord / ULInt       | ulong    | 8     |
| LInt                | long     | 8     |
| Real                | float    | 4     |
| LReal               | double   | 8     |
| String              | string   | n     |

> Mitsubishi 32-bit / 64-bit / Real lưu **lower word ở địa chỉ thấp** (D100 = low,
> D101 = high). HslCommunication `ByteTransform` xử lý đúng tự động.

## Quick start

```csharp
using McProtocolClientLib.Core;
using McProtocolClientLib.IO;
using McProtocolClientLib.Subscription;
using McProtocolClientLib.Tags;

// 1) Kết nối PLC
var plc = new PlcClient(
    host: "192.168.1.10",
    port: 6000,
    frameType: McFrameType.QnA3E_Binary
);
await plc.ConnectAsync();
plc.StartWatchdog(2000);

// 2) Khai báo tag
var tags = new List<PlcTag>
{
    new() { Name="ScaleValue", Address="D20", DataType=PlcDataType.Real, Deadband=0.01, NumDecimal=2 },
    new() { Name="IsCheck",    Address="M100", DataType=PlcDataType.Bool },
    new() { Name="BoxId",      Address="D100", DataType=PlcDataType.String, StringLength=32 },
    new() { Name="AlarmBit",   Address="D40.5", DataType=PlcDataType.Bool }, // bit-in-word
};

// 3) Read/Write group
var reader = new PlcGroupReader(plc);
await reader.ReadGroupAsync(tags);

var writer = new PlcGroupWriter(plc);
tags.First(t => t.Name == "IsCheck").NewValue = true;
await writer.WriteGroupAsync(new[] { tags.First(t => t.Name == "IsCheck") });

// 4) Subscription (polling + event change)
var sub = new PlcSubscriptionManager(reader);
sub.OnValueChanged += t => Console.WriteLine($"{t.Name} = {t.NewValue}");
sub.Subscribe(tags, intervalMs: 200);
```

## File `tags.json` mẫu

```json
[
  {
    "Name": "PLC_1",
    "Host": "192.168.1.10",
    "Port": 6000,
    "FrameType": "QnA3E_Binary",
    "Network": 0,
    "Station": 255,
    "Tags": [
      { "Name": "ScaleValue", "Address": "D20",  "DataType": "Real", "Deadband": 0.01, "NumDecimal": 2 },
      { "Name": "IsCheck",    "Address": "M100", "DataType": "Bool" },
      { "Name": "BoxIdScale", "Address": "D100", "DataType": "String", "Length": 32 },
      { "Name": "Output1",    "Address": "Y10",  "DataType": "Bool" },
      { "Name": "AlarmBit",   "Address": "D40.5","DataType": "Bool" }
    ]
  }
]
```

## So sánh với bản Snap7 (Siemens)

| Khía cạnh         | Snap7 (Siemens)                | MC Protocol (Mitsubishi)        |
|-------------------|--------------------------------|---------------------------------|
| Driver lib        | `Sharp7`                       | `HslCommunication`              |
| Endian word       | Big-endian                     | Little-endian (DCBA cho 32/64)  |
| Địa chỉ           | `DB1.DBW0`, `DB1.DBX2.0`       | `D100`, `M100`, `D100.5`        |
| String layout     | `MaxLen + CurLen + Data`       | ASCII liên tục, 2 char/word     |
| Bit-in-word write | DBRead → mask → DBWrite        | Read → mask → Write             |
| Port              | 102 (TSAP)                     | 6000-6019 (3E) / 5006 (4E)      |
| Multi-rack        | rack/slot                      | network/station                 |

## Ghi chú quan trọng (RẤT QUAN TRỌNG)

1. **Luôn Read trước khi Write** trên word device — tránh phá bit/byte khác (đặc biệt khi
   `tag.DataType = Bool` dạng `D100.5`). Lớp `PlcGroupWriter` đã làm sẵn.
2. **Bit device write** (`M`, `X`, `Y`, `B` …) có thể ghi 1 bit trực tiếp, không cần RMW.
3. **Hostname** sẽ được resolve bằng DNS lúc Connect → có thể dùng tên DDNS.
4. **Watchdog** dùng `Read("D0",1)` rất nhẹ → an toàn cho mọi PLC.
5. **String** Mitsubishi không có header. `StringLength` = số ký tự ASCII, sẽ được pad
   `\0` cho đủ word. Khi đọc về sẽ tự cắt ở `\0` đầu tiên.
6. **Endian** xử lý tự động qua `IByteTransform` của HslCommunication — không tự code lại.

## Yêu cầu môi trường

- .NET SDK 8.0+
- NuGet: `HslCommunication`, `Microsoft.Data.Sqlite`, `Newtonsoft.Json`
- PLC Mitsubishi đã bật **MC Protocol** trên cổng Ethernet (cấu hình tại
  *GX Works2/3 → Parameter → Built-in Ethernet Port → Open Setting*)
