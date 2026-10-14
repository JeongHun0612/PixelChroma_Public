using UnityEngine;

namespace PixelChroma
{
    // 사용자가 마지막으로 사용한 방 생성 설정을 로컬에 저장하고 새 RoomOption에 반영한다.
    public static class RoomOptionPreferences
    {
        private const string LastRoomNameKey = "RoomOption.LastRoomName";

        public static void ApplyCreationDefaults(RoomOption option)
        {
            if (option == null) return;

            option.roomName = PlayerPrefs.GetString(LastRoomNameKey, option.roomName);
        }

        public static void SaveCreationDefaults(RoomOption option)
        {
            if (option == null) return;

            PlayerPrefs.SetString(LastRoomNameKey, option.roomName ?? "");
            PlayerPrefs.Save();
        }
    }
}
