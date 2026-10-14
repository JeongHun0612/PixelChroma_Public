using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuntimeTileFactory
{
    private static Sprite _whitePixelSprite;

    public static TileBase CreateTile(PixelChromaTileExportData data)
    {
        if (data == null) return null;

        switch (data.kind)
        {
            case "Color":
                return CreateColorTile(data.colorHex, data.collision);

            case "Pixels":
                return CreatePixelsTile(data.pixelHexes, data.pixelResolution, data.collision);

            case "AnimatedPixels":
                return CreateAnimatedTile(data);

            default:
                Debug.LogWarning("[RuntimeTileFactory] 알 수 없는 타일 종류: " + data.kind);
                return null;
        }
    }

    static Tile CreateColorTile(string hex, bool collision)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        ColorUtility.TryParseHtmlString(hex, out Color c);
        tile.color = c;
        tile.sprite = GetWhitePixelSprite();
        if (collision)
            tile.colliderType = Tile.ColliderType.Grid;
        return tile;
    }

    static Tile CreatePixelsTile(string[] hexes, int resolution, bool collision)
    {
        if (hexes == null || hexes.Length == 0 || resolution <= 0)
            return null;

        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < hexes.Length; i++)
        {
            int px = i % resolution;
            int py = resolution - 1 - (i / resolution);
            ColorUtility.TryParseHtmlString(hexes[i], out Color c);
            tex.SetPixel(px, py, c);
        }
        tex.Apply();

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(tex,
            new Rect(0, 0, resolution, resolution),
            Vector2.one * 0.5f,
            16f);
        if (collision)
            tile.colliderType = Tile.ColliderType.Grid;
        return tile;
    }

    static TileBase CreateAnimatedTile(PixelChromaTileExportData data)
    {
        if (data.animationFrames == null || data.animationFrames.Length == 0)
            return null;

        var firstFrame = data.animationFrames[0];
        return CreatePixelsTile(firstFrame.pixelHexes, firstFrame.pixelResolution, data.collision);
    }

    static Sprite GetWhitePixelSprite()
    {
        if (_whitePixelSprite != null)
            return _whitePixelSprite;

        const int size = 16; // TilePixelSize와 동일
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        _whitePixelSprite = Sprite.Create(tex,
            new Rect(0, 0, size, size),
            Vector2.one * 0.5f,
            16f); // 16×16 ÷ PPU 16 = 1유닛 = 1셀
        return _whitePixelSprite;
    }
}