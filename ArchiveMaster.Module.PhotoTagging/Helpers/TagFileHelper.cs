using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Helpers;

public static class TagFileHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    public static async Task<List<TaggingPhotoFileInfo>> ReadPhotoTaggingFileInfosAsync(string tagFile, string rootDir,
        bool generateFileInfo, CancellationToken ct)
    {
        var tagFileContent = await File.ReadAllTextAsync(tagFile, ct);
        List<TaggingPhotoFileInfo> result = null;
        await Task.Run(() =>
        {
            var photos = JsonSerializer.Deserialize<TaggedPhotoCollection>(tagFileContent, JsonOptions);
            result = new List<TaggingPhotoFileInfo>(photos.Photos.Count + 4);
            foreach (var photo in photos.Photos)
            {
                if (generateFileInfo)
                {
                    result.Add(new TaggingPhotoFileInfo(
                        new FileInfo(Path.Combine(rootDir, photo.RelativePath)),
                        photo.Tags, rootDir));
                }
                else
                {
                    result.Add(new TaggingPhotoFileInfo(photo, rootDir));
                }
            }
        }, ct);
        return result;
    }

    public static async Task<TaggedPhotoCollection> ReadPhotoTagCollectionAsync(string tagFile, CancellationToken ct)
    {
        var tagFileContent = await File.ReadAllTextAsync(tagFile, ct);
        TaggedPhotoCollection result = null;
        await Task.Run(
            () => { result = JsonSerializer.Deserialize<TaggedPhotoCollection>(tagFileContent, JsonOptions); }, ct);
        return result;
    }

    public static async Task WritePhotoTagCollectionAsync(string tagFile, TaggedPhotoCollection taggedPhotos,
        CancellationToken ct = default)
    {
        var tagFileContent = JsonSerializer.Serialize(taggedPhotos, JsonOptions);
        await File.WriteAllTextAsync(tagFile, tagFileContent, ct);
    }
}