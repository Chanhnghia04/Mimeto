# Tài liệu cơ chế và tutorial Mimeto

Bộ tài liệu này mô tả cơ chế hiện tại của game và kế hoạch thêm hướng dẫn cho người chơi lần đầu.

## Đọc theo thứ tự

1. [Kế hoạch tutorial lần đầu](01_FirstTimeTutorial_Plan.md)
2. [Đặc tả luồng tutorial theo scene](02_TutorialScene_Flow.md)
3. [Hướng dẫn hoàn thành game](03_GameCompletion_Guide.md)
4. [Checklist triển khai và kiểm thử](04_Implementation_Checklist.md)
5. [Thiết kế Tutorial Station trong Waiting](05_Tutorial_TestStation.md)
6. [Hướng dẫn chạy scene Tutorial mới](06_TutorialScene_Runbook.md)

## Phạm vi hiện tại

Luồng game chính là:

`StartGame → Waiting → Map → hoàn thành mục tiêu thoát → cửa thoát → Waiting`

Các scene chính đã có trong `ProjectSettings/EditorBuildSettings.asset`. Ngoài ra dự án hiện có scene `Tutorial.unity` độc lập để học và test offline. Scene này tự dựng phòng 3D, các trạm kiến thức, guided gameplay course (line → pickup → Safe Zone → Mutant → exit) và UI bằng `TutorialSceneController`, không phụ thuộc prefab mạng của Waiting/Map.
