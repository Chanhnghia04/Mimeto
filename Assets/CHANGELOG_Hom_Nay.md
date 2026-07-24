# 📝 Tổng Hợp Các Thay Đổi (Cập Nhật Hôm Nay)

Dưới đây là danh sách toàn bộ các tính năng và lỗi đã được xử lý trong ngày hôm nay:

## 1. Cơ Chế Chiến Đấu & Sát Thương Của Mutant
* **Sửa lỗi Mutant đánh hụt / giật lag:** 
  - Thay đổi cách tính khoảng cách từ 3D sang 2D (bỏ qua trục Y) để giải quyết triệt để lỗi tâm (pivot) bị lệch cao độ khiến Mutant tưởng Player ở ngoài tầm đánh.
  - Thêm "độ trễ sát thương" (Leniency +1.5m). Nếu Player lọt vào tầm đánh khi Mutant vung tay, dù lùi lại 1 chút vẫn sẽ dính đòn.
  - Thêm vùng đệm (buffer +0.5m) để Mutant không bị kẹt chớp nhoáng (stuttering) giữa trạng thái Chạy và Đánh.
* **Cân bằng Sát Thương & Chảy Máu (Bleed):**
  - Xóa bỏ sát thương thụ động (Toxic Aura) khi đứng gần Mutant.
  - **Logic mới:** 
    - Đòn đánh đầu tiên trừ thẳng **10 HP** và gây hiệu ứng chảy máu (**2 HP/giây** kéo dài đúng **4 giây**).
    - Nếu bị Mutant đập bồi thêm trong lúc đang chảy máu: Vẫn mất **10 HP** cho đòn đánh đó, nhưng **KHÔNG** làm mới hay kéo dài thời gian chảy máu. (Để tránh việc sát thương dồn quá nhanh khiến người chơi "đột tử", đồng thời giữ cho hiệu ứng chảy máu tự hết đúng 4s kể từ đòn đầu tiên).

## 2. Hệ Thống Spawner Cho Mutant (Giống Mimic)
* Tạo script `MutantSpawner.cs` và `MutantSpawnPoint.cs` hoạt động hoàn toàn tự động giống hệ thống của Mimic.
* **Cơ chế hoạt động:** Random vị trí spawn dựa trên trọng số (weight), bắt buộc phải nằm trên NavMesh và tự động tránh việc spawn quá gần người chơi (tối thiểu cách 20m).
* **Công cụ 1-Click Setup:** Viết thêm Editor Tool tại thanh menu `Tools -> Mimeto -> Tạo Mutant Spawner`. Chỉ cần bấm 1 phát là game tự vẽ ra 8 điểm Spawn xung quanh map và thiết lập sẵn Spawner GameObject.

## 3. Hiệu Ứng Phát Sáng (Glow) Cho Vật Phẩm (Item)
* Chỉnh sửa script `ScrapItem.cs` (áp dụng cho Pin, Bo mạch, v.v.).
* Xóa bỏ cơ chế xoay/lơ lửng ban đầu theo yêu cầu.
* Thêm hiệu ứng **Hào Quang (Aura Glow)**: Tự động sinh ra một nguồn sáng (Point Light) tỏa ra xung quanh item khi nó rơi xuống đất.
* Mặc định màu sáng là **Xanh lơ (Cyan)**, bán kính nhỏ đủ để người chơi dễ dàng nhận diện và nhặt đồ trong bóng tối.
