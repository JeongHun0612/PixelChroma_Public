[System.Serializable]
public sealed class PixelChromaWorkshopManifest
{
    public string format = "PixelChromaWorkshopMap";
    public int version = 1;
    public string mapId;
    public string title;
    public string author;
    public string description;
    public string mapFile = "map.json";
    public string previewImage = "preview.png";
    public string tilesetFolder = "tilesets";
    public string loaderStrategy = "RuntimeTilemapLoader";
    public string mapFormat = "PixelChromaMap";
    public int mapFormatVersion = 3;
    public string entryScene = "RuntimeWorkshopMap";
    public string[] requiredFeatures = new string[]
    {
        "RuntimeTilemapLoader",
        "LayerBindings",
        "SpawnPoints",
        "WallCollision",
        "Zones"
    };
    public string requiredGameVersion = "1.0.0";
}