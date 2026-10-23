using System.Text;
using System.Text.Json;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.Infrastructure;

public static class DataFactory
{
    public static string WriteGpx(string path,
        IEnumerable<(double latitude, double longitude, DateTime time)> points,
        bool useNamespace = true)
    {
        string ns = useNamespace ? " xmlns=\"http://www.topografix.com/GPX/1/0\"" : "";
        var body = new StringBuilder();
        foreach (var point in points)
        {
            body.AppendLine(
                $"<trkpt lat=\"{point.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" " +
                $"lon=\"{point.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}\">" +
                $"<time>{point.time.ToString("O")}</time></trkpt>");
        }

        string xml = $"""
                      <?xml version="1.0" encoding="UTF-8"?>
                      <gpx{ns}>
                        <trk>
                          <trkseg>
                      {body}
                          </trkseg>
                        </trk>
                      </gpx>
                      """;
        return WriteText(path, xml);
    }

    public static string WriteTagJson(string path, params TaggedPhoto[] photos)
    {
        string json = JsonSerializer.Serialize(new TaggedPhotoCollection(photos.ToList()),
            new JsonSerializerOptions { WriteIndented = true });
        return WriteText(path, json);
    }

    public static string WriteGeoJsonSquare(string path, string fieldName, string fieldValue,
        double centerLatitude, double centerLongitude, double delta = 0.01)
    {
        double minLon = centerLongitude - delta;
        double maxLon = centerLongitude + delta;
        double minLat = centerLatitude - delta;
        double maxLat = centerLatitude + delta;
        string json = $$"""
                        {
                          "type": "FeatureCollection",
                          "features": [
                            {
                              "type": "Feature",
                              "properties": { "{{fieldName}}": "{{fieldValue}}" },
                              "geometry": {
                                "type": "Polygon",
                                "coordinates": [[
                                  [{{minLon}}, {{minLat}}],
                                  [{{maxLon}}, {{minLat}}],
                                  [{{maxLon}}, {{maxLat}}],
                                  [{{minLon}}, {{maxLat}}],
                                  [{{minLon}}, {{minLat}}]
                                ]]
                              }
                            }
                          ]
                        }
                        """;
        return WriteText(path, json);
    }

    public static string WriteSnapshot(string path, params SyncFileInfo[] files)
    {
        ZipService.WriteToZip(new Step1Model { Files = files.ToList() }, path);
        return path;
    }

    public static string WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }
}
