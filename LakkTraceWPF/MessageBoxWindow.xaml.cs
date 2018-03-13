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

namespace LakkTraceWPF
{
    /// <summary>
    /// Interaction logic for MessageBoxWindow.xaml
    /// </summary>
    public partial class MessageBoxWindow : Window
    {
        public MessageBoxWindow(string msg)
        {
            InitializeComponent();
            MessageLbl.Text = msg;
            this.SizeToContent = SizeToContent.Height;
            this.Topmost = true;
            this.Focus();
            OkBtn.Focus();
            
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void focus(object sender, EventArgs e)
        {
            OkBtn.Focus();
        }
    }
}
