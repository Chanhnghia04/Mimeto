# MIMETO - TÀI LIỆU TỔNG QUAN DỰ ÁN (PROJECT OVERVIEW)
*Tài liệu tổng hợp kiến trúc, gameplay và tiến độ cập nhật dành cho Developer / AI để nắm bắt nhanh toàn bộ dự án tính đến thời điểm hiện tại.*

## 1. Thông Tin Chung
- **Tên dự án:** Mimeto
- **Thể loại:** Co-op Multiplayer Survival / Extraction Horror / Social Deduction.
- **Engine:** Unity
- **Networking:** Dựa trên Unity.Netcode (NetworkBehaviour được sử dụng để hỗ trợ đồng bộ trạng thái nhiều người chơi).
- **Vibe / Cảm hứng:** Lethal Company, Escape from Tarkov, SCP Foundation. Không khí tối tăm, sci-fi, áp lực sinh tồn kết hợp giải trí cờ bạc ở khu vực an toàn.

## 2. Vòng Lặp Gameplay (Core Loop)
Người chơi sẽ trải qua vòng lặp cốt lõi chia thành các giai đoạn:
1. **Waiting Room (Khu Vực An Toàn):**
   - Khu vực chuẩn bị được chia thành 4 phòng riêng biệt (Giải trí, Mua bán, Nghỉ ngơi, Trưng bày).
   - Mua đồ tại **ShopStation** và bán phế liệu tại **ScrapSellStation** để đổi lấy Energy Credits (EC).
   - Giải trí, "đốt tiền" tại các máy Minigame: **Blackjack**, **Dice Bet**, **Slot Machine**.
   - Hồi phục sức khỏe, nhịp tim tại **RestBench** và xem bản đồ tại **InfoBoard**.
2. **Danger Zones (Khu Vực Nguy Hiểm):**
   - Đi xuống khu vực thám hiểm, đối mặt với khí độc (Toxicity), đòi hỏi sự quản lý nghiêm ngặt về lượng Oxy.
   - Sử dụng **Mặt nạ phòng độc (Gas Masks)** để giảm tốc độ mất Oxy.
   - Tìm kiếm phế liệu (`ScrapScatterer`) và mở rương đồ (`ChestSpawner`, `ChestUI`) rải rác trên bản đồ.
   - Lẩn trốn các mối nguy hiểm như quái vật (Mimic) hoặc Tàu hỏa (`TrainMovement`).
3. **Extraction (Trốn Thoát):**
   - Thu thập đủ đồ và chạy đến các điểm thoát hiểm được chỉ định ngẫu nhiên để trở về phòng chờ hoặc qua màn.

## 3. Các Hệ Thống Lõi (Core Systems)

### A. Quái vật AI (MimicAI.cs) & Sinh Vật Địch
- **MimicAI:** Trí tuệ nhân tạo cực kỳ nguy hiểm, có khả năng giả dạng người chơi.
  - `HumanForm`: Giả làm người chơi, cầm đèn pin (đôi lúc chớp đỏ để nhận diện), đi lại như thật.
  - `Stalking`: Rình rập theo dõi từ xa (khoảng cách ~20m). Tự động phát âm thanh rùng rợn khi vào bán kính 15m.
  - `Revealed`: Hiện nguyên hình quái vật.
  - `Chasing`: Truy đuổi tốc độ cao, có khả năng tiêu diệt người chơi cực nhanh.
  - **Lẩn trốn:** Người chơi có thể sử dụng `HidingSpot` để trốn tránh sự truy đuổi.
- **Train Hazard (`TrainMovement.cs`):** Một đoàn tàu di chuyển theo lộ trình, sẵn sàng tông chết cả người chơi lẫn Mimic nếu đứng trên đường ray.

### B. Hệ Thống Trốn Thoát Ngẫu Nhiên (EscapeManager.cs)
Để tăng tính chơi lại, hệ thống sẽ ngẫu nhiên (Procedural) chọn 1 trong 4 kịch bản thoát hiểm mỗi ván:
1. **EscapeAssembly (Lắp Ráp):** Tìm và thu thập 3 bộ phận máy móc nằm rải rác. Có tiếng click/lắp ráp 2D khi thu thập.
2. **EscapeBeacon (Cột Sóng):** Kích hoạt cột tín hiệu và chờ đếm ngược 3 phút. Trong lúc đó, radar sẽ phát tiếng `Ping` mỗi 5s để thu hút Mimic đến.
3. **EscapeCipher (Mật Mã):** Tìm các tờ giấy ghi chú ẩn chứa 4 chữ số, nhập vào bảng điều khiển (có âm thanh phím bấm cơ học). Nhập sai sẽ bị trừ máu.
4. **EscapeReactor (Lò Phản Ứng):** Đóng lò phản ứng đang quá tải bằng Scrap. Khi lò quá tải sẽ có còi báo động khẩn cấp và hiệu ứng nổ bùm kinh hoàng kèm âm thanh nổ lớn.

### C. Sinh Tồn & Hành Trang (PlayerSurvival.cs, PlayerInventory.cs)
- **Máu (HP) & Nhịp tim (BPM):** Nhịp tim biến đổi theo độ căng thẳng/máu. Nhân vật sẽ thở dốc khi cạn Oxy. Có âm thanh rên rỉ khi chịu sát thương.
- **Toxicity (Khí độc) & Oxy:** Khí độc rút máu. Người chơi phải có thiết bị bảo hộ hoặc đến `OxygenSafeZone` để hồi phục.
- **Hành trang (Inventory):** Giới hạn slot, hỗ trợ UI kéo thả (`InventoryItemDrag`), hệ thống thiết bị/quần áo (`EquipmentManager`).
- **Crafting (Chế tạo):** Tương tác với `Workbench` và `CraftingUI` để ghép phế liệu thành vật phẩm xịn hơn.

### D. Giao Diện (UI) & Tương Tác
- Sử dụng mạnh mẽ **IMGUI (OnGUI)** với các hiệu ứng giả lập CRT, nhiễu sóng, scanline, phong cách Cyberpunk / Retro.
- `InteractionSystem` dùng Raycast bắn từ Camera để tương tác (phím E) với mọi thứ (Hòm, Trạm, Minigame, Phế liệu) thông qua interface `IInteractable`.
- Chú ý Kỹ thuật: Các bảng UI phải gọi `Cursor.lockState = CursorLockMode.None` và `PlayerController.IsUIOpen()` để không bị giật/kẹt chuột.

## 4. Cấu Trúc Thư Mục Quan Trọng (`Assets/Scripts/`)
- `Audio/`: Quản lý âm thanh môi trường.
- `Chest/`: Sinh rương đồ ngẫu nhiên, UI hòm đồ, thuật toán snap xuống mặt đất (`SpawnUtils.SnapToGround`).
- `Crafting/`: Hệ thống chế tạo, bàn làm việc.
- `Editor/`: Các công cụ Custom Editor hỗ trợ thiết kế map (tự động gắn Collider, xóa Collider cây, tạo Mimic, ánh sáng chớp tắt).
- `Enemies/`: Logic AI của Mimic và Spawner.
- `Environment/`: Chứa TẤT CẢ các trạm tương tác trong Waiting Room (Shop, Blackjack, Dice, Slot Machine, v.v.).
- `Escape/` & `Extraction/`: Kịch bản trốn thoát ngẫu nhiên.
- `Items/`: Spawner phế liệu, cơ sở dữ liệu Scrap.
- `Player/`: Sinh tồn, Điều khiển, Chết (DeathUIEffect), Túi đồ, Đeo mặt nạ.
- `UI/`: UI Kéo thả, hiệu ứng UIJuice, Animation cho UI.

## 5. Nhật Ký Cập Nhật (Gần Nhất)
- **Đại tu Đồ họa & Hoạt ảnh (Visuals & Animation Overhaul):**
  - Sửa lỗi mô hình và vật liệu của NPC (Claire, AJ), kích hoạt tính năng Alpha Clipping (URP) để khắc phục các mảng lỗi đồ họa trên khuôn mặt.
  - Tích hợp hệ thống Animator cho NPC: tự động vẫy tay khi lại gần (chỉ 1 lần) và chuyển sang trạng thái nói chuyện khi ấn E. Phân tách file hoạt ảnh riêng biệt (NPC_Idle, v.v.) để tránh xung đột hệ thống xương với người chơi (Player).
  - Nâng cấp chất lượng đồ họa cho `WaitingRoom`: Bật Post-Processing (Bloom, ACES Tonemapping, Vignette), ốp vật liệu PBR bóng loáng cho sàn/tường và thêm đèn Accent Lights (Xanh/Cam) cho các trạm tương tác.
  - Trùng tu bản đồ thám hiểm `Map` theo phong cách hậu tận thế (Dark & Gritty): Áp dụng màn sương mù xám đen, Film Grain u ám, hạ thấp nguồn sáng môi trường, thay toàn bộ 30+ bóng đèn đường thành màu cam ánh kim kết hợp với vật liệu mặt đường trơn ướt.
- **Kiến trúc Phòng Chờ (Waiting Room):** Phân chia lại 4 khu vực phòng riêng biệt (Giải trí, Mua bán, Nghỉ ngơi, Trưng bày) với các bức tường sơn màu để dễ điều hướng.
- **Sửa lỗi Hòm đồ:** Bổ sung thuật toán `SpawnUtils.SnapToGround` để tránh rương đồ (Chest) sinh ra bị lọt dưới mặt đất.
- **Đại tu Âm thanh Toàn diện (Audio Overhaul):**
  - Fix lỗi âm thanh Mimic đánh và tiếng Player bị đau chồng chéo liên tục. Mọi âm thanh UI/tương tác được ép về **2D** (`spatialBlend = 0f`) để đảm bảo người chơi nghe to, rõ bất kể khoảng cách Camera.
  - Thêm hiệu ứng âm thanh rùng rợn mỗi khi Mimic ở gần (15m).
  - Hoàn thiện âm thanh đặc trưng cho 4 Nhiệm vụ Trốn thoát: tiếng lắp ráp `assemble.wav`, tiếng radar `ping.wav`, tiếng gõ phím `button_press.wav`, và tiếng còi/nổ `alarm.wav` / `explosion.wav` của Lò phản ứng.
