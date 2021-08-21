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
using System.Windows.Shapes;

namespace Subfish
{
    /// <summary>
    /// Interaction logic for DownloadOptions.xaml
    /// </summary>
    public partial class DownloadOptionsWindow : Window
    {
        public DownloadOptionsWindow()
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
            ((MainWindow)this.Owner).DownloadSubs();
        }

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
