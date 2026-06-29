/// <summary>4 phương thức thoát hiểm ngẫu nhiên được quản lý bởi EscapeManager.</summary>
public enum EscapeMethodType
{
    Assembly,  // Tìm 3 bộ phận rải trên bản đồ → lắp cửa
    Beacon,    // Xây beacon bằng scrap → đếm ngược 3 phút sống sót
    Cipher,    // Tìm 2 ghi chú → giải mã bàn phím cửa
    Reactor    // Tắt lò phản ứng bằng scrap → cửa mở
}
