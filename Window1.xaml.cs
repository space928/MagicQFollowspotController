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
using System.Runtime.InteropServices;


namespace MidiApp
{

    public partial class Window1 : Window
    {
        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);


        public Window1()
        {
            InitializeComponent();
        }

        public void grab()
        {
            Left = 0;
            Top = 0;
            Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            Width =  System.Windows.SystemParameters.PrimaryScreenWidth;

            ShowDialog();
        }


        private static void SetCursor(int x, int y)
        {

            SetCursorPos(x, y);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            SetCursor((int)Width / 2, (int)Height / 2);
            //   Console.WriteLine("Catured: " + CaptureMouse());

            vertLine.Height = Height;
            horizLine.Width = Width;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            vertLine.Margin = new Thickness(e.GetPosition(this).X, 0,0,0);
            horizLine.Margin = new Thickness(0,e.GetPosition(this).y, 0, 0);
        }
    }
}
