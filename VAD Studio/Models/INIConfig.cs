using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VAD.Models
{
    public class INIConfig
    {
        public enum ConfigType
        {
            Section,
            KeyValuePair
        }

        Dictionary<string, INIConfig> configObj = new Dictionary<string, INIConfig>();

        public string[] Sections { get { return configObj.Where(kv => kv.Value.Type == ConfigType.Section).ToDictionary(k => k.Key).Keys.ToArray(); } }
        public string[] Keys { get { return configObj.Where(kv => kv.Value.Type == ConfigType.KeyValuePair).ToDictionary(k => k.Key).Keys.ToArray(); } }

        public ConfigType Type { get; }
        public string Key { get; }
        public string Value { get; }

        public INIConfig(ConfigType type, string key, string value=null)
        {
            Type = type;
            Key = key;
            Value = value;
        }

        public INIConfig this[string key]
        {
            get
            {
                Utils.Assert(Sections.Contains(key), new Exception());
                return configObj[key];
            }
            set
            {
                configObj[key] = value;
            }
        }
    }
}
