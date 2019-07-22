using Newtonsoft.Json;
using System;
using System.IO;

namespace VADEdit
{
    public static class Settings
    {
        public static int MaxSilence { get; set; } = 300;
        public static float MinVolume { get; set; } = 5.0F;
        public static int MinLength { get; set; } = 1000;
        public static string LanguageCode { get; set; } = "fil-PH";
        public static string STTCredentialtPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service_account.json");
        public static bool IncludeSttResult { get; set; } = false;
        public static bool IncludeAudioFileSize { get; set; } = false;

        private static string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        static Settings()
        {
            if (!File.Exists(configFilePath))
                File.WriteAllText(configFilePath, "{}");

            var settings = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(configFilePath));

            MaxSilence = (int)(settings.MaxSilence ?? 300);
            MinVolume = (float)(settings.MinVolume ?? 5.0);
            MinLength = (int)(settings.MinLength ?? 1000);
            LanguageCode = (string)(settings.LanguageCode ?? "en-US");
            STTCredentialtPath = (string)(settings.STTCredentialtPath ?? "service_account.json");
            IncludeSttResult = (bool)(settings.IncludeSttResult ?? true);
            IncludeAudioFileSize = (bool)(settings.IncludeAudioFileSize ?? true);
        }

        public static void Save()
        {
            File.WriteAllText(
                configFilePath,
                JsonConvert.SerializeObject(new
                {
                    MaxSilence,
                    MinVolume,
                    MinLength,
                    LanguageCode,
                    STTCredentialtPath,
                    IncludeSttResult,
                    IncludeAudioFileSize
                }, Formatting.Indented));

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", STTCredentialtPath, EnvironmentVariableTarget.Process);
        }
    }
}
