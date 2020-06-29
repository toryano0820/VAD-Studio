using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;

namespace VADEdit
{
    public static class Settings
    {
        public static bool SplitOnSilence { get; set; } = false;
        public static int SplitLength { get; set; } = 10000;
        public static int MaxSilence { get; set; } = 300;
        public static float MinVolume { get; set; } = 5.0F;
        public static int MinLength { get; set; } = 1000;
        public static int BatchSize { get; set; } = 100;
        public static string LanguageCode { get; set; } = "fil-PH";
        public static string STTCredentialtPath { get; set; } = "service_account.json";
        public static bool IncludeSttResult { get; set; } = false;
        public static bool IncludeAudioFileSize { get; set; } = false;
        public static bool IncludeAudioLengthMillis { get; set; } = false;
        public static string AudioWaveColor { get; set; } = "#FFFF00";
        public static string AudioWaveBackgroundColor { get; set; } = "#000000";
        public static string AudioWaveSelectionColor { get; set; } = "#AA0000FF";
        public static string ChunkTextColor { get; set; } = "#000000";
        public static string ChunkSTTColor { get; set; } = "#00FF00";
        public static string ChunkExportColor { get; set; } = "#0000FF";
        public static string ChunkErrorColor { get; set; } = "#FF0000";
        public static string ChunkSelectionColor { get; set; } = "#33000000";
        public static string ChunkTextSelectionColor { get; set; } = "#0000FF";
        public static string AppBackgroundColor { get; set; } = "#FFFFFF";
        public static string ProjectBaseLocation { get; set; } = "c:\\sentence_extractor";
        public static string LastMediaLocation { get; set; } = "c:\\";
        public static string KinpoSttInferHost { get; set; } = "http://203.177.163.136";

        private static string configFilePath = Path.Combine(App.AppDir, "config.json");

        static Settings()
        {
            if (!File.Exists(configFilePath))
                File.WriteAllText(configFilePath, "{}");

            var settings = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(configFilePath));

            SplitOnSilence = (bool)(settings.SplitOnSilence ?? true);
            SplitLength = (int)(settings.SplitLength ?? 10000);
            MaxSilence = (int)(settings.MaxSilence ?? 300);
            MinVolume = (float)(settings.MinVolume ?? 5.0);
            MinLength = (int)(settings.MinLength ?? 1000);
            BatchSize = (int)(settings.BatchSize ?? 100);
            LanguageCode = (string)(settings.LanguageCode ?? "en-US");
            STTCredentialtPath = (string)(settings.STTCredentialtPath ?? "service_account.json");
            IncludeSttResult = (bool)(settings.IncludeSttResult ?? true);
            IncludeAudioFileSize = (bool)(settings.IncludeAudioFileSize ?? true);
            IncludeAudioLengthMillis = (bool)(settings.IncludeAudioLengthMillis ?? true);
            AudioWaveColor = (string)(settings.AudioWaveColor ?? "#FFFF00");
            AudioWaveBackgroundColor = (string)(settings.AudioWaveBackgroundColor ?? "#000000");
            AudioWaveSelectionColor = (string)(settings.AudioWaveSelectionColor ?? "#AA0000FF");
            ChunkTextColor = (string)(settings.ChunkTextColor ?? "#000000");
            ChunkSTTColor = (string)(settings.ChunkSTTColor ?? "#00FF00");
            ChunkExportColor = (string)(settings.ChunkExportColor ?? "#0000FF");
            ChunkErrorColor = (string)(settings.ChunkErrorColor ?? "#FF0000");
            ChunkSelectionColor = (string)(settings.ChunkSelectionColor ?? "#330000FF");
            ChunkTextSelectionColor = (string)(settings.ChunkTextSelectionColor ?? "#0000FF");
            AppBackgroundColor = (string)(settings.AppBackgroundColor ?? "#FFFFFF");
            LastMediaLocation = (string)(settings.LastMediaLocation ?? "c:\\");
            ProjectBaseLocation = (string)(settings.ProjectBaseLocation ?? "c:\\sentence_extractor");
            KinpoSttInferHost = (string)(settings.KinpoSttInferHost ?? "http://10.24.254.166");
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
                    LanguageCode,
                    STTCredentialtPath,
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

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var sttCredentialtPath = STTCredentialtPath.Contains(appDir) ? STTCredentialtPath.Substring(appDir.Length) : STTCredentialtPath;
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", sttCredentialtPath, EnvironmentVariableTarget.Process);
            (Application.Current.MainWindow as MainWindow).RenewSpeechClient();
        }
    }
}
