# Báo Cáo Kiến Trúc & Logic Dự Án: MIMETO

**Ngày lập:** 09/08/2026
**Mức độ:** Phân tích Toàn diện (A-Z) từ cấu trúc mã nguồn.

---

## 1. TỔNG QUAN KIẾN TRÚC (ARCHITECTURE)
Dự án **Mimeto** là một game thể loại **Survival Horror / Extraction Multiplayer**. Kiến trúc game được chia module rất tốt (Modular Design), phân tách rõ ràng giữa các hệ thống: Player, AI, Environment, UI, Multiplayer và Escape. 
Sự hiện diện của các công cụ Custom Editor (`MultiplayerSetupTool.cs`, `MapAutoSetupTool.cs`) cho thấy dự án được thiết kế để dễ dàng mở rộng (scale) và thiết lập màn chơi tự động.

---

## 2. HỆ THỐNG TRÍ TUỆ NHÂN TẠO (ENEMY AI)
Hệ thống AI tập trung vào hành vi săn đuổi và sinh tồn kinh dị.
*   **MutantAI (`MutantAI.cs`):** 
    *   Sử dụng NavMesh để di chuyển.
    *   Logic chiến đấu đã được tinh chỉnh 2D distance và Leniency (như đã cập nhật) để chống giật lag và tối ưu hitbox.
    *   Sát thương kết hợp: Trừ HP trực tiếp + Hiệu ứng Chảy máu (Bleed) không cộng dồn, giúp cân bằng nhịp độ.
*   **ExilerAI (`ExilerAI.cs`):** Một biến thể AI khác, có thể phức tạp hơn hoặc đóng vai trò khác (ẩn nấp, ám sát) trong game.
*   **Spawner System (`MutantSpawner.cs`, `MutantSpawnPoint.cs`):** Hệ thống sinh quái tự động, tính toán trọng số (weight) và khoảng cách an toàn với người chơi (trên 20m), có hỗ trợ 1-Click setup từ Editor.

---

## 3. LOGIC NGƯỜI CHƠI (PLAYER SYSTEM)
Người chơi không chỉ di chuyển mà còn phải quản lý sinh tồn và tương tác sâu với môi trường.
*   **Core Controller (`PlayerController.cs`, 33KB):** Xử lý di chuyển cốt lõi, tích hợp chặt chẽ với hệ thống Input và Audio (`PlayerAudioController.cs`).
*   **Survival Mechanics (`PlayerSurvival.cs`, 35KB):** Quản lý Máu (HP), Thể lực (Stamina), Oxy/Độc tố. Có tương tác với các trạng thái dị thường (`PlayerStatusEffect.cs`) và điểm an toàn (`OxygenSafeZone.cs`).
*   **Inventory & Equipment (`PlayerInventory.cs`, `EquipmentManager.cs`):** Quản lý đồ đạc, kết hợp với hệ thống nhặt đồ 3D (`InteractionSystem.cs`, `Live3DItemViewer.cs`).

---

## 4. GIAO DIỆN NGƯỜI DÙNG (UI & UX)
Giao diện tuân thủ phong cách **Holographic Industrial** (Thiết kế GDD).
*   **Core UI:** Quản lý Kho đồ (`InventoryUI.cs`, kéo thả với `InventoryItemDrag.cs`), Tiền tệ (`CurrencyUI.cs`), Cài đặt (`SettingsUI.cs`).
*   **Juice & Feedback (`UIJuice.cs`, `UITweenAnimator.cs`):** Cung cấp các hiệu ứng hover, popup, digital glitch để tăng cảm giác "High-Tech Scavenger". Hiệu ứng chết (`DeathUIEffect.cs`) tạo độ căng thẳng.
*   **Lobby & Network UI (`MultiplayerCenter.cs`, `StartGameUI.cs`):** Quản lý luồng vào game và tìm phòng.

---

## 5. CƠ CHẾ THOÁT HIỂM & KẾT THÚC (ESCAPE & EXTRACTION)
Đây là cốt lõi của vòng lặp game (Core Loop).
*   **Escape Manager (`EscapeManager.cs`):** Điều phối toàn bộ quá trình thoát hiểm. Game hỗ trợ ngẫu nhiên hóa các điểm/cách thoát (`EscapeRandomizer.cs`).
*   **Nhiều phương thức thoát (Escape Methods):**
    *   `EscapeAssembly.cs`: Lắp ráp linh kiện.
    *   `EscapeBeacon.cs`: Gọi tín hiệu cứu hộ.
    *   `EscapeCipher.cs`: Giải mã mật mã.
    *   `EscapeReactor.cs`: Khởi động lò phản ứng.

---

## 6. ĐA NGƯỜI CHƠI (MULTIPLAYER)
Sử dụng giải pháp mạng (có vẻ như là Netcode for GameObjects dựa trên các script `ClientNetworkAnimator.cs`).
*   **Lobby & Voice:** Tích hợp Vivox cho Voice Chat (`VivoxManager.cs`) và quản lý phòng (`LobbyManager.cs`).
*   **Đồng bộ:** Dữ liệu người chơi toàn cục (`GlobalPlayerData.cs`) và đồng bộ Animation/Transform phía Client.

---

## 7. MÔI TRƯỜNG & KINH TẾ (ENVIRONMENT & STATIONS)
Game có hệ thống kinh tế và tương tác môi trường rất phong phú (Gamble & Trade).
*   **Trạm tương tác (Stations):** `ShopStation.cs` (Mua bán), `ScrapSellStation.cs` (Bán phế liệu).
*   **Gacha/Gamble:** `BlackjackStation.cs`, `DiceBetStation.cs`, `SlotMachineStation.cs` -> Cho thấy game có yếu tố "đánh cược" rủi ro cao/phần thưởng lớn bằng tài nguyên kiếm được.
*   **Kinh dị sinh tồn:** `FlickerLight.cs`, `GasLeak.cs`, môi trường rác thải (`ScrapScatterer.cs`) tạo không khí ngột ngạt.

---

### 💡 ĐÁNH GIÁ TỔNG KẾT TỪ DEV 50 NĂM KINH NGHIỆM:
1. **Điểm mạnh:** Game có vòng lặp (Loop) cực kỳ rõ ràng: Thu thập (Scrap) -> Sinh tồn (Survival/Mutant) -> Tương tác/Gamble (Stations) -> Thoát hiểm (Escape). Việc chia tách logic và viết Custom Editor Tools cho thấy tư duy kỹ thuật rất vững.
2. **Điểm cần lưu ý (Rủi ro tiềm ẩn):** 
   - `PlayerController.cs` và `PlayerSurvival.cs` khá lớn (>30KB). Nếu không cẩn thận, chúng có thể trở thành "God Classes". Nên xem xét tách bớt logic (ví dụ: tách State Machine cho Player Movement).
   - Có rất nhiều trạm mini-game (Blackjack, Dice, Slot). Cần đảm bảo logic server-authoritative chặt chẽ để chống hack tiền trong chế độ Multiplayer.

---

## 8. CẬP NHẬT VÀ TỐI ƯU HÓA GẦN ĐÂY (MULTIPLAYER & LOGIC)
Nhằm tăng tính ổn định và công bằng cho chế độ chơi nhiều người, một đợt tái cấu trúc (Refactor) và nâng cấp diện rộng đã được thực hiện:

*   **Hệ thống Tiền tệ & Nhặt đồ (Chest/Inventory):**
    *   Chuyển hoàn toàn cơ chế nhặt đồ và cộng/trừ tiền sang mô hình **Server-Authoritative**. Máy chủ sẽ là bên kiểm duyệt duy nhất cho mọi tương tác nhặt đồ từ rương và tính toán tiền bạc.
    *   Đã vá các lỗ hổng liên quan đến việc nhân bản đồ (Duplication Glitch) khi nhiều người nhặt cùng lúc, và lỗi nhận/trừ tiền gấp đôi của Host.
*   **Hệ thống Trạm Mini-game (Dice, Slot):**
    *   Đã được nâng cấp để Client chỉ xử lý UI và Animation, mọi logic quay thưởng, tính toán thắng/thua được chuyển lên Server nhằm chặn mọi hình thức gian lận (Hack Credits).
*   **Hệ thống Trí tuệ Nhân tạo (Enemy AI):**
    *   Sử dụng **NetworkVariables** để đồng bộ Tốc độ và Trạng thái của quái vật (Mutant, Exiler). Nhờ vậy, mọi người chơi (Client) đều thấy được hoạt ảnh chạy, tấn công, khựng lại của quái vật một cách mượt mà và chính xác 100% so với Host.
*   **Cơ chế Thoát hiểm & Môi trường (Escape & Hazards):**
    *   **Đồng bộ Tọa độ (Spawn Desync):** Các vị trí sinh đồ ngẫu nhiên (`EscapeAssembly`, `EscapeCipher`) giờ đây sử dụng tọa độ trung tâm tuyệt đối và `GlobalMatchSeed` chung, chấm dứt hoàn toàn tình trạng lệch vị trí vật phẩm giữa Host và Client.
    *   **Logic Trừng phạt & Sát thương:** Sửa logic phạt của bảng mật mã (chỉ trừ máu người nhập sai) và logic vụ nổ Lò phản ứng (gây sát thương cho tất cả người chơi trong phạm vi thay vì chỉ 1 người).
    *   **Sửa lỗi kẹt UI (Softlock):** Xử lý triệt để tình trạng kẹt chuột khi đóng giao diện của `EscapeBeacon`.
    *   **Đa nhiệm Khí độc (GasLeak):** Nâng cấp logic theo dõi khí độc bằng dạng Danh sách (Dictionary), cho phép xử lý hiệu ứng giảm tốc và trừ oxy chính xác khi có nhiều người chơi cùng lúc dẫm vào khí độc.

**[HẾT BÁO CÁO]**
