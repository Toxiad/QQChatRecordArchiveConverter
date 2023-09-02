
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
using Wpf.Ui.Controls;

namespace QQChatRecordArchiveConverter.Pages
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainView : Window
    { 
        public MainView()
        {
            InitializeComponent(); 
            //Loaded += (sender, args) =>
            //{
            //    Wpf.Ui.Appearance.Watcher.Watch(
            //        this,                                  // Window class
            //        Wpf.Ui.Appearance.BackgroundType.None, // Background type
            //        true                                   // Whether to change accents automatically
            //    );
            //};
        }
    }
}
