using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace InstallationStatusUpdater.Presentation
{
    /// <summary>
    /// Interaction logic for StatusUpdateResultsWindow.xaml
    /// </summary>
    public partial class StatusUpdateResultsWindow : UserControl
    {
        public StatusUpdateResultsWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is StatusUpdateResultsWindowViewModel viewModel)
            {
                viewModel.CloseRequested -= OnCloseRequested;
                viewModel.CloseRequested += OnCloseRequested;
            }
        }

        private void OnCloseRequested(object sender, EventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        /// <summary>
        /// Binds the selection of every row to the game it presents, so a single click on a row
        /// selects it and the commands of the view model always know what is selected. Doing it
        /// here instead of through a row style keeps the theme's row appearance untouched.
        /// </summary>
        private void OnGridLoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row.DataContext is StatusUpdateResultEntryViewModel entry))
            {
                return;
            }

            var binding = new Binding(nameof(StatusUpdateResultEntryViewModel.IsSelected))
            {
                Mode = BindingMode.TwoWay,
                Source = entry
            };
            BindingOperations.SetBinding(e.Row, DataGridRow.IsSelectedProperty, binding);
        }
    }
}
