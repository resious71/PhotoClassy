using System;
using System.ComponentModel;
using System.Windows;
using PhotoClassy.Core.Settings;
using PhotoClassy.Services;
using PhotoClassy.ViewModels;

namespace PhotoClassy.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel(new DialogService(this), SettingsService.ForInstallFolder());
            _viewModel.LogAdded += OnLogAdded;
            DataContext = _viewModel;

            if (AdminHelper.IsAdministrator())
                Title += " (관리자)";
        }

        private void OnLogAdded(object sender, EventArgs e)
        {
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_viewModel.OnClosing())
                e.Cancel = true;
            base.OnClosing(e);
        }
    }
}
