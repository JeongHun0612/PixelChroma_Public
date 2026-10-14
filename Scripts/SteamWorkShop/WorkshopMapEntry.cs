using UnityEngine;

public sealed class WorkshopMapEntry
{
    public readonly ulong publishedFileId;
    public readonly string installPath;
    public readonly bool isInstalled;

    public string MapId { get; private set; }
    public string Title { get; private set; }
    public string Author { get; private set; }
    public Sprite Preview { get; private set; }

    public string NetworkId => "workshop:" + publishedFileId;

    public WorkshopMapEntry(ulong publishedFileId, string installPath, bool isInstalled)
    {
        this.publishedFileId = publishedFileId;
        this.installPath = installPath;
        this.isInstalled = isInstalled;
    }

    /// <summary>
    /// manifest.json에서 메타데이터를 읽어 캐시한다.
    /// 설치 완료 후 한 번만 호출하면 된다.
    /// </summary>
    public void LoadMetadata()
    {
        if (!isInstalled || string.IsNullOrEmpty(installPath))
            return;

        try
        {
            var manifest = WorkshopMapPackageReader.ReadManifest(installPath);
            MapId = manifest.mapId;
            Title = manifest.title;
            Author = manifest.author;
            Preview = WorkshopMapPackageReader.LoadPreview(installPath, manifest.previewImage);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WorkshopMapEntry] 메타데이터 로드 실패: " + e.Message);
            Title = "???";
        }
    }
}