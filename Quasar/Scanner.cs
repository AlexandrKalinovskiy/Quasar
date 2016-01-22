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
                    IsSmooth();
                    if (ATRPlay() >= 0.5m && VolumePlay() >= 0.8m)
                    {
                        int trend = Trend(30, 40);
                        decimal openPrice = 0;
                        switch (trend)
                        {
                            case 1:
                                openPrice = IsBaseRoundPrice(4, Sides.Buy);
                                if (openPrice > 0)
                                    StrategyStart(Sides.Buy, openPrice);
                                break;
                            case -1:
                                openPrice = IsBaseRoundPrice(4, Sides.Sell);
                                if (openPrice > 0)
                                    StrategyStart(Sides.Sell, openPrice);
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

        private void StrategyStart(Sides sides, decimal price)
        {
            strategy = new LevelsStrategy()  //Создаем экземпляр стратегии с определенными параметрами
            {
                Security = dayCandles[0].Security,
                Portfolio = new Portfolio(),
                Sides = sides,
                Price = price,
                DisposeOnStop = true,
            };
            StrategyStarted(strategy);

            strategy.Start();
        }

        //Метод определяет находится ли база на/под круглым уровнем
        private decimal IsBaseRoundPrice(int count, Sides sides)
        {
            decimal price = 0;
            for (int i = 0; i < count; i++)
            {
                if (sides == Sides.Buy)
                {
                    decimal lowPrice = intradayCandles[intradayCandles.Count - (i + 1)].LowPrice;
                    decimal round = Math.Round(lowPrice, 1);
                    int integer = (int)round;                   //Целая часть
                    decimal fraction = round - integer;         //Дробная часть
                    decimal dif = lowPrice - round;

                    if ((fraction == 0 || fraction == 0.5m) && dif <= 0.5m && dif >= 0) //Если уровень круглый и цена хаев не дальше чем на допустимое количество центов
                    {
                        //Debug.Print("Low {0}, Круглый уровень {1}, low - round = {2} {3}", lowPrice, round, lowPrice - round, dayCandles[0].Security);
                        price = round + 0.02m;
                    }
                    else
                    {                        
                        return 0;   //Если хоть одна свечка не соответствует требованиям, то завершаем выполнение метода с отрицательным результатом
                    }
                }
                else
                {
                    decimal highPrice = intradayCandles[intradayCandles.Count - (i + 1)].HighPrice;
                    decimal round = Math.Round(highPrice, 1);
                    int integer = (int)round;                   //Целая часть
                    decimal fraction = round - integer;         //Дробная часть
                    decimal dif = round - highPrice;

                    if ((fraction == 0 || fraction == 0.5m) && dif <= 0.5m && dif >= 0) //Если уровень круглый и цена хаев не дальше чем на допустимое количество центов
                    {
                        //Debug.Print("High {0}, Круглый уровень {1}, round - high = {2} {3}", highPrice, round, round - highPrice, dayCandles[0].Security);
                        price = round - 0.02m;
                    }
                    else
                    {                       
                        return 0;   //Если хоть одна свечка не соответствует требованиям, то завершаем выполнение метода с отрицательным результатом
                    }

                }              
            }

            return price;
        }

        //Метод проверяет акцию на плавность
        /// <summary>
        /// Алгоритм заключается в следующем: прогоняется один или более дней внутрдиневных свечек и суммируется разница цен закрытия предыдущей свечки с ценой открытия текущей.
        /// Так же считается сумма размеров тел свечек и сумма общих размеров свечек, после чего выводится средний процент.
        /// Путем подбора допустимых значений будет определятся плавность акции. Т.к акции будут примерно с одинаковым ATR, то и сумма должна быть примерна одинакова.
        /// </summary>
        private bool IsSmooth()
        {
            decimal summGaps = 0;
            decimal bodiesSumm = 0;
            decimal hlSumm = 0;
            int percent = 0;

            for (int i = 0; i < intradayCandles.Count - 1; i++)
            {
                summGaps += Math.Abs(intradayCandles[i].ClosePrice - intradayCandles[i + 1].OpenPrice);     //Суммируем ГЕПы
                bodiesSumm += Math.Abs(intradayCandles[i].OpenPrice - intradayCandles[i].ClosePrice);       //Сумма размеров тел свечек
                hlSumm += Math.Abs(intradayCandles[i].HighPrice - intradayCandles[i].LowPrice);             //Сумма размеров свечек
            }

            bodiesSumm += Math.Abs(intradayCandles[intradayCandles.Count - 1].OpenPrice - intradayCandles[intradayCandles.Count - 1].ClosePrice);       //Добавляем последнюю свечку
            hlSumm += Math.Abs(intradayCandles[intradayCandles.Count - 1].HighPrice - intradayCandles[intradayCandles.Count - 1].LowPrice);             //Добавляем последнюю свечку
            
            //Считаем средние значения
            bodiesSumm = bodiesSumm / intradayCandles.Count;    
            hlSumm = hlSumm / intradayCandles.Count;

            //Считаем процент размера тел от размеров свечек
            percent = (int)Math.Round(bodiesSumm * 100 / hlSumm);

            if (summGaps <= 1.3m && percent >= 57)
            {
                Debug.Print("Summ gaps: {0}. Тело равно {1}% от размера свечек {2}", summGaps, percent, intradayCandles[0].Security);
                return true;
            }
                
            return false;
        }

        public event Action<LevelsStrategy> StrategyStarted;
    }
}
