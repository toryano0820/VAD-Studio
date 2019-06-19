using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VADEdit
{
    public static class Settings
    {
        public static int MaxSilence { get; set; } = 300;
        public static float MinVolume { get; set; } = 5.0F;
        public static int MinLength { get; set; } = 1000;

        public static string LanguageCode { get; set; } = "fil-PH";
    }
}
