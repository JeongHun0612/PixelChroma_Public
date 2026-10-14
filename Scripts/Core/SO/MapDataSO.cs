using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    public enum MapType { Normal, Random }

    [CreateAssetMenu(fileName = "MapData", menuName = "PixelChroma/Map/MapData")]
    public class MapDataSO : ScriptableObject
    {
        public MapType mapType = MapType.Normal;
        public LocalizedString displayName;
        public GameObject mapPrefab;
        public Sprite previewSprite;

        public string GetDisplayName()
        {
            if (displayName == null || displayName.IsEmpty)
                return name;
            return displayName.GetLocalizedString();
        }
    }
}