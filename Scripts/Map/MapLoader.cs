using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PixelChroma
{
    public class MapLoader : MonoSingleton<MapLoader>
    {
        private readonly Vector2 MapOffset = new Vector2(1000f, 1000f);

        private GameObject _currentMapInstance;
        //private AssetBundle _currentBundle;

        public void LoadBuiltInMap(MapDataSO mapData)
        {
            if (mapData == null || mapData.mapPrefab == null)
            {
                Debug.LogError("[MapLoader] mapData 또는 mapPrefab이 null입니다.");
                return;
            }

            UnloadCurrentMap();

            _currentMapInstance = Instantiate(mapData.mapPrefab, MapOffset, Quaternion.identity);
            SetupMap(_currentMapInstance);

            Debug.Log($"[MapLoader] 내장 맵 로드 완료: {mapData.name}");
        }

        public void LoadWorkshopMap(string installPath)
        {
            UnloadCurrentMap();

            var manifest = WorkshopMapPackageReader.ReadManifest(installPath);
            var mapData = WorkshopMapPackageReader.ReadMapData(installPath, manifest.mapFile);

            _currentMapInstance = RuntimeTilemapBuilder.Build(mapData, MapOffset);
            SetupMap(_currentMapInstance);
        }

        public void UnloadCurrentMap()
        {
            if (_currentMapInstance != null)
            {
                Destroy(_currentMapInstance);
                _currentMapInstance = null;
            }

            //if (_currentBundle != null)
            //{
            //    _currentBundle.Unload(true);
            //    _currentBundle = null;
            //}
        }

        private void SetupMap(GameObject mapRoot)
        {
            var spawnPoint = mapRoot.GetComponentInChildren<MapSpawnPoint>();
            if (spawnPoint != null)
                spawnPoint.Register();
        }
    }
}