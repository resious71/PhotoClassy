using System;
using System.Windows;
using System.Windows.Controls;
using PhotoClassy.Core.Engine;
using PhotoClassy.Core.Progress;

namespace PhotoClassy.Views
{
    public partial class SummaryWindow : Window
    {
        private readonly Action _saveLog;
        private readonly Action _openTarget;

        public SummaryWindow(ClassificationResult result, Action saveLog, Action openTarget)
        {
            InitializeComponent();
            _saveLog = saveLog;
            _openTarget = openTarget;

            MessageText.Text = result.CompletionMessage;

            AddRow(StatsGrid, "전체 처리 파일 수", $"{result.TotalFiles:N0}개");
            AddRow(StatsGrid, "이동/복사된 파일 수", $"{result.Transferred:N0}개");
            AddRow(StatsGrid, "중복 파일 수", $"{result.Duplicates:N0}개");
            AddRow(StatsGrid, "실패한 파일 수", $"{result.Failed:N0}개");
            AddRow(StatsGrid, "건너뛴 파일 수", result.SkippedHiddenOrSystem > 0
                ? $"{result.Skipped:N0}개 (확장자 필터, 숨김/시스템 파일 {result.SkippedHiddenOrSystem:N0}개 포함)"
                : $"{result.Skipped:N0}개 (확장자 필터)");
            if (result.Renamed > 0)
                AddRow(StatsGrid, "이름이 변경된 파일", $"{result.Renamed:N0}개 (같은 파일명 존재)");
            if (result.FallbackDates > 0)
                AddRow(StatsGrid, "수정일/생성일로 분류", $"{result.FallbackDates:N0}개 (EXIF 촬영 시간 없음)");

            if (result.RemovedEmptyFolders > 0)
                AddRow(StatsGrid, "삭제한 빈 폴더", $"{result.RemovedEmptyFolders:N0}개 (이동 후 비게 된 소스 폴더)");

            AddRow(TimeGrid, "소요 시간", TimeFormatter.Korean(result.Duration));
            AddRow(TimeGrid, "평균 처리 시간", TimeFormatter.PerFile(result.AveragePerFile));
            AddRow(TimeGrid, "시작 시간", result.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            AddRow(TimeGrid, "완료 시간", result.EndTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static void AddRow(Grid grid, string label, string value)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock { Text = label, Margin = new Thickness(0, 2, 16, 2), Foreground = System.Windows.Media.Brushes.DimGray };
            var valueBlock = new TextBlock { Text = value, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.SemiBold };
            Grid.SetRow(labelBlock, row);
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
        }

        private void OnOpenTarget(object sender, RoutedEventArgs e) => _openTarget?.Invoke();

        private void OnSaveLog(object sender, RoutedEventArgs e) => _saveLog?.Invoke();

        private void OnOk(object sender, RoutedEventArgs e) => Close();
    }
}
