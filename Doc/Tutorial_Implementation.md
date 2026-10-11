# Tutorial cơ bản của PickleVerse

## Cách mở

- Scene: `My project/Assets/Scenes/Tutorial.unity`.
- Bấm **Chơi ngay / Play** ở đại sảnh luôn đi thẳng vào màn chọn chế độ, kể cả khi người chơi chưa học tutorial.
- Mục **TUTORIAL / HƯỚNG DẪN CƠ BẢN** ở màn chọn chế độ mở hướng dẫn khi người chơi chủ động chọn. Mục này ẩn khi đang cấu hình trận bot.
- Hoàn thành hoặc xác nhận **Để sau** sẽ chuyển sang màn chọn chế độ; có thể học lại bất cứ lúc nào.
- Đóng tutorial qua cài đặt không đánh dấu đã học xong. Chỉ hoàn thành đủ 9 cú đánh mới ghi nhận hoàn thành.

Tiến độ hiện lưu bằng PlayerPrefs trên thiết bị, qua `ITutorialProgressStore`. Khi có tài khoản/backend, có thể thay kho lưu bằng hồ sơ người chơi mà không đổi nội dung bài học. Hai cờ local là `PKV.Tutorial.Basic.Completed.v1` và `PKV.Tutorial.Basic.Skipped.v1`; đây chưa phải tiến độ đồng bộ theo tài khoản. Luồng tự mở tutorial khi khởi động app/cài lần đầu chưa được triển khai; nút Play không dùng cờ tiến độ để định tuyến nữa.

## Nội dung và trải nghiệm

Tutorial ngắn khoảng 2–3 phút, gồm 17 bước học và màn hoàn thành. Dùng sân, khán đài, sky, nhân vật và hiệu ứng bóng của BotMatch, với controller riêng để không phát sinh điểm, thưởng hoặc một trận đấu thật.

1. **Toàn sân:** camera bay nhìn sân; đường sáng chạy quanh biên; nhãn 13,41 m × 6,10 m. Giải thích bóng chạm vạch biên vẫn trong sân.
2. **Kitchen:** camera tiến gần lưới; hai vùng được viền sáng, hiện chiều sâu 2,13 m mỗi bên. Vạch kitchen thuộc vùng không volley; bóng đã nảy thì được đánh trong vùng này.
3. **Giao bóng và hai lần nảy:** nhân vật nam giao dưới tay từ sau cuối sân sang ô chéo. Nữ chờ bóng nảy rồi trả; nam cũng chờ bóng nảy trước khi đánh. Chú thích NẢY 1 / NẢY 2 hiện cùng diễn biến.
4. **Lỗi cơ bản:** các minh họa bằng animation và đường bay thật cho bóng không qua lưới, bóng ngoài biên, bóng nảy hai lần, và volley khi chân chạm kitchen. Camera đổi góc để nhìn rõ tình huống; Kitchen đổi sang màu cảnh báo khi phạm lỗi.
5. **Di chuyển:** người chơi phải di chuyển một đoạn và thử chạy. Dùng phím đã thiết lập trong cài đặt, mặc định WASD + Shift; cảm ứng dùng cần di chuyển và kéo xa để chạy.
6. **Chín lượt tập đánh:** đối phương đưa bóng đến đúng vùng tiếp xúc. Lời giải thích ở bên phải; lời nhắc phím ở góc trái dưới khi dùng bàn phím, phía trên khi dùng cảm ứng để không che nhân vật.
7. **Hoàn thành:** nhắc cách tính điểm cơ bản: đấu đơn đến 11, cách 2, chỉ bên giao ghi điểm; chuyển sang chọn chế độ để chơi BotMatch.

Các bài giới thiệu có nút Tiếp tục và Xem lại. Bài đánh chuyển tiếp sau khi người chơi chọn đúng cú và bóng thực sự tiếp xúc với vợt. Bấm sai không qua bài; bỏ lỡ bóng thì đối phương đưa lại. Có thể chọn cú sớm khi bóng đang tới để giảm áp lực căn thời điểm cho người mới.

## Phím và ý nghĩa cú đánh

| Mặc định | Bài làm quen | Bóng trước khi đánh |
|---|---|---|
| J | Drive: đưa bóng sâu | Đã nảy |
| K | Dink: đánh nhẹ vào kitchen đối phương | Đã nảy |
| L | Volley: đánh trên không, đứng ngoài kitchen | Chưa nảy |
| J + U | Drive mạnh | Đã nảy |
| K + U | Speed-up: chuyển thành cú tăng tốc | Đã nảy |
| L + U | Volley mạnh | Chưa nảy |
| J + I | Drive chéo | Đã nảy |
| K + I | Dink chéo | Đã nảy |
| L + I | Volley chéo | Chưa nảy |

K + U dùng cùng bộ giải mã input của BotMatch: nó là Drive mạnh/speed-up, không phải Dink mạnh. Mỗi bài dùng đúng `ShotInput`, `ShotIntent`, animation tiếp xúc, quỹ đạo và luật hiện có. Pha tập Volley bắt đầu ở trạng thái đã hoàn tất hai lần nảy bắt buộc.

Bàn phím: **giữ U hoặc I → bấm rồi thả J/K/L → thả nút bổ trợ**. Không có nút bổ trợ thì bấm rồi thả nút đánh. UI lấy binding hiện tại nên vẫn đúng sau khi người chơi đổi phím.

Cảm ứng: chỉ có Drive, Dink, Volley, Lực, Chéo và cần di chuyển. Chạm Lực/Chéo để bật rồi chạm nút đánh. Vị trí, kích thước, độ mờ và thiết lập thuận tay trái dùng dữ liệu cài đặt của game; anchor tương đối theo canvas giữ bố cục khi đổi tỷ lệ màn hình.

Tutorial không dạy Jump, Drop/Smash/Reset từ tổ hợp hai nút gốc, chiến thuật nâng cao hoặc AI thích ứng. Các nội dung đó dành cho phần luyện tập sau.

## Luật và nền móng kỹ thuật

- Kích thước dùng cùng hằng số gameplay: rộng 6,096 m, dài 13,4112 m, Kitchen 2,1336 m mỗi bên; UI làm tròn để dễ đọc.
- Bóng chạm lưới rồi vẫn qua hợp lệ không tự động là lỗi; minh họa lỗi lưới là bóng không vượt qua.
- Bóng chạm vạch biên được tính trong sân; khi giao bóng, vạch kitchen thuộc vùng giao không hợp lệ.
- Không volley khi chạm kitchen, và không để đà sau volley đưa người vào kitchen. Đánh bóng đã nảy trong kitchen được phép.
- Demo dùng `SinglesRules`; bài feed dùng cùng trọng lực và độ nảy của bóng trong gameplay. Animation phải tới cửa sổ tiếp xúc mới có thể đánh.
- `TutorialDirector` chịu trách nhiệm bài học, feed, camera và tiếp xúc; `TutorialHud` quản lý UI; `TutorialCurriculum` chứa nội dung và kế hoạch bóng; `TutorialProgress` tách kho tiến độ khỏi UI.
- Camera chuyển mượt giữa các phần, có chuyển động nhẹ ở phần sân/Kitchen và từng lượt đánh. Esc mở cài đặt; khi tạm dừng, timeline bài học dừng theo.
- Không có nhạc nền trong tutorial; giữ các âm đánh bóng và còi minh họa lỗi của gameplay.
- Scene được đăng ký trong build settings chung và profile MultiplayerWindows.

Nội dung luật đối chiếu [hướng dẫn chính thức của USA Pickleball](https://usapickleball.org/pickleball-skills/level-one/how-to-play-pickleball/) và [bộ luật hiện hành](https://usapickleball.org/rules/). Luồng làm quen ngắn → thử thao tác → vào chế độ chơi tham khảo cách [VALORANT giới thiệu Basic Training cho người mới](https://playvalorant.com/en-gb/news/game-updates/valorant-patch-notes-902/), không sao chép hình ảnh hoặc tài nguyên giao diện.

## Kiểm chứng

- Runtime: 32 kiểm tra về camera, các demo luật, di chuyển, đủ 9 tổ hợp và tiếp xúc vợt thực tế; tiến độ thật không bị thay đổi bởi kiểm thử.
- Tương tác: 16 kiểm tra về Play đi thẳng chọn chế độ, mở tutorial chủ động, lời nhắc binding, input sai, cảm ứng, tạm dừng, xác nhận để sau, học lại và chuyển scene.
- UI/cài đặt: 8 kiểm tra về đăng ký build, tham chiếu nhãn, mở/đóng cài đặt thật, đóng băng timeline và bố cục bàn phím/cảm ứng.
- Unit test: **121/121 test toàn project đã qua**, gồm 15 test tutorial về tiến độ, 9 tổ hợp qua bộ giải mã input thật, quỹ đạo feed, điều kiện Volley sau hai lần nảy và luật Kitchen.
- Kiểm tra hình ảnh trên tỷ lệ 16:9 và 4:3; ảnh và báo cáo để trong `UnityWork/Tutorial`, ngoài thư mục Assets để không tăng dung lượng game.

Các kiểm tra là trong Unity Editor, chưa thay thế thử nghiệm trên thiết bị Android/iOS thật hoặc đồng bộ tiến độ tài khoản.
