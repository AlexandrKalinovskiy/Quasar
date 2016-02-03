using StockSharp.Algo.Candles;
using StockSharp.Algo.Storages;
using StockSharp.Blackwood;
using StockSharp.BusinessEntities;
using StockSharp.IQFeed;
using StockSharp.Logging;
using StockSharp.Messages;
using StockSharp.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace Quasar
{
    public class Worker
    {

        //public static MainWindow Instance { get; private set; }
        public BlackwoodTrader Trader { get; private set; }
        public Portfolio Portfolio;
        private IStorageRegistry storage;
        private ISecurityStorage securityStorage;
        private Security security;
        private List<IQFeedTrader> connectors;
        private bool IsOneStart = true;

        // объявляем шлюз
        private IQFeedTrader trader;
        private bool run = false;

        public Worker()
        {
            storage = new StorageRegistry();
            securityStorage = storage.GetSecurityStorage();

            connectors = new List<IQFeedTrader>();
        }

        private int securitiesCount = 0;

        public int SecuritiesCount
        {
            get
            {
                return securitiesCount;
            }
        }

        private DateTime startSession = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 3, 9, 30, 00);  //начало торговой сессии 
        private DateTime endSession = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 3, 15, 55, 00);   //окончание торговой сессии 

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

            var address = IPAddress.Parse("72.5.42.156");

            Trader = new BlackwoodTrader();
            logManager.Sources.Add(Trader);

            Trader.Login = "FUSDEMO09";
            Trader.Password = "m6e533";
            Trader.ExecutionAddress = new IPEndPoint(address, BlackwoodAddresses.ExecutionPort);
            Trader.MarketDataAddress = new IPEndPoint(address, BlackwoodAddresses.MarketDataPort);
            Trader.HistoricalDataAddress = new IPEndPoint(address, BlackwoodAddresses.HistoricalDataPort);

            Trader.Connected += Trader_Connected;
            Trader.ConnectionError += Trader_ConnectionError;
            Trader.Disconnected += Trader_Disconnected;

            Trader.NewPortfolios += portfolios =>
            {
                foreach (var portfolio in portfolios)
                {
                    Portfolio = portfolio;
                    Debug.Print("Portfolio name {0}", portfolio.Name);
                    Debug.Print("Portfolio RealizedPnL {0}", portfolio.RealizedPnL);
                    Debug.Print("Portfolio UnrealizedPnL {0}", portfolio.UnrealizedPnL);
                }
            };

            Trader.NewCandles += Trader_NewCandles;

            Trader.NewOrders += Trader_NewOrders;
            Trader.NewPositions += Trader_NewPositions;
            Trader.Connect();
        }

        private void Trader_NewCandles(CandleSeries arg1, IEnumerable<Candle> arg2)
        {
            foreach (var candle in arg2)
            {
                var candleOpenTime = candle.OpenTime - TimeSpan.FromHours(11);

                if (candleOpenTime.Ticks >= startSession.Ticks && candleOpenTime.Ticks <= endSession.Ticks) 
                    Debug.Print("Open time {0}, Open price {1} {2} - {3}", candleOpenTime, candle.OpenPrice, candle.OpenTime.Ticks, endSession.Ticks);
            }
        }

        private void Trader_NewPositions(IEnumerable<Position> positions)
        {
            foreach (var position in positions)
            {
                Debug.Print("Positions {0}", position);
            }
        }

        private void Trader_NewOrders(IEnumerable<Order> orders)
        {
            foreach(var order in orders)
            {
                Debug.Print("Orders {0}", order);
            }            
        }

        private void Trader_Disconnected()
        {
            Debug.Print("Blackwood disconnected"); ;
        }

        private void Trader_ConnectionError(Exception obj)
        {
            Debug.Print("Blackwood connection error {0}", obj);
        }

        private void Trader_Connected()
        {
            Debug.Print("Blackwood connected {0}", Trader.ConnectionState);

            foreach (var security in securityStorage.Lookup(new Security()))
            {
                if (security.Code == "A")
                {
                    this.security = security;
                }
            }
           

            //var order = new Order
            //{
            //    Type = OrderTypes.Limit,
            //    Portfolio = portfolio,
            //    Volume = 100,
            //    Price = 7.30m,
            //    Security = criteria,
            //    Direction = Sides.Buy
            //};

            //Trader.RegisterOrder(order);
        }

        public void Disconnect()
        {
            trader.Disconnect();
            Trader.Disconnect();
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

        //Метод закачивает инструменты для обработки если флаг установлен в True
        public void Download(bool IsSync)
        {
            if (IsSync)
            {
                securityStorage.DeleteBy(new Security());
                trader.NewSecurities += trader_NewSecurities;

                var criteria = new Security
                {
                    Type = SecurityTypes.Stock,
                    Board = ExchangeBoard.Nyse
                };

                trader.LookupSecurities(criteria);
            }
        }

        int sc = 0;

        //в метод поступают закачанные инструменты
        public void trader_NewSecurities(IEnumerable<Security> allSecurities)
        {
            foreach (var security in allSecurities)
            {
                if (security.Board == ExchangeBoard.Nyse && security.Type == SecurityTypes.Stock && !security.Code.Contains("-")/* && security.Code == "AA"*/ /*&& !security.Code.Contains("*") && !security.Code.Contains("+")*/)
                {
                    //if (sc < 30)
                    //securities.Add(security);
                    //sc++;
                    securityStorage.Save(security);
                }
            }
        }

        public void GetSecurities()
        {
            int count = 0;
            foreach(var s in securityStorage.GetSecurityIds())
            {
                Debug.Print("{0}", s);
                count++;
            }
            Debug.Print("Count {0}", count);
        }

        public void Stop()
        {
            run = false;
        }

        public void Start(int useThreads)
        {
            var storSec = securityStorage.Lookup(new Security());
            securities.Clear();
            foreach (var sec in storSec)
            {
                securities.Add(sec);
            }
            run = true;
            securitiesCount = securities.Count;         //количество загруженный инструментов
            int stakeSize = securitiesCount / useThreads;   //размер "пучка" инструментов для отправки в поток
            List<Security> securitiesStake = new List<Security>();

            Debug.Print("Securities count {0}", securitiesCount);

            Func<object, IQFeedTrader, int> func = new Func<object, IQFeedTrader, int>(GetCandles);
            IAsyncResult asyncResult;

            int j = 0;

            for (int i = 0; i < securitiesCount; i++)
            {
                securitiesStake.Add(securities[i]);
                if (securitiesStake.Count == stakeSize)
                {
                    if (IsOneStart)
                    {
                        IQFeedTrader connector = new IQFeedTrader();
                        //connector.Connect();
                        connectors.Add(connector);
                    }
                    //Debug.Print("STAKE SIZE: {0} j = {1}", securitiesStake.Count, j);
                    asyncResult = func.BeginInvoke(securitiesStake, connectors[j], null, null);
                    securitiesStake = new List<Security>();                  
                    j++;
                }
            }

            if (securitiesStake.Count > 0)  //Отправляем последний неполный пучок
            {
                if (IsOneStart)
                {
                    IQFeedTrader connector = new IQFeedTrader();
                    //connector.Connect();
                    connectors.Add(connector);
                }
                //Debug.Print("STAKE SIZE: {0} j = {1}", securitiesStake.Count, j);
                asyncResult = func.BeginInvoke(securitiesStake, connectors[j], null, null);                
            }
            IsOneStart = false;
        }

        //Метод получает свечки для каждого обрабатываемого инстумента
        private int GetCandles(object obj, IQFeedTrader trader)
        {
            List<Security> securities = (List<Security>)obj;    //коллекция хранит "пучок", переданных в данный метод, инструментов для закачки таймсерий и передачи их в стратегию          

            //while (run)
            //{
            //trader = new IQFeedTrader();

            trader.Connect();
            Debug.Print("CONNECTION STATE: {0}", trader.ConnectionState);
            Thread.Sleep(1000);  //Уснуть на 1000 миллисекунд для того, чтобы дать коннектору установить подключение

            foreach (var security in securities)
            {
                if (!run)
                {
                    return 0;
                }
                bool isDaysSuccess;
                bool is5MinutesSucсess;

                //Debug.Print("BEFORE");
                List<Candle> dayCandles = (List<Candle>)trader.GetHistoricalCandles(security, typeof(TimeFrameCandle), TimeSpan.FromDays(1), 65, out isDaysSuccess);   //получить дневные свечки текущего инструмента
                List<Candle> candles = (List<Candle>)trader.GetHistoricalCandles(security, typeof(TimeFrameCandle), TimeSpan.FromMinutes(5), 100, out is5MinutesSucсess);   //получить пятиминутные свечки текущего инструмента
                List<Candle> intradayCandles = new List<Candle>();     //"чистые" свечки - с начала сессии по текущую 
                                                                       //Debug.Print("AFTER");

                foreach (var candle in candles)
                {
                    var candleOpenTime = candle.OpenTime - TimeSpan.FromHours(5);

                    if (candleOpenTime.Ticks >= startSession.Ticks && candleOpenTime.Ticks <= endSession.Ticks) //если свечка относится к текущей сессии, то добавляем ее в "чистую" коллекцию
                    {
                        intradayCandles.Add(candle);
                        //if(candle.Security.Code == "A")
                        //    Debug.Print("{0}, {1}", candleOpenTime, candle.ClosePrice);
                    }
                }

                if (!blackList.Contains(security))  //Если по текущему инструменту еще нет запущенных стратегий, то отправляем его на сканирование
                {
                    Scanner scanner = new Scanner
                    {
                        DayCandles = dayCandles,
                        IntradayCandles = intradayCandles,
                        Trader = Trader,
                        //Portfolio = Portfolio
                        Portfolio = new Portfolio()
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
            //trader.Disconnect();
            //Thread.Sleep(1000);
            //trader.Dispose();
            //}
            return 0;
        }

        private void Scanner_StrategyStarted(LevelsStrategy strategy)
        {
            blackList.Add(strategy.Security);
            logManager.Sources.Add(strategy); //при старте стратегии регистрируем данную стратегию в менеджере логирования, чтобы следить за её работой
        }

        //на данное событие подписываются внешние обработчики. Событие наступает после анализа и обработки инструмента
        public event Action<int, int> Processed;

        //public void GetCandleStorage()
        //{
        //    DateTime date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 23, 0, 0);
        //    foreach (var security in securityStorage.Lookup(new Security()))
        //    {
        //        var candleStorage = storage.GetCandleStorage(typeof(TimeFrameCandle), security, TimeSpan.FromMinutes(5));
        //        var candles = candleStorage.Load(date);
                
        //    }         
        //}
    }
}
