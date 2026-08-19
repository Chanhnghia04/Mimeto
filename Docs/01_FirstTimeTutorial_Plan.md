# Kế hoạch triển khai hướng dẫn người chơi lần đầu

## 1. Mục tiêu

Khi một người chơi vào game lần đầu, họ phải hiểu được bốn việc trước khi tự chơi một ván:

1. Cách điều khiển và tương tác.
2. Waiting dùng để mua, bán, chuẩn bị và tập hợp đội.
3. Map có Oxygen, Stamina, kẻ địch nghe tiếng động và vật phẩm cần nhặt.
4. Mục tiêu cuối cùng là hoàn thành một trong bốn cách thoát rồi mở cửa thoát.

Tutorial phải dễ bỏ qua, không làm kẹt người chơi khác trong phòng và không phụ thuộc vào việc người chơi là host hay client.

## 2. Những gì dự án đang có

- `StartGame` tạo/join Lobby, Relay và voice chat.
- `Waiting` có Shop, bán Scrap, mini-game, InfoBoard và khu an toàn Oxygen.
- `Map` sinh vật phẩm, rương, kẻ địch và mục tiêu thoát theo seed của host.
- `InfoBoard` đã có hướng dẫn về Oxygen, vùng nguy hiểm và Economy nhưng chưa phải tutorial từng bước.
- `Waiting` sẽ có một `TutorialStation` riêng để mở tutorial bằng E và test lại bất kỳ lúc nào.
- `SettingsCanvas → CÀI ĐẶT → PHÍM ĐIỀU KHIỂN` đã có danh sách phím.
- `EscapeHUD` mở bằng `R` và hiển thị mục tiêu thoát.
- Chưa có bộ điều phối hướng dẫn lần đầu, cờ hoàn thành tutorial, popup onboarding hoặc `TutorialStation` trong scene thật.

## 3. Thiết kế đề xuất

### 3.1. Thành phần mới

Đề xuất tạo các thành phần sau:

- `FirstTimeTutorialManager.cs`: quản lý trạng thái và chuyển bước.
- `FirstTimeTutorialUI.cs`: hiển thị popup, nút Tiếp tục/Bỏ qua/Đóng.
- `TutorialStepId.cs`: enum các bước để tránh dùng chuỗi rời rạc.
- `TutorialCanvas.prefab`: Canvas dùng chung, có thể tồn tại qua scene.
- `TutorialCopy.vi.asset` hoặc một lớp dữ liệu tương đương: chứa nội dung tiếng Việt, dễ chỉnh sửa mà không phải sửa logic.

Tên trên là tên đề xuất; có thể dùng hệ thống UI hiện có nếu muốn giảm số file.

### 3.2. Lưu trạng thái

Lưu trạng thái tutorial theo từng máy bằng PlayerPrefs, ví dụ:

- `Mimeto_TutorialVersion = 1`: đã hoàn tất phiên bản tutorial nào.
- `Mimeto_TutorialSkipped = 1`: người chơi đã bỏ qua.

Không lưu trạng thái tutorial trong `GlobalPlayerData` hoặc `PlayerInventory`, vì tutorial không phải dữ liệu của một chuyến đi và không nên bị xóa khi người chơi chết.

Tutorial là trạng thái **local của từng client**. Không dùng NetworkVariable để một người mở popup làm dừng toàn đội.

### 3.3. Thời điểm khởi động

- Không mở tutorial trước khi player object và camera local đã xuất hiện.
- Sau khi vào `Waiting`, hiển thị một gợi ý ngắn chỉ tới `TutorialStation` cho người chơi chưa có `Mimeto_TutorialVersion`.
- Người chơi đi tới `TutorialStation` và nhấn E để mở toàn bộ tutorial. Đây là đường vào chính để test, kể cả sau khi đã đánh dấu hoàn thành.
- Khi chuyển sang `Map`, tiếp tục bằng các bước gameplay còn lại nếu người chơi chưa hoàn thành.
- Nếu người chơi bỏ qua, cho phép xem lại tại `TutorialStation` hoặc `ESC → CÀI ĐẶT → HƯỚNG DẪN`.

### 3.4. TutorialStation trong Waiting

Đặt một điểm tương tác riêng trong `Waiting`, gần khu spawn/InfoBoard nhưng không nằm trong vùng tự động chuyển Map.

- Tên GameObject đề xuất: `TutorialStation_Object`.
- Component đề xuất: `TutorialStation.cs : MonoBehaviour, IInteractable`.
- Hint: `Press [E] to Open New Player Tutorial`.
- Tương tác chỉ mở UI local, không khóa người chơi khác và không thay đổi NetworkVariable.
- Có nút `PLAY TUTORIAL`, `REPLAY`, `RESET FOR TEST` (nút reset chỉ hiện trong Editor/QA).
- Có thể dùng model console/bảng màn hình; không nên dùng chung GameObject với `LevelTransportStation`.

## 4. Luồng tổng thể

```text
StartGame
  ↓ tạo hoặc tham gia phòng
Waiting: đi tới TutorialStation và nhấn E để xem hướng dẫn
  ↓ chuẩn bị tại shop/InfoBoard
  ↓ tất cả người chơi đứng trong vùng vận chuyển đủ 5 giây
Map: nhặt đồ + Oxygen + stealth + mở mục tiêu bằng R
  ↓ hoàn thành một phương án thoát
Cửa thoát: tương tác E
  ↓ thắng hoặc thua
Waiting: tutorial không tự lặp lại
```

## 5. Quy tắc UX

- Mỗi popup chỉ truyền đạt một ý chính.
- Không che HUD quá lâu; người chơi có thể đóng bằng `ESC` hoặc nút Bỏ qua.
- Không bắt người chơi phải mua vật phẩm hoặc chơi mini-game để được đi tiếp.
- Không yêu cầu host bấm một nút riêng mà client không thấy.
- TutorialStation phải có thể được mở riêng bởi từng client; một client mở tutorial không làm dừng countdown hoặc input của người khác.
- TutorialStation phải nằm ngoài phạm vi tính người chơi của `LevelTransportStation` để người chơi test không vô tình bắt đầu Expedition.
- Nếu một bước phụ thuộc vào vật phẩm/ngẫu nhiên, chỉ hướng dẫn cách làm, không đặt mục tiêu cố định.
- Mọi phím hiển thị phải lấy từ `KeybindManager` để khớp với phím người chơi đã đổi.

## 6. Vấn đề hiện tại cần xử lý trong lúc triển khai

1. `Waiting.unity` có dòng `Press [E] to Start Expedition`, nhưng `LevelTransportStation.Interact()` đang rỗng. Tutorial phải nói đúng cơ chế hiện tại: tất cả người chơi đứng trong vùng, không phải nhấn E.
2. `WaitingRoomManager.cs` tồn tại nhưng chưa được gắn vào `Waiting.unity`. Không nên xây tutorial dựa trên nút Start của script này trước khi quyết định dùng lại nó.
3. `TutorialStation` chưa được đặt trong `Waiting.unity`; đây là hạng mục scene bắt buộc trước khi test flow.
4. `InfoBoard` hiện chỉ có ở Waiting. Nếu người chơi đóng tutorial rồi vào Map, các nhắc nhở Oxygen/stealth phải nằm trong tutorial hoặc HUD Map.
5. Nội dung `InfoBoard` đang là tiếng Anh; phần tutorial mới nên dùng tiếng Việt thống nhất hoặc chuẩn bị hệ thống bản địa hóa.

## 7. Tiêu chí hoàn thành

- Người chơi mới được chỉ tới TutorialStation sau khi vào Waiting và có thể mở tutorial bằng E.
- Người chơi đã xem xong vẫn có thể mở lại tutorial tại TutorialStation để test.
- Host và client có thể đọc/đóng tutorial độc lập.
- Tutorial không ngăn scene chuyển Map và không làm mất input gameplay sau khi đóng.
- Người chơi hiểu cách mở mục tiêu bằng `R`, nhặt bằng `E`, quản lý Oxygen và vào cửa thoát.
- Tutorial vẫn hoạt động nếu người chơi chết, thắng, rời phòng hoặc vào lại game.
- Có nút reset tutorial dành cho QA/developer.
