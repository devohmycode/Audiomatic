namespace Audiomatic.Services;

public static class MetadataWriter
{
    public record WriteResult(bool Success, string? Error = null);

    public static WriteResult WriteTags(string filePath, string title, string artist, string album)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            tagFile.Tag.Title = title;
            tagFile.Tag.Performers = string.IsNullOrWhiteSpace(artist)
                ? [] : [artist];
            tagFile.Tag.Album = album;
            tagFile.Save();
            return new WriteResult(true);
        }
        catch (IOException)
        {
            return new WriteResult(false, "File is in use. Stop playback and try again.");
        }
        catch (Exception ex)
        {
            return new WriteResult(false, ex.Message);
        }
    }

    public static WriteResult WriteArtwork(string filePath, byte[]? imageData, string mimeType = "image/jpeg")
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            if (imageData == null)
            {
                tagFile.Tag.Pictures = [];
            }
            else
            {
                var picture = new TagLib.Picture(new TagLib.ByteVector(imageData))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = mimeType
                };
                tagFile.Tag.Pictures = [picture];
            }
            tagFile.Save();
            return new WriteResult(true);
        }
        catch (IOException)
        {
            return new WriteResult(false, "File is in use. Stop playback and try again.");
        }
        catch (Exception ex)
        {
            return new WriteResult(false, ex.Message);
        }
    }
}
