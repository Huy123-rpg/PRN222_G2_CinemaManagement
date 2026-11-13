using Microsoft.AspNetCore.Http;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

public sealed class MoviePosterStorage(
    IWebHostEnvironment environment,
    ILogger<MoviePosterStorage> logger) : IMoviePosterStorage
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const string PosterUrlPrefix = "/uploads/posters/";

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        Validate(file);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var directory = Path.Combine(GetWebRootPath(), "uploads", "posters");
        Directory.CreateDirectory(directory);

        var destinationPath = Path.Combine(directory, fileName);
        try
        {
            await using var output = new FileStream(
                destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await file.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            try
            {
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
            }
            catch (IOException cleanupException)
            {
                logger.LogWarning(cleanupException, "Could not remove an incomplete poster upload.");
            }
            throw;
        }

        return PosterUrlPrefix + fileName;
    }

    public Task DeleteAsync(string? posterUrl)
    {
        if (string.IsNullOrWhiteSpace(posterUrl)
            || !posterUrl.StartsWith(PosterUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var fileName = Path.GetFileName(posterUrl);
        if (string.IsNullOrWhiteSpace(fileName)) return Task.CompletedTask;

        var path = Path.Combine(GetWebRootPath(), "uploads", "posters", fileName);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete the old movie poster {PosterUrl}.", posterUrl);
        }
        return Task.CompletedTask;
    }

    private string GetWebRootPath()
        => environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");

    private static void Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new InvalidDataException("Vui lòng chọn ảnh poster.");

        if (file.Length > MaxFileSize)
            throw new InvalidDataException("Ảnh poster không được vượt quá 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new InvalidDataException("Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP.");

        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        var isJpeg = extension is ".jpg" or ".jpeg"
            && read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = extension == ".png"
            && read >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var isWebp = extension == ".webp"
            && read >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8);

        if (!isJpeg && !isPng && !isWebp)
            throw new InvalidDataException("Tệp đã chọn không phải ảnh JPG, PNG hoặc WebP hợp lệ.");
    }
}
