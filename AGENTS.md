# Quy tắc phát triển dự án

- Chọn giải pháp đơn giản, tối ưu vừa đủ cho nhu cầu thực tế; tránh làm phức tạp hoặc xây dựng dư thừa khi chưa cần. Tách trách nhiệm rõ ràng, hạn chế phụ thuộc chéo để từng tính năng dễ mở rộng, thay thế hoặc gỡ bỏ mà không ảnh hưởng đến các phần không liên quan.
- Luôn xem đây là dự án được phát triển từ đầu; dùng cách diễn đạt và tư duy triển khai của một dự án độc lập.
- Không đưa các từ hoặc mô tả như `reverse`, `reverse engineering`, `decompile`, `reconstruct`, `clone`, “dịch ngược”, “tái dựng từ bản gốc”, “sao chép game gốc” hoặc cách diễn đạt tương tự vào nội dung tạo mới: tên file, tên class/biến/hàm, comment, log, tài liệu, commit, mô tả PR và phần giải thích cho người dùng.
- Mô tả tính năng, yêu cầu, thiết kế và thay đổi bằng hành vi thực tế của dự án. Không tự suy diễn hay nhắc đến nguồn gốc từ một dự án hoặc sản phẩm khác.
- Các thuật ngữ kỹ thuật chỉ thao tác đảo thứ tự hoặc đảo hướng thực sự (ví dụ `List.Reverse`) vẫn được dùng khi cần thiết cho chức năng; không đổi API hay làm sai hành vi chỉ để tránh từ khóa.
- Không tự động đổi tên hoặc sửa nội dung hiện có chỉ để áp dụng quy tắc này nếu việc đó nằm ngoài phạm vi tác vụ đang được yêu cầu.

# Quy tắc triển khai và kiểm tra

- Giữ đúng phạm vi yêu cầu: chỉ sửa những phần cần thiết; tránh chỉnh sửa các phần không liên quan.
- Mỗi luật gameplay có một nơi quản lý thống nhất; tránh viết trùng logic ở nhiều chỗ. UI và hiệu ứng phản ánh trạng thái gameplay thay vì tự duy trì luật riêng.
- Tách dữ liệu cấu hình khỏi logic xử lý. Các thông số như màn chơi, màu sắc, tốc độ và sức chứa nên chỉnh được qua dữ liệu hoặc Unity Inspector khi phù hợp.
- Ưu tiên gắn sẵn reference cho object/component/asset cố định qua Unity Inspector trên scene/prefab; với object tạo lúc chạy, truyền reference từ nơi tạo qua hàm khởi tạo hoặc `Init(...)`. Chỉ được tìm reference lúc chạy bằng `Find*`, tìm theo tên/tag, duyệt hierarchy hoặc API tương tự khi bắt buộc do vòng đời động, không thể gắn/truyền reference trực tiếp, hoặc có căn cứ rõ ràng rằng cách tìm tối ưu hơn gắn sẵn. Không dùng tìm kiếm làm fallback chỉ để bỏ qua việc gắn reference bắt buộc. Khi cần tìm, giới hạn phạm vi và số lần, cache kết quả theo vòng đời phù hợp, đồng thời ghi rõ lý do ngoại lệ. Quy tắc này áp dụng cho việc lấy reference Unity, không áp dụng cho tìm kiếm dữ liệu gameplay hay tìm đường.
- Trước khi bàn giao, kiểm tra code biên dịch và các luồng bị ảnh hưởng; báo rõ nội dung đã kiểm tra và phần chưa thể kiểm chứng.
- Tối ưu có căn cứ: ưu tiên tính đúng và dễ đọc; chỉ thêm giải pháp tối ưu phức tạp khi đo được vấn đề hiệu năng.
- Khi chỉnh sửa Unity, giữ các liên kết scene/prefab hiện có; không tự ý sửa file `.meta`, đổi GUID hoặc làm mất liên kết tài nguyên.

# Quy tắc art và giao diện

- Lấy art của gameplay hiện hành làm chuẩn cho toàn bộ UI và visual, gồm Home, HUD, Cài đặt, popup, icon và hiệu ứng. Giữ đồng nhất bảng màu, typography, hình khối, bo góc, viền, chất liệu, ánh sáng và độ nổi của các thành phần.
- Với asset lấy từ nguồn khác hoặc tạo mới, chỉnh lại theo chuẩn art của gameplay trước khi đưa vào scene. Ưu tiên dùng lại asset và kiểu hiển thị đã được duyệt trong dự án.
- Khi duyệt giao diện, xem ảnh chụp Home, các popup mới và gameplay cạnh nhau ở tỷ lệ màn hình mục tiêu để kiểm tra độ nhất quán.

# Bản đặc tả cơ chế mới

- `Reference/HoleSortGarden/` là prototype web (Three.js) mô tả cơ chế gameplay đang chuyển sang Unity: kéo khối trượt trên bàn, đưa qua cổng cùng màu để hố lên khay chờ, nhân vật tìm đường ra mép dưới tranh, tranh dài trượt xuống khi hàng sạch.
- Luật nằm ở `game.js`, `drag.js`, `hole-doors.js`, `character-motion.js`, `crowd-intro.js`; dữ liệu màn ở `artworks.cjs`, `campaign.cjs`, `campaign.js`, `verified-solutions.json`; mô tả luật trong `README.md`.
- Chỉ đọc để tham khảo, không sửa file trong thư mục này. Không đặt nó vào `Assets/`.
