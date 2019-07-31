﻿using Newtonsoft.Json;
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

        private static string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

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
                    IncludeAudioLengthMillis
                }, Formatting.Indented));

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var sttCredentialtPath = STTCredentialtPath.Contains(appDir) ? STTCredentialtPath.Substring(appDir.Length) : STTCredentialtPath;
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", sttCredentialtPath, EnvironmentVariableTarget.Process);
            (Application.Current.MainWindow as MainWindow).RenewSpeechClient();
        }
    }
}
