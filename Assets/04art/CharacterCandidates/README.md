# Character Candidates Preview

Scene xem thử riêng cho các mesh nhân vật ứng viên và texture animation trong `Assets/04art/CharacterCandidates/`.

## Cách dựng và mở scene preview

1. Mở project trong Unity Editor.
2. Trên thanh menu chọn: **`Tools > Character Candidates > Build Preview Scene`**.
   - Tự động cấu hình import cho các texture trong `AnimMap/` (không nén `RGBA32`, tắt mipmap, tắt sRGB, filter `Point`, wrap `Clamp`, `NPOT Scale = None`, giữ nguyên GUID).
   - Đọc trực tiếp từng file PNG ở không gian tuyến tính bằng `Texture2D.LoadImage` (không phụ thuộc cờ `Read/Write` trên TextureImporter), quét các pixel RGB khác 0 để tìm số cột vertex thực sự được dùng:
     - Các animMap 512 cột dùng 302 cột đầu → tự khớp với `Mesh/Cube.asset` (302 vertex, thân nhân vật).
     - `character_head_only_idle` (256 cột) dùng 129 cột đầu → tự khớp với `Mesh/Cube_0.asset` (129 vertex).
     - Nếu không có mesh nào trong `Mesh/` có số vertex bằng đúng số cột sử dụng, builder sẽ log cảnh báo và bỏ qua clip đó.
   - Tự động tính `_BoundsMin` / `_BoundsMax` mặc định riêng cho từng clip bằng hồi quy tuyến tính bình phương tối thiểu theo từng trục (`R ↔ x`, `G ↔ y`, `B ↔ z`) giữa frame 0 (hàng `y = 0`) và danh sách vertex tư thế gốc của mesh khớp (`pos = k * c + b` ⇒ `_BoundsMin = b`, `_BoundsMax = k + b`), đồng thời ghi log giá trị tính được cùng hệ số tương quan `r` ra Console.
   - Tạo hoặc cập nhật material trong `Materials/` và dựng lại scene tại `Assets/04art/CharacterCandidates/CharacterPreview.unity`.
3. Để xem animation chạy liên tục:
   - Bấm **Play**, hoặc
   - Trong **Scene View**, bật biểu tượng **Always Refresh** (cập nhật liên tục theo thời gian).

## Bố cục trong scene

- **Hàng 1 (`AnimatedCandidates`)**: Các đối tượng chạy animMap với đúng mesh khớp theo số cột thực dùng (`Cube` 302 vertex hoặc `Cube_0` 129 vertex), mỗi đối tượng gắn material `Materials/Mat_AnimMap_<tên_clip>.mat`, kèm nhãn tên clip, tên mesh và kích thước texture.
- **Hàng 2 (`StaticCandidates`)**: Toàn bộ mesh tĩnh trong `Mesh/` ở tư thế gốc — bao gồm `Head`, `Cube`, `Cube_0` và các mesh khác (`fire_man`, `Body`, `body1`, `Char_Ambulance_2`, `eye_*`, ...). Các mesh có kích thước quá lớn như `Char_Ambulance_2` được tự động thu nhỏ về cùng tầm kích thước tham chiếu (`Cube`) để dễ quan sát.

## Cách lấy mẫu và chỉnh thông số material

Shader `CharacterCandidates/AnimMapPreview` đọc mỗi cột là một vertex theo `SV_VertexID` và mỗi hàng là một frame theo kích thước texture thật (`_AnimMap_TexelSize`):

- `u = (vertexID + 0.5) / width`
- `v = (frame + 0.5) / height`
- `pos = lerp(_BoundsMin, _BoundsMax, rgb)`

Chọn một hoặc nhiều material `Assets/04art/CharacterCandidates/Materials/Mat_AnimMap_*.mat` để tinh chỉnh trong Inspector:

- **`Bounds Min` / `Bounds Max`** (`_BoundsMin`, `_BoundsMax`): Khoảng giải mã từ RGB tuyến tính `0..1` sang tọa độ vertex local của từng clip. Giá trị mặc định được ước lượng tự động từ hồi quy frame 0 so với tư thế gốc (chỉ gần đúng khi frame 0 lệch tư thế gốc), nên có thể tinh chỉnh thêm trong Inspector nếu cần.
- **`Speed (Loops/Sec)`** (`_Speed`): Số vòng lặp animation mỗi giây (mặc định `1.0`, đặt `0` để dừng tại frame 0 và so sánh trực tiếp với mesh gốc).
- **`Frame Count`** (`_FrameCount`): Số frame dùng để lặp (mặc định bằng chiều cao texture).
- **`Enable Animation`** (`_EnableAnim`): Tắt để xem lại mesh tĩnh tư thế gốc ngay tại vị trí clip.
- **`Interpolate Frames`** (`_InterpolateFrames`): Bật để nội suy mượt giữa 2 frame liền kề theo trục V (trục U vẫn lấy mẫu đúng tâm texel từng vertex). Tắt để xem từng frame rời rạc.
- **`Flip V`** (`_FlipV`): Mặc định `0` (Unity lưu hàng 0 ở `v = 0`, đáy ảnh). Bật nếu cần đảo chiều đọc thứ tự frame.
- **`Cull Mode`** (`_Cull`): Mặc định `Back`; có thể đổi sang `Off` để kiểm tra hai mặt khi tinh chỉnh bounds.

## Lưu ý

- `Mesh/Cube.asset` (302 vertex) và `Mesh/Cube_0.asset` (129 vertex) có cùng `m_Name` nội bộ là `Cube`, nên builder dùng tên file asset để hiển thị rõ ràng trên Hierarchy và nhãn.
- `Head.asset` (430 vertex) không khớp với animMap nào nên chỉ xuất hiện ở hàng mesh tĩnh.
- Khi chạy lại menu **`Build Preview Scene`**, builder sẽ tính lại và ghi đè `_BoundsMin`, `_BoundsMax`, `_AnimMap` và `_FrameCount` trên các material `Mat_AnimMap_*.mat` hiện có (các thiết lập hiển thị như `_Speed`, `_Color`, `_InterpolateFrames`, `_FlipV`, `_Cull` trên material có sẵn được giữ nguyên).
