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

namespace MidiApp
{
    /// <summary>
    /// Interaction logic for LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        //https://stackoverflow.com/a/46548292
        private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer scrollViewer &&
                Math.Abs(e.ExtentHeightChange) > 0.0)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private void ScrollToEnd_Click(object sender, RoutedEventArgs e)
        {
            var item = Logger.logList.LastOrDefault();
            if (item != null)
                LogListBox.ScrollIntoView(item);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Logger.logList.Clear();
        }
    }
}
