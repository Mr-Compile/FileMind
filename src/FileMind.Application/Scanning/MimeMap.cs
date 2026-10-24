namespace FileMind.Application.Scanning;

public static class MimeMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["txt"] = "text/plain",
        ["md"] = "text/markdown",
        ["rtf"] = "application/rtf",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["csv"] = "text/csv",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["bmp"] = "image/bmp",
        ["webp"] = "image/webp",
        ["svg"] = "image/svg+xml",
        ["mp4"] = "video/mp4",
        ["mkv"] = "video/x-matroska",
        ["avi"] = "video/x-msvideo",
        ["mov"] = "video/quicktime",
        ["mp3"] = "audio/mpeg",
        ["wav"] = "audio/wav",
        ["flac"] = "audio/flac",
        ["zip"] = "application/zip",
        ["rar"] = "application/vnd.rar",
        ["7z"] = "application/x-7z-compressed",
        ["json"] = "application/json",
        ["xml"] = "application/xml",
        ["html"] = "text/html",
        ["css"] = "text/css",
        ["cs"] = "text/plain",
        ["exe"] = "application/vnd.microsoft.portable-executable"
    };

    public static string? FromExtension(string extension) =>
        Map.TryGetValue(extension.TrimStart('.'), out var mime) ? mime : null;
}
