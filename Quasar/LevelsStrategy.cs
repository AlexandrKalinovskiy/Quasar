using StockSharp.Algo;
using StockSharp.Algo.Candles;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Quasar
{
    public class LevelsStrategy : Strategy
    {
        private List<Candle> intradayCandles = null;       //таймсерия 5-минуток для выявления формаций
        private List<Candle> dayCandles = null;            //таймсерия дневок для анализа объемов и ATR
        decimal sumDaysVolume = 0;                  //сумма объемов за 65 дней
        decimal todayVolume = 0;                    //сегодняшний объем  
        decimal sumDaysHigh = 0;                    //сумма максимумов свечек за 65 дней
        decimal sumDaysLow = 0;                     //сумма минимумов свечек за 65 дней 
        decimal todayHigh = 0;                      //сегодняшний максимум 
        decimal todayLow = 1000;                    //сегодняшний минимум
        decimal aTr = 0;                            //ATR

        public event Action RegistrationOrder;      //на данное событие подписываются внешние обработчики. Событие наступает при выполненнии всех условий стратегии и перед регистрацией заявки на бирже
        public event Action<int, int> Processed;   //собыитие принимает 2 апараметра <Количество успешно обработанных инструментов, количество необработанных инструментов>

        public List<Candle> IntradayCandles
        {
            set
            {
                if (value.Count > 0)    //Если есть загруженные свечки
                {
                    if (value[value.Count - 1].ClosePrice < 100)    //и их цена ниже 100$
                    {
                        intradayCandles = value;
                    }
                }
            }
        }

        public List<Candle> DayCandles
        {
            set
            {
                if (value.Count > 0)
                    dayCandles = value;
            }
        }

        protected override void OnStarted()
        {
            base.OnStarted();
            if (intradayCandles != null && dayCandles != null)
            {
                foreach (var dayCandle in dayCandles)
                {
                    sumDaysVolume += dayCandle.TotalVolume;
                    sumDaysLow += dayCandle.LowPrice;
                    sumDaysHigh += dayCandle.HighPrice;
                }

                int count = 0;
                foreach (var candle in intradayCandles)
                {
                    todayVolume += candle.TotalVolume;

                    if (candle.HighPrice > todayHigh)
                        todayHigh = candle.HighPrice;

                    if (candle.LowPrice < todayLow)
                        todayLow = candle.LowPrice;
                    count++;
                }

                aTr = ATR();

                if (aTr >= 1m && aTr <= 2m)
                {
                    if (ATRPlay() > 1m && VolumePlay() >= 1m)
                    {
                        int trend = Trend(30, 40);
                        switch (trend)
                        {
                            case 1:
                                SendOrder(Sides.Buy);
                                break;
                            case -1:
                                SendOrder(Sides.Sell);
                                break;
                            default:
                                Stop(); //если условия стратегии не выполняются, то данный экземпляр останавливаем
                                break;
                        }
                    }
                    else
                    {
                        Stop(); //если условия стратегии не выполняются, то данный экземпляр останавливаем
                    }
                }
                else if (ProcessState != ProcessStates.Stopped)
                {
                    Stop(); //если условия стратегии не выполняются, то данный экземпляр останавливаем
                }

                Processed(1, 0);    //инициируем событие обработки инструмента <1 - количество успешно обработанных инструментов, 0 - количество необработанных инструментов>                
            }
            else
            {
                Processed(0, 1);
            }
        }

        //метод возвращает текущий коэффициент объема
        private decimal VolumePlay()
        {
            return Math.Round(todayVolume / (sumDaysVolume / 65), 1);   //коэффициент объема
        }

        //метод возвращает ATR
        private decimal ATR()
        {
            return Math.Round(sumDaysHigh / 65 - sumDaysLow / 65, 2);       //считаем ATR за 65 дней
        }

        //метод возвращает коэффициент ATR
        private decimal ATRPlay()
        {
            return Math.Round((todayHigh - todayLow) / aTr, 1);         //возвращаем значение коэффициента ATR
        }

        //метод определяет направления тренда сегодня
        private int Trend(int allowableDifference, int allowableNetChange)
        {
            decimal allowableDifferenceLocal = allowableDifference / 100m;
            decimal allowableNetChangeLocal = allowableNetChange / 100m;
            decimal openPrice = intradayCandles[0].OpenPrice;                       //цена открытия сессии
            decimal closePrice = intradayCandles[intradayCandles.Count - 1].ClosePrice;     //текущая цена
            decimal difference = 0;//Math.Abs(openPrice - closePrice);          //разница цены открытия и текущей цены
            decimal netChange = 0;                                          //NetChange

            if (openPrice < closePrice)
            {
                netChange = todayHigh - openPrice;
                difference = closePrice - openPrice;
                if (difference >= allowableDifferenceLocal && netChange >= allowableNetChangeLocal)
                {
                    return 1;
                }
            }

            if (openPrice > closePrice)
            {
                netChange = openPrice - todayLow;
                difference = openPrice - closePrice;
                if (difference >= allowableDifferenceLocal && netChange >= allowableNetChangeLocal)
                {
                    return -1;
                }
            }

            return 0;
        }

        //метод регистрирует ордер на бирже
        private bool SendOrder(Sides orderSide)
        {
            RegistrationOrder();

            Order order = new Order()
            {
                Security = Security,
                Price = intradayCandles[intradayCandles.Count - 1].ClosePrice,
                Volume = 100,
                Type = OrderTypes.Limit,
                Direction = orderSide

            };
            RegisterOrder(order);

            return true;
        }
    }
}
