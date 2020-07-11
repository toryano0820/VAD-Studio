using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VAD
{
    public static class SttClient
    {
        public static double MaxDurationInSeconds { get; } = 30.0;

        public static async Task<string> Infer(MemoryStream rawAudioStream)
        {
            await Task.Yield();
            string url = Settings.SttUrl;
            string language = Settings.SttLanguage;
            // TODO: add your STT code here
            return null;
        }
    }
}
