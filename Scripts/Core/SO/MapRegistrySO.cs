using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "MapRegistry", menuName = "PixelChroma/Map/MapRegistry")]
    public class MapRegistrySO : ScriptableObject
    {
        [SerializeField] private List<MapDataSO> maps = new();

        public IReadOnlyList<MapDataSO> Maps => maps;

        public MapDataSO FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i] != null && maps[i].name == id)
                    return maps[i];
            }

            return null;
        }

        public MapDataSO GetRandomConcrete()
        {
            var pool = maps.FindAll(m => m != null && m.mapType == MapType.Normal && m.mapPrefab != null);
            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }
    }
}
