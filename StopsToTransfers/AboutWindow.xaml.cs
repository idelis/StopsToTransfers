using System.Windows;

namespace StopsToTransfers
{
    /// <summary>
    /// Logique d'interaction pour AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            txtAbout.Text = "Cette application permet la génération du fichier GTFS transfers.txt depuis le fichiers stops.txt.\n\nVersion 1.0.0\n\nIDELIS © 2019";
        }
    }
}