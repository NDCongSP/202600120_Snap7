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
    Làm cho McProtocolScadaSolution (Mitsubishi MC Protocol driver) chạy được hoàn chỉnh.
    Driver đã có đầy đủ cấu trúc (port từ Snap7), cần verify + fix để build pass
    và WinForms demo kết nối PLC thực tế chạy đúng.
  branch:         "PLC_Mitsubíhi_MC_Protocol"
  related_files:
    - "McProtocolScadaSolution/McProtocolScada.Core/Core/PlcClient.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/IO/PlcGroupReader.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/IO/PlcGroupWriter.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Tags/PlcAddressParser.cs"
    - "McProtocolScadaSolution/McProtocolScada.Core/Subscription/PlcSubscriptionManager.cs"
    - "McProtocolScadaSolution/McProtocolScada.WinFormsTest/Form1.cs"
    - "McProtocolScadaSolution/McProtocolScada.WinFormsTest/tags.json"
  next_steps:
    - "1. Build McProtocolScada.Core → fix compile errors nếu có"
    - "2. Kiểm tra PlcAddressParser với X/Y/B (HEX) và D100.5 (bit-in-word)"
    - "3. Kiểm tra PlcGroupReader batch read logic (grouping by device)"
    - "4. Kiểm tra PlcGroupWriter Read-Modify-Write cho bit-in-word"
    - "5. Chạy WinForms demo với PLC thực tế hoặc simulator"
    - "6. Kiểm tra PlcSubscriptionManager polling + OnValueChanged"
    - "7. Kiểm tra SqliteHistorian ghi/đọc đúng"
  blocked_by:     "Cần PLC Mitsubishi thực tế hoặc GX Simulator để test live"
  last_session:   "2026-06-03"
  open_questions:
    - "PlcAddressParser có parse đúng X1A (HEX), B1F0, D100.5 chưa?"
    - "HslCommunication version nào đang dùng? Có cần update không?"
    - "tags.json hiện cấu hình IP PLC nào? Có simulator sẵn không?"
    - "String Mitsubishi: wordCount padding đã đúng chưa trong GroupReader?"
```

### 4.2 Decision Log

| ID      | Ngày       | Quyết định                                      | File liên quan                    |
|---------|------------|-------------------------------------------------|-----------------------------------|
| DEC-001 | 2026-06-01 | Dùng HslCommunication cho MC Protocol           | `Core/PlcClient.cs`               |
| DEC-002 | 2026-06-01 | lock(SyncLock) bao quanh mọi HslComm call       | `Core/PlcClient.cs`, `IO/*.cs`    |
| DEC-003 | 2026-06-01 | PlcGroupWriter tự RMW — không để caller lo      | `IO/PlcGroupWriter.cs`            |
| DEC-004 | 2026-06-01 | Watchdog dùng `Read("D0", 1)` — nhẹ, an toàn   | `Core/PlcClient.cs`               |

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
