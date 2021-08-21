using System.Windows;

namespace Subfish
{
    /// <summary>
    /// Interaction logic for ExportOptionsWindow.xaml
    /// </summary>
    public partial class ExportOptionsWindow : Window
    {
        public ExportOptionsWindow()
        {
            InitializeComponent();
        }

        void CloseWindow()
        {
            this.Visibility = Visibility.Hidden;
        }
        private void butCancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void windowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            CloseWindow();

        }

        private void buttonGo_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
           
            ((MainWindow)this.Owner).ExportAsEDL();
        }

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
