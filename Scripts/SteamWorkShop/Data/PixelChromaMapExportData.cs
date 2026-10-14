using System.Collections.Generic;

[System.Serializable]
public sealed class PixelChromaMapExportData
{
    public string format;
    public int version;
    public string mapId;
    public int width;
    public int height;
    public int cellSize;
    public PixelChromaRuntimeExportData runtime;
    public PixelChromaMapBoundsExportData bounds;
    public List<PixelChromaTilesetExportData> tilesets = new List<PixelChromaTilesetExportData>();
    public List<PixelChromaMapLayerExportData> layers = new List<PixelChromaMapLayerExportData>();
    public List<PixelChromaSpawnPointExportData> spawnPoints = new List<PixelChromaSpawnPointExportData>();
    public List<PixelChromaZoneExportData> zones = new List<PixelChromaZoneExportData>();
    public List<MapEditorMovingRegionData> movingRegions = new List<MapEditorMovingRegionData>();
}

[System.Serializable]
public sealed class PixelChromaRuntimeExportData
{
    public string target;
    public string loaderStrategy;
    public string mapRootObjectName;
    public string gridObjectName;
    public string coordinateSystem;
    public string mapOrigin;
    public string mapYAxis;
    public string unityTilemapOrigin;
    public string unityYConversion;
    public float tileWorldSize;
    public float pixelsPerUnit;
    public string groundLayerName;
    public string objectLayerName;
    public string wallVisualLayerName;
    public string wallLayerName;
    public string wallCollider;
    public string spawnComponent;
    public string zoneComponent;
    public List<PixelChromaRuntimeLayerBindingData> layerBindings = new List<PixelChromaRuntimeLayerBindingData>();
}

[System.Serializable]
public sealed class PixelChromaRuntimeLayerBindingData
{
    public string editorLayer;
    public string exportLayerName;
    public string kind;
    public string targetTilemapName;
    public string unityLayerName;
    public int sortingOrder;
    public bool createsRenderer;
    public bool createsCollider;
    public bool metadataOnly;
}

[System.Serializable]
public sealed class PixelChromaMapBoundsExportData
{
    public int minX;
    public int minY;
    public int maxX;
    public int maxY;
    public float centerX;
    public float centerY;
}

[System.Serializable]
public sealed class PixelChromaTilesetExportData
{
    public string tilesetId;
    public string sourceName;
    public string file;
}

[System.Serializable]
public sealed class PixelChromaMapLayerExportData
{
    public string name;
    public string kind;
    public string unityLayerName;
    public int sortingOrder;
    public bool collider;
    public string colliderType;
    public bool colliderIsTrigger;
    public string compositeOperation;
    public List<PixelChromaTileExportData> tiles = new List<PixelChromaTileExportData>();
}

[System.Serializable]
public sealed class PixelChromaTileExportData
{
    public int x;
    public int y;
    public string kind;
    public string tilesetId;
    public int tileId = -1;
    public string colorHex;
    public int pixelResolution;
    public string[] pixelHexes;
    public int rotation;
    public bool flipX;
    public bool flipY;
    public bool collision;
    public float animationFps;
    public bool animationLoop;
    public PixelChromaAnimationFrameExportData[] animationFrames;
}

[System.Serializable]
public sealed class PixelChromaAnimationFrameExportData
{
    public int pixelResolution;
    public string[] pixelHexes;
}

[System.Serializable]
public sealed class PixelChromaSpawnPointExportData
{
    public string id;
    public int x;
    public int y;
    public float worldX;
    public float worldY;
    public string role;
    public string component;
}

[System.Serializable]
public sealed class PixelChromaZoneExportData
{
    public string id;
    public string type;
    public int x;
    public int y;
    public int width;
    public int height;
    public string unityLayerName;
    public bool collider;
}

[System.Serializable]
public sealed class MapEditorPathPointData
{
    public int x;
    public int y;
}

[System.Serializable]
public sealed class MapEditorMovingRegionData
{
    public string id;
    public string displayName;
    public int canvasLayerIndex;
    public int x;
    public int y;
    public int width = 1;
    public int height = 1;
    public MapEditorPathPointData[] path;
    public float tilesPerSecond = 1f;
    public float waitSeconds;
    public bool loop = true;
    public bool pingPong;
}
