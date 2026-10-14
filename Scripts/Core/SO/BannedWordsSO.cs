using UnityEngine;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "BannedWords", menuName = "PixelChroma/BannedWords")]
    public class BannedWordsSO : ScriptableObject
    {
        [SerializeField] private string[] words;

        public bool Contains(string text)
        {
            if (words == null || words.Length == 0) return false;

            string lowerText = text.ToLower();

            foreach (string word in words)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;
                if (lowerText.Contains(word.ToLower())) return true;
            }

            return false;
        }
    }
}
