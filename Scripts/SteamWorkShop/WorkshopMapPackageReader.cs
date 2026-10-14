using System.IO;
using UnityEngine;

public static class WorkshopMapPackageReader
{
    public static PixelChromaWorkshopManifest ReadManifest(string folderPath)
    {
        string path = Path.Combine(folderPath, "manifest.json");
        return JsonUtility.FromJson<PixelChromaWorkshopManifest>(File.ReadAllText(path));
    }

    public static PixelChromaMapExportData ReadMapData(string folderPath, string mapFile)
    {
        string path = Path.Combine(folderPath, mapFile);
        return JsonUtility.FromJson<PixelChromaMapExportData>(File.ReadAllText(path));
    }

    public static Sprite LoadPreview(string folderPath, string previewFile)
    {
        string fullPath = Path.Combine(folderPath, previewFile);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
    }
}