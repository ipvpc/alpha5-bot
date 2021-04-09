﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using QuantConnect.Orders;
using QuantConnect.Securities;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using com.sun.security.ntlm;
using CryptoExchange.Net.Objects;
using Exante.Net;
using Exante.Net.Enums;
using Exante.Net.Objects;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Exante
{
    public class ExanteBrokerage : Brokerage, IDataQueueHandler
    {
        private bool _isConnected;
        private readonly ExanteClientWrapper _client;
        private string _accountId;

        public ExanteBrokerage(
            ExanteClient client,
            string accountId
            )
            : base("Exante Brokerage")
        {
            _client = new ExanteClientWrapper(client);
            _accountId = accountId;
        }

        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            throw new NotImplementedException();
        }

        public void SetJob(LiveNodePacket job)
        {
            throw new NotImplementedException();
        }

        public override bool IsConnected => _isConnected;

        public override List<Order> GetOpenOrders()
        {
            throw new NotImplementedException();
        }

        public override List<Holding> GetAccountHoldings()
        {
            throw new NotImplementedException();
        }

        public override List<CashAmount> GetCashBalance()
        {
            const string reportCurrency = "USD";
            var accountSummary = _client.GetAccountSummary(_accountId, reportCurrency);
            var cashAmounts =
                from currencyData in accountSummary.Currencies
                select new CashAmount(currencyData.Value, currencyData.Currency);
            return cashAmounts.ToList();
        }

        public override bool PlaceOrder(Order order)
        {
            var orderSide = default(ExanteOrderSide);
            switch (order.Direction)
            {
                case OrderDirection.Buy:
                    orderSide = ExanteOrderSide.Buy;
                    break;
                case OrderDirection.Sell:
                    orderSide = ExanteOrderSide.Sell;
                    break;
                case OrderDirection.Hold:
                    throw new NotSupportedException(
                        $"ExanteBrokerage.ConvertOrderDirection: Unsupported order direction: {order.Direction}");
            }

            IEnumerable<ExanteOrder> orderPlacement;
            switch (order.Type)
            {
                case OrderType.Market:
                    orderPlacement = _client.PlaceOrder(
                        _accountId,
                        order.Symbol.ID.Symbol,
                        ExanteOrderType.Market,
                        orderSide,
                        order.Quantity,
                        ExanteOrderDuration.AtTheClose
                    );
                    break;

                default:
                    throw new NotSupportedException(
                        $"ExanteBrokerage.ConvertOrderType: Unsupported order type: {order.Type}");
            }

            var isPlaced = orderPlacement.ToList()[0].OrderState.Status != ExanteOrderStatus.Cancelled;
            return isPlaced;
        }

        public override bool UpdateOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public override bool CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public override void Connect()
        {
            _isConnected = true;
        }

        public override void Disconnect()
        {
            _isConnected = false;
        }
    }
}
