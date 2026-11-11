using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Data;

/// <summary>
/// Dữ liệu mẫu phong phú cho module đặt vé online:
/// - 4 phòng chiếu với các cấu hình đa dạng (Standard 2D, 3D Digital, IMAX Laser, Gold Class Luxe).
/// - 12 phim bom tấn với poster sắc nét, mô tả chi tiết, phân loại độ tuổi và thời lượng thực tế.
/// - Lịch chiếu dày đặc cho hôm nay và 7 ngày tới, bao gồm nhiều khung giờ từ sáng đến khuya.
/// - Lưới ghế ShowtimeSeats đầy đủ cho từng suất, điểm xuyết một số ghế Sold để mô phỏng rạp phim thực tế.
/// - Tài khoản khách hàng mẫu để demo tranh ghế và đặt vé.
/// </summary>
public static class BookingDemoData
{
    private record MovieDefinition(
        string Title,
        string Description,
        int DurationMinutes,
        string AgeRating,
        DateOnly ReleaseDate,
        string PosterUrl,
        string TrailerUrl,
        int[] GenreIds
    );

    public static async Task SeedAsync(CinemaDbContext context)
    {
        var now = DateTime.UtcNow;

        // 1. Đảm bảo đầy đủ 4 phòng chiếu và toàn bộ ghế vật lý
        await EnsureAuditoriumsAndSeatsAsync(context);

        // 2. Cập nhật và bổ sung 12 phim bom tấn đang chiếu
        var movies = await EnsureMoviesAsync(context);

        // 3. Tạo lịch chiếu cho hôm nay và 7 ngày tới
        await EnsureShowtimesAsync(context, movies, now);

        // 4. Tạo lưới ghế (ShowtimeSeats) cho tất cả các suất chiếu
        await EnsureShowtimeSeatsAsync(context, now);

        // 5. Bổ sung menu bắp, đồ uống và combo đa dạng
        await EnsureConcessionItemsAsync(context);
    }

    private static async Task EnsureAuditoriumsAndSeatsAsync(CinemaDbContext context)
    {
        var standardType = await context.SeatTypes.FirstOrDefaultAsync(t => t.TypeName == "Standard");
        var vipType = await context.SeatTypes.FirstOrDefaultAsync(t => t.TypeName == "VIP");
        var coupleType = await context.SeatTypes.FirstOrDefaultAsync(t => t.TypeName == "Couple");

        int standardId = standardType?.SeatTypeId ?? 1;
        int vipId = vipType?.SeatTypeId ?? 2;
        int coupleId = coupleType?.SeatTypeId ?? 3;

        // Danh sách 4 phòng chiếu chuẩn
        var roomConfigs = new[]
        {
            new { Id = 1, Name = "Phòng chiếu 1 (Standard 2D)", Rows = 8, Cols = 10 },
            new { Id = 2, Name = "Phòng chiếu 2 (3D Digital)", Rows = 7, Cols = 12 },
            new { Id = 3, Name = "Phòng chiếu 3 (IMAX Laser)", Rows = 10, Cols = 14 },
            new { Id = 4, Name = "Phòng chiếu 4 (Gold Class Luxe)", Rows = 5, Cols = 8 }
        };

        foreach (var config in roomConfigs)
        {
            var room = await context.Auditoriums.FirstOrDefaultAsync(a => a.AuditoriumId == config.Id);
            if (room == null)
            {
                room = new Auditorium
                {
                    Name = config.Name,
                    TotalRows = config.Rows,
                    TotalColumns = config.Cols,
                    Status = "Active"
                };
                context.Auditoriums.Add(room);
                await context.SaveChangesAsync();
            }
            else if (room.Name != config.Name)
            {
                room.Name = config.Name;
                room.TotalRows = config.Rows;
                room.TotalColumns = config.Cols;
                room.Status = "Active";
                await context.SaveChangesAsync();
            }

            // Kiểm tra và tạo ghế cho phòng
            var seatCount = await context.Seats.CountAsync(s => s.AuditoriumId == room.AuditoriumId);
            int expectedSeats = config.Rows * config.Cols;
            if (seatCount < expectedSeats)
            {
                var existingSeats = (await context.Seats
                    .Where(s => s.AuditoriumId == room.AuditoriumId)
                    .Select(s => s.RowLabel + s.SeatNumber)
                    .ToListAsync())
                    .ToHashSet();

                var newSeats = new List<Seat>();
                for (int r = 1; r <= config.Rows; r++)
                {
                    string rowLabel = ((char)('A' + r - 1)).ToString();

                    // Xác định loại ghế theo cấu hình từng phòng
                    int seatTypeId;
                    if (config.Id == 1) // Standard 2D
                    {
                        seatTypeId = (r >= 7) ? vipId : standardId;
                    }
                    else if (config.Id == 2) // 3D Digital
                    {
                        if (r <= 3) seatTypeId = standardId;
                        else if (r <= 6) seatTypeId = vipId;
                        else seatTypeId = coupleId;
                    }
                    else if (config.Id == 3) // IMAX Laser
                    {
                        if (r <= 4) seatTypeId = standardId;
                        else if (r <= 8) seatTypeId = vipId;
                        else seatTypeId = coupleId;
                    }
                    else // Gold Class Luxe
                    {
                        seatTypeId = (r <= 3) ? vipId : coupleId;
                    }

                    for (int c = 1; c <= config.Cols; c++)
                    {
                        if (!existingSeats.Contains(rowLabel + c))
                        {
                            newSeats.Add(new Seat
                            {
                                AuditoriumId = room.AuditoriumId,
                                RowLabel = rowLabel,
                                SeatNumber = c,
                                SeatTypeId = seatTypeId,
                                Status = "Active"
                            });
                        }
                    }
                }

                if (newSeats.Count > 0)
                {
                    context.Seats.AddRange(newSeats);
                    await context.SaveChangesAsync();
                }
            }
        }
    }

    private static async Task<List<Movie>> EnsureMoviesAsync(CinemaDbContext context)
    {
        var movieDefs = new List<MovieDefinition>
        {
            new(
                "Dune: Hành Tinh Cát - Phần Hai",
                "Hành trình huyền thoại của Paul Atreides khi anh hợp nhất với Chani và người Fremen trên con đường tìm kiếm sự trả thù cho gia tộc của mình. Đối mặt với sự lựa chọn giữa tình yêu và số phận vũ trụ, Paul phải ngăn chặn viễn cảnh tương lai đen tối mà chỉ anh mới có thể nhìn thấy.",
                166,
                "T16",
                new DateOnly(2024, 3, 1),
                "/images/posters/dune2.jpg",
                "https://www.youtube.com/watch?v=Way9Dexny3w",
                [5, 1]
            ),
            new(
                "Mai",
                "Tác phẩm điện ảnh tâm lý tình cảm cảm động do Trấn Thành đạo diễn. Kể về cuộc đời đầy thăng trầm của Mai - một phụ nữ làm nghề mát-xa luôn khao khát được yêu thương, và mối tình ngang trái nhiều định kiến với chàng thiếu gia Dương lãng tử đào hoa.",
                131,
                "T18",
                new DateOnly(2024, 2, 10),
                "/images/posters/mai.jpg",
                "https://www.youtube.com/watch?v=Opx_7c_29a0",
                [2]
            ),
            new(
                "Lật Mặt 7: Một Điều Ước",
                "Tác phẩm gia đình đầy xúc động của đạo diễn Lý Hải. Câu chuyện xoay quanh bà Hai 73 tuổi một mình nuôi 5 người con khôn lớn. Khi bà gặp biến cố sức khỏe, gia đình nảy sinh những xung đột nhưng cuối cùng tình mẫu tử thiêng liêng đã sưởi ấm tất cả.",
                138,
                "P",
                new DateOnly(2024, 4, 26),
                "/images/posters/latmat7.jpg",
                "https://www.youtube.com/watch?v=b4yYm3sQ0e8",
                [2]
            ),
            new(
                "Deadpool & Wolverine",
                "Tổ chức Phương sai Thời gian (TVA) đưa Wade Wilson / Deadpool ra khỏi cuộc sống yên bình để thực hiện một nhiệm vụ sống còn cùng Wolverine (Người Sói). Một sự kết hợp bùng nổ, hài hước và nghẹt thở bậc nhất vũ trụ Marvel.",
                128,
                "T18",
                new DateOnly(2024, 7, 26),
                "/images/posters/deadpool_wolverine.jpg",
                "https://www.youtube.com/watch?v=73_1biulkYk",
                [1, 5]
            ),
            new(
                "Godzilla x Kong: Đế Chế Mới",
                "Hai quái thú vĩ đại Godzilla và Kong bất ngờ phải liên minh với nhau để chống lại một mối đe dọa khổng lồ chưa từng thấy ẩn sâu trong Trái Đất Rỗng, thách thức sự tồn vong của loài người cũng như nguồn gốc của chính các Titan.",
                115,
                "T13",
                new DateOnly(2024, 3, 29),
                "/images/posters/godzilla_kong.jpg",
                "https://www.youtube.com/watch?v=qqrpMRDuPfc",
                [1, 5]
            ),
            new(
                "Những Mảnh Ghép Cảm Xúc 2 (Inside Out 2)",
                "Bộ phim hoạt hình bom tấn của Pixar tiếp tục hành trình khám phá tâm trí cô bé Riley khi bước vào tuổi dậy thì. Bên cạnh Vui Vẻ, Buồn Bã, Giận Dữ, Sợ Hãi và Chảnh Chọe, một loạt cảm xúc mới toanh xuất hiện: Lo Âu, Ganh Tị, Xấu Hổ và Chán Nản.",
                96,
                "P",
                new DateOnly(2024, 6, 14),
                "/images/posters/insideout2.jpg",
                "https://www.youtube.com/watch?v=LEjhY15eCx0",
                [3]
            ),
            new(
                "Kung Fu Panda 4",
                "Gấu Po chuẩn bị trở thành Thủ lĩnh Tinh thần của Thung lũng Bình Yên và cần tìm kiếm một Thần Long Đại Hiệp mới. Trong hành trình đó, Po phải đối đầu với Tắc Kè Bông - một phù thủy biến hình nguy hiểm có khả năng triệu hồi linh hồn các đại ác nhân quá khứ.",
                94,
                "P",
                new DateOnly(2024, 3, 8),
                "/images/posters/kungfupanda4.jpg",
                "https://www.youtube.com/watch?v=_inKs4eeHiI",
                [3, 1]
            ),
            new(
                "Kẻ Trộm Mặt Trăng 4 (Despicable Me 4)",
                "Gru và Lucy chào đón thành viên mới Gru Jr. - cậu nhóc tinh nghịch thích làm khổ bố. Gia đình Gru buộc phải chuyển đến một nơi ẩn náu bí mật khi kẻ thù truyền kiếp Maxime Le Mal vượt ngục cùng bạn gái độc ác Valentina để trả thù.",
                95,
                "P",
                new DateOnly(2024, 7, 3),
                "/images/posters/despicableme4.jpg",
                "https://www.youtube.com/watch?v=qQlr9-rF32A",
                [3]
            ),
            new(
                "Hành Tinh Khỉ: Vương Quốc Mới",
                "Nhiều thế hệ sau triều đại của Caesar, loài khỉ trở thành giống loài thống trị tối cao trong khi loài người bị đẩy về trạng thái hoang dã. Chú khỉ trẻ Noa dấn thân vào một hành trình nguy hiểm để định đoạt tương lai của cả hai giống loài.",
                145,
                "T13",
                new DateOnly(2024, 5, 10),
                "/images/posters/planet_apes.jpg",
                "https://www.youtube.com/watch?v=XtFI7SNtVpY",
                [1, 5]
            ),
            new(
                "Furiosa: Câu Chuyện Từ Max Điên",
                "Khi thế giới sụp đổ, cô gái trẻ Furiosa bị bắt cóc khỏi Nơi Xanh của Nhiều Người Mẹ và rơi vào tay Biker Horde do Warlord Dementus lãnh đạo. Cô phải sống sót qua nhiều thử thách khắc nghiệt ở Vùng Đất Hoang để tìm đường trở về nhà.",
                148,
                "T18",
                new DateOnly(2024, 5, 24),
                "/images/posters/furiosa.jpg",
                "https://www.youtube.com/watch?v=XJMuhwVlca4",
                [1, 5]
            ),
            new(
                "Oppenheimer",
                "Kiệt tác của đạo diễn Christopher Nolan tái hiện cuộc đời nhà vật lý lý thuyết J. Robert Oppenheimer, người đứng đầu Dự án Manhattan phát triển bom nguyên tử trong Thế chiến II, cùng những dằn vặt đạo đức và phiên điều trần an ninh khắc nghiệt sau đó.",
                180,
                "T18",
                new DateOnly(2023, 7, 21),
                "/images/posters/oppenheimer.jpg",
                "https://www.youtube.com/watch?v=uYPbbksJxIg",
                [5]
            ),
            new(
                "Người Nhện: Du Hành Vũ Trụ Nhện",
                "Miles Morales tái hợp với Gwen Stacy và được đưa qua Đa vũ trụ, nơi cậu gặp Hiệp hội Người Nhện có nhiệm vụ bảo vệ sự tồn tại của các dòng thời gian. Khi các anh hùng xung đột về cách xử lý mối đe dọa, Miles phải tự định nghĩa ý nghĩa của việc làm anh hùng.",
                140,
                "P",
                new DateOnly(2023, 6, 2),
                "/images/posters/spiderman.jpg",
                "https://www.youtube.com/watch?v=cqGjhVJWtEg",
                [3, 1]
            )
        };

        var existingMovies = await context.Movies.ToListAsync();

        // Cập nhật lại 3 phim mặc định cũ nếu có tên dummy
        if (existingMovies.Count >= 1 && existingMovies[0].Title == "Hành trình ngôi sao")
        {
            ApplyMovieData(existingMovies[0], movieDefs[0]);
        }
        if (existingMovies.Count >= 2 && existingMovies[1].Title == "Tâm điểm mùa hè")
        {
            ApplyMovieData(existingMovies[1], movieDefs[1]);
        }
        if (existingMovies.Count >= 3 && existingMovies[2].Title == "Cỗ máy thời gian")
        {
            ApplyMovieData(existingMovies[2], movieDefs[2]);
        }

        // Bổ sung các phim còn thiếu trong danh sách 12 phim
        foreach (var def in movieDefs)
        {
            var match = existingMovies.FirstOrDefault(m => m.Title == def.Title);
            if (match == null)
            {
                var newMovie = new Movie();
                ApplyMovieData(newMovie, def);
                context.Movies.Add(newMovie);
                existingMovies.Add(newMovie);
            }
            else
            {
                ApplyMovieData(match, def);
            }
        }
        await context.SaveChangesAsync();

        // Gán thể loại cho phim (MovieGenres)
        var movieGenreSet = (await context.MovieGenres
            .Select(mg => mg.MovieId + "_" + mg.GenreId)
            .ToListAsync())
            .ToHashSet();
        var newGenres = new List<MovieGenre>();
        foreach (var def in movieDefs)
        {
            var movie = existingMovies.First(m => m.Title == def.Title);
            foreach (var genreId in def.GenreIds)
            {
                string key = $"{movie.MovieId}_{genreId}";
                if (!movieGenreSet.Contains(key))
                {
                    newGenres.Add(new MovieGenre { MovieId = movie.MovieId, GenreId = genreId });
                    movieGenreSet.Add(key);
                }
            }
        }
        if (newGenres.Count > 0)
        {
            context.MovieGenres.AddRange(newGenres);
            await context.SaveChangesAsync();
        }

        return existingMovies.Where(m => m.Status == "Showing").ToList();
    }

    private static void ApplyMovieData(Movie movie, MovieDefinition def)
    {
        movie.Title = def.Title;
        movie.Description = def.Description;
        movie.DurationMinutes = def.DurationMinutes;
        movie.AgeRating = def.AgeRating;
        movie.ReleaseDate = def.ReleaseDate;
        movie.PosterUrl = def.PosterUrl;
        movie.TrailerUrl = def.TrailerUrl;
        movie.Status = "Showing";
    }

    private static async Task EnsureShowtimesAsync(CinemaDbContext context, List<Movie> movies, DateTime nowUtc)
    {
        if (movies.Count == 0) return;

        // Kiểm tra xem hiện có bao nhiêu suất chiếu sắp tới
        var upcomingCount = await context.Showtimes.CountAsync(s => s.StartAt > nowUtc);
        if (upcomingCount >= 30)
        {
            // Đã có đủ lịch chiếu phong phú, không cần tạo thêm
            return;
        }

        var auditoriums = await context.Auditoriums.Where(a => a.Status == "Active").ToListAsync();
        if (auditoriums.Count == 0) return;

        // Giờ Việt Nam hiện tại (UTC+7)
        var nowVn = nowUtc.AddHours(7);
        var baseDateVn = nowVn.Date;

        // Cấu hình khung giờ và giá vé theo từng phòng chiếu
        var scheduleTemplates = new Dictionary<int, (decimal BasePrice, TimeSpan[] Slots)>
        {
            // Phòng 1 (Standard 2D): 80,000đ - 6 suất mỗi ngày
            [1] = (80_000m, [
                new TimeSpan(8, 30, 0),
                new TimeSpan(11, 15, 0),
                new TimeSpan(14, 0, 0),
                new TimeSpan(16, 45, 0),
                new TimeSpan(19, 30, 0),
                new TimeSpan(22, 15, 0)
            ]),
            // Phòng 2 (3D Digital): 100,000đ - 6 suất mỗi ngày
            [2] = (100_000m, [
                new TimeSpan(9, 0, 0),
                new TimeSpan(11, 45, 0),
                new TimeSpan(14, 30, 0),
                new TimeSpan(17, 15, 0),
                new TimeSpan(20, 0, 0),
                new TimeSpan(22, 45, 0)
            ]),
            // Phòng 3 (IMAX Laser): 130,000đ - 5 suất mỗi ngày
            [3] = (130_000m, [
                new TimeSpan(9, 30, 0),
                new TimeSpan(12, 45, 0),
                new TimeSpan(16, 0, 0),
                new TimeSpan(19, 15, 0),
                new TimeSpan(22, 30, 0)
            ]),
            // Phòng 4 (Gold Class Luxe): 160,000đ - 5 suất mỗi ngày
            [4] = (160_000m, [
                new TimeSpan(10, 0, 0),
                new TimeSpan(13, 0, 0),
                new TimeSpan(15, 45, 0),
                new TimeSpan(18, 30, 0),
                new TimeSpan(21, 15, 0)
            ])
        };

        var existingShowtimes = await context.Showtimes
            .Select(s => new { s.AuditoriumId, s.StartAt })
            .ToListAsync();
        var existingSet = existingShowtimes.Select(s => $"{s.AuditoriumId}_{s.StartAt:yyyyMMddHHmm}").ToHashSet();

        var newShowtimes = new List<Showtime>();
        int movieIndex = 0;

        // Sinh lịch chiếu cho 8 ngày liên tiếp (hôm nay + 7 ngày tiếp theo)
        for (int dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var dayVn = baseDateVn.AddDays(dayOffset);

            foreach (var room in auditoriums)
            {
                if (!scheduleTemplates.TryGetValue(room.AuditoriumId, out var template))
                {
                    template = (85_000m, [new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0), new TimeSpan(18, 0, 0), new TimeSpan(21, 0, 0)]);
                }

                foreach (var slot in template.Slots)
                {
                    var startVn = dayVn.Add(slot);
                    var startUtc = startVn.AddHours(-7); // Đổi về UTC để lưu chuẩn CSDL

                    // Bỏ qua các suất đã qua trong ngày hôm nay
                    if (startUtc <= nowUtc)
                    {
                        continue;
                    }

                    string key = $"{room.AuditoriumId}_{startUtc:yyyyMMddHHmm}";
                    if (existingSet.Contains(key))
                    {
                        continue;
                    }

                    var movie = movies[(movieIndex++) % movies.Count];
                    var endUtc = startUtc.AddMinutes(movie.DurationMinutes);

                    newShowtimes.Add(new Showtime
                    {
                        MovieId = movie.MovieId,
                        AuditoriumId = room.AuditoriumId,
                        StartAt = startUtc,
                        EndAt = endUtc,
                        CleanupMinutes = 15,
                        BasePrice = template.BasePrice,
                        Status = "Scheduled"
                    });

                    existingSet.Add(key);
                }
            }
        }

        if (newShowtimes.Count > 0)
        {
            context.Showtimes.AddRange(newShowtimes);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureShowtimeSeatsAsync(CinemaDbContext context, DateTime nowUtc)
    {
        var existingSeatShowtimes = (await context.ShowtimeSeats
            .Select(s => s.ShowtimeId)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var showtimesWithoutSeats = await context.Showtimes.AsNoTracking()
            .Where(s => !existingSeatShowtimes.Contains(s.ShowtimeId))
            .ToListAsync();

        if (showtimesWithoutSeats.Count == 0) return;

        var allPhysicalSeats = await context.Seats.AsNoTracking().ToListAsync();
        var seatMultipliers = await context.SeatTypes.AsNoTracking()
            .ToDictionaryAsync(st => st.SeatTypeId, st => st.PriceMultiplier);

        var batch = new List<ShowtimeSeat>();
        foreach (var show in showtimesWithoutSeats)
        {
            var roomSeats = allPhysicalSeats.Where(s => s.AuditoriumId == show.AuditoriumId).ToList();

            // Đối với các suất chiếu hôm nay và ngày mai, mô phỏng một số ghế đã bán (Sold)
            bool isNearShow = show.StartAt < nowUtc.AddHours(36);

            foreach (var seat in roomSeats)
            {
                var mult = seatMultipliers.TryGetValue(seat.SeatTypeId, out var m) ? m : 1.000m;
                var price = Math.Round(show.BasePrice * mult, 2);

                string seatStatus = "Available";
                // Mô phỏng ghế đã bán ở các hàng giữa (D, E, F) để sơ đồ ghế trông tự nhiên
                if (isNearShow && seat.SeatNumber >= 4 && seat.SeatNumber <= 7)
                {
                    if (seat.RowLabel is "D" or "E" or "F" && (seat.SeatId + show.ShowtimeId) % 3 == 0)
                    {
                        seatStatus = "Sold";
                    }
                }

                batch.Add(new ShowtimeSeat
                {
                    ShowtimeId = show.ShowtimeId,
                    AuditoriumId = show.AuditoriumId,
                    SeatId = seat.SeatId,
                    Price = price,
                    Status = seatStatus
                });

                // Lưu theo đợt 2000 bản ghi để tối ưu hiệu năng
                if (batch.Count >= 2000)
                {
                    context.ShowtimeSeats.AddRange(batch);
                    await context.SaveChangesAsync();
                    batch.Clear();
                }
            }
        }

        if (batch.Count > 0)
        {
            context.ShowtimeSeats.AddRange(batch);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureConcessionItemsAsync(CinemaDbContext context)
    {
        var items = new (string Name, string Description, decimal Price, int Stock)[]
        {
            ("Popcorn Caramel", "Bắp rang bơ vị caramel giòn thơm (cỡ L)", 65_000m, 100),
            ("Bắp Phô Mai (L)", "Bắp rang giòn phủ phô mai đậm đà cỡ lớn", 65_000m, 80),
            ("Bắp Ngọt Truyền Thống (M)", "Bắp rang bơ vị ngọt vừa vặn cho 1 người", 50_000m, 90),
            ("Bắp Mix 2 Vị (Caramel & Phô Mai)", "Hộp lớn kết hợp hai vị caramel và phô mai", 75_000m, 60),
            ("Coca-Cola 32oz", "Nước ngọt có ga mát lạnh cỡ lớn", 30_000m, 120),
            ("Sprite / 7Up 32oz", "Nước ngọt vị chanh tươi mát lạnh cỡ lớn", 30_000m, 120),
            ("Trà Đào Cam Sả (L)", "Trà đào thanh mát kèm thạch đào giòn", 45_000m, 70),
            ("Nước Suối Dasani 500ml", "Nước khoáng tinh khiết đóng chai ướp lạnh", 20_000m, 150),
            ("Combo Solo", "1 bắp rang cỡ vừa + 1 nước ngọt 32oz tiết kiệm", 85_000m, 50),
            ("Combo Couple (1 Bắp L + 2 Nước)", "1 bắp rang cỡ lớn tự chọn vị + 2 ly nước ngọt 32oz", 115_000m, 50),
            ("Combo Gia Đình (2 Bắp L + 4 Nước)", "2 bắp rang cỡ lớn + 4 nước ngọt cho nhóm bạn và gia đình", 215_000m, 40),
            ("Khoai Tây Lắc Phô Mai", "Khoai tây chiên giòn lắc bột phô mai nóng hổi", 40_000m, 60)
        };

        var existingNames = (await context.ConcessionItems
            .Select(c => c.Name)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newItems = new List<ConcessionItem>();
        foreach (var (name, desc, price, stock) in items)
        {
            if (!existingNames.Contains(name))
            {
                newItems.Add(new ConcessionItem
                {
                    Name = name,
                    Description = desc,
                    Price = price,
                    StockQuantity = stock,
                    LowStockThreshold = 10,
                    Status = "Active"
                });
                existingNames.Add(name);
            }
        }

        if (newItems.Count > 0)
        {
            context.ConcessionItems.AddRange(newItems);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Tạo thêm các khách hàng demo (mật khẩu 123456) để kiểm tra luồng
    /// tranh ghế giữa hai hoặc nhiều người dùng cùng lúc.
    /// </summary>
    public static async Task SeedDemoCustomersAsync(CinemaDbContext context, Services.IPasswordService passwords)
    {
        var hash = passwords.HashPassword("123456");

        var customers = new[]
        {
            ("customer", "customer@cinemadb.com", "Nguyễn Văn An", "CUS0001"),
            ("customer2", "customer2@cinemadb.com", "Trần Thị Bình", "CUS0002"),
            ("customer3", "customer3@cinemadb.com", "Lê Văn Cường", "CUS0003")
        };

        foreach (var (username, email, name, code) in customers)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = new User
                {
                    Username = username,
                    Email = email,
                    FullName = name,
                    PhoneNumber = "098800000" + code[^1],
                    RoleId = 4, // Customer
                    Status = "Active",
                    PasswordHash = hash,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                context.Customers.Add(new Customer
                {
                    UserId = user.UserId,
                    CustomerCode = code
                });
                await context.SaveChangesAsync();
            }
            else
            {
                user.PasswordHash = hash;
                user.FullName = name;
                user.Status = "Active";
                await context.SaveChangesAsync();
            }
        }
    }
}
