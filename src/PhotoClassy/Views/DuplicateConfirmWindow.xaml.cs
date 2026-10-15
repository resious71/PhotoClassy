using System.Windows;

namespace PhotoClassy.Views
{
    public partial class DuplicateConfirmWindow : Window
    {
        public DuplicateConfirmWindow(int duplicateCount, string duplicatesFolder)
        {
            InitializeComponent();
            CountText.Text = $"중복 파일 {duplicateCount:N0}개가 감지되었습니다.";
            FolderText.Text = duplicatesFolder;
        }

        private void OnDelete(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnKeep(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
