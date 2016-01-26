using Ecng.Xaml;
using System;
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


        public MainWindow()
        {
            InitializeComponent();

            worker.ConnectionStatus += status => this.GuiAsync(() => ConnectionStatus(status));
            worker.Processed += (a, b) => this.GuiAsync(() => Worker_Processed(a, b));

            Processed += (i) => this.GuiAsync(() => MainWindow_Processed(i));

            Func<int> func = new Func<int>(GetAvailableWorkThreads);
            IAsyncResult asyncResult;
            asyncResult = func.BeginInvoke(null, null);
        }

        //Метод запускается асинхронно и считает количество доступных потоков, после чего вызывая действия Processed 
        private int GetAvailableWorkThreads()
        {
            while (true)
            {
                Thread.Sleep(500);

                int availableWorkThreads, availableIOThreads;

                ThreadPool.GetAvailableThreads(out availableWorkThreads, out availableIOThreads);

                Processed(availableWorkThreads);
            }
        }

        //Вызывается из GetAvailableWorkThreads
        private event Action<int> Processed;

        //В метод поступает количество доступных рабочих потоков
        private void MainWindow_Processed(int availableWorkThreads)
        {
            lblThreads.Content = "Доступно потоков: " + availableWorkThreads;
        }

        private void Worker_Processed(int a, int b)
        {
            successCount += a;
            failCount += b;

            lblSec.Content = "Успешно обработано: " + successCount + ", Не обработано: " + failCount + ", Всего: " + (successCount + failCount);           

            if ((successCount + failCount) == worker.SecuritiesCount)
            {
                successCount = 0;
                failCount = 0;               
            }
        }

        ~MainWindow()
        {
            Debug.Print("Destructor!!!");
        }    

        private void ConnectionStatus(int connectionStatus)
        {           
            switch (connectionStatus)
            {
                case 0:
                    Debug.Print("Connected");
                    btnConnect.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;
                    worker.Download();
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
            btnStop.IsEnabled = false;
            btnStart.IsEnabled = true;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            worker.Start(30);
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
        }
    }
}
