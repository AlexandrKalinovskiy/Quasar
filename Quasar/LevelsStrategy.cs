using StockSharp.Algo;
using StockSharp.Algo.Candles;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using System;
using System.Collections.Generic;

namespace Quasar
{
    public class LevelsStrategy : Strategy
    {
        private Sides sides;    //Направление ордера
        private decimal price;  //Цена ордера

        public Sides Sides
        {
            set
            {
                sides = value;
            }
        }

        public decimal Price
        {
            set
            {
                price = value;
            }
        }

        protected override void OnStarted()
        {
            var order = new Order
            {
                Type = OrderTypes.Limit,
                Portfolio = Portfolio,
                Volume = 100,
                Price = price,
                Security = Security,
                Direction = sides
            };

            Connector.RegisterOrder(order);

            base.OnStarted();
        }     
    }
}
