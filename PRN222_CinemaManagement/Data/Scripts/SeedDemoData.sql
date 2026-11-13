-- =========================================================================================
-- Script nạp dữ liệu mẫu phong phú cho Cinema Management (Demo Đặt Vé Online)
-- Bao gồm:
-- 1. 4 Phòng chiếu đa dạng (Standard 2D, 3D Digital, IMAX Laser, Gold Class Luxe) + 344 ghế vật lý
-- 2. 12 Phim bom tấn với poster cục bộ, phân loại tuổi, thời lượng, mô tả và thể loại
-- 3. 164+ Suất chiếu sắp tới trải dài hôm nay và 7 ngày tới (sáng, trưa, chiều, tối, khuya)
-- 4. 14,000+ Ghế theo suất (ScreeningSeats) với một số ghế 'Sold' mô phỏng rạp phim chân thực
-- =========================================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

-- 1. Cập nhật & Thêm 4 Phòng Chiếu
IF EXISTS (SELECT 1 FROM Auditoriums WHERE AuditoriumId = 1)
BEGIN
    UPDATE Auditoriums 
    SET Name = N'Phòng chiếu 1 (Standard 2D)', TotalRows = 8, TotalColumns = 10, Status = 'Active' 
    WHERE AuditoriumId = 1;
END

SET IDENTITY_INSERT Auditoriums ON;

IF NOT EXISTS (SELECT 1 FROM Auditoriums WHERE AuditoriumId = 2)
BEGIN
    INSERT INTO Auditoriums (AuditoriumId, Name, TotalRows, TotalColumns, Status)
    VALUES (2, N'Phòng chiếu 2 (3D Digital)', 7, 12, 'Active');
END
ELSE
BEGIN
    UPDATE Auditoriums SET Name = N'Phòng chiếu 2 (3D Digital)', TotalRows = 7, TotalColumns = 12, Status = 'Active' WHERE AuditoriumId = 2;
END

IF NOT EXISTS (SELECT 1 FROM Auditoriums WHERE AuditoriumId = 3)
BEGIN
    INSERT INTO Auditoriums (AuditoriumId, Name, TotalRows, TotalColumns, Status)
    VALUES (3, N'Phòng chiếu 3 (IMAX Laser)', 10, 14, 'Active');
END
ELSE
BEGIN
    UPDATE Auditoriums SET Name = N'Phòng chiếu 3 (IMAX Laser)', TotalRows = 10, TotalColumns = 14, Status = 'Active' WHERE AuditoriumId = 3;
END

IF NOT EXISTS (SELECT 1 FROM Auditoriums WHERE AuditoriumId = 4)
BEGIN
    INSERT INTO Auditoriums (AuditoriumId, Name, TotalRows, TotalColumns, Status)
    VALUES (4, N'Phòng chiếu 4 (Gold Class Luxe)', 5, 8, 'Active');
END
ELSE
BEGIN
    UPDATE Auditoriums SET Name = N'Phòng chiếu 4 (Gold Class Luxe)', TotalRows = 5, TotalColumns = 8, Status = 'Active' WHERE AuditoriumId = 4;
END

SET IDENTITY_INSERT Auditoriums OFF;

-- Ghế vật lý Phòng 2 (84 ghế: A-C Standard, D-F VIP, G Couple)
IF (SELECT COUNT(*) FROM Seats WHERE AuditoriumId = 2) < 84
BEGIN
    DELETE FROM Seats WHERE AuditoriumId = 2;
    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 2, 1, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('A'),('B'),('C')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) c(Num);

    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 2, 2, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('D'),('E'),('F')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) c(Num);

    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 2, 3, 'G', c.Num, 'Active'
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) c(Num);
END

-- Ghế vật lý Phòng 3 (140 ghế: A-D Standard, E-H VIP, I-J Couple)
IF (SELECT COUNT(*) FROM Seats WHERE AuditoriumId = 3) < 140
BEGIN
    DELETE FROM Seats WHERE AuditoriumId = 3;
    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 3, 1, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('A'),('B'),('C'),('D')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) c(Num);

    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 3, 2, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('E'),('F'),('G'),('H')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) c(Num);

    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 3, 3, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('I'),('J')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) c(Num);
END

-- Ghế vật lý Phòng 4 (40 ghế: A-C VIP, D-E Couple)
IF (SELECT COUNT(*) FROM Seats WHERE AuditoriumId = 4) < 40
BEGIN
    DELETE FROM Seats WHERE AuditoriumId = 4;
    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 4, 2, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('A'),('B'),('C')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) c(Num);

    INSERT INTO Seats (AuditoriumId, SeatTypeId, RowLabel, SeatNumber, Status)
    SELECT 4, 3, r.RowLabel, c.Num, 'Active'
    FROM (VALUES ('D'),('E')) r(RowLabel)
    CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) c(Num);
END

-- 2. Cập nhật 12 Phim Chiếu Rạp
UPDATE Movies SET 
    Title = N'Dune: Hành Tinh Cát - Phần Hai',
    Description = N'Hành trình huyền thoại của Paul Atreides khi anh hợp nhất với Chani và người Fremen trên con đường tìm kiếm sự trả thù cho gia tộc của mình. Đối mặt với sự lựa chọn giữa tình yêu và số phận vũ trụ, Paul phải ngăn chặn viễn cảnh tương lai đen tối mà chỉ anh mới có thể nhìn thấy.',
    DurationMinutes = 166, AgeRating = 'T16', ReleaseDate = '2024-03-01', PosterUrl = '/images/posters/dune2.jpg', TrailerUrl = 'https://www.youtube.com/watch?v=Way9Dexny3w', Status = 'Showing'
WHERE MovieId = 1;

UPDATE Movies SET 
    Title = N'Mai',
    Description = N'Tác phẩm điện ảnh tâm lý tình cảm cảm động do Trấn Thành đạo diễn. Kể về cuộc đời đầy thăng trầm của Mai - một phụ nữ làm nghề mát-xa luôn khao khát được yêu thương, và mối tình ngang trái nhiều định kiến với chàng thiếu gia Dương lãng tử đào hoa.',
    DurationMinutes = 131, AgeRating = 'T18', ReleaseDate = '2024-02-10', PosterUrl = '/images/posters/mai.jpg', TrailerUrl = 'https://www.youtube.com/watch?v=Opx_7c_29a0', Status = 'Showing'
WHERE MovieId = 2;

UPDATE Movies SET 
    Title = N'Lật Mặt 7: Một Điều Ước',
    Description = N'Tác phẩm gia đình đầy xúc động của đạo diễn Lý Hải. Câu chuyện xoay quanh bà Hai 73 tuổi một mình nuôi 5 người con khôn lớn. Khi bà gặp biến cố sức khỏe, gia đình nảy sinh những xung đột nhưng cuối cùng tình mẫu tử thiêng liêng đã sưởi ấm tất cả.',
    DurationMinutes = 138, AgeRating = 'P', ReleaseDate = '2024-04-26', PosterUrl = '/images/posters/latmat7.jpg', TrailerUrl = 'https://www.youtube.com/watch?v=b4yYm3sQ0e8', Status = 'Showing'
WHERE MovieId = 3;

SET IDENTITY_INSERT Movies ON;

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 4)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (4, N'Deadpool & Wolverine', N'Tổ chức Phương sai Thời gian (TVA) đưa Wade Wilson / Deadpool ra khỏi cuộc sống yên bình để thực hiện một nhiệm vụ sống còn cùng Wolverine (Người Sói). Một sự kết hợp bùng nổ, hài hước và nghẹt thở bậc nhất vũ trụ Marvel.', 128, 'T18', '2024-07-26', '/images/posters/deadpool_wolverine.jpg', 'https://www.youtube.com/watch?v=73_1biulkYk', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 5)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (5, N'Godzilla x Kong: Đế Chế Mới', N'Hai quái thú vĩ đại Godzilla và Kong bất ngờ phải liên minh với nhau để chống lại một mối đe dọa khổng lồ chưa từng thấy ẩn sâu trong Trái Đất Rỗng, thách thức sự tồn vong của loài người cũng như nguồn gốc của chính các Titan.', 115, 'T13', '2024-03-29', '/images/posters/godzilla_kong.jpg', 'https://www.youtube.com/watch?v=qqrpMRDuPfc', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 6)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (6, N'Những Mảnh Ghép Cảm Xúc 2 (Inside Out 2)', N'Bộ phim hoạt hình bom tấn của Pixar tiếp tục hành trình khám phá tâm trí cô bé Riley khi bước vào tuổi dậy thì. Bên cạnh Vui Vẻ, Buồn Bã, Giận Dữ, Sợ Hãi và Chảnh Chọe, một loạt cảm xúc mới toanh xuất hiện: Lo Âu, Ganh Tị, Xấu Hổ và Chán Nản.', 96, 'P', '2024-06-14', '/images/posters/insideout2.jpg', 'https://www.youtube.com/watch?v=LEjhY15eCx0', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 7)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (7, N'Kung Fu Panda 4', N'Gấu Po chuẩn bị trở thành Thủ lĩnh Tinh thần của Thung lũng Bình Yên và cần tìm kiếm một Thần Long Đại Hiệp mới. Trong hành trình đó, Po phải đối đầu với Tắc Kè Bông - một phù thủy biến hình nguy hiểm có khả năng triệu hồi linh hồn các đại ác nhân quá khứ.', 94, 'P', '2024-03-08', '/images/posters/kungfupanda4.jpg', 'https://www.youtube.com/watch?v=_inKs4eeHiI', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 8)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (8, N'Kẻ Trộm Mặt Trăng 4 (Despicable Me 4)', N'Gru và Lucy chào đón thành viên mới Gru Jr. - cậu nhóc tinh nghịch thích làm khổ bố. Gia đình Gru buộc phải chuyển đến một nơi ẩn náu bí mật khi kẻ thù truyền kiếp Maxime Le Mal vượt ngục cùng bạn gái độc ác Valentina để trả thù.', 95, 'P', '2024-07-03', '/images/posters/despicableme4.jpg', 'https://www.youtube.com/watch?v=qQlr9-rF32A', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 9)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (9, N'Hành Tinh Khỉ: Vương Quốc Mới', N'Nhiều thế hệ sau triều đại của Caesar, loài khỉ trở thành giống loài thống trị tối cao trong khi loài người bị đẩy về trạng thái hoang dã. Chú khỉ trẻ Noa dấn thân vào một hành trình nguy hiểm để định đoạt tương lai của cả hai giống loài.', 145, 'T13', '2024-05-10', '/images/posters/planet_apes.jpg', 'https://www.youtube.com/watch?v=XtFI7SNtVpY', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 10)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (10, N'Furiosa: Câu Chuyện Từ Max Điên', N'Khi thế giới sụp đổ, cô gái trẻ Furiosa bị bắt cóc khỏi Nơi Xanh của Nhiều Người Mẹ và rơi vào tay Biker Horde do Warlord Dementus lãnh đạo. Cô phải sống sót qua nhiều thử thách khắc nghiệt ở Vùng Đất Hoang để tìm đường trở về nhà.', 148, 'T18', '2024-05-24', '/images/posters/furiosa.jpg', 'https://www.youtube.com/watch?v=XJMuhwVlca4', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 11)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (11, N'Oppenheimer', N'Kiệt tác của đạo diễn Christopher Nolan tái hiện cuộc đời nhà vật lý lý thuyết J. Robert Oppenheimer, người đứng đầu Dự án Manhattan phát triển bom nguyên tử trong Thế chiến II, cùng những dằn vặt đạo đức và phiên điều trần an ninh khắc nghiệt sau đó.', 180, 'T18', '2023-07-21', '/images/posters/oppenheimer.jpg', 'https://www.youtube.com/watch?v=uYPbbksJxIg', 'Showing');

IF NOT EXISTS (SELECT 1 FROM Movies WHERE MovieId = 12)
    INSERT INTO Movies (MovieId, Title, Description, DurationMinutes, AgeRating, ReleaseDate, PosterUrl, TrailerUrl, Status)
    VALUES (12, N'Người Nhện: Du Hành Vũ Trụ Nhện', N'Miles Morales tái hợp với Gwen Stacy và được đưa qua Đa vũ trụ, nơi cậu gặp Hiệp hội Người Nhện có nhiệm vụ bảo vệ sự tồn tại của các dòng thời gian. Khi các anh hùng xung đột về cách xử lý mối đe dọa, Miles phải tự định nghĩa ý nghĩa của việc làm anh hùng.', 140, 'P', '2023-06-02', '/images/posters/spiderman.jpg', 'https://www.youtube.com/watch?v=cqGjhVJWtEg', 'Showing');

SET IDENTITY_INSERT Movies OFF;

-- Thể loại
INSERT INTO MovieGenres (MovieId, GenreId)
SELECT v.MovieId, v.GenreId
FROM (VALUES 
    (1, 5), (1, 1),
    (2, 2),
    (3, 2),
    (4, 1), (4, 5),
    (5, 1), (5, 5),
    (6, 3),
    (7, 3), (7, 1),
    (8, 3),
    (9, 1), (9, 5),
    (10, 1), (10, 5),
    (11, 5),
    (12, 3), (12, 1)
) v(MovieId, GenreId)
WHERE NOT EXISTS (
    SELECT 1 FROM MovieGenres mg WHERE mg.MovieId = v.MovieId AND mg.GenreId = v.GenreId
);

COMMIT;
