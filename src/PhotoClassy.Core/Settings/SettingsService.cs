using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace PhotoClassy.Core.Settings
{
    /// <summary>
    /// 설정 파일(config.json)의 로드/저장/초기화를 담당한다.
    /// 기본 위치는 설치 폴더(실행 파일이 있는 폴더)이다.
    /// </summary>
    public class SettingsService
    {
        public const string FileName = "config.json";

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public SettingsService(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("설정 파일 경로가 필요합니다.", nameof(filePath));
            FilePath = filePath;
        }

        /// <summary>설치 폴더의 config.json 을 사용하는 서비스.</summary>
        public static SettingsService ForInstallFolder()
        {
            return new SettingsService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName));
        }

        public string FilePath { get; }

        /// <summary>
        /// 설정을 읽는다. 파일이 없으면 기본값을 돌려준다.
        /// 파일이 손상되었으면 기본값을 돌려주고 <paramref name="error"/> 에 사유를 담는다.
        /// </summary>
        public AppSettings Load(out string error)
        {
            error = null;
            if (!File.Exists(FilePath))
                return AppSettings.CreateDefault();

            try
            {
                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json, SerializerSettings) ?? AppSettings.CreateDefault();
                Sanitize(settings);
                return settings;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                error = $"설정 파일을 읽을 수 없어 기본값을 사용합니다: {ex.Message}";
                return AppSettings.CreateDefault();
            }
        }

        public AppSettings Load() => Load(out _);

        /// <summary>설정을 저장한다. 임시 파일에 쓴 뒤 교체하여 저장 도중 손상을 방지한다.</summary>
        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(settings, SerializerSettings);
            var encoding = new UTF8Encoding(false);
            var tempPath = FilePath + ".tmp";

            try
            {
                File.WriteAllText(tempPath, json, encoding);
            }
            catch (UnauthorizedAccessException) when (File.Exists(FilePath))
            {
                // 설치 폴더(Program Files)에는 config.json 파일에만 쓰기 권한이 있으므로 직접 덮어쓴다.
                File.WriteAllText(FilePath, json, encoding);
                return;
            }

            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(tempPath, FilePath);
        }

        /// <summary>설정을 기본값으로 초기화하고 저장한다.</summary>
        public AppSettings Reset()
        {
            var settings = AppSettings.CreateDefault();
            Save(settings);
            return settings;
        }

        private static void Sanitize(AppSettings settings)
        {
            settings.SourceFolders = settings.SourceFolders ?? new System.Collections.Generic.List<string>();
            settings.SourceFolders.RemoveAll(string.IsNullOrWhiteSpace);
            settings.TargetFolder = settings.TargetFolder ?? string.Empty;
            settings.IncludingExtensions = settings.IncludingExtensions ?? new System.Collections.Generic.List<string>();
            settings.BarringExtensions = settings.BarringExtensions ?? new System.Collections.Generic.List<string>();
        }
    }
}
