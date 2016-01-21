using StockSharp.Algo.Candles;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Quasar
{
    public class Scanner
    {
        private List<Candle> dayCandles = null;            //таймсерия дневок для анализа объемов и ATR
        private List<Candle> intradayCandles = null;       //таймсерия 5-минуток для выявления формаций
        private decimal sumDaysVolume = 0;                  //сумма объемов за 65 дней
        private decimal todayVolume = 0;                    //сегодняшний объем  
        private decimal sumDaysHigh = 0;                    //сумма максимумов свечек за 65 дней
        private decimal sumDaysLow = 0;                     //сумма минимумов свечек за 65 дней 
        private decimal todayHigh = 0;                      //сегодняшний максимум 
        private decimal todayLow = 1000;                    //сегодняшний минимум
        private decimal aTr = 0;                            //ATR
        private LevelsStrategy strategy;

        public List<Candle> DayCandles
        {
            set
            {
                if (value.Count > 0)
                    dayCandles = value;
            }
        }

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

        public int Scan()
        {
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
                    if (ATRPlay() >= 3m && VolumePlay() >= 1m)
                    {
                        int trend = Trend(30, 40);

                        switch (trend)
                        {
                            case 1:
                                StrategyStart(Sides.Buy);
                                break;
                            case -1:
                                StrategyStart(Sides.Sell);
                                break;
                            default:
                                break;
                        }
                    }
                }

                return 1;
            }

            return 0;
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
            decimal openPrice = intradayCandles[0].OpenPrice;                               //цена открытия сессии
            decimal closePrice = intradayCandles[intradayCandles.Count - 1].ClosePrice;     //текущая цена
            decimal difference = 0;                                                         //разница цены открытия и текущей цены
            decimal netChange = 0;                                                          //NetChange

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

        private void StrategyStart(Sides sides)
        {
            strategy = new LevelsStrategy()  //Создаем экземпляр стратегии с определенными параметрами
            {
                Security = dayCandles[0].Security,
                Portfolio = new Portfolio(),
                Sides = sides,
                DisposeOnStop = true,
            };
            StrategyStarted(strategy);

            strategy.Start();
        }

        public event Action<LevelsStrategy> StrategyStarted;
    }
}
