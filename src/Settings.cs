using System;
using System.IO;
using System.Globalization;
using System.Xml.Serialization;

namespace StayLit
{
    public sealed class AppSettings
    {
        public int Hours = 0;
        public int Minutes = 30;
        public bool AlwaysMode = false;
        public string ThemeName = Theme.DefaultName;
        public long LastAlwaysMilliseconds;
        public string LastAlwaysStartedUtc;
        public string LastAlwaysEndedUtc;

        public void CaptureRecord(Session session)
        {
            if (!session.HasAlwaysRecord) return;
            LastAlwaysMilliseconds = session.AlwaysElapsedMilliseconds;
            LastAlwaysStartedUtc = session.AlwaysStartedUtc.Value.ToString("o", CultureInfo.InvariantCulture);
            // A running task is saved as a snapshot, never resumed on startup.
            LastAlwaysEndedUtc = (session.AlwaysEndedUtc ?? session.RecordSnapshotUtc).ToString("o", CultureInfo.InvariantCulture);
        }

        public void RestoreRecord(Session session)
        {
            if (session.IsActive || session.HasAlwaysRecord) return;
            DateTime started;
            DateTime ended;
            if (LastAlwaysMilliseconds < 0 || LastAlwaysMilliseconds > (long)TimeSpan.FromDays(3650).TotalMilliseconds ||
                !DateTime.TryParse(LastAlwaysStartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out started) ||
                !DateTime.TryParse(LastAlwaysEndedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out ended)) return;
            session.RestoreAlwaysRecord(LastAlwaysMilliseconds, started, ended);
        }
    }

    internal sealed class SettingsStore
    {
        private readonly string path;
        public SettingsStore(string path) { this.path = path; }
        public static SettingsStore Default()
        {
            return new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StayLit", "settings.xml"));
        }
        public AppSettings Load()
        {
            try
            {
                using (FileStream file = File.OpenRead(path))
                {
                    AppSettings settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(file);
                    settings.Hours = Math.Max(0, Math.Min(99, settings.Hours));
                    settings.Minutes = Math.Max(0, Math.Min(59, settings.Minutes));
                    settings.ThemeName = Theme.Normalize(settings.ThemeName);
                    if (settings.Hours == 0 && settings.Minutes == 0) settings.Minutes = 30;
                    return settings;
                }
            }
            catch { return new AppSettings(); }
        }
        public bool Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                using (FileStream file = File.Create(temporary))
                    new XmlSerializer(typeof(AppSettings)).Serialize(file, settings);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch { return false; }
        }
    }
}
