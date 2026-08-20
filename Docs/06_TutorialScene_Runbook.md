# Hướng dẫn chạy scene Tutorial mới

## 1. Scene này dùng để làm gì?

`Assets/Scenes/Tutorial.unity` là một phòng hướng dẫn độc lập dành cho người chơi mới và QA. Scene không tạo Lobby, không kết nối Relay, không cần player network prefab và không ảnh hưởng tới dữ liệu chuyến đi.

Khi chạy, scene nạp lại asset gameplay thật ở chế độ offline:

- Toàn bộ map gameplay thật từ `Assets/Scenes/Map.unity` (đóng gói tại `Assets/Prefabs/Tutorial/GameplayMap.prefab`).
- Nhân vật thật từ `Assets/Prefabs/Player.prefab`, giữ model và `PlayerAnimatorController` gốc; các script network/lobby được tắt riêng cho scene Tutorial.
- Mutant thật từ `Assets/Prefabs/EnamiMutant.prefab`, dùng Animator của Mutant nhưng AI network được thay bằng `TutorialMonster` offline.
- Item thật (Circuit Board và Battery dùng làm Oxygen Tank luyện tập), SafeBase và cửa thoát thật của Map.
- Một guided gameplay course đặt trực tiếp trên Map thật: đi theo line, nhặt Circuit Board, nhặt Oxygen Tank, vào Safe Zone, cúi tránh Mutant và mở cửa thoát.
- Bảng hướng dẫn nhiều trang tự mở khi vào scene; nhấn R/ESC để mở lại trong lúc thực hành.
- Chỉ sau khi hoàn tất đủ 5 bước gameplay và mở cửa thoát, scene mới tự chuyển về `StartGame` sau 2,5 giây. Đọc hết các trang hướng dẫn chỉ đóng bảng; nút `VỀ START GAME` vẫn chuyển ngay nếu muốn thoát thủ công.

Script chính: [TutorialSceneController.cs](../Assets/Scripts/Tutorial/TutorialSceneController.cs). Script trạm: [TutorialWorldStation.cs](../Assets/Scripts/Tutorial/TutorialWorldStation.cs).

## 2. Cách chạy trong Unity Editor

1. Mở project `Mimeto`.
2. Trong Project, mở `Assets/Scenes/Tutorial.unity`.
3. Nhấn Play; camera sẽ đứng tại SafeBase trong Map thật.
4. Scene tự mở trang chào mừng. Nhấn `TIẾP TỤC` hoặc Enter/Space để đọc hết.
5. Đóng bảng bằng `ĐÓNG` hoặc ESC.
6. Sau khi đóng bảng, nhìn HUD `GUIDED GAMEPLAY // MỤC TIÊU` và đi theo đường line phát sáng.
7. Nhìn vào Circuit Board/Oxygen Tank rồi nhấn E để nhận vật phẩm.
8. Đi vào vòng xanh Safe Zone.
9. Khi tới vùng Mutant, giữ C/Ctrl và đi chậm để thực hành stealth.
10. Theo line tới cửa thoát và nhấn E.
11. Xem thông báo hoàn tất; sau 2,5 giây scene tự về `StartGame` để tạo phòng và chơi thật.
12. Có thể nhấn `VỀ START GAME` để chuyển ngay, hoặc F1 để reset trạng thái tutorial khi QA.

Scene đã được thêm vào `ProjectSettings/EditorBuildSettings.asset` với tên `Tutorial`.

## 3. Các trang hướng dẫn

| Trang | Nội dung |
|---|---|
| 01 WELCOME | Mục tiêu chung và vòng lặp một ván |
| 02 CONTROLS | WASD, chuột, E, Shift, C, Space, I, F, R, ESC |
| 03 WAITING | Shop, Reclaimer, InfoBoard, Safe Zone |
| 04 START RUN | Cách tất cả người chơi đứng trong vùng 5 giây để vào Map |
| 05 SURVIVAL | Oxygen, máu, Stamina và mặt nạ |
| 06 STEALTH | Âm thanh, Mutant, ExilerAI và Hiding Spot |
| 07 OBJECTIVES | Assembly, Beacon, Cipher và Reactor |
| 08 EXTRACTION | EscapeHUD, cửa thoát, thắng/thua và dữ liệu lưu |

## 4.1. Bài thực hành gameplay

Đường line luôn nối từ vị trí người chơi tới mục tiêu hiện tại:

```text
Circuit Board
  → Oxygen Tank
  → Safe Zone (vòng xanh)
  → Mutant demo (giữ C/Ctrl để cúi)
  → Exit Door (nhấn E)
```

- Nhặt item chỉ được ghi nhận khi tâm ngắm đang trỏ vào item và người chơi nhấn E.
- Sau khi nhặt đúng, mục tiêu trên HUD và điểm cuối của line sẽ đổi sang bước tiếp theo.
- Nếu Mutant bắt được người chơi, người chơi được đưa về Safe Zone checkpoint để thử lại.
- Khi tới cửa trước khi hoàn thành đủ bước, game hiển thị lý do cửa chưa mở.

## 4. Điều khiển scene Tutorial

| Phím | Tác dụng |
|---|---|
| WASD | Di chuyển trong Map thật |
| Chuột | Nhìn xung quanh |
| Left Shift | Chạy nhanh trong Map thật |
| C hoặc Ctrl | Cúi; camera hạ theo tư thế và collider thu gọn |
| E | Nhặt item hoặc tương tác với cửa đang nhìn vào |
| R | Mở lại bảng hướng dẫn |
| ESC | Đóng/mở bảng hướng dẫn |
| Chuột trái | Kích hoạt animation đánh để thử animation gốc |
| F | Bật/tắt đèn UV của Player thật |
| I | Hiện gợi ý túi đồ trong bài học |
| F1 | Reset trạng thái tutorial để test |

Khi giữ `C` hoặc `Ctrl` để cúi, camera của Player thật hạ mượt khoảng `0,5m` (local Y từ `1,6` xuống `1,1`) và `CharacterController` thu gọn từ `2,0m` xuống `1,2m`. Khi thả phím, cả camera và collider phục hồi mượt; animation `isSneaking` vẫn được cập nhật đồng bộ.

## 5. Lưu ý triển khai

- `useGameplayAssets` trong `TutorialSceneController` phải bật. Nếu tắt, scene quay về layout phòng mẫu cũ.
- `autoReturnToStartGame` đang bật và `returnScene` là `StartGame`; nếu đổi tên scene phải cập nhật cả Build Settings.
- Prefab Map/Player/Mutant được tham chiếu trực tiếp trong `Tutorial.unity`; không xóa các object con đang inactive dưới `TutorialSceneController`.
- Chế độ offline chỉ tắt NetworkObject, PlayerController/Inventory/Survival, PlayerInput và các spawner/manager network. Animator, model, collider và camera thật vẫn hoạt động.
- Guided course dùng các script offline riêng (`TutorialCollectible`, `TutorialSafeZone`, `TutorialMonster`, `TutorialExitTarget`), không dùng Mutant AI network thật để tránh ảnh hưởng lobby.
- `TutorialSceneController` dùng camera `PlayerCamera` nằm trong Player thật và cập nhật các parameter Animator (`InputX`, `InputY`, `isSneaking`, `isGrounded`).
- Cấu hình độ cúi nằm ở `crouchCameraDrop`, `crouchTransitionSpeed` và `crouchControllerHeight`; giữ camera thấp hơn giúp góc nhìn và va chạm phản hồi đúng với tư thế cúi.
- Scene chỉ mô phỏng cơ chế để học. Muốn chơi multiplayer thật, quay về `StartGame`.
- `TutorialStation` trong Waiting vẫn là kế hoạch cho đường vào tutorial từ phòng chờ; scene mới này có thể dùng để test trước khi thêm station vào Waiting.
- Nếu muốn đổi nội dung, sửa danh sách `_pages` trong `TutorialSceneController.cs`.

## 6. Kiểm tra đã thực hiện

- Scene asset đã tạo: `Assets/Scenes/Tutorial.unity`.
- Gameplay asset prefabs đã tạo: `Assets/Prefabs/Tutorial/GameplayMap.prefab`, `GameplaySafeBase.prefab`, `GameplayExitDoor.prefab`.
- Các material embedded của Map đã được externalize vào `Assets/Prefabs/Tutorial/MapMaterials/` khi đóng gói prefab; audit prefab còn `0` material slot null để tránh nền hồng/magenta.
- Root scene đã gắn `TutorialSceneController`.
- Các script tutorial (`TutorialSceneController`, `TutorialWorldStation`, `TutorialCollectible`, `TutorialSafeZone`, `TutorialMonster`, `TutorialExitTarget`) đã import và compile được trong Unity.
- Guided course đã nối line, pickup, Safe Zone, Mutant thật và exit interaction.
- Scene đã được thêm vào Build Settings.
- Đã chạy thử Play Mode: `TutorialPlayer` active trong hierarchy, `PlayerCamera` là MainCamera, PlayerInput/network scripts offline đều disabled, Animator/model thật active, Map/Mutant/item đều active.
- Logic tự chuyển có guard `_courseComplete`: đọc hết các trang khi chưa làm đủ 5 bước vẫn giữ ở `Tutorial`; chỉ tương tác cửa thoát sau khi hoàn tất mới chuyển sang `StartGame`. `Application.CanStreamedLevelBeLoaded("StartGame")` trả về `true`.
- Đã kiểm tra screenshot runtime: model Player và Enami Mutant là asset thật, không còn placeholder cube.
- Lỗi Vivox `HTTP/1.1 403 Forbidden` vẫn là lỗi dịch vụ có sẵn của project, không liên quan tới scene Tutorial offline.
