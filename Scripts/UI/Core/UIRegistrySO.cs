using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PixelChroma.UI
{
    [CreateAssetMenu(fileName = "UIRegistry", menuName = "PixelChroma/UI/Registry")]
    public class UIRegistrySO : ScriptableObject
    {
        [Header("Scene UI")]
        public List<AssetReferenceGameObject> sceneUIs = new();

        [Header("Popup UI")]
        public List<AssetReferenceGameObject> popupUIs = new();
    }
}
