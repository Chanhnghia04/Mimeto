# Checklist triển khai tutorial lần đầu

## Phase 0 — Chuẩn bị

- [ ] Chốt ngôn ngữ tutorial: tiếng Việt, tiếng Anh hoặc hỗ trợ cả hai.
- [ ] Chốt phiên bản tutorial đầu tiên là `1`.
- [x] Xác nhận scene build order có `StartGame`, `Waiting`, `Map` và `Tutorial`.
- [ ] Xác nhận Canvas của tutorial không trùng với `InventoryCanvas`, `PauseMenuCanvas` hoặc `SettingsCanvas`.
- [ ] Xác nhận tutorial không dùng NetworkVariable cho trạng thái local.
- [ ] Chọn vị trí `TutorialStation_Object` gần spawn/InfoBoard và ngoài vùng `LevelTransportStation`.

## Phase 1 — Runtime và lưu trạng thái

- [ ] Tạo `FirstTimeTutorialManager` tồn tại qua scene.
- [ ] Tạo hàm `ShouldShowTutorial()` đọc `Mimeto_TutorialVersion`.
- [ ] Tạo `CompleteTutorial()`, `SkipTutorial()` và `ResetTutorialForQA()`.
- [ ] Đảm bảo không tạo nhiều manager khi scene đổi.
- [ ] Khi đổi phiên bản nội dung, tăng version để người chơi cũ được xem phần mới.
- [ ] Có API `OpenTutorialFromStation()` để QA mở lại không phụ thuộc vào cờ đã hoàn thành.

## Phase 2 — UI

- [ ] Tạo popup có tiêu đề, nội dung, số bước và nút Tiếp tục.
- [ ] Có nút Bỏ qua và đóng bằng ESC.
- [ ] Có thể mở lại từ `ESC → CÀI ĐẶT → HƯỚNG DẪN`.
- [ ] Hiển thị phím qua `KeybindManager` thay vì ghi cứng.
- [ ] Có trạng thái loading/ẩn khi player local chưa spawn.
- [ ] Kiểm tra font Unicode tiếng Việt và độ dài text trên độ phân giải khác nhau.

## Phase 3 — Nối scene

- [ ] Thêm `TutorialStation_Object` vào `Waiting.unity`.
- [ ] Gắn `TutorialStation.cs` và collider tương tác.
- [ ] Hint của station là `Press [E] to Open New Player Tutorial`.
- [ ] Nhấn E tại station mở W0; không dùng `LevelTransportStation` để mở tutorial.
- [ ] Waiting có các bước W1, W2 và W3.
- [ ] W3 mô tả đúng cơ chế `LevelTransportStation`: đủ người trong vùng và đếm 5 giây.
- [ ] Map mở M0 sau khi player local spawn.
- [ ] M4 đọc đúng `EscapeManager.CurrentMethod` và chỉ hiển thị nhánh đang dùng.
- [ ] M5 liên kết với trạng thái `IsEscapeUnlocked` và cửa `ExtractionSystem`.
- [x] Tutorial không tự chuyển scene khi chỉ đọc xong các trang; chỉ chuyển sau khi hoàn tất đủ 5 bước và mở cửa thoát.

## Phase 4 — Sửa nội dung gây hiểu nhầm

- [ ] Đổi hint của trạm Waiting từ “Press [E]” thành “Stand with all players in the zone” nếu vẫn giữ cơ chế tự động.
- [ ] Nếu muốn chuyển bằng E, phải triển khai thật `LevelTransportStation.Interact()` và thống nhất lại với tutorial.
- [ ] Quyết định có gắn `WaitingRoomManager` hay không; không để song song hai cơ chế Start gây nhầm lẫn.
- [ ] Thống nhất tên Mimic/ExilerAI trong tutorial và UI.

## Phase 5 — Kiểm thử chức năng

| Trường hợp | Kết quả mong đợi |
|---|---|
| Người chơi mới vào Waiting | Có tooltip chỉ tới TutorialStation |
| Nhấn E tại TutorialStation | Mở W0 và chạy được toàn bộ W1–W3 |
| Người chơi cũ nhấn E tại station | Tutorial vẫn mở lại để xem/test |
| Bấm Bỏ qua | Gameplay tiếp tục, có thể xem lại trong Settings |
| Host và một client cùng phòng | Mỗi người có popup local độc lập |
| Client vào trễ | Không làm reset tutorial của người khác |
| Đóng bằng ESC | Popup đóng, camera/input gameplay hoạt động lại |
| Vào Map offline | Tutorial vẫn hiển thị, không chờ NetworkManager |
| Objective = Assembly | Chỉ hiện hướng dẫn Assembly |
| Objective = Beacon | Hiện đúng 2 Circuits + 1 Battery và 180 giây |
| Objective = Cipher | Hiện đúng 2 ghi chú và mã 4 số |
| Objective = Reactor | Hiện đúng 3 Chemicals + 2 Circuits và cảnh báo vụ nổ |
| Thắng | Tutorial không tự bật lại ở Waiting |
| Chết | Tutorial không tự bật lại nếu đã hoàn thành |
| Xóa PlayerPrefs QA | Tutorial chạy lại từ W0 |
| Đứng tại TutorialStation khi chưa đủ đội | Không tự chuyển Map |

## Definition of Done

- [ ] Người mới có thể tự đi từ StartGame đến cửa thoát mà không cần đọc code hoặc hỏi người khác.
- [ ] Tất cả thông tin tutorial khớp với logic hiện tại của script.
- [ ] Không có popup bị kẹt, mất con trỏ, khóa camera hoặc khóa input sau khi đóng.
- [ ] Host/client, offline và scene chuyển qua lại đều đã test.
- [ ] Các dòng hint gây hiểu nhầm trong Waiting đã được sửa hoặc cơ chế E đã được triển khai thật.
- [ ] TutorialStation đã được test trong scene Waiting thật, không chỉ test bằng prefab riêng.
- [x] Scene `Tutorial.unity` có trong Build Settings và có thể chạy offline.
- [x] Scene tutorial không yêu cầu Unity Services, Lobby, Relay hoặc NetworkManager.
- [x] Guided course có line dẫn đường từ player tới mục tiêu hiện tại.
- [x] Có pickup Circuit Board và Oxygen Tank bằng phím E.
- [x] Có Safe Zone, Mutant demo và Exit Door để thực hành trọn vòng lặp.
