using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using System.Diagnostics;

namespace StopsToTransfers
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        BackgroundWorker bg;
        bool stop = false;
        string fileStopsPath = null;
        string fileTransfersPath = null;
        string walkSpeed = "3";
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            InitializeBackgroundWorker();
        }

        #region FilesSelection
        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text file(*.txt) | *.txt";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (openFileDialog.ShowDialog() == true)
            {
                txtFileStops.Text = openFileDialog.FileName;
                if (txtFileTransfers.Text != "")
                    btnConvert.IsEnabled = true;
            }
        }
        private void btnOpenFileTransfers_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file(*.txt) | *.txt";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (saveFileDialog.ShowDialog() == true)
            {
                txtFileTransfers.Text = saveFileDialog.FileName;
                if (txtFileStops.Text != "")
                    btnConvert.IsEnabled = true;
            }
        }

        #endregion

        #region Initialization
        private void InitializeProgressBar()
        {
            progressBarConvert.Value = 0;
            progressBarConvert.Maximum = 100;
        }
        private void InitializeBackgroundWorker()
        {
            bg = new BackgroundWorker();
            bg.DoWork += Bg_DoWork;
            bg.ProgressChanged += Bg_ProgressChanged;
            bg.RunWorkerCompleted += Bg_RunWorkerCompleted;
            bg.WorkerReportsProgress = true;
            bg.WorkerSupportsCancellation = true;
        }
        #endregion

        private void Bg_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                btnCancel.IsEnabled = false;
                btnConvert.IsEnabled = true;
                progressBarConvert.IsIndeterminate = false;
                MessageBoxResult result = MessageBox.Show("Opération annulée !", "Attention", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                btnCancel.IsEnabled = false;
                btnConvert.IsEnabled = true;
                progressBarConvert.IsIndeterminate = false;
                progressBarConvert.Value = 100;
                MessageBoxResult result = MessageBox.Show("Le fichier à été compilé avec succès !\n\nVoulez-vous ouvrir le fichier ?", "Information", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(fileTransfersPath);
                }
            }
        }

        private void Bg_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        private void Bg_DoWork(object sender, DoWorkEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                progressBarConvert.Value = 0;
                fileStopsPath = txtFileStops.Text;
                fileTransfersPath = txtFileTransfers.Text;
                btnCancel.IsEnabled = true;
                progressBarConvert.IsIndeterminate = true;
                walkSpeed = cbWalkSpeed.SelectionBoxItem.ToString();
            });

            #region Variables
            List<string> fileStops1 = new List<string>();
            fileStops1 = File.ReadAllLines(fileStopsPath).ToList();
            List<string> fileStops2 = new List<string>();
            fileStops2 = File.ReadAllLines(fileStopsPath).ToList();
            int colStops_Id = fileStops1[0].Split(',').Select((c, ix) => new { col = c, index = ix }).FirstOrDefault(c => c.col.Contains("stop_id")).index;
            int colStop_lat = fileStops1[0].Split(',').Select((c, ix) => new { col = c, index = ix }).FirstOrDefault(c => c.col.Contains("stop_lat")).index;
            int colStop_lon = fileStops1[0].Split(',').Select((c, ix) => new { col = c, index = ix }).FirstOrDefault(c => c.col.Contains("stop_lon")).index;
            List<string> lineValues1;
            List<string> lineValues2;
            double lat1 = 0;
            double lat2 = 0;
            double lon1 = 0;
            double lon2 = 0;
            double distance = 0;
            string timeDistance = null;
            #endregion
            using (StreamWriter writer = new StreamWriter(fileTransfersPath))
            {
                writer.WriteLine("from_stop_id,to_stop_id,transfer_type,min_transfer_time");
                foreach (var lineFile1 in fileStops1.Skip(1))
                {
                    lineValues1 = lineFile1.Split(',').ToList();
                    if (lineValues1[colStops_Id] != null)
                    {
                        if (lineValues1[colStop_lat].Any(char.IsDigit) || lineValues1[colStop_lon].Any(char.IsDigit))
                        {
                            lat1 = Convert.ToDouble(lineValues1[colStop_lat].Replace('.', ','));
                            lon1 = Convert.ToDouble(lineValues1[colStop_lon].Replace('.', ','));
                        }
                        foreach (var lineFile2 in fileStops2.Skip(1))
                        {
                            if (bg.CancellationPending)
                            {
                                e.Cancel = true;
                            }
                            else
                            {
                                lineValues2 = lineFile2.Split(',').ToList();
                                if (lineValues2[colStops_Id] != null)
                                {
                                    if (lineValues2[colStops_Id] != lineValues1[colStops_Id])
                                    {
                                        if (lineValues2[colStop_lat].Any(char.IsDigit) || lineValues2[colStop_lon].Any(char.IsDigit))
                                        {
                                            lat2 = Convert.ToDouble(lineValues2[colStop_lat].Replace('.', ','));
                                            lon2 = Convert.ToDouble(lineValues2[colStop_lon].Replace('.', ','));
                                        }
                                        distance = CalculateGeoDistance(lat1, lon1, lat2, lon2);
                                        if (distance <= 0.3)
                                        {
                                            timeDistance = CalculateTime(distance, walkSpeed);
                                            writer.WriteLine(lineValues1[colStops_Id] + "," + lineValues2[colStops_Id] + ",2," + timeDistance);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #region Functions
        private double CalculateGeoDistance(double lat1, double lon1, double lat2, double lon2)
        {
            if ((lat1 == lat2) && (lon1 == lon2))
            {
                return 0;
            }
            else
            {
                double dist = 0;
                double rlat1 = Math.PI * lat1 / 180;
                double rlat2 = Math.PI * lat2 / 180;
                double theta = lon1 - lon2;
                double rtheta = Math.PI * theta / 180;
                dist = Math.Sin(rlat1) * Math.Sin(rlat2) + Math.Cos(rlat1) * Math.Cos(rlat2) * Math.Cos(rtheta);
                dist = Math.Acos(dist);
                dist = dist * 180 / Math.PI;
                dist = dist * 60 * 1.1515;
                return dist * 1.609344;
            }
        }

        private string CalculateTime(double distance, string speed)
        {
            string test = (Math.Round((distance / Convert.ToDouble(speed)) * 3600, 0)).ToString();
            return test;
        }
        #endregion

        #region Buttons
        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Show();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            bg.RunWorkerAsync();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Voulez-vous vraiment arreter le processus en cours ?", "Attention", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                bg.CancelAsync();
            }
        }
        #endregion
    }
}
