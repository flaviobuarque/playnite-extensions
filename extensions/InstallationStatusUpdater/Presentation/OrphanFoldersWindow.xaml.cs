using System;
using System.Windows;
using System.Windows.Controls;

namespace InstallationStatusUpdater.Presentation
{
    public partial class OrphanFoldersWindow : UserControl
    {
        public OrphanFoldersWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is OrphanFoldersWindowViewModel viewModel)
            {
                viewModel.CloseRequested -= OnCloseRequested;
                viewModel.CloseRequested += OnCloseRequested;
            }
        }

        private void OnCloseRequested(object sender, EventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
