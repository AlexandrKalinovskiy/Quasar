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
        private Object lockThis = new Object();
        //List<IQFeedTrader> tradersCollection = new List<IQFeedTrader>();

        //коллекция для хранения списка загруженных инструментов
        private List<Security> securities = new List<Security>();

        public readonly LogManager logManager = new LogManager();
        LevelsStrategy strategy;        

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

        //метод запускает основной цикл обработки
        public void Start()
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

        public void Scanner(int useThreads)
        {
            run = true;
            int securitiesCount = securities.Count;         //количество загруженный инструментов
            int stakeSize = securitiesCount / useThreads;   //размер "пучка" инструментов для отправки в поток
            List<Security> securitiesStake = new List<Security>();

            Debug.Print("Threads count: {0} ", useThreads);
            Debug.Print("Securities count: {0} ", securitiesCount);

            Func<object, int> func = new Func<object, int>(RunStrategy);
            IAsyncResult asyncResult;

            //if (tradersCollection.Count == 0)
            //{
            for (int i = 0; i < securitiesCount; i++)
            {
                securitiesStake.Add(securities[i]);
                if (securitiesStake.Count == stakeSize)
                {
                    //tradersCollection.Add(trader);

                    asyncResult = func.BeginInvoke(securitiesStake, null, null);
                    Debug.Print("Stake size: {0}, i = {1}", securitiesStake.Count, i);
                    securitiesStake = new List<Security>();
                }
            }
            //trader = new IQFeedTrader();

            //tradersCollection.Add(trader);
            if (securitiesStake.Count > 0)
            {
                asyncResult = func.BeginInvoke(securitiesStake, null, null);
                Debug.Print("Stake size: {0}", securitiesStake.Count);
            }
            //}
            //else
            //{
            //    int conCount = 0;
            //    for (int i = 0; i < securitiesCount; i++)
            //    {
            //        securitiesStake.Add(securities[i]);
            //        if (securitiesStake.Count == stakeSize)
            //        {
            //            asyncResult = func.BeginInvoke(securitiesStake, tradersCollection[conCount], null, null);
            //            Debug.Print("Stake size: {0}, i = {1}", securitiesStake.Count, i);
            //            securitiesStake = new List<Security>();
            //            conCount++;
            //        }
            //    }

            //    asyncResult = func.BeginInvoke(securitiesStake, tradersCollection[conCount], null, null);
            //    Debug.Print("Stake size: {0}", securitiesStake.Count);

            //}
        }

        public int RunStrategy(object obj)
        {
            List<Security> securities = (List<Security>)obj;    //коллекция хранит "пучок", переданных в данный метод, инструментов для закачки таймсерий и передачи их в стратегию          
            IQFeedTrader trader = new IQFeedTrader();

            while (run)
            {
                trader.Connect();
                Thread.Sleep(500);  //уснуть на 500 миллисекунд для того, чтобы дать коннектору установить подключение 

                foreach (var security in securities)
                {
                    var startSession = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 19, 9, 30, 00);  //начало торговой сессии 
                    var endSession = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 19, 15, 55, 00);   //окончание торговой сессии 

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

                    strategy = new LevelsStrategy()  //создаем экземпляр стратегии с определенными параметрами
                    {
                        Security = security,
                        Portfolio = new Portfolio(),
                        IntradayCandles = intradayCandles,
                        DayCandles = dayCandles,
                        DisposeOnStop = true,
                    };
                    strategy.RegistrationOrder += Strategy_RegistrationOrder;
                    strategy.Processed += Strategy_Processed;
                    strategy.Start();   //запускаем стратегию на выполнение
                }
            }

            return 0;
        }

        //на данное событие подписываются внешние обработчики. Событие наступает после анализа и обработки инструмента
        public event Action<int, int> Processed;

        private void Strategy_Processed(int a, int b)
        {
            Processed(a, b);   
        }

        private void Strategy_RegistrationOrder()
        {
            //logManager.Sources.Add(strategy); //при открытии позиции, регистрируем данную стратегию в менеджере логирования, чтобы следить за её работой
        }
    }
}
