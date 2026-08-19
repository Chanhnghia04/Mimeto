# Tài liệu cơ chế và tutorial Mimeto

Bộ tài liệu này mô tả cơ chế hiện tại của game và kế hoạch thêm hướng dẫn cho người chơi lần đầu.

## Đọc theo thứ tự

1. [Kế hoạch tutorial lần đầu](01_FirstTimeTutorial_Plan.md)
2. [Đặc tả luồng tutorial theo scene](02_TutorialScene_Flow.md)
3. [Hướng dẫn hoàn thành game](03_GameCompletion_Guide.md)
4. [Checklist triển khai và kiểm thử](04_Implementation_Checklist.md)
5. [Thiết kế Tutorial Station trong Waiting](05_Tutorial_TestStation.md)

## Phạm vi hiện tại

Luồng game chính là:

`StartGame → Waiting → Map → hoàn thành mục tiêu thoát → cửa thoát → Waiting`

Các scene này đã có trong `ProjectSettings/EditorBuildSettings.asset`. Tutorial được đề xuất là một lớp UI hướng dẫn chạy trên các scene hiện có, có một `TutorialStation` trong `Waiting` để người chơi và QA chủ động vào xem trước, không tạo một gameplay map riêng và không can thiệp vào quyền điều khiển mạng của host.
