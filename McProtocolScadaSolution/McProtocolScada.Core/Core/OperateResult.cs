namespace McProtocolClientLib.Core
{
    /// <summary>
    /// Kết quả thao tác (thành công/thất bại + message lỗi). Thay thế HslCommunication.OperateResult
    /// (DEC-013: bỏ hoàn toàn dependency HslCommunication) — API giữ nguyên hình dạng để
    /// Mc3EBinaryClient/PlcGroupReader/PlcGroupWriter không cần sửa logic.
    /// </summary>
    public class OperateResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ErrorCode { get; set; }

        public OperateResult() { }
        public OperateResult(string message) { Message = message; }
        public OperateResult(int errorCode, string message) { ErrorCode = errorCode; Message = message; }

        public static OperateResult CreateSuccessResult() => new OperateResult { IsSuccess = true, Message = "Success" };

        public static OperateResult<T> CreateSuccessResult<T>(T content) =>
            new OperateResult<T> { IsSuccess = true, Message = "Success", Content = content };
    }

    /// <summary>Kết quả thao tác kèm dữ liệu trả về (đọc thành công thì có Content).</summary>
    public class OperateResult<T> : OperateResult
    {
        public T Content { get; set; } = default!;

        public OperateResult() { }
        public OperateResult(string message) : base(message) { }
    }
}
