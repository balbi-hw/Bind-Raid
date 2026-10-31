using UnityEngine;
namespace ChainboundRaid
{
    public static class RaidUserSettings
    {
        public static float Sensitivity { get => PlayerPrefs.GetFloat("RaidMouseSensitivity", 3f); set => PlayerPrefs.SetFloat("RaidMouseSensitivity", Mathf.Clamp(value, .2f, 8f)); }
        public static bool ScreenShake { get => PlayerPrefs.GetInt("RaidScreenShake", 1) == 1; set => PlayerPrefs.SetInt("RaidScreenShake", value ? 1 : 0); }
        public static float SubtitleScale { get => PlayerPrefs.GetFloat("RaidSubtitleScale", 1f); set => PlayerPrefs.SetFloat("RaidSubtitleScale", Mathf.Clamp(value, .8f, 1.6f)); }
        public static void Save() => PlayerPrefs.Save();
    }
    public static class RaidInputContext
    {
        public static bool SettingsOpen { get; private set; }
        public static bool Playing { get; private set; }
        public static bool AcceptingInput => Playing && !SettingsOpen && Application.isFocused;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { SettingsOpen = false; Playing = false; }
        public static void Update(bool playing)
        {
            if (!playing || !Playing) SettingsOpen = false;
            if (playing && Input.GetKeyDown(KeyCode.Escape))
            { SettingsOpen = !SettingsOpen; RaidUserSettings.Save(); }
            Playing = playing;
            Cursor.lockState = AcceptingInput ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !AcceptingInput;
        }
    }
}
