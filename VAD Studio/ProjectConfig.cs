using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VAD.Types;

namespace VAD
{
    public class ProjectConfig
    {
        public async Task<string> GetWavPath()
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT WavPath FROM project LIMIT 1;";
            return Path.GetFullPath(Path.Combine(Path.GetFullPath(projectDirectory), (string)await cmd.ExecuteScalarAsync()));
        }

        public async Task SetWavPath(string value)
        {
            var mPath = Utils.GetRelativePath(Path.GetFullPath(projectDirectory), Path.GetFullPath(value));
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"UPDATE project SET WavPath='{mPath}';";
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<string> GetProjectName()
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Name FROM project LIMIT 1;";
            return (string)await cmd.ExecuteScalarAsync();
        }

        public async void SetProjectName(string value)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"UPDATE project SET Name='{value}';";
            await cmd.ExecuteNonQueryAsync();
        }

        public IEnumerator<AudioChunkView> GetAudioChunkViews()
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ChunkStart, ChunkStart, ChunkEnd, SttText, SpeechText, VisualState FROM audioChunks";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    yield return new AudioChunkView()
                    {
                        TimeRange = new TimeRange(reader.GetFloat(1), reader.GetFloat(2)),
                        SttText = reader.GetString(3),
                        SpeechText = reader.GetString(4),
                        VisualState = (AudioChunkView.State)reader.GetInt16(5)
                    };
                }
            }

        }

        public async Task SetAudioChunkViews(List<AudioChunkView> value)
        {
            var cmd = connection.CreateCommand();
            var cmdBuilder = new StringBuilder("BEGIN; DELETE FROM audioChunks;");
            foreach (var cv in value)
            {
                var timeRange = cv.TimeRange;
                var sttText = cv.SttText;
                var speechText = cv.SpeechText;
                var visualState = cv.VisualState;
                cmdBuilder.Append("INSERT INTO audioChunks (ChunkStart, ChunkEnd, SttText, SpeechText, VisualState) VALUES" +
                    $"({timeRange.Start.TotalSeconds}, {timeRange.End.TotalSeconds}, '{sttText.Replace("'", "''")}', '{speechText.Replace("'", "''")}', {(short)visualState});");
            }
            cmdBuilder.Append("COMMIT;");
            cmd.CommandText = cmdBuilder.ToString();

            await cmd.ExecuteNonQueryAsync();
        }

        private SQLiteConnection connection;
        private string projectDirectory;

        public ProjectConfig(string dbPath, string projectName = null)
        {
            var isNewProject = false;
            if (!File.Exists(dbPath))
            {
                isNewProject = true;
                SQLiteConnection.CreateFile(dbPath);
            }

            projectDirectory = Path.GetDirectoryName(dbPath);

            connection = new SQLiteConnection("URI=file:" + dbPath);
            connection.Open();

            if (isNewProject)
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = File.ReadAllText(Path.Combine(App.AppDir, "init.sql"));
                cmd.ExecuteNonQuery();
            }

            if (projectName != null)
                SetProjectName(projectName);
        }

        ~ProjectConfig()
        {
            try
            {
                connection.Close();
            }
            catch { }
        }
    }
}
