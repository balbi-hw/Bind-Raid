using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace ChainboundRaid
{
    public static class RaidLanguage
    {
        [Serializable] sealed class Entry { public string id, english, korean; }
        [Serializable] sealed class Catalog { public Entry[] entries; }
        static Dictionary<string, Entry> byId, byEnglish;
        static Font koreanFont;
        public static bool Korean => PlayerPrefs.GetInt("RaidLanguage", Application.systemLanguage == SystemLanguage.Korean ? 1 : 0) == 1;
        static void Load()
        {
            if (byId != null) return;
            byId = new Dictionary<string, Entry>(); byEnglish = new Dictionary<string, Entry>();
            var resource = Resources.Load<TextAsset>("RaidLocalization");
            if (resource == null) return;
            foreach (var entry in JsonUtility.FromJson<Catalog>(resource.text).entries)
            { byId[entry.id] = entry; byEnglish[entry.english] = entry; }
        }
        public static string Get(string id)
        { Load(); return byId.TryGetValue(id, out var entry) ? Korean ? entry.korean : entry.english : id; }
        public static string Value(object value)
        {
            string text = value != null ? value.ToString() : "";
            Load(); return Korean && byEnglish.TryGetValue(text, out var entry) ? entry.korean : text;
        }
        public static string Text(string value) => Value(value);
        public static string Format(string id, params object[] values) => string.Format(
            Korean ? CultureInfo.GetCultureInfo("ko-KR") : CultureInfo.GetCultureInfo("en-US"), Get(id), values);
        public static Rect SettingsRect => new Rect(Mathf.Max(8, Screen.width - 316), 12, 304, Mathf.Min(310, Screen.height - 24));
        public static bool PointerOverSettings => (!RaidInputContext.Playing || RaidInputContext.SettingsOpen) &&
            SettingsRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        public static void DrawSettings()
        {
            if (koreanFont == null) koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 16);
            if (koreanFont != null) GUI.skin.font = koreanFont;
            if (RaidInputContext.Playing && !RaidInputContext.SettingsOpen) return;
            GUILayout.BeginArea(SettingsRect, GUI.skin.box);
            GUILayout.Label(Get("settings.title"));
            GUILayout.Label(Get("settings.language"));
            int language = GUILayout.SelectionGrid(Korean ? 0 : 1, new[] { "한국어", "English" }, 2);
            if (language != (Korean ? 0 : 1)) { PlayerPrefs.SetInt("RaidLanguage", language == 0 ? 1 : 0); RaidUserSettings.Save(); }
            GUILayout.Label(Get("settings.sensitivity") + "  " + RaidUserSettings.Sensitivity.ToString("F1"));
            RaidUserSettings.Sensitivity = GUILayout.HorizontalSlider(RaidUserSettings.Sensitivity, .2f, 8f);
            bool shake = GUILayout.Toggle(RaidUserSettings.ScreenShake, Get("settings.shake"));
            if (shake != RaidUserSettings.ScreenShake) { RaidUserSettings.ScreenShake = shake; RaidUserSettings.Save(); }
            GUILayout.Label(Get("settings.textsize") + "  " + RaidUserSettings.SubtitleScale.ToString("F1"));
            RaidUserSettings.SubtitleScale = GUILayout.HorizontalSlider(RaidUserSettings.SubtitleScale, .8f, 1.6f);
            GUILayout.Label(Get("settings.controls"));
            GUILayout.EndArea();
        }
    }
}
