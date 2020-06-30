using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VADEdit.Types;

namespace VADEdit
{
    public class ProjectConfig
    {
        public async Task<string> GetWavPath()
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT WavPath FROM project LIMIT 1;";
            return (string)await cmd.ExecuteScalarAsync();
        }

        public async Task SetWavPath(string value)
        {
            var mPath = Utils.GetRelativePath(projectDirectory, value);
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
            var thread = new Thread(() =>
            {
                var cmd = connection.CreateCommand();
                var cmdBuilder = new StringBuilder("DELETE FROM audioChunks; BEGIN;");
                foreach (var cv in value)
                {
                    var timeRange = Application.Current.Dispatcher.Invoke(() => cv.TimeRange);
                    var sttText = Application.Current.Dispatcher.Invoke(() => cv.SttText);
                    var speechText = Application.Current.Dispatcher.Invoke(() => cv.SpeechText);
                    var visualState = Application.Current.Dispatcher.Invoke(() => cv.VisualState);
                    cmdBuilder.Append("INSERT INTO audioChunks (ChunkStart, ChunkEnd, SttText, SpeechText, VisualState) VALUES" +
                        $"({timeRange.Start.TotalSeconds}, {timeRange.End.TotalSeconds}, '{sttText}', '{speechText}', {(short)visualState});");
                }
                cmdBuilder.Append("COMMIT;");
                cmd.CommandText = cmdBuilder.ToString();

                cmd.ExecuteNonQuery();
            });
            thread.Start();
            while (thread.IsAlive)
                await Task.Delay(10);
        }



        
        SQLiteConnection connection;
        string projectDirectory;

        public ProjectConfig(string dbPath, string projectName=null)
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
