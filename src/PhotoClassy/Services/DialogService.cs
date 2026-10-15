using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PhotoClassy.Core.Engine;
using PhotoClassy.Views;

namespace PhotoClassy.Services
{
    public class DialogService : IDialogService
    {
        private const string Title = "PhotoClassy";
        private readonly Window _owner;

        public DialogService(Window owner)
        {
            _owner = owner;
        }

        public string PickFolder(string description, string initialFolder)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                    dialog.SelectedPath = initialFolder;

                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        public string PickLogSavePath(string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Title = "로그 저장",
                FileName = defaultFileName,
                Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                DefaultExt = ".txt"
            };
            return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
        }

        public void ShowInfo(string message) =>
            MessageBox.Show(_owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);

        public void ShowWarning(string message) =>
            MessageBox.Show(_owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public void ShowError(string message) =>
            MessageBox.Show(_owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Error);

        public bool Confirm(string message) =>
            MessageBox.Show(_owner, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public bool AskDeleteDuplicates(int duplicateCount, string duplicatesFolder)
        {
            var window = new DuplicateConfirmWindow(duplicateCount, duplicatesFolder) { Owner = _owner };
            return window.ShowDialog() == true;
        }

        public void ShowSummary(ClassificationResult result, Action saveLog, Action openTarget)
        {
            var window = new SummaryWindow(result, saveLog, openTarget) { Owner = _owner };
            window.ShowDialog();
        }
    }
}
