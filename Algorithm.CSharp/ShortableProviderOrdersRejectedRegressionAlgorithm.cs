

using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class ShortableProviderOrdersRejectedRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _spy;
        private Symbol _bac;
        private List<OrderTicket> _ordersAllowed = new List<OrderTicket>();
        private List<OrderTicket> _ordersDenied = new List<OrderTicket>();
        private bool _initialize;
        private bool _invalidatedAllowedOrder;
        private bool _invalidatedNewOrderWithPortfolioHoldings;

        public override void Initialize()
        {
            SetStartDate(2013, 10, 4);
            SetEndDate(2013, 10, 11);
            SetCash(100000);

            _spy = AddEquity("SPY", Resolution.Minute).Symbol;
            _bac = AddEquity("BAC", Resolution.Minute).Symbol;

            SetShortableProvider(new RegressionTestShortableProvider());
        }

        public override void OnData(Slice data)
        {
            if (!_initialize)
            {
                HandleOrder(LimitOrder(_spy, -1001, 10000m)); // Should be canceled, exceeds the max shortable quantity
                HandleOrder(LimitOrder(_spy, -1000, 10000m)); // Allowed, orders at or below 1000 should be accepted
                HandleOrder(LimitOrder(_spy, -10, 0.01m)); // Should be canceled, the total quantity we would be short would exceed the max shortable quantity.
                _initialize = true;
                return;
            }

            if (!_invalidatedAllowedOrder)
            {
                if (_ordersAllowed.Count != 1)
                {
                    throw new Exception($"Expected 1 successful order, found: {_ordersAllowed.Count}");
                }
                if (_ordersDenied.Count != 2)
                {
                    throw new Exception($"Expected 2 failed orders, found: {_ordersDenied.Count}");
                }

                var allowedOrder = _ordersAllowed[0];
                var orderUpdate = new UpdateOrderFields()
                {
                    Quantity = -1001,
                    Tag = "Testing updating and exceeding maximum quantity"
                };

                var response = allowedOrder.Update(orderUpdate);
                if (response.ErrorCode != OrderResponseErrorCode.ExceedsShortableQuantity)
                {
                    throw new Exception($"Expected order to fail due to exceeded shortable quantity, found: {response.ErrorCode.ToString()}");
                }

                var cancelResponse = allowedOrder.Cancel();
                if (cancelResponse.IsError)
                {
                    throw new Exception("Expected to be able to cancel open order after bad qty update");
                }

                _invalidatedAllowedOrder = true;
                _ordersDenied.Clear();
                _ordersAllowed.Clear();
                return;
            }

            if (!_invalidatedNewOrderWithPortfolioHoldings)
            {
                HandleOrder(MarketOrder(_spy, -1000)); // Should succeed, no holdings and no open orders to stop this
                var spyShares = Portfolio[_spy].Quantity;
                if (spyShares != -1000m)
                {
                    throw new Exception($"Expected -1000 shares in portfolio, found: {spyShares}");
                }

                HandleOrder(LimitOrder(_spy, -1, 0.01m)); // Should fail, portfolio holdings are at the max shortable quantity.
                if (_ordersDenied.Count != 1)
                {
                    throw new Exception($"Expected limit order to fail due to existing holdings, but found {_ordersDenied.Count} failures");
                }

                _ordersAllowed.Clear();
                _ordersDenied.Clear();

                HandleOrder(MarketOrder(_bac, -10000));
                if (_ordersAllowed.Count != 1)
                {
                    throw new Exception($"Expected market order of -10000 BAC to not fail, but failed with tag: {_ordersDenied[0].Tag}");
                }

                _invalidatedNewOrderWithPortfolioHoldings = true;
            }
        }

        private void HandleOrder(OrderTicket orderTicket)
        {
            if (orderTicket.SubmitRequest.Status == OrderRequestStatus.Error)
            {
                _ordersDenied.Add(orderTicket);
                return;
            }

            _ordersAllowed.Add(orderTicket);
        }

        public class RegressionTestShortableProvider : IShortableProvider
        {
            public Dictionary<Symbol, long> AllShortableSymbols(DateTime localTime)
            {
                return new Dictionary<Symbol, long>
                {
                    { QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA), 1000 }
                };
            }

            public long? ShortableQuantity(Symbol symbol, DateTime localTime)
            {
                return 1000;
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "8"},
            {"Average Win", "0%"},
            {"Average Loss", "-0.01%"},
            {"Compounding Annual Return", "362.291%"},
            {"Drawdown", "2.300%"},
            {"Expectancy", "-1"},
            {"Net Profit", "1.977%"},
            {"Sharpe Ratio", "12.257"},
            {"Probabilistic Sharpe Ratio", "65.989%"},
            {"Loss Rate", "100%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "1.061"},
            {"Beta", "1"},
            {"Annual Standard Deviation", "0.244"},
            {"Annual Variance", "0.059"},
            {"Information Ratio", "10.062"},
            {"Tracking Error", "0.105"},
            {"Treynor Ratio", "2.987"},
            {"Total Fees", "$11.48"},
            {"Fitness Score", "0.549"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "18.728"},
            {"Return Over Maximum Drawdown", "125.812"},
            {"Portfolio Turnover", "0.551"},
            {"Total Insights Generated", "0"},
            {"Total Insights Closed", "0"},
            {"Total Insights Analysis Completed", "0"},
            {"Long Insight Count", "0"},
            {"Short Insight Count", "0"},
            {"Long/Short Ratio", "100%"},
            {"Estimated Monthly Alpha Value", "$0"},
            {"Total Accumulated Estimated Alpha Value", "$0"},
            {"Mean Population Estimated Insight Value", "$0"},
            {"Mean Population Direction", "0%"},
            {"Mean Population Magnitude", "0%"},
            {"Rolling Averaged Population Direction", "0%"},
            {"Rolling Averaged Population Magnitude", "0%"},
            {"OrderListHash", "-1052079344"}
        };
    }
}
