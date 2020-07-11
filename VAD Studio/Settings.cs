using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace VAD
{
    public static class Settings
    {
        public static bool SplitOnSilence { get; set; }
        public static int SplitLength { get; set; }
        public static int MaxSilence { get; set; }
        public static float MinVolume { get; set; }
        public static int MinLength { get; set; }
        public static int BatchSize { get; set; }
        public static bool IncludeSttResult { get; set; }
        public static bool IncludeAudioFileSize { get; set; }
        public static bool IncludeAudioLengthMillis { get; set; }
        public static string AudioWaveColor { get; set; }
        public static string AudioWaveBackgroundColor { get; set; }
        public static string AudioWaveSelectionColor { get; set; }
        public static string ChunkTextColor { get; set; }
        public static string ChunkSTTColor { get; set; }
        public static string ChunkExportColor { get; set; }
        public static string ChunkErrorColor { get; set; }
        public static string ChunkSelectionColor { get; set; }
        public static string ChunkTextSelectionColor { get; set; }
        public static string AppBackgroundColor { get; set; }
        public static string ProjectBaseLocation { get; set; }
        public static string LastMediaLocation { get; set; }
        public static string SttUrl { get; set; }
        public static string SttLanguage { get; set; }

        private static string configFilePath;

        static Settings()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            configFilePath = Path.Combine(App.AppDir, "config.json");

            if (!File.Exists(configFilePath))
                File.WriteAllText(configFilePath, "{}");

            var settings = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(configFilePath));

            SplitOnSilence = (bool)(settings.SplitOnSilence ?? true);
            SplitLength = (int)(settings.SplitLength ?? 10000);
            MaxSilence = (int)(settings.MaxSilence ?? 300);
            MinVolume = (float)(settings.MinVolume ?? 5.0F);
            MinLength = (int)(settings.MinLength ?? 1000);
            BatchSize = (int)(settings.BatchSize ?? 100);
            IncludeSttResult = (bool)(settings.IncludeSttResult ?? true);
            IncludeAudioFileSize = (bool)(settings.IncludeAudioFileSize ?? true);
            IncludeAudioLengthMillis = (bool)(settings.IncludeAudioLengthMillis ?? true);
            AudioWaveColor = (string)(settings.AudioWaveColor ?? "#FFA6A1B7");
            AudioWaveBackgroundColor = (string)(settings.AudioWaveBackgroundColor ?? "#FF2D2D30");
            AudioWaveSelectionColor = (string)(settings.AudioWaveSelectionColor ?? "#1EFFFFFF");
            ChunkTextColor = (string)(settings.ChunkTextColor ?? "White");
            ChunkSTTColor = (string)(settings.ChunkSTTColor ?? "#FF324F46");
            ChunkExportColor = (string)(settings.ChunkExportColor ?? "#FF424666");
            ChunkErrorColor = (string)(settings.ChunkErrorColor ?? "#FF784343");
            ChunkSelectionColor = (string)(settings.ChunkSelectionColor ?? "#28FFFFFF");
            ChunkTextSelectionColor = (string)(settings.ChunkTextSelectionColor ?? "#28FFFFFF");
            AppBackgroundColor = (string)(settings.AppBackgroundColor ?? "#FF2D2D30");
            LastMediaLocation = (string)(settings.LastMediaLocation ?? "c:\\");
            ProjectBaseLocation = (string)(settings.ProjectBaseLocation ?? "c:\\vad");
            SttUrl = (string)(settings.SttUrl ?? "");
            SttLanguage = (string)(settings.LanguageCode ?? "en-US");
        }

        public static void Save()
        {
            File.WriteAllText(
                configFilePath,
                JsonConvert.SerializeObject(new
                {
                    SplitOnSilence,
                    SplitLength,
                    MaxSilence,
                    MinVolume,
                    MinLength,
                    BatchSize,
                    SttLanguage,
                    IncludeSttResult,
                    IncludeAudioFileSize,
                    IncludeAudioLengthMillis,
                    AudioWaveColor,
                    AudioWaveBackgroundColor,
                    AudioWaveSelectionColor,
                    ChunkTextColor,
                    ChunkSTTColor,
                    ChunkExportColor,
                    ChunkErrorColor,
                    ChunkSelectionColor,
                    ChunkTextSelectionColor,
                    AppBackgroundColor,
                    LastMediaLocation,
                    ProjectBaseLocation
                }, Formatting.Indented));
        }
    }
}
