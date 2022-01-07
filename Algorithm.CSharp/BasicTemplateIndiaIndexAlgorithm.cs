/*
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
 *
*/

using System;
using QuantConnect.Data;
using System.Collections.Generic;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This example demonstrates how to add index asset types.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="benchmarks" />
    /// <meta name="tag" content="indexes" />
    public class BasicTemplateIndiaIndexAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        protected Symbol Nifty;
        protected Symbol NiftyETF;
        private ExponentialMovingAverage _emaSlow;
        private ExponentialMovingAverage _emaFast;

        /// <summary>
        /// Initialize your algorithm and add desired assets.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2019, 1, 1);
            SetEndDate(2019, 1, 5);
            SetCash(1000000);

            // Use indicator for signal; but it cannot be traded
            Nifty = AddIndex("NIFTY", Resolution.Minute, Market.India).Symbol;

            //Trade Index based ETF
            NiftyETF = AddEquity("NIFTYBEES", Resolution.Minute, Market.India).Symbol;

            //Set Order Prperties as per the requirements for order placement
            DefaultOrderProperties = new IndiaOrderProperties(exchange: Exchange.NSE);

            _emaSlow = EMA(Nifty, 80);
            _emaFast = EMA(Nifty, 200);
        }

        /// <summary>
        /// Index EMA Cross trading underlying.
        /// </summary>
        public override void OnData(Slice slice)
        {
            if (!slice.Bars.ContainsKey(Nifty) || !slice.Bars.ContainsKey(NiftyETF))
            {
                return;
            }

            // Warm up indicators
            if (!_emaSlow.IsReady)
            {
                return;
            }

            if (_emaFast > _emaSlow)
            {
                if (!Portfolio.Invested)
                {
                    var marketTicket = MarketOrder(NiftyETF, 1);
                }
            }
            else
            {
                Liquidate();
            }
        }

        public override void OnEndOfAlgorithm()
        {
            if (Portfolio[Nifty].TotalSaleVolume > 0)
            {
                throw new Exception("Index is not tradable.");
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public virtual bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public virtual Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public virtual Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "4"},
            {"Average Win", "0%"},
            {"Average Loss", "-53.10%"},
            {"Compounding Annual Return", "-92.544%"},
            {"Drawdown", "10.100%"},
            {"Expectancy", "-1"},
            {"Net Profit", "-9.915%"},
            {"Sharpe Ratio", "-3.845"},
            {"Probabilistic Sharpe Ratio", "0.053%"},
            {"Loss Rate", "100%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "-0.558"},
            {"Beta", "0.313"},
            {"Annual Standard Deviation", "0.112"},
            {"Annual Variance", "0.013"},
            {"Information Ratio", "-6.652"},
            {"Tracking Error", "0.125"},
            {"Treynor Ratio", "-1.379"},
            {"Total Fees", "$0.00"},
            {"Estimated Strategy Capacity", "$13000000.00"},
            {"Lowest Capacity Asset", "SPX XL80P3GHDZXQ|SPX 31"},
            {"Fitness Score", "0.039"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "-1.763"},
            {"Return Over Maximum Drawdown", "-9.371"},
            {"Portfolio Turnover", "0.278"},
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
            {"OrderListHash", "0668385036aba3e95127607dfc2f1a59"}
        };
    }
}
