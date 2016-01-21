using StockSharp.Algo.Candles;
using StockSharp.BusinessEntities;
using StockSharp.IQFeed;
using StockSharp.Logging;
using StockSharp.Messages;
using StockSharp.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Quasar
{
    public class Worker
    {
        // объявляем шлюз
        private IQFeedTrader trader;
        private bool run = false;

        private DateTime startSession = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 20, 9, 30, 00);  //начало торговой сессии 
        private DateTime endSession = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 20, 15, 55, 00);   //окончание торговой сессии 

        //коллекция для хранения списка загруженных инструментов
        private List<Security> securities = new List<Security>();

        private List<Security> blackList = new List<Security>();    //Коллекция хранит в себе инструменты, по которым запущены стратегии. Такие инструменты повторно на запуск стратегии передавать нельзя

        public readonly LogManager logManager = new LogManager();    

        public void Connect()
        {
            trader = new IQFeedTrader();
            trader.Connected += () => SetConnectionStatus(0);
            trader.Disconnected += () => SetConnectionStatus(1);
            trader.ConnectionError += error => SetConnectionStatus(error);

            trader.Connect();

            var monitor = new MonitorWindow();
            monitor.Show();

            logManager.Listeners.Add(new GuiLogListener(monitor));
            logManager.Sources.Add(trader);
        }

        public void Disconnect()
        {
            trader.Disconnect();
        }

        public event Action<int> ConnectionStatus;

        private void SetConnectionStatus(object obj)
        {
            try
            {
                ConnectionStatus((int)obj); //возврат кода успешного подключения/отключения
            }
            catch (Exception e)
            {
                ConnectionStatus(2); //возврат кода разрыва соединения
            }
        }

        //====================================================================================================================================================

        //Метод закачивает инструменты для обработки
        public void Download()
        {
            trader.NewSecurities += trader_NewSecurities;

            var criteria = new Security
            {
                Type = SecurityTypes.Stock,
                Board = ExchangeBoard.Nyse
            };

            trader.LookupSecurities(criteria);
        }

        int sc = 0;

        //в метод поступают закачанные инструменты
        public void trader_NewSecurities(IEnumerable<Security> allSecurities)
        {
            foreach (var security in allSecurities)
            {
                if (security.Board == ExchangeBoard.Nyse && security.Type == SecurityTypes.Stock && !security.Code.Contains("-")/* && security.Code == "BRK.A" && !security.Code.Contains("*") && !security.Code.Contains("+")*/)
                {
                    //if (sc < 30)
                    securities.Add(security);
                    //sc++;
                }
            }
        }

        public void Stop()
        {
            run = false;
        }

        public void Start(int useThreads)
        {
            run = true;
            int securitiesCount = securities.Count;         //количество загруженный инструментов
            int stakeSize = securitiesCount / useThreads;   //размер "пучка" инструментов для отправки в поток
            List<Security> securitiesStake = new List<Security>();

            Func<object, int> func = new Func<object, int>(GetCandles);
            IAsyncResult asyncResult;

            for (int i = 0; i < securitiesCount; i++)
            {
                securitiesStake.Add(securities[i]);
                if (securitiesStake.Count == stakeSize)
                {
                    asyncResult = func.BeginInvoke(securitiesStake, null, null);
                    securitiesStake = new List<Security>();
                }
            }

            if (securitiesStake.Count > 0)  //Отправляем последний неполный пучок
            {
                asyncResult = func.BeginInvoke(securitiesStake, null, null);
            }
        }

        //Метод получает свечки для каждого обрабатываемого инстумента
        private int GetCandles(object obj)
        {
            List<Security> securities = (List<Security>)obj;    //коллекция хранит "пучок", переданных в данный метод, инструментов для закачки таймсерий и передачи их в стратегию          
            IQFeedTrader trader = new IQFeedTrader();

            while (run)
            {
                trader.Connect();
                Thread.Sleep(1000);  //Уснуть на 1000 миллисекунд для того, чтобы дать коннектору установить подключение 

                foreach (var security in securities)
                {
                    if (!run)
                    {
                        return 0;
                    }
                    bool isDaysSuccess;
                    bool is5MinutesSucсess;

                    List<Candle> dayCandles = (List<Candle>)trader.GetHistoricalCandles(security, typeof(TimeFrameCandle), TimeSpan.FromDays(1), 65, out isDaysSuccess);   //получить дневные свечки текущего инструмента
                    List<Candle> candles = (List<Candle>)trader.GetHistoricalCandles(security, typeof(TimeFrameCandle), TimeSpan.FromMinutes(5), 100, out is5MinutesSucсess);   //получить пятиминутные свечки текущего инструмента
                    List<Candle> intradayCandles = new List<Candle>();     //"чистые" свечки - с начала сессии по текущую 

                    foreach (var candle in candles)
                    {
                        var candleOpenTime = candle.OpenTime - TimeSpan.FromHours(5);

                        if (candleOpenTime.Ticks >= startSession.Ticks && candleOpenTime.Ticks <= endSession.Ticks) //если свечка относится к текущей сессии, то добавляем ее в "чистую" коллекцию
                        {
                            intradayCandles.Add(candle);
                        }
                    }

                    if (!blackList.Contains(security))  //Если по текущему инструменту еще нет запущенных стратегий, то отправляем его на сканирование
                    {
                        Scanner scanner = new Scanner
                        {
                            DayCandles = dayCandles,
                            IntradayCandles = intradayCandles
                        };

                        scanner.StrategyStarted += Scanner_StrategyStarted; //Подписываемся на события старта стратегии, чтобы добавить ее в менеджер логгирования для отслеживания

                        int i = scanner.Scan();
                        if (i == 1)
                            Processed(i, 0);
                        else
                        {
                            Processed(i, 1);
                        }
                    }                  
                }
            }
            return 0;
        }

        private void Scanner_StrategyStarted(LevelsStrategy strategy)
        {
            blackList.Add(strategy.Security);
            logManager.Sources.Add(strategy); //при старте стратегии регистрируем данную стратегию в менеджере логирования, чтобы следить за её работой
        }

        //на данное событие подписываются внешние обработчики. Событие наступает после анализа и обработки инструмента
        public event Action<int, int> Processed;
    }
}
