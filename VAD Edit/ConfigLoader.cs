using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VADEdit
{
    public static class ConfigLoader
    {
        public static void Load(string filePath)
        {
            foreach (var line in File.ReadLines(filePath))
            {
                Console.WriteLine(line);
            }
        }
    }
}
