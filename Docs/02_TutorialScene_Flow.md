# Đặc tả luồng tutorial theo scene

Tài liệu này là nội dung cụ thể để dựng UI và nối sự kiện. Các bước có thể hiển thị bằng popup ở giữa màn hình, hoặc bằng marker/arrow trong scene nếu sau này cần trực quan hơn.

## A. Waiting — phần làm quen

### T0 — TutorialStation: điểm vào hướng dẫn

**Vị trí scene:** đặt `TutorialStation_Object` gần khu spawn hoặc `InfoBoard_Object`, nhưng cách `LevelTransportStation` đủ xa để không bị hiểu nhầm là cổng bắt đầu Expedition.

**Tương tác:** player nhìn vào station và nhấn E. `TutorialStation.cs` chỉ gọi mở tutorial UI cho player local; không tải scene, không đếm người chơi và không thay đổi trạng thái mạng.

**Hint hiển thị:** `Press [E] to Open New Player Tutorial`.

**Nút trong UI:**

- `CONTINUE`: đi qua từng bước W0–W3.
- `REPLAY`: mở lại toàn bộ nội dung sau khi đã hoàn thành.
- `CLOSE/ESC`: đóng mà không làm mất tiến độ.
- `RESET FOR TEST`: xóa `Mimeto_TutorialVersion`; chỉ bật trong Editor/QA.

**Cách test nhanh:** vào `Waiting` → đi tới TutorialStation → nhấn E → kiểm tra W0–W3 → đóng bằng ESC → nhấn E lần nữa để xác nhận có thể mở lại.

### W0 — Chào mừng

**Trigger chính:** player local nhấn E tại `TutorialStation`. Khi là lần đầu vào Waiting, có thể hiện thêm một tooltip nhỏ chỉ hướng tới station nhưng không tự mở popup lớn.

**Nội dung đề xuất:**

> Chào mừng đến Mimeto. Bạn sẽ cùng đội vào khu vực độc hại, thu thập tài nguyên, hoàn thành một nhiệm vụ thoát hiểm ngẫu nhiên và quay về bằng cửa thoát. Nếu chết, vật phẩm của chuyến đi sẽ mất.

**Hoàn tất:** người chơi bấm Tiếp tục hoặc Bỏ qua. Hoàn tất chỉ lưu trạng thái local, không làm station mất khả năng mở lại.

### W1 — Điều khiển cơ bản

Hiển thị các phím:

| Hành động | Phím mặc định |
|---|---|
| Di chuyển | WASD |
| Nhìn | Chuột |
| Tương tác | E |
| Chạy nhanh | Left Shift |
| Cúi | C hoặc Ctrl tùy Input System |
| Nhảy/thoát chỗ trốn | Space |
| Tấn công | Chuột trái |
| Túi đồ | I theo Settings UI |
| Đèn pin | F |
| Mục tiêu thoát | R |

**Lưu ý triển khai:** lấy phím từ `KeybindManager`, không ghi cứng phím trong UI nếu hệ thống cho phép đổi phím.

### W2 — Waiting là nơi chuẩn bị

**Nội dung:**

> Dùng E tại Shop để mua mặt nạ gas, Oxygen Tank, thuốc và vũ khí. Dùng E tại Reclaimer để bán Scrap lấy Energy Credits. InfoBoard giải thích vùng nguy hiểm và Oxygen. Khu an toàn giúp hồi Oxygen.

**Hoàn tất:** chỉ cần người chơi bấm Tiếp tục. Không bắt buộc phải mua hàng.

### W3 — Cách bắt đầu Expedition

**Nội dung phải khớp code hiện tại:**

> Khi cả đội sẵn sàng, mọi người đứng gần trạm vận chuyển. Khi đủ người trong vùng, bộ đếm 5 giây bắt đầu và server tự chuyển sang Map. Dòng E trên trạm hiện chỉ là gợi ý; cơ chế chuyển scene là tự động.

**Điều kiện hiển thị:** hiển thị tại TutorialStation; không gắn flow tutorial vào `LevelTransportStation`.

## B. Map — phần học gameplay

### G0 — Guided gameplay course trong Tutorial.unity

Scene Tutorial có một tuyến thực hành mô phỏng Map thật, không chỉ là popup chữ:

1. Đi theo đường line phát sáng tới Circuit Board.
2. Nhìn vào item và nhấn E để nhận vật phẩm.
3. Đi theo line tới Oxygen Tank và nhấn E lần nữa.
4. Vào vòng xanh Safe Zone để hiểu nơi hồi Oxygen.
5. Tới vùng Mutant demo; giữ C/Ctrl để cúi và đi qua vùng phát hiện.
6. Theo line tới Exit Door và nhấn E để hoàn thành bài thực hành.

Nếu Mutant bắt được người chơi, scene đưa người chơi về Safe Zone checkpoint và hiển thị lý do cần cúi/đi chậm.

Các script mô phỏng nằm trong `Assets/Scripts/Tutorial/`; chúng chỉ chạy trong scene Tutorial, không thay thế AI network trong Map.

### M0 — Mục tiêu của ván

**Trigger:** player local spawn trong `Map`.

**Nội dung:**

> Đây là khu vực độc hại. Nhấn R để mở bảng nhiệm vụ thoát. Seed của host quyết định vị trí đồ, rương, kẻ địch và phương án thoát của ván này.

### M1 — Nhặt đồ và quản lý túi

**Nội dung:**

> Nhìn vào vật phẩm hoặc rương rồi nhấn E để tương tác. Mở túi đồ bằng phím được hiển thị trong Settings. Túi có giới hạn slot; Scrap có thể xếp chồng, còn dụng cụ thường chiếm một slot riêng.

**Hoàn tất tùy chọn:** player nhặt thành công một vật phẩm hoặc đóng bước.

### M2 — Oxygen và Stamina

**Nội dung:**

> Theo dõi Oxygen, máu và Stamina trên HUD. Không có mặt nạ, Oxygen giảm nhanh trong khu độc hại. Hết Oxygen sẽ làm mất máu. Safe Zone hồi Oxygen; chạy nhanh tiêu hao Stamina.

**Hoàn tất tùy chọn:** player đi vào Safe Zone hoặc đóng bước.

### M3 — Âm thanh và trốn tránh

**Nội dung:**

> Mutant và ExilerAI có thể nghe tiếng động. Chạy và đánh nhau dễ gây chú ý. Cúi người, đứng yên hoặc vào Hiding Spot để giảm nguy cơ bị phát hiện. Dùng vũ khí khi cần, nhưng không nên chạy liên tục trong khu nguy hiểm.

### M4 — Hướng dẫn theo mục tiêu ngẫu nhiên

Tutorial đọc `EscapeManager.CurrentMethod` và chỉ hiển thị nhánh tương ứng:

| Phương án | Nội dung hướng dẫn ngắn |
|---|---|
| Assembly | Tìm Gear, Fuel Tank và Circuit Board. Sau đó thực hiện đủ các bước lắp ráp tại cửa thoát. |
| Beacon | Thu 2 Circuits và 1 Battery, lắp Beacon rồi sống sót 180 giây. |
| Cipher | Tìm 2 mảnh ghi chú, ghép thành mã 4 số và nhập tại Keypad. Nhập sai gây mất máu. |
| Reactor | Thu 3 Chemicals và 2 Circuits, kích hoạt Reactor và tránh xa vụ nổ trong thời gian meltdown. |

Không hướng dẫn người chơi tìm vật phẩm ở tọa độ cố định vì vị trí được sinh theo seed.

### M5 — Cửa thoát và kết thúc

**Nội dung:**

> Khi mục tiêu hoàn thành, bảng nhiệm vụ sẽ báo đã mở khóa. Đi tới cửa thoát và nhấn E. Thắng sẽ lưu vật phẩm; chết hoặc thua sẽ xóa vật phẩm của chuyến đi.

**Hoàn tất tutorial:** chỉ đánh dấu phiên bản khi player hoàn thành đủ các bước thực hành và tương tác cửa thoát; đọc hết nội dung M5 không tự kết thúc scene.

## C. Trường hợp đặc biệt

- **Client vào trễ:** chỉ hiển thị các bước local còn thiếu, không reset tiến độ của người khác.
- **Player bỏ qua:** không khóa gameplay; cho phép xem lại trong Settings.
- **Test lại:** luôn có thể quay về TutorialStation trong Waiting và nhấn E để mở lại, kể cả khi đã hoàn thành tutorial.
- **Player chết:** không mở lại W0 khi quay về Waiting nếu phiên bản tutorial đã hoàn thành.
- **Đổi phiên bản tutorial:** tăng `Mimeto_TutorialVersion` để chạy lại phần nội dung mới.
- **Không có NetworkManager:** tutorial vẫn hiển thị trong chế độ offline và Map vẫn dùng `SceneManager.LoadScene` như code hiện có.
