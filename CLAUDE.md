# CLAUDE.md — Project Intelligence File
> Đọc file này **trước tiên** mỗi khi bắt đầu làm việc với project.  
> Dành cho: Claude Code · Claude Cowork · Cursor · Copilot  
> Cập nhật lần cuối: xem `## CHANGELOG`

---

## 📌 MỤC LỤC

1. [Project Overview](#1-project-overview)
2. [Architecture Manual](#2-architecture-manual)
3. [Coding Standards & Comment Rules](#3-coding-standards--comment-rules)
4. [Session Memory & Context Linking](#4-session-memory--context-linking)
5. [Changelog — Edit Log](#5-changelog--edit-log)
6. [Unit Test Guidelines](#6-unit-test-guidelines)
7. [Performance Optimization Rules](#7-performance-optimization-rules)
8. [How to Use This File](#8-how-to-use-this-file)

---

## 1. PROJECT OVERVIEW

```yaml
project_name:     "202600120_Snap7 — SCADA PLC Driver Library"
version:          "0.1.0"
language:         "C#"
framework:        ".NET 8 / .NET Standard 2.0"
package_manager:  "NuGet"
primary_author:   "NDCongSP"
repo:             "https://github.com/NDCongSP/202600120_Snap7"
env:              "development"
branch_active:    "PLC_Mitsubíhi_MC_Protocol"
```

### Mục tiêu
Xây dựng thư viện SCADA driver cho PLC:
- **Snap7ScadaSolution** — Siemens S7-1200/1500 qua Profinet (dùng `Sharp7`)
- **McProtocolScadaSolution** — Mitsubishi Q/L/iQ-R/FX qua MC Protocol SLMP (dùng `HslCommunication`)

Cả hai đều có cấu trúc giống nhau: `PlcClient` → `PlcGroupReader/Writer` → `PlcSubscriptionManager` → WinForms demo.

### Ràng buộc quan trọng
- Không tự code endian — dùng `IByteTransform` của HslCommunication (Mitsubishi) hoặc `S7.GetXxxAt()` (Siemens)
- Luôn `ReadGroup` trước khi `WriteGroup` trên word device để tránh mất bit khác
- Không commit connection string / PLC IP thật vào code (dùng `tags.json` config)
- `PlcGroupWriter` đã handle Read-Modify-Write cho bit-in-word — không tự implement lại

---

## 2. ARCHITECTURE MANUAL

### 2.1 Sơ đồ thư mục

```
202600120_Snap7/
├── Snap7ScadaSolution/                  # Driver Siemens S7 (DONE ✅)
│   ├── Snap7Scada.Core/                 # .NET Standard 2.0 Class Library
│   │   ├── Core/        PlcClient, PlcManager
│   │   ├── IO/          PlcGroupReader, PlcGroupWriter
│   │   ├── Tags/        PlcTag, PlcAddressParser, PlcDataType
│   │   ├── Subscription/ PlcSubscriptionManager
│   │   ├── Diagnostics/  PlcDiagnostics
│   │   └── Historian/   SqliteHistorian
│   └── Snap7Scada.WinFormsTest/         # .NET 8 WinForms demo
│
├── McProtocolScadaSolution/             # Driver Mitsubishi MC Protocol (IN PROGRESS 🔧)
│   ├── McProtocolScada.Core/            # .NET Standard 2.0 Class Library
│   │   ├── Core/        PlcClient.cs, PlcConnectionState.cs, PlcManager.cs
│   │   ├── IO/          PlcGroupReader.cs, PlcGroupWriter.cs
│   │   ├── Tags/        PlcTag.cs, PlcAddressParser.cs, PlcDataType.cs, PlcDeviceCode.cs
│   │   ├── Subscription/ PlcSubscriptionManager.cs
│   │   ├── Diagnostics/  PlcDiagnostics.cs
│   │   ├── Historian/   SqliteHistorian.cs
│   │   └── Config/      PlcConfigLoader.cs
│   └── McProtocolScada.WinFormsTest/   # .NET 8 WinForms demo
│       ├── Form1.cs / .Designer.cs
│       ├── tags.json                   # cấu hình PLC + danh sách tag
│       └── tags1.json
│
├── Snap7Client/                         # Thư mục release lib Snap7
├── EasyDriver_SSFG_fGE.json
├── Mitsubishi MC Protocol.docx          # Tài liệu giao thức
└── CLAUDE.md
```

### 2.2 Luồng dữ liệu

```
[WinForms Form1]
      │ load tags.json → PlcManager.LoadFromConfig()
      ▼
[PlcManager]  →  PlcRuntime { Client, Reader, Writer, Tags }
      │
      ▼
[PlcClient]  ←── HslCommunication (Mitsubishi) / Sharp7 (Siemens)
      │  ConnectAsync / Watchdog / Reconnect
      ▼
[PlcGroupReader / PlcGroupWriter]
      │  ReadGroupAsync / WriteGroupAsync (batch)
      ▼
[PlcSubscriptionManager]
      │  polling timer → OnValueChanged event
      ▼
[UI / Historian]
```

### 2.3 Quy tắc phân tầng

| Layer              | Được phép import          | Không được import   |
|--------------------|---------------------------|----------------------|
| WinForms (Form1)   | Core, IO, Tags, Sub, Config | không giới hạn UI   |
| PlcSubscriptionManager | PlcGroupReader, Tags  | Core trực tiếp       |
| PlcGroupReader/Writer | PlcClient, Tags        | Subscription, UI     |
| PlcClient          | Tags (PlcTag)             | IO, Subscription     |
| PlcAddressParser   | PlcDeviceCode, PlcDataType | mọi layer khác       |

### 2.4 MC Protocol — Mitsubishi address mapping

| Device | Mô tả                    | Ví dụ          | Radix    |
|--------|--------------------------|----------------|----------|
| D      | Data register (word)     | `D100`         | DEC      |
| W      | Link register (word)     | `W30`          | DEC      |
| R / ZR | File register            | `R100`, `ZR1000`| DEC     |
| M      | Internal relay (bit)     | `M100`         | DEC      |
| X / Y  | I/O (bit)                | `X1A`, `Y20`   | **HEX**  |
| B / SB | Link relay (bit)         | `B1F0`         | **HEX**  |
| D100.5 | Bit-in-word              | `D100.5`       | DEC      |

### 2.5 Frame types (McFrameType enum)

| Enum             | Mô tả                        | Port mặc định |
|------------------|------------------------------|---------------|
| QnA3E_Binary     | Q/L/iQ-R binary (mặc định)   | 6000          |
| QnA3E_Ascii      | Q/L/iQ-R ASCII               | 6001          |
| A1E_Binary       | Series A cũ, binary          | 5006          |
| A1E_Ascii        | Series A cũ, ASCII           | 5006          |
| iQR_Binary       | iQ-R frame 4E                | 6000          |

### 2.6 ADR — Architecture Decision Records

| ID    | Ngày       | Quyết định                                    | Lý do                            | Trạng thái |
|-------|------------|-----------------------------------------------|----------------------------------|------------|
| ADR-1 | 2026-06-01 | Dùng HslCommunication thay tự code MC Protocol | Đã test, stable, hỗ trợ đủ frame | Accepted   |
| ADR-2 | 2026-06-01 | Port 1-1 cấu trúc từ Snap7Solution            | Giữ API nhất quán 2 driver       | Accepted   |
| ADR-3 | 2026-06-01 | PlcGroupWriter tự RMW cho bit-in-word          | Tránh lỗi mất bit khác           | Accepted   |
| ADR-4 | 2026-06-01 | tags.json config thay hardcode IP              | Dễ deploy, không commit IP thật  | Accepted   |

---

## 3. CODING STANDARDS & COMMENT RULES

### 3.1 Cấu trúc comment bắt buộc

#### File header
```csharp
/// <summary>
/// Mô tả class.
/// </summary>
/// <remarks>
/// File: McProtocolScada.Core/IO/PlcGroupReader.cs
/// Created: YYYY-MM-DD | Modified: YYYY-MM-DD
/// </remarks>
```

#### Method
```csharp
/// <summary>
/// Đọc nhóm tag theo batch, phân nhóm theo device (D, M, X...).
/// </summary>
/// <param name="tags">Danh sách tag cần đọc.</param>
/// <returns>Task hoàn thành khi tất cả tag đã được cập nhật Value.</returns>
/// <exception cref="PlcReadException">Khi PLC ngắt kết nối trong lúc đọc.</exception>
public async Task ReadGroupAsync(IEnumerable<PlcTag> tags) { ... }
```

#### Inline — chỉ khi logic KHÔNG tự giải thích
```csharp
// ✅ Đúng: giải thích tại sao
var wordCount = (StringLength + 1) / 2; // Mitsubishi: 2 ASCII char/word, làm tròn lên

// ❌ Sai: lặp lại code
var wordCount = (StringLength + 1) / 2; // chia StringLength+1 cho 2
```

#### TODO / FIXME
```csharp
// TODO(ndcong, 2026-06-03): Thêm test cho PlcAddressParser với địa chỉ HEX (X1A, B1F0)
// FIXME(ndcong, 2026-06-03): Watchdog có thể gây deadlock khi cùng lock SyncLock
```

### 3.2 Naming conventions

| Loại           | Convention        | Ví dụ                         |
|----------------|-------------------|-------------------------------|
| Class/Interface | PascalCase        | `PlcGroupReader`, `IPlcClient`|
| Method          | PascalCase verb   | `ReadGroupAsync`, `Connect`   |
| Property        | PascalCase        | `State`, `SyncLock`           |
| Private field   | `_camelCase`      | `_client`, `_watchdogCts`     |
| Constant        | UPPER_SNAKE_CASE  | `MAX_RETRY`, `DEFAULT_PORT`   |
| File            | PascalCase.cs     | `PlcGroupReader.cs`           |
| Enum value      | PascalCase        | `QnA3E_Binary`, `Disconnected`|

### 3.3 Code style

```
- Indent: 4 spaces
- Max line: 120 chars
- async/await — KHÔNG dùng .Result / .Wait() (deadlock risk)
- lock(SyncLock) trước mọi thao tác HslCommunication
- Không throw exception từ event handler (bọc try/catch)
- Dùng cancellationToken cho mọi async loop dài
```

---

## 4. SESSION MEMORY & CONTEXT LINKING

### 4.1 Active Context — Việc đang làm

```yaml
active_context:
  current_task: >
    Session 11: Bỏ HOÀN TOÀN dependency HslCommunication khỏi project (yêu cầu user: "BO LUON
    THU VIEN HslCommunication"). PackageReference đã được gỡ khỏi McProtocolScada.Lib.csproj
    từ trước (code không còn compile được) — task này viết lại các phần còn phụ thuộc:
    (1) OperateResult.cs (mới) — OperateResult/OperateResult<T> tự viết thay HslCommunication.OperateResult;
    (2) ByteTransform.cs (mới) — IByteTransform/RegularByteTransform tự viết. Đã XÁC NHẬN BẰNG THỰC
    NGHIỆM (chạy RegularByteTransform thật của HslCommunication 11.6.4 qua probe project tham chiếu
    trực tiếp DLL trong NuGet cache) rằng DataFormat.DCBA = little-endian thuần, KHÔNG hoán đổi byte/word
    nào — nên class tự viết chỉ cần delegate sang BitConverter, không cần enum DataFormat (ABCD/BADC/CDAB
    không bao giờ được dùng trong project này);
    (3) Mc3EBinaryClient.cs — bỏ `using HslCommunication`/`HslCommunication.Core`, ByteTransform giờ
    là `new RegularByteTransform()` (tự viết, không cần DataFormat tham số vì chỉ có 1 hành vi);
    (4) PlcGroupReader.cs/PlcGroupWriter.cs — bỏ `using HslCommunication`, cast IByteTransform giờ
    trỏ về type tự viết trong McProtocolClientLib.Core;
    (5) PlcClient.cs — bỏ `using HslCommunication.Profinet.Melsec`; CreateClient() chỉ còn case
    QnA3E_Binary (Mc3EBinaryClient); 4 frame type còn lại (ASCII/A1E/iQR) — đã xác nhận KHÔNG dùng
    với hardware hiện tại — throw NotSupportedException rõ ràng thay vì âm thầm sai, vì chưa có
    bản raw-TCP thay thế cho các frame đó.
    Build 0 error/0 warning, 97/97 test PASS (không cần sửa test vì hành vi IByteTransform giữ nguyên).
  branch:         "PLC_Mitsubishi_MC_Protocol_Dev"
  plc_hardware:
    series:        "Mitsubishi MELSEC-Q"
    model:         "Q06UDV (QCPU Q mode)"
    ip:            "192.168.11.1"
    port:          8000
    frame:         "QnA3E_Binary (Binary Code)"
    station:       0
    note:          "Port 8000 = MC Protocol (Open Setting Line 2). Station=0 (=0xFF gây timeout). Pingable dùng ICMP tránh làm đầy connection table Q series (~8 slots)."
  related_files:
    - "McProtocolScadaSolution/McProtocolScada.Core/Core/PlcClient.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Core/Mc3EBinaryClient.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Core/OperateResult.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Core/ByteTransform.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/IO/PlcGroupReader.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/IO/PlcGroupWriter.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Diagnostics/PlcLogger.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Tags/PlcAddressParser.cs"
    - "McProtocolScadaSolution/McProtocolScada.WinFormsTest/Form1.cs"
    - "McProtocolScadaSolution/McProtocolScada.WinFormsTest/tags.json"
    - "McProtocolScadaSolution/McProtocolScada.Tests/Mc3EBinaryClientTests.cs"
  verified_ok:
    - "Build: 0 error 0 warning (2026-06-16, sau khi bỏ HslCommunication hoàn toàn)"
    - "Unit tests: 97/97 PASS — không cần sửa test, hành vi IByteTransform tự viết giữ nguyên 100%"
    - "RegularByteTransform tự viết đã xác nhận khớp byte-for-byte với HslCommunication thật (DataFormat.DCBA = little-endian thuần) qua probe project thực nghiệm"
    - "BuildBatchReadRequest(D162) khớp byte-for-byte với capture thật từ PLC (TestRawAsync, Q06UDV 192.168.11.1:8000)"
    - "ROOT CAUSE license limit (Session 9/DEC-011) ĐÃ XỬ LÝ TRIỆT ĐỂ — project không còn 1 dòng code nào gọi HslCommunication (PackageReference đã gỡ khỏi mọi .csproj)"
  next_steps:
    - "1. ƯU TIÊN CAO: test Mc3EBinaryClient với PLC thật Q06UDV — chạy app, đọc liên tục nhiều giờ,"
    - "   xác nhận KHÔNG còn 'System authorization failed' và watchdog reconnect hoạt động đúng"
    - "2. Verify Write (word D-register) hoạt động đúng trên PLC thật — chỉ Read đã được verify qua TestRawAsync trước đây"
    - "3. RỦI RO CHƯA KIỂM CHỨNG: device code byte cho M/X/Y/B/W/R/ZR/SD/SW/SM/SB/L/F/S (chỉ D=0xA8 đã verify"
    - "   thực tế) và bit-packing 2-điểm/byte cho ReadBool/Write(bool) — lấy từ tài liệu chuẩn, CHƯA test hardware."
    - "   → Test kỹ các tag bit (StepRun, M-device nếu có) trước khi tin tưởng production"
    - "4. Frame ASCII/A1E/iQR giờ throw NotSupportedException (không còn HslCommunication để fallback) —"
    - "   nếu cần dùng thật thì phải viết raw-TCP client riêng cho từng frame đó (chưa làm, không cấp thiết)"
    - "5. Test Write từ ComboBox dạng 'PLC_1:Part' → verify PLC nhận đúng"
    - "6. Verify plc_history.db ghi dữ liệu cho cả 3 PLC"
  last_session:   "2026-06-16"
  open_questions:
    - "Device code byte ngoài D (0xA8) — đúng theo tài liệu chuẩn nhưng CHƯA verify trên Q06UDV thật"
    - "Bit-packing nibble (low=chẵn, high=lẻ) cho M/X/Y/B — CHƯA verify trên hardware, chỉ có round-trip unit test logic"
    - "ASCII/A1E/iQR frame: throw NotSupportedException — nếu sau này cần dùng, phải viết raw-TCP client riêng (không còn HslCommunication để dùng tạm)"
  simulator_note: >
    GX Simulator (GX Works2 built-in) chỉ nhận MELSOFT connection, KHÔNG hỗ trợ MC Protocol
    từ HslCommunication. Phải dùng PLC thật Q06UDV hoặc MC Protocol Simulator bên thứ ba.
```

### 4.2 Decision Log

| ID      | Ngày       | Quyết định                                      | File liên quan                    |
|---------|------------|-------------------------------------------------|-----------------------------------|
| DEC-001 | 2026-06-01 | Dùng HslCommunication cho MC Protocol           | `Core/PlcClient.cs`               |
| DEC-002 | 2026-06-01 | lock(SyncLock) bao quanh mọi HslComm call       | `Core/PlcClient.cs`, `IO/*.cs`    |
| DEC-003 | 2026-06-01 | PlcGroupWriter tự RMW — không để caller lo      | `IO/PlcGroupWriter.cs`            |
| DEC-004 | 2026-06-07 | ~~Watchdog Read("D0")~~ → **ICMP ping** để check alive; KHÔNG tạo TCP vào port MC Protocol (Q series giới hạn ~8 connection slots) | `Core/PlcClient.cs` |
| DEC-005 | 2026-06-07 | String Mitsubishi: LOW_BYTE=ký tự đầu, KHÔNG swap byte | `IO/PlcGroupReader.cs`, `IO/PlcGroupWriter.cs` |
| DEC-006 | 2026-06-07 | String tag: Length=chars (10 word×2char=Length 20), tương tự Snap7 | `tags.json` |
| DEC-007 | 2026-06-07 | Station=0 trong tags.json (NetworkStationNumber=0 cho Q series direct Ethernet); Station=255 gây timeout | `tags.json`, `Core/PlcClient.cs` |
| DEC-008 | 2026-06-12 | ~~Connect() luôn recreate HslComm instance (_client mutable)~~ — **REVERTED bởi DEC-011** | `Core/PlcClient.cs` |
| DEC-009 | 2026-06-12 | PlcGroupReader KHÔNG gọi EnsureConnected() — watchdog là sole reconnect, tránh concurrent Connect() flood | `Core/PlcClient.cs`, `IO/PlcGroupReader.cs` |
| DEC-011 | 2026-06-16 | **REVERT DEC-008**: _client readonly, KHÔNG recreate. Bằng chứng từ log thật (plc_20260616.log): "System authorization failed... Active device number" tăng dần liên tục (~1780 lần) bất kể recreate instance bao nhiêu lần → lỗi là **license limit của HslCommunication ở mức process**, không phải state riêng của 1 instance. Recreate chỉ làm cạn counter nhanh hơn. Thêm `PlcClient.IsAuthorizationError()` để nhận diện lỗi này riêng + `IsLicenseLimited` property + watchdog backoff 60s (thay vì retry 3s vô nghĩa khi đã biết là lỗi license, không phải lỗi mạng tạm thời) | `Core/PlcClient.cs`, `WinFormsTest/Form1.cs` |
| DEC-010 | 2026-06-15 | Watchdog dùng SemaphoreSlim.WaitAsync thay Task.Delay → NotifyError() wake watchdog ngay, không đợi interval | `Core/PlcClient.cs` |
| DEC-012 | 2026-06-16 | Viết `Mc3EBinaryClient` (raw TCP tự cài đặt QnA-3E Binary frame) thay `HslCommunication.MelsecMcNet` cho frame QnA3E_Binary — loại bỏ HOÀN TOÀN giới hạn license process-level (DEC-011) vì không còn gọi HslCommunication trên đường dữ liệu chính. API khớp 1-1 (Read/ReadBool/Write/ConnectClose/ByteTransform) nên PlcGroupReader/Writer không cần sửa. Giữ ByteTransform=RegularByteTransform(DataFormat.DCBA) khớp default MelsecMcNet. Frame đã verify byte-for-byte với capture PLC thật (D162); device code ngoài D và bit-packing CHƯA verify hardware. Các frame ASCII/A1E/iQR (không dùng) vẫn giữ HslCommunication | `Core/Mc3EBinaryClient.cs` (mới), `Core/PlcClient.cs` |
| DEC-013 | 2026-06-16 | Bỏ HOÀN TOÀN dependency HslCommunication khỏi project (yêu cầu user). Viết `OperateResult`/`OperateResult<T>` và `IByteTransform`/`RegularByteTransform` tự có (POCO đơn giản). Xác nhận bằng thực nghiệm (chạy thật `RegularByteTransform` của HslCommunication 11.6.4 qua probe project tham chiếu DLL trong NuGet cache) rằng `DataFormat.DCBA` = little-endian thuần, không hoán đổi byte/word — nên bản tự viết bỏ luôn enum DataFormat, chỉ delegate sang `BitConverter`. `PlcClient.CreateClient()` chỉ còn case QnA3E_Binary; 4 frame ASCII/A1E/iQR (xác nhận không dùng) giờ throw `NotSupportedException` vì không còn HslCommunication để dùng tạm. Build 0 error/0 warning, 97/97 test PASS không cần sửa | `Core/OperateResult.cs` (mới), `Core/ByteTransform.cs` (mới), `Core/Mc3EBinaryClient.cs`, `Core/PlcClient.cs`, `IO/PlcGroupReader.cs`, `IO/PlcGroupWriter.cs` |

### 4.3 Hướng dẫn Claude đọc context

Khi bắt đầu session mới, Claude PHẢI:
1. Đọc `active_context` → biết đang làm gì
2. Đọc `CHANGELOG` gần nhất → biết đã thay đổi gì
3. Đọc `Decision Log` → tránh đề xuất lại phương án đã bác bỏ
4. **Không** hỏi lại những gì đã ghi trong file này

---

## 5. CHANGELOG — EDIT LOG

> Format: `[YYYY-MM-DD] [TYPE] [File/Module] — Mô tả`  
> Types: `FEAT` · `FIX` · `REFACTOR` · `PERF` · `TEST` · `DOCS` · `CHORE` · `BREAK`

---

### [2026-06-16] — Session 11 (Bỏ HOÀN TOÀN dependency HslCommunication)

```
[FEAT]  McProtocolScada.Core/Core/OperateResult.cs          — NEW: OperateResult/OperateResult<T> tự viết thay HslCommunication.OperateResult (IsSuccess/Message/ErrorCode/Content + CreateSuccessResult)
[FEAT]  McProtocolScada.Core/Core/ByteTransform.cs           — NEW: IByteTransform/RegularByteTransform tự viết, hành vi = DataFormat.DCBA (little-endian thuần, không enum DataFormat vì chỉ dùng 1 hành vi)
[FIX]   McProtocolScada.Core/Core/Mc3EBinaryClient.cs        — Bỏ using HslCommunication/HslCommunication.Core; ByteTransform = new RegularByteTransform() (tự viết)
[FIX]   McProtocolScada.Core/IO/PlcGroupReader.cs            — Bỏ using HslCommunication; cast IByteTransform trỏ về type tự viết trong McProtocolClientLib.Core
[FIX]   McProtocolScada.Core/IO/PlcGroupWriter.cs            — Bỏ using HslCommunication; cast IByteTransform trỏ về type tự viết trong McProtocolClientLib.Core
[FIX]   McProtocolScada.Core/Core/PlcClient.cs               — Bỏ using HslCommunication.Profinet.Melsec; CreateClient() chỉ còn case QnA3E_Binary, 4 frame ASCII/A1E/iQR throw NotSupportedException (DEC-013)
[CHORE] McProtocolScadaSolution/README.md                    — Cập nhật bảng so sánh + yêu cầu môi trường: không còn NuGet HslCommunication
[BUILD] Build 0 error 0 warning — 2026-06-16
[TEST]  97/97 tests PASS (không cần sửa test) — 2026-06-16
[DOCS]  CLAUDE.md                                            — Thêm DEC-013, active_context, CHANGELOG Session 11
[VERIFY] RegularByteTransform tự viết xác nhận khớp byte-for-byte với HslCommunication 11.6.4 thật qua probe project tham chiếu trực tiếp DLL trong NuGet cache (~/.nuget/packages/hslcommunication)
[RISK]  Frame ASCII/A1E/iQR (không dùng với hardware hiện tại) giờ KHÔNG còn hoạt động được (throw NotSupportedException) — cần viết raw-TCP riêng nếu sau này cần dùng
```

---

### [2026-06-16] — Session 10 (Raw TCP MC Protocol — loại bỏ HslCommunication license dependency)

```
[FEAT]  McProtocolScada.Core/Core/Mc3EBinaryClient.cs       — NEW: raw TCP client tự cài đặt MC Protocol QnA-3E Binary (Read/ReadBool/Write/ConnectClose/ByteTransform khớp API MelsecMcNet)
[FEAT]  McProtocolScada.Core/Core/Mc3EBinaryClient.cs       — TryParseAddress: parse D/W/R/ZR/SD/SW/M/L/F/S/SM/X/Y/B/SB, dùng PlcDeviceCode.Resolve() cho radix (khớp hành vi hiện có, vd W=DEC)
[FEAT]  McProtocolScada.Core/Core/Mc3EBinaryClient.cs       — BuildBatchReadRequest/BuildBatchWriteRequest/BuildFrame: command 0x0401 (read)/0x1401 (write), subcommand word/bit units, frame QnA-3E Binary đầy đủ
[FEAT]  McProtocolScada.Core/Core/Mc3EBinaryClient.cs       — SendReceive: short-connection TCP (mở/đóng mỗi request) giữ đúng convention Session 3, đọc header 9 byte rồi đọc đủ body theo dataLength
[FEAT]  McProtocolScada.Core/Core/Mc3EBinaryClient.cs       — ByteTransform = RegularByteTransform(DataFormat.DCBA) — khớp default MelsecMcNet (xác nhận qua reflection) để không phá decode DWord/Real/LReal
[FIX]   McProtocolScada.Core/Core/PlcClient.cs              — CreateClient(): case QnA3E_Binary đổi từ MelsecMcNet sang Mc3EBinaryClient (DEC-012); các frame ASCII/A1E/iQR (không dùng) giữ HslCommunication
[TEST]  McProtocolScada.Tests/Mc3EBinaryClientTests.cs      — NEW: 27 test case — frame encoding byte-exact match với capture PLC thật (D162), address parsing 16 device type, bit-packing nibble round-trip
[BUILD] Build 0 error 0 warning — 2026-06-16
[TEST]  97/97 tests PASS — 2026-06-16
[DOCS]  CLAUDE.md                                           — Cập nhật active_context, DEC-012, CHANGELOG Session 10
[RISK]  Device code byte ngoài D (0xA8) và bit-packing nibble CHƯA verify trên PLC thật — chỉ D-register word read đã confirm qua TestRawAsync gốc
```

---

### [2026-06-16] — Session 9 (ROOT CAUSE CONFIRMED: HslCommunication license limit — revert DEC-008)

```
[FIX]   McProtocolScada.Core/Core/PlcClient.cs              — REVERT DEC-008: _client lại thành readonly, KHÔNG recreate instance mỗi Connect()
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs              — Thêm IsAuthorizationError(string?) static — nhận diện "System authorization failed" (HslCommunication license error)
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs              — Thêm IsLicenseLimited property — phân biệt lỗi license vs lỗi mạng/PLC thật cho UI/watchdog
[FIX]   McProtocolScada.Core/Core/PlcClient.cs              — Watchdog: backoff 60s khi _licenseLimited=true (thay vì retry 3s vô nghĩa — lỗi license không tự sửa bằng retry nhanh)
[FIX]   McProtocolScada.WinFormsTest/Form1.cs               — lblStatus hiển thị "(LICENSE LIMIT)" khi client.IsLicenseLimited=true, giúp operator phân biệt lỗi license vs lỗi PLC
[TEST]  McProtocolScada.Tests/PlcClientStateTests.cs        — Thêm 9 test: IsAuthorizationError (7 case message khác nhau) + Connect_ToNonExistentHost_IsNotLicenseLimited
[DOCS]  CLAUDE.md                                            — DEC-011 (revert DEC-008 với bằng chứng log thật), active_context: root cause confirmed = library/process-level license limit, KHÔNG phải PLC/mạng
[BUILD] Build 0 error 0 warning — 2026-06-16
[TEST]  70/70 tests PASS — 2026-06-16
[ROOT CAUSE] Log thực tế plc_20260616.log: "System authorization failed... Active device number: 12687" tăng dần liên tục
             (~1780 lần fail) trên CẢ 3 PLC đồng thời, trong khi ping cả 3 PLC đều 0% packet loss (xác nhận qua ảnh user gửi).
             → KẾT LUẬN: lỗi 100% từ thư viện HslCommunication (free-tier license usage cap, process-wide),
             KHÔNG phải lỗi PLC hay lỗi mạng. DEC-008 (recreate instance) không sửa được lỗi này — chỉ làm
             counter cạn nhanh hơn vì mỗi instance mới vẫn tính vào cùng 1 counter process-level.
[PENDING] Cần quyết định từ user: (a) mua license HslCommunication, (b) thử downgrade version cũ, hoặc
          (c) viết raw TCP MC Protocol thay HslCommunication hoàn toàn (đã có PoC TestRawAsync() đọc D162 OK)
```

---

### [2026-06-15] — Session 8 (PlcLogger + watchdog wake-on-error + unit tests)

```
[FEAT]  McProtocolScada.Core/Diagnostics/PlcLogger.cs       — NEW: static thread-safe file logger → [AppBase]/logs/plc_YYYYMMDD.log
[FIX]   McProtocolScada.Core/Core/PlcClient.cs              — Watchdog: SemaphoreSlim _reconnectSignal thay Task.Delay → NotifyError() wake watchdog NGAY (không đợi 3s interval)
[FIX]   McProtocolScada.Core/Core/PlcClient.cs              — Log Connect() attempt + kết quả + res.Message của HslCommunication (chẩn đoán nguyên nhân reconnect fail)
[FIX]   McProtocolScada.Core/Core/PlcClient.cs              — Watchdog log state transition, consecutive failure count
[FIX]   McProtocolScada.Core/IO/PlcGroupReader.cs           — Log ReadGroup FAIL với exact error message khi HslCommunication trả về lỗi
[FIX]   McProtocolScada.WinFormsTest/Form1.cs               — StartWatchdog(10000) → StartWatchdog(3000): phát hiện mất kết nối nhanh hơn
[FEAT]  McProtocolScada.Core/McProtocolScada.Lib.csproj     — Thêm InternalsVisibleTo("McProtocolScada.Tests")
[TEST]  McProtocolScada.Tests/McProtocolScada.Tests.csproj  — NEW: xUnit test project (net8.0), thêm vào solution
[TEST]  McProtocolScada.Tests/PlcAddressParserTests.cs      — 44 test cases: D/M/X(HEX)/Y(HEX)/B(HEX)/ZR/D100.5/String/error cases
[TEST]  McProtocolScada.Tests/BlockSplitterTests.cs         — 11 test cases: gap/length split logic, boundary conditions, thực tế từ tags.json
[TEST]  McProtocolScada.Tests/PlcClientStateTests.cs        — 16 test cases: NotifyError state machine, watchdog lifecycle, wake-on-error
[DOCS]  CLAUDE.md                                           — Cập nhật active_context, DEC-010, CHANGELOG
[BUILD] Build 0 error 0 warning — 2026-06-15
[TEST]  61/61 tests PASS — 2026-06-15
```

---

### [2026-06-12] — Session 7 (Triệt để fix reconnect — recreate HslComm instance)

```
[FIX]   McProtocolScada.Core/Core/PlcClient.cs          — _client non-readonly: Connect() luôn recreate MelsecMcNet/... instance mới để xóa "System authorization failed" error state trong HslCommunication
[FIX]   McProtocolScada.Core/Core/PlcClient.cs          — _connectGuard SemaphoreSlim(1,1): ngăn concurrent Connect() calls (non-blocking Wait(0))
[REFACTOR] McProtocolScada.Core/Core/PlcClient.cs       — Watchdog đơn giản hóa: if(State!=Connected) → ConnectAsync(); không cần check Pingable riêng vì Connect() sẽ fail nhanh nếu host down
[FIX]   McProtocolScada.Core/Core/PlcClient.cs          — StartWatchdog default 10000→3000ms: phát hiện và recover mất kết nối nhanh hơn
[FIX]   McProtocolScada.Core/IO/PlcGroupReader.cs       — Bỏ EnsureConnected(): thay bằng State check đơn giản. Watchdog là sole reconnect. Tránh N×200ms subscription polls cùng flood Connect()
[DOCS]  CLAUDE.md                                        — Thêm DEC-008, DEC-009; cập nhật active_context, next_steps
[BUILD] Build 0 error 0 warning — 2026-06-12
```

---

### [2026-06-11] — Session 5 (Auto-reconnect fix + Multi-PLC support)

```
[FIX]   McProtocolScada.Core/Core/PlcClient.cs          — Watchdog: thêm check State==Error để reconnect ngay cả khi ICMP alive (fix mất kết nối không tự khôi phục)
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs          — Thêm NotifyError(): Reader/Writer gọi khi lỗi để watchdog kích hoạt reconnect
[FIX]   McProtocolScada.Core/IO/PlcGroupReader.cs       — ReadGroup catch: gọi _plc.NotifyError() khi đọc thất bại
[FEAT]  McProtocolScada.Core/Core/PlcManager.cs         — Thêm GetAllPlcNames() để enumerate tất cả PLC trong config
[FEAT]  McProtocolScada.WinFormsTest/tags.json          — Thêm PLC_2 (192.168.11.4:8000) và PLC_3 (192.168.11.5:8000) với cùng bộ tag
[REFACTOR] McProtocolScada.WinFormsTest/Form1.cs        — Refactor multi-PLC: Dictionary<plcName,(Runtime,Sub)>, connect song song Task.WhenAll
[FEAT]  McProtocolScada.WinFormsTest/Form1.cs           — listBox hiển thị "[PLC_1:Part]...", ComboBox Write dạng "PLC_1:Part"
[FEAT]  McProtocolScada.WinFormsTest/Form1.cs           — lblStatus hiển thị trạng thái cả 3 PLC: "PLC_1:Connected | PLC_2:Error | ..."
[BUILD] Build 0 error 0 warning — 2026-06-11
```

---

### [2026-06-07] — Session 4 (Cleanup debug code + SqliteHistorian wiring)

```
[CHORE] McProtocolScada.WinFormsTest/Form1.cs           — Xóa TestRawAsync debug MessageBox (production ready)
[FIX]   McProtocolScada.WinFormsTest/Form1.cs           — AttachTagDebug: sửa tag name "Step_Run"→"StepRun", "PartCode"→"Part" (khớp tags.json)
[FIX]   McProtocolScada.WinFormsTest/Form1.cs           — StepRun.ValueChanged: bọc null-check tránh NullReferenceException khi tag không tồn tại
[FEAT]  McProtocolScada.WinFormsTest/Form1.cs           — Wire SqliteHistorian: tạo _historian field, init "plc_history.db", ghi async trong Sub_OnValueChanged
[DOCS]  CLAUDE.md                                        — Cập nhật next_steps, Build: 0 error 0 warning xác nhận
```

---

### [2026-06-07] — Session 3 (Connection debug + reconnect fix + production test)

```
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs          — Connect(): bỏ ConnectServer(), dùng short-connection Read("D0") test
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs          — Pingable(): đổi từ HslComm Read sang ICMP ping (tránh làm đầy connection table Q series)
[FIX]   McProtocolScada.Core/Core/PlcClient.cs          — EnsureConnected(): fix trạng thái "Error" sau reconnect (gọi Connect() khi Pingable()=true && State!=Connected)
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs          — Thêm TestRawAsync() static method để debug raw socket MC Protocol
[CHORE] McProtocolScada.WinFormsTest/tags.json          — Cập nhật 19 tags mới: Part/D162, TimeRunStep1-15/D671-685, StepRun/D1953, PartName/D8020(String-16), RecipeSettingStep/D10520
[CHORE] McProtocolScada.WinFormsTest/tags.json          — Port đổi 5001→8000; Station đổi 255→0 (Request station No. = 0x00 cho Q series direct Ethernet)
[CHORE] McProtocolScada.WinFormsTest/Form1.cs           — Thêm TestRawAsync debug MessageBox (cần xóa khi production); Watchdog 2000→10000ms
[DOCS]  CLAUDE.md                                        — Cập nhật active_context + Decision Log; root cause documented
[CHORE] RESOLVE: Root cause timeout = Q series connection table ~8 slots lấp đầy bởi HslComm TCP watchdog fail; giải pháp = ICMP ping + watchdog 10s + PLC reset
[VERIFY] Read thành công: Part=546, PartName="CW180-WS-1", StepRun event fired, tất cả 19 tag OK
```

---

### [2026-06-07] — Session 2 (Build verify + code review + tags.json + PLC confirm)

```
[FIX]   McProtocolScada.WinFormsTest/Form1.cs          — Xóa field _historian chưa dùng (CS0169 warning)
[FIX]   McProtocolScada.Core/IO/PlcGroupReader.cs       — String decode: NO swap (LOW_BYTE=ký tự đầu, in-order đúng)
[FIX]   McProtocolScada.Core/IO/PlcGroupWriter.cs       — String encode: NO swap, in-order đúng
[CHORE] McProtocolScada.WinFormsTest/tags.json          — 42 tags D4000–D4067; PartName/Work gộp thành String×20
[DOCS]  CLAUDE.md  — Xác nhận PLC: Q06UDV, QnA3E_Binary, port 6000, IP 192.168.11.1
[DOCS]  CLAUDE.md  — Ghi chú: GX Simulator không hỗ trợ MC Protocol external; cần PLC thật
[DOCS]  CLAUDE.md  — Hướng dẫn Open Setting: line 3 = TCP + MC Protocol + port 6000
```

---

### [2026-06-03] — Session 1 (CLAUDE.md init)

```
[DOCS]  CLAUDE.md  — Khởi tạo project intelligence file, điền đầy đủ context thực tế
[DOCS]  CLAUDE.md  — Ghi active_context: task tiếp theo là chạy MC Protocol driver
```

---

### [2026-06-01] — Port từ Snap7Solution

```
[FEAT]  McProtocolScada.Core/Core/PlcClient.cs          — Port + adapt cho HslCommunication
[FEAT]  McProtocolScada.Core/IO/PlcGroupReader.cs        — Batch read theo device group
[FEAT]  McProtocolScada.Core/IO/PlcGroupWriter.cs        — RMW cho bit-in-word
[FEAT]  McProtocolScada.Core/Tags/PlcAddressParser.cs   — Parse D/M/X/Y/B/ZR/D100.5
[FEAT]  McProtocolScada.Core/Subscription/PlcSubscriptionManager.cs — Polling + event
[FEAT]  McProtocolScada.WinFormsTest/Form1.cs            — WinForms demo + tags.json
[DOCS]  McProtocolScadaSolution/README.md               — Tài liệu đầy đủ driver
```

---

## 6. UNIT TEST GUIDELINES

### 6.1 Ưu tiên test cho McProtocol

Các case cần test ngay (chưa có test file):

```csharp
// PlcAddressParser
PlcAddressParser.Parse("D100")    // → device=D, offset=100, isBit=false
PlcAddressParser.Parse("M50")     // → device=M, offset=50, isBit=true
PlcAddressParser.Parse("X1A")     // → device=X, offset=26 (HEX!), isBit=true
PlcAddressParser.Parse("B1F0")    // → device=B, offset=496 (HEX), isBit=true
PlcAddressParser.Parse("D100.5")  // → device=D, offset=100, bitIndex=5, isBit=true
PlcAddressParser.Parse("ZR1000")  // → device=ZR, offset=1000
```

### 6.2 Coverage tối thiểu

| Module              | Min Coverage | Ghi chú                        |
|---------------------|--------------|--------------------------------|
| PlcAddressParser    | 95%          | Pure logic — dễ test, quan trọng |
| PlcGroupReader      | 80%          | Mock HslCommunication          |
| PlcGroupWriter      | 85%          | Test RMW bit-in-word           |
| PlcClient           | 60%          | Khó test live, mock connect    |
| PlcSubscriptionManager | 75%       | Test event firing              |

### 6.3 Lệnh build & test

```bash
# Build solution
dotnet build McProtocolScadaSolution/McProtocolScadaSolution.slnx

# Chạy test (khi có test project)
dotnet test McProtocolScadaSolution/

# Build WinForms
dotnet build McProtocolScadaSolution/McProtocolScada.WinFormsTest/
```

---

## 7. PERFORMANCE OPTIMIZATION RULES

### 7.1 Quy tắc cho PLC driver

```
RULE-PERF-01: Batch read — gom tag cùng device vào 1 request thay vì đọc từng tag
RULE-PERF-02: Subscription interval tối thiểu 100ms — dưới đó PLC có thể từ chối
RULE-PERF-03: Deadband trên Real/LReal — tránh flood OnValueChanged với noise nhỏ
RULE-PERF-04: lock(SyncLock) — KHÔNG giữ lock trong async await (giải phóng trước await)
RULE-PERF-05: Watchdog read D0 (1 word) — KHÔNG đọc nhiều địa chỉ để check alive
```

### 7.2 Checklist trước khi release

```markdown
- [ ] Build Release không có warning
- [ ] PlcAddressParser test pass 100%
- [ ] WinForms demo connect + read + subscription OK với simulator hoặc PLC thật
- [ ] Memory leak check: Form closing → Dispose PlcClient → Watchdog dừng
- [ ] tags.json không chứa IP thật (dùng 192.168.1.10 placeholder)
```

---

## 8. HOW TO USE THIS FILE

### 8.1 Dành cho Claude

```
Khi đọc file này, Claude phải:
1. LUÔN đọc active_context trước khi code
2. TUÂN THỦ naming convention C# (PascalCase methods, _camelCase fields)
3. CẬP NHẬT active_context sau mỗi session
4. THÊM entry CHANGELOG mỗi khi sửa/thêm code đáng kể 
5. KHÔNG đề xuất lại kiến trúc đã có trong Decision Log
6. lock(SyncLock) mọi lúc gọi HslCommunication
7. KHÔNG dùng .Result / .Wait() — luôn async/await
```

### 8.2 Template prompt

```
# Tiếp tục task MC Protocol:
"Đọc CLAUDE.md active_context. Task: fix [vấn đề cụ thể].
File cần xem: McProtocolScada.Core/[file].cs
Sau khi xong, cập nhật active_context và CHANGELOG."

# Debug:
"Đọc CLAUDE.md section 4. Bug: [mô tả].
Expected: [hành vi đúng]. File: [X]."

# Review:
"Đọc CLAUDE.md section 3 coding standards.
Review file [X]. Liệt kê vi phạm: [Line] [Rule] [Gợi ý sửa]."
```

### 8.3 Maintenance

| Việc cần làm             | Tần suất    | Ai làm            |
|--------------------------|-------------|-------------------|
| Cập nhật active_context  | Mỗi session | Dev               |
| Thêm CHANGELOG entry     | Mỗi commit  | Dev               |
| Review Decision Log      | Khi có ADR mới | Tech lead      |
| Cập nhật test coverage   | Mỗi sprint  | Dev               |

---

> **Nguồn sự thật duy nhất** cho AI assistant làm việc với project này.  
> Khi mâu thuẫn giữa code và CLAUDE.md → **ưu tiên CLAUDE.md**, sau đó sửa code.

---
*CLAUDE.md · Updated by Claude Sonnet 4.6 · 2026-06-03*
