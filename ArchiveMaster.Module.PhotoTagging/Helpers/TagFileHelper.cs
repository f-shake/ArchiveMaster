using System.Text.Json;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Helpers;

public static class TagFileHelper
{
    public static async Task<List<TaggingPhotoFileInfo>> GetPhotoTaggingFileInfosAsync(string tagFile, string rootDir,
        CancellationToken ct)
    {
        var tagFileContent = await File.ReadAllTextAsync(tagFile, ct);
        List<TaggingPhotoFileInfo> result = null;
        await Task.Run(() =>
        {
            var photos = JsonSerializer.Deserialize<PhotoTagCollection>(tagFileContent);
            result = new List<TaggingPhotoFileInfo>(photos.Photos.Count + 4);
            foreach (var photo in photos.Photos)
            {
                result.Add(new TaggingPhotoFileInfo(photo, rootDir));
            }
        }, ct);
        return result;
    }

    public static async Task<PhotoTagCollection> GetPhotoTagCollectionAsync(string tagFile, CancellationToken ct)
    {
        var tagFileContent = await File.ReadAllTextAsync(tagFile, ct);
        PhotoTagCollection result = null;
        await Task.Run(() => { result = JsonSerializer.Deserialize<PhotoTagCollection>(tagFileContent); }, ct);
        return result;
    }
}