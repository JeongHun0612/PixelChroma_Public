using UnityEngine;

namespace PixelChroma
{
    public static class LikeService
    {
        private const string PrefsKey = "PlayerLikeCount";

        public static int Load()
        {
            return PlayerPrefs.GetInt(PrefsKey, 0);
        }

        public static void Save(int count)
        {
            PlayerPrefs.SetInt(PrefsKey, Mathf.Max(0, count));
            PlayerPrefs.Save();
        }
    }
}