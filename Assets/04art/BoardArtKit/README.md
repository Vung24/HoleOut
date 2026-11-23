# Board Art Kit

Bộ sprite PNG rời cho bàn chơi, giữ màu và kênh alpha. Không phóng lớn ảnh.

## Import Unity

Copy cả folder BoardArtKit vào Assets/04art/; giữ các file .png.meta đi cùng. Texture được cấu hình Sprite (2D and UI), Single, 100 Pixels Per Unit, Bilinear, không mipmap và không nén. GUID của bộ này được tạo riêng.

## Thành phần

- Textures/Blocks/<Màu>/: mặt giữa, cạnh, góc lồi/lõm, phần nối, bóng và DotSprite (nút tròn nổi), đủ 12 màu.
- Textures/Walls/: thành bàn, góc và bóng tương ứng.
- Textures/Gates/: mảnh nền và mép cổng theo màu; hậu tố h/v giữ theo tên sprite, kiểm tra chiều hiển thị khi ghép.
- Textures/GateArrows/: mũi tên chỉ hướng.
- Textures/Floor/: ô nền bàn.
- Textures/Shadows/: bóng trong ô và góc.

## Ghép block

Chín mảnh cơ bản: ConvexUpLeft1, LinearXUp2, ConvexUpRight3 / LinearYLeft4, Close5, LinearYRight6 / ConvexDownLeft7, LinearXDown8, ConvexDownRight9. Dùng các phần Concave và Connection cho góc lõm và các điểm nối. DotSprite là lớp riêng để đặt nút tròn nổi trên mặt block; chừa khoảng cho miệng hole và số đếm. Không kéo giãn toàn bộ ảnh nút tròn theo kích thước block.

Bộ này chứa các mảnh hiển thị, chưa gồm bộ ghép block tự động, collider, logic kéo, shader hiệu ứng hay chuyển động về tray. Chưa gồm hole/cup 3D. Pivot của PNG rời dùng tâm; khi ghép cần đặt vị trí và sorting order cho từng lớp.

manifest.json ghi kích thước và vị trí từng sprite để đối chiếu. Preview-*.jpg là bảng xem trước, không cần import vào Assets.

