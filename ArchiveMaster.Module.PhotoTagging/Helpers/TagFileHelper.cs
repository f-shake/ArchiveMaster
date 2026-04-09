using System.Text.Json;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Helpers;

public static class TagFileHelper
{
    public static async Task<List<TaggingPhotoFileInfo>> GetPhotoTaggingFileInfosAsync(string tagFile, string rootDir,
        bool generateFileInfo, CancellationToken ct)
    {
        var tagFileContent = await File.ReadAllTextAsync(tagFile, ct);
        List<TaggingPhotoFileInfo> result = null;
        await Task.Run(() =>
        {
            var photos = JsonSerializer.Deserialize<TaggedPhotoCollection>(tagFileContent);
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

    public static async Task<TaggedPhotoCollection> GetPhotoTagCollectionAsync(string tagFile, CancellationToken ct)
    {
        var tagFileContent = await File.ReadAllTextAsync(tagFile, ct);
        TaggedPhotoCollection result = null;
        await Task.Run(() => { result = JsonSerializer.Deserialize<TaggedPhotoCollection>(tagFileContent); }, ct);
        return result;
    }
}