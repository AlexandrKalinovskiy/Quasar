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
        private Sides sides;    //Направление заявки

        public Sides Sides
        {
            set
            {
                sides = value;
            }
        }

        protected override void OnStarted()
        {
            Order order = new Order()
            {
                Security = Security,
                Price = 10,
                Volume = 100,
                Type = OrderTypes.Limit,
                Direction = sides

            };

            RegisterOrder(order);

            base.OnStarted();
        }     
    }
}
