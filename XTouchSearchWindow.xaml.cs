using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class XtouchSearcher : AdonisUI.Controls.AdonisWindow
    {
        public XtouchSearcher()
        {
            InitializeComponent();
        }


        public static void DoWorkWithModal(AdonisUI.Controls.AdonisWindow parent, Action<IProgress<string>> work)
        {
            XtouchSearcher splash = new XtouchSearcher();

            splash.Owner = parent;

            splash.Loaded += (_, args) =>
            {
                BackgroundWorker worker = new BackgroundWorker();

                Progress<string> progress = new Progress<string>(
                    data => { 
                        splash.Text.Text = data;
                        splash.questionMark.Visibility = (splash.questionMark.Visibility == Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
                    }); ;

                worker.DoWork += (s, workerArgs) => work(progress);

                worker.RunWorkerCompleted +=
                    (s, workerArgs) => splash.Close();

                worker.RunWorkerAsync();
            };

            splash.ShowDialog();
        }
    }
}
