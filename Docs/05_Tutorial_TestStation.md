# Thiết kế Tutorial Station trong Waiting

## 1. Mục đích

`TutorialStation` là một điểm tương tác cố định trong phòng chờ. Người chơi mới có thể đi tới đó để học trước khi vào Map; người phát triển/QA có thể mở lại nhiều lần để kiểm tra giao diện và logic.

Đây là **điểm vào thủ công** của tutorial. Tutorial không tự bật popup lớn ngay khi spawn, vì popup tự động có thể che màn hình, làm người chơi bỏ qua và khó kiểm thử trong multiplayer.

## 2. Vị trí đề xuất

Trong `Waiting.unity`, đặt station:

- Gần vị trí player spawn để người mới dễ nhìn thấy.
- Gần `InfoBoard_Object` để tạo thành khu “Help/Training”.
- Cách `LevelTransportStation` đủ xa, khuyến nghị ít nhất 8–10 mét.
- Không đặt trong vùng 5 mét được `LevelTransportStation` dùng để đếm người chơi.
- Có ánh sáng màu cyan, bảng chữ `NEW PLAYER // TUTORIAL` hoặc marker nổi.

Không dùng chung GameObject với `InfoBoard` hoặc `LevelTransportStation`. InfoBoard vẫn giữ vai trò tra cứu Oxygen/Economy; TutorialStation mở luồng hướng dẫn nhiều bước.

## 3. Thành phần scene

### GameObject

```text
TutorialStation_Object
├── Model/Console hoặc bảng hướng dẫn
├── BoxCollider (Is Trigger = false, dùng để raycast tương tác)
└── TutorialStation.cs
```

### Component đề xuất

`TutorialStation.cs` triển khai `IInteractable` và có các trường:

- `tutorialManager`: tham chiếu tới `FirstTimeTutorialManager` hoặc tự tìm singleton.
- `interactHint`: `Press [E] to Open New Player Tutorial`.
- `interactionDistance`: dùng cùng khoảng cách với hệ thống tương tác hiện tại.
- `showFirstTimeMarker`: bật marker cho người chưa có `Mimeto_TutorialVersion`.

Hàm `Interact(GameObject interactor)` chỉ mở Canvas cho owner/local player. Không gọi `SceneManager.LoadScene`, không gọi `NetworkManager.SceneManager.LoadScene` và không can thiệp vào số người trong vùng vận chuyển.

## 4. Luồng sử dụng

```text
Player spawn trong Waiting
  ↓ lần đầu: hiện marker “Tutorial ở phía trước”
Player nhìn vào TutorialStation
  ↓ nhấn E
Tutorial UI mở W0
  ↓ Continue
W1 → W2 → W3
  ↓ Close hoặc Complete
Player quay lại gameplay Waiting
  ↓ nhấn E lần nữa bất cứ lúc nào
Tutorial mở lại để xem/test
```

## 5. Nút và chế độ test

### Người chơi bình thường

- `CONTINUE`: sang bước tiếp theo.
- `BACK`: quay lại bước trước nếu muốn.
- `SKIP`: bỏ qua phần còn lại, không khóa gameplay.
- `CLOSE` hoặc `ESC`: đóng UI.
- Sau khi hoàn thành, station vẫn hiện nút `REPLAY TUTORIAL`.

### QA/Developer

- `RESET FOR TEST`: xóa `Mimeto_TutorialVersion` và mở lại từ W0.
- `OPEN MAP TUTORIAL`: tùy chọn nhảy tới M0–M5 để không phải chạy toàn bộ ván.
- `LOG STEP`: ghi `TutorialStepId` hiện tại vào Console.

Các nút QA không nên xuất hiện trong build phát hành; có thể dùng `#if UNITY_EDITOR` hoặc cờ `developmentBuild`.

## 6. Tiêu chí nghiệm thu station

- Station nhìn thấy rõ ngay khi vào Waiting.
- Nhấn E ở station mở đúng tutorial; nhấn E ở LevelTransportStation không mở tutorial.
- Một client mở station không làm host/client khác dừng hoặc đổi scene.
- Khi chỉ có một người đứng tại station, game không tự chuyển Map.
- Sau khi tutorial đã hoàn thành, station vẫn mở được bằng E.
- Đóng UI trả lại camera, con trỏ và input gameplay bình thường.
- Xóa PlayerPrefs QA cho phép chạy lại W0.
- Station không nằm trong vùng đếm người của `LevelTransportStation`.

## 7. Cách test trong Unity Editor

1. Mở `Assets/Scenes/Waiting.unity`.
2. Chọn `TutorialStation_Object` và kiểm tra collider/hint.
3. Chạy scene với một player offline.
4. Đi tới station, nhấn E và kiểm tra W0–W3.
5. Đóng UI, mở lại station và kiểm tra `REPLAY`.
6. Xóa PlayerPrefs hoặc dùng `RESET FOR TEST`.
7. Chạy Host + Client, mở tutorial trên client và xác nhận host vẫn điều khiển bình thường.
8. Đưa cả đội vào `LevelTransportStation` để xác nhận tutorial station không làm hỏng countdown 5 giây.

