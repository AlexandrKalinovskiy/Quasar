using Ecng.Xaml;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace Quasar
{
    public partial class MainWindow : Window
    {
        Worker worker = new Worker();

        int successCount = 0;
        int failCount = 0;
        int availableWorkThreads, availableIOThreads;

        public MainWindow()
        {
            InitializeComponent();
            
            worker.ConnectionStatus += status => this.GuiAsync(() => ConnectionStatus(status));
            worker.Processed += (a, b) => this.GuiAsync(() => Worker_Processed(a, b));
        }


        private void Worker_Processed(int a, int b)
        {
            successCount += a;
            failCount += b;

            lblSec.Content = "Успешно обработано: " + successCount + ", Не обработано: " + failCount + ", Всего: " + (successCount + failCount);
            
            ThreadPool.GetAvailableThreads(out availableWorkThreads, out availableIOThreads);

            lblThreads.Content = "Доступно потоков: " + availableWorkThreads;

            if ((successCount + failCount) == 2230)
            {
                successCount = 0;
                failCount = 0;               
                //worker.Scanner(30);
            }
        }

        ~MainWindow()
        {
            Debug.Print("Destructor!!!");
            worker.Disconnect();
        }    

        private void ConnectionStatus(int connectionStatus)
        {           
            switch (connectionStatus)
            {
                case 0:
                    Debug.Print("Connected");
                    btnConnect.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;
                    worker.Start();
                    break;
                case 1:
                    Debug.Print("Disconnected");
                    btnConnect.IsEnabled = true;
                    btnDisconnect.IsEnabled = false;
                    break;
                case 2:
                    Debug.Print("Connection error");
                    break;
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            worker.Connect();
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            worker.Disconnect();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            worker.Stop();
        }

        private void btnScanner_Click(object sender, RoutedEventArgs e)
        {
            worker.Scanner(50);
        }
    }
}
