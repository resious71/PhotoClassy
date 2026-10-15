using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using PhotoClassy.Core.Duplicates;
using PhotoClassy.Core.Engine;
using PhotoClassy.Core.Filtering;
using PhotoClassy.Core.Logging;
using PhotoClassy.Core.Progress;
using PhotoClassy.Core.Settings;
using PhotoClassy.Core.Validation;
using PhotoClassy.Services;

namespace PhotoClassy.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs;
        private readonly SettingsService _settingsService;
        private readonly ConcurrentQueue<LogEntry> _pendingLog = new ConcurrentQueue<LogEntry>();
        private readonly DispatcherTimer _timer;

        private CancellationTokenSource _cancellation;
        private ProgressInfo _lastProgress;
        private ClassificationResult _lastResult;

        private string _selectedSourceFolder;
        private string _targetFolder = string.Empty;
        private ClassificationFormat _format = ClassificationFormat.YearMonth;
        private FileOperation _operation = FileOperation.Move;
        private string _includeText = string.Empty;
        private string _excludeText = string.Empty;
        private bool _isRunning;

        private string _stageText = ProcessingStage.Idle.ToDisplayText();
        private double _progressPercent;
        private bool _isIndeterminate;
        private string _totalText = "-";
        private string _processedText = "-";
        private string _remainingText = "-";
        private string _percentText = "0%";
        private string _currentFileName = string.Empty;
        private string _currentFilePath = string.Empty;
        private string _currentIndexText = "-";
        private string _elapsedText = "00:00:00";
        private string _averageText = "-";
        private string _estimatedTotalText = "-";
        private string _remainingTimeText = "-";
        private string _completionText = "-";

        public MainViewModel(IDialogService dialogs, SettingsService settingsService)
        {
            _dialogs = dialogs;
            _settingsService = settingsService;

            AddSourceCommand = new RelayCommand(AddSource, () => !IsRunning);
            RemoveSourceCommand = new RelayCommand(RemoveSource, () => !IsRunning && SelectedSourceFolder != null);
            SelectTargetCommand = new RelayCommand(SelectTarget, () => !IsRunning);
            StartCommand = new RelayCommand(async () => await StartAsync(), () => !IsRunning);
            CancelCommand = new RelayCommand(Cancel, () => IsRunning);
            ResetSettingsCommand = new RelayCommand(ResetSettings, () => !IsRunning);
            SaveLogCommand = new RelayCommand(SaveLog, () => Log.Count > 0);
            ClearLogCommand = new RelayCommand(() => Log.Clear(), () => !IsRunning && Log.Count > 0);
            OpenTargetCommand = new RelayCommand(OpenTarget, () => Directory.Exists(TargetFolder));

            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (s, e) => OnTick();
            _timer.Start();

            LoadSettings();
        }

        #region 설정

        public ObservableCollection<string> SourceFolders { get; } = new ObservableCollection<string>();

        public string SelectedSourceFolder
        {
            get => _selectedSourceFolder;
            set => SetProperty(ref _selectedSourceFolder, value);
        }

        public string TargetFolder
        {
            get => _targetFolder;
            set => SetProperty(ref _targetFolder, value ?? string.Empty);
        }

        public bool IsYearMonth
        {
            get => _format == ClassificationFormat.YearMonth;
            set { if (value) SetFormat(ClassificationFormat.YearMonth); }
        }

        public bool IsYearMonthDay
        {
            get => _format == ClassificationFormat.YearMonthDay;
            set { if (value) SetFormat(ClassificationFormat.YearMonthDay); }
        }

        public bool IsMove
        {
            get => _operation == FileOperation.Move;
            set { if (value) SetOperation(FileOperation.Move); }
        }

        public bool IsCopy
        {
            get => _operation == FileOperation.Copy;
            set { if (value) SetOperation(FileOperation.Copy); }
        }

        public string IncludeText
        {
            get => _includeText;
            set => SetProperty(ref _includeText, value ?? string.Empty);
        }

        public string ExcludeText
        {
            get => _excludeText;
            set => SetProperty(ref _excludeText, value ?? string.Empty);
        }

        #endregion

        #region 진행 상황

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsIdle => !IsRunning;

        public string StageText { get => _stageText; private set => SetProperty(ref _stageText, value); }

        public double ProgressPercent { get => _progressPercent; private set => SetProperty(ref _progressPercent, value); }

        public bool IsIndeterminate { get => _isIndeterminate; private set => SetProperty(ref _isIndeterminate, value); }

        public string TotalText { get => _totalText; private set => SetProperty(ref _totalText, value); }

        public string ProcessedText { get => _processedText; private set => SetProperty(ref _processedText, value); }

        public string RemainingText { get => _remainingText; private set => SetProperty(ref _remainingText, value); }

        public string PercentText { get => _percentText; private set => SetProperty(ref _percentText, value); }

        public string CurrentFileName { get => _currentFileName; private set => SetProperty(ref _currentFileName, value); }

        public string CurrentFilePath { get => _currentFilePath; private set => SetProperty(ref _currentFilePath, value); }

        public string CurrentIndexText { get => _currentIndexText; private set => SetProperty(ref _currentIndexText, value); }

        public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

        public string AverageText { get => _averageText; private set => SetProperty(ref _averageText, value); }

        public string EstimatedTotalText { get => _estimatedTotalText; private set => SetProperty(ref _estimatedTotalText, value); }

        public string RemainingTimeText { get => _remainingTimeText; private set => SetProperty(ref _remainingTimeText, value); }

        public string CompletionText { get => _completionText; private set => SetProperty(ref _completionText, value); }

        public ObservableCollection<LogEntry> Log { get; } = new ObservableCollection<LogEntry>();

        #endregion

        #region 명령

        public ICommand AddSourceCommand { get; }
        public ICommand RemoveSourceCommand { get; }
        public ICommand SelectTargetCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        public ICommand SaveLogCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand OpenTargetCommand { get; }

        #endregion

        /// <summary>창을 닫을 때 호출. 닫아도 되면 true.</summary>
        public bool OnClosing()
        {
            if (IsRunning)
            {
                if (!_dialogs.Confirm("작업이 진행 중입니다. 작업을 취소하고 종료하시겠습니까?"))
                    return false;
                _cancellation?.Cancel();
            }
            TrySaveSettings();
            return true;
        }

        private void SetFormat(ClassificationFormat format)
        {
            if (_format == format) return;
            _format = format;
            OnPropertyChanged(nameof(IsYearMonth));
            OnPropertyChanged(nameof(IsYearMonthDay));
        }

        private void SetOperation(FileOperation operation)
        {
            if (_operation == operation) return;
            _operation = operation;
            OnPropertyChanged(nameof(IsMove));
            OnPropertyChanged(nameof(IsCopy));
        }

        private void AddSource()
        {
            var folder = _dialogs.PickFolder("추가할 소스 폴더를 선택하세요.", SelectedSourceFolder ?? SourceFolders.LastOrDefault());
            if (folder == null)
                return;

            if (!Directory.Exists(folder))
            {
                _dialogs.ShowError($"소스 폴더가 존재하지 않습니다:\n{folder}");
                return;
            }
            if (SourceFolders.Any(f => string.Equals(f.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                _dialogs.ShowWarning("이미 추가된 폴더입니다.");
                return;
            }

            SourceFolders.Add(folder);
            SelectedSourceFolder = folder;
        }

        private void RemoveSource()
        {
            if (SelectedSourceFolder != null)
                SourceFolders.Remove(SelectedSourceFolder);
        }

        private void SelectTarget()
        {
            var folder = _dialogs.PickFolder("사진을 분류해 저장할 대상 폴더를 선택하세요.", TargetFolder);
            if (folder != null)
                TargetFolder = folder;
        }

        private AppSettings CollectSettings()
        {
            return new AppSettings
            {
                SourceFolders = SourceFolders.ToList(),
                TargetFolder = TargetFolder.Trim(),
                Format = _format,
                Operation = _operation,
                IncludingExtensions = ExtensionFilter.Parse(IncludeText),
                BarringExtensions = ExtensionFilter.Parse(ExcludeText)
            };
        }

        private void ApplySettings(AppSettings settings)
        {
            SourceFolders.Clear();
            foreach (var folder in settings.SourceFolders)
                SourceFolders.Add(folder);
            TargetFolder = settings.TargetFolder;
            SetFormat(settings.Format);
            SetOperation(settings.Operation);
            IncludeText = ExtensionFilter.Format(settings.IncludingExtensions);
            ExcludeText = ExtensionFilter.Format(settings.BarringExtensions);
        }

        private void LoadSettings()
        {
            var settings = _settingsService.Load(out var error);
            ApplySettings(settings);
            if (error != null)
                AddLog(new LogEntry(LogLevel.Warning, error, _settingsService.FilePath));
        }

        private void TrySaveSettings()
        {
            try
            {
                _settingsService.Save(CollectSettings());
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AddLog(new LogEntry(LogLevel.Warning, $"설정을 저장하지 못했습니다: {ex.Message}", _settingsService.FilePath));
            }
        }

        private void ResetSettings()
        {
            if (!_dialogs.Confirm("모든 설정을 기본값으로 초기화하시겠습니까?"))
                return;

            try
            {
                ApplySettings(_settingsService.Reset());
                AddLog(new LogEntry(LogLevel.Info, "설정을 초기화했습니다."));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ApplySettings(AppSettings.CreateDefault());
                _dialogs.ShowWarning($"설정을 기본값으로 되돌렸지만 파일에 저장하지 못했습니다.\n{ex.Message}");
            }
        }

        private async Task StartAsync()
        {
            var settings = CollectSettings();

            var validation = FolderValidator.Validate(settings);
            if (!validation.IsValid)
            {
                _dialogs.ShowWarning("작업을 시작할 수 없습니다.\n\n" + string.Join("\n", validation.Errors));
                return;
            }

            if (validation.TargetMissing)
            {
                if (!_dialogs.Confirm($"대상 폴더가 존재하지 않습니다.\n{settings.TargetFolder}\n\n폴더를 만드시겠습니까?"))
                    return;
                try
                {
                    Directory.CreateDirectory(settings.TargetFolder);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                           || ex is NotSupportedException || ex is ArgumentException)
                {
                    _dialogs.ShowError($"대상 폴더 생성 실패:\n{ex.Message}");
                    OfferAdministrator(ex is UnauthorizedAccessException);
                    return;
                }
            }

            TrySaveSettings();
            ResetProgress();
            IsRunning = true;
            _cancellation = new CancellationTokenSource();

            var progress = new Progress<ProgressInfo>(info => _lastProgress = info);
            ClassificationResult result;
            try
            {
                result = await new ClassificationEngine().RunAsync(settings, progress, entry => _pendingLog.Enqueue(entry),
                    _cancellation.Token);
            }
            finally
            {
                _cancellation.Dispose();
                _cancellation = null;
                IsRunning = false;
            }

            FlushLog();
            _lastResult = result;
            OnTick();
            ShowCompletion(result);
        }

        private void ShowCompletion(ClassificationResult result)
        {
            if (result.Status == RunStatus.Aborted)
                _dialogs.ShowError(result.CompletionMessage);

            if (result.Duplicates > 0 && Directory.Exists(result.DuplicatesFolder))
            {
                if (_dialogs.AskDeleteDuplicates(result.Duplicates, result.DuplicatesFolder))
                {
                    var deleted = DuplicateCleaner.DeleteAll(result.TargetFolder, out var error);
                    AddLog(new LogEntry(LogLevel.Info, $"_Duplicates 폴더에서 {deleted}개 파일을 삭제했습니다.",
                        result.DuplicatesFolder));
                    if (error != null)
                    {
                        AddLog(new LogEntry(LogLevel.Error, error, result.DuplicatesFolder));
                        _dialogs.ShowWarning(error);
                    }
                }
                else
                {
                    AddLog(new LogEntry(LogLevel.Info, "중복 파일을 _Duplicates 폴더에 보관합니다.", result.DuplicatesFolder));
                }
            }

            _dialogs.ShowSummary(result, SaveLog, () => OpenFolder(result.TargetFolder));
            OfferAdministrator(result.HadPermissionErrors);
        }

        private void OfferAdministrator(bool permissionError)
        {
            if (!permissionError || AdminHelper.IsAdministrator())
                return;

            if (_dialogs.Confirm("권한이 없어 처리하지 못한 파일이나 폴더가 있습니다.\n" +
                                 "관리자 권한으로 PhotoClassy 를 다시 실행하시겠습니까?"))
            {
                TrySaveSettings();
                if (!AdminHelper.RestartAsAdministrator())
                    _dialogs.ShowWarning("관리자 권한으로 다시 실행하지 못했습니다.");
            }
        }

        private void Cancel()
        {
            _cancellation?.Cancel();
            StageText = "취소하는 중...";
        }

        private void SaveLog()
        {
            FlushLog();
            var path = _dialogs.PickLogSavePath($"PhotoClassy_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            if (path == null)
                return;

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine("PhotoClassy 작업 로그");
                if (_lastResult != null)
                {
                    var r = _lastResult;
                    builder.AppendLine($"시작: {r.StartTime:yyyy-MM-dd HH:mm:ss}, 완료: {r.EndTime:yyyy-MM-dd HH:mm:ss}, " +
                                       $"소요: {TimeFormatter.Korean(r.Duration)}");
                    builder.AppendLine($"전체 {r.TotalFiles}, 이동/복사 {r.Transferred}, 중복 {r.Duplicates}, " +
                                       $"실패 {r.Failed}, 건너뜀 {r.Skipped}");
                }
                builder.AppendLine(new string('-', 80));
                foreach (var entry in Log)
                    builder.AppendLine(entry.ToString());

                File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
                _dialogs.ShowInfo($"로그를 저장했습니다.\n{path}");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _dialogs.ShowError($"로그를 저장하지 못했습니다.\n{ex.Message}");
            }
        }

        private void OpenTarget() => OpenFolder(TargetFolder);

        private void OpenFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                _dialogs.ShowWarning("대상 폴더가 존재하지 않습니다.");
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }

        private void AddLog(LogEntry entry) => _pendingLog.Enqueue(entry);

        private void FlushLog()
        {
            var added = false;
            while (_pendingLog.TryDequeue(out var entry))
            {
                Log.Add(entry);
                added = true;
            }
            if (added)
                LogAdded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>로그가 추가되었을 때 (뷰에서 자동 스크롤용).</summary>
        public event EventHandler LogAdded;

        private void ResetProgress()
        {
            _lastProgress = null;
            _lastResult = null;
            ProgressPercent = 0;
            PercentText = "0%";
            TotalText = ProcessedText = RemainingText = CurrentIndexText = "-";
            CurrentFileName = CurrentFilePath = string.Empty;
            ElapsedText = "00:00:00";
            AverageText = EstimatedTotalText = RemainingTimeText = CompletionText = "-";
        }

        private void OnTick()
        {
            FlushLog();

            var info = _lastProgress;
            if (info == null)
                return;

            StageText = _cancellation?.IsCancellationRequested == true ? "취소하는 중..." : info.Stage.ToDisplayText();

            var hasTotal = info.Total > 0;
            IsIndeterminate = IsRunning && !hasTotal && info.Stage == ProcessingStage.Scanning;
            ProgressPercent = info.Stage == ProcessingStage.Completed ? 100 : info.Percent;
            PercentText = $"{ProgressPercent:0}%";

            if (hasTotal)
            {
                TotalText = $"{info.Total:N0}개";
                ProcessedText = $"{info.Processed:N0}개";
                RemainingText = $"{info.Remaining:N0}개";
                CurrentIndexText = $"{Math.Min(info.Processed + (IsRunning ? 1 : 0), info.Total):N0}/{info.Total:N0}";
            }
            else
            {
                TotalText = info.Stage == ProcessingStage.Scanning ? $"{info.Processed:N0}개 발견" : "-";
                ProcessedText = RemainingText = CurrentIndexText = "-";
            }

            if (info.CurrentFilePath != null || !IsRunning)
            {
                CurrentFileName = info.CurrentFileName ?? string.Empty;
                CurrentFilePath = info.CurrentFilePath ?? string.Empty;
            }

            var now = DateTime.Now;
            var end = !IsRunning && _lastResult != null ? _lastResult.EndTime : now;
            ElapsedText = TimeFormatter.Full(end - info.StartTime);

            if (!IsRunning)
            {
                RemainingTimeText = "00:00:00";
                CompletionText = _lastResult != null ? $"{_lastResult.EndTime:t} 완료" : "-";
                if (_lastResult != null && _lastResult.Processed > 0)
                    AverageText = TimeFormatter.PerFile(_lastResult.AveragePerFile);
                return;
            }

            // 남은 시간은 현재 단계의 처리 속도로 추정한다.
            var estimate = TimeEstimator.Compute(now - info.StageStartTime, info.Processed, info.Total, now);
            AverageText = estimate.AveragePerFile.HasValue ? TimeFormatter.PerFile(estimate.AveragePerFile.Value) : "-";
            EstimatedTotalText = estimate.EstimatedTotal.HasValue ? TimeFormatter.Full(estimate.EstimatedTotal.Value) : "-";
            RemainingTimeText = estimate.Remaining.HasValue
                ? $"{TimeFormatter.Full(estimate.Remaining.Value)} ({TimeFormatter.Short(estimate.Remaining.Value)})"
                : "-";
            CompletionText = estimate.EstimatedCompletion.HasValue
                ? TimeFormatter.Completion(estimate.EstimatedCompletion.Value)
                : "-";
        }
    }
}
