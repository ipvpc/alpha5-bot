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
*/

using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using System;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Demonstration of payments for cash dividends in backtesting. When data normalization mode is set
    /// to "Raw" the dividends are paid as cash directly into your portfolio.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="data event handlers" />
    /// <meta name="tag" content="dividend event" />
    public class DividendRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _symbol;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(1998, 01, 01);  //Set Start Date
            SetEndDate(2006, 01, 01);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            // Find more symbols here: http://quantconnect.com/data
            _symbol = AddEquity("SPY", Resolution.Daily,
                dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            if (Portfolio.Invested) return;
            SetHoldings(_symbol, .5);
        }

        /// <summary>
        /// Raises the data event.
        /// </summary>
        /// <param name="data">Data.</param>
        public void OnData(Dividends data) // update this to Dividends dictionary
        {
            var dividend = data[_symbol];
            Debug($"{dividend.Time.ToStringInvariant("o")} >> DIVIDEND >> {dividend.Symbol} - " +
                $"{dividend.Distribution.ToStringInvariant("C")} - {Portfolio.Cash} - " +
                $"{Portfolio[_symbol].Price.ToStringInvariant("C")}"
            );
        }
        
        public override void OnEndOfAlgorithm()
        {
            // The expected value refers to dividend payments
            const decimal expected = 6789.12m;
            if (Portfolio.TotalProfit != expected)
            {
                throw new Exception($"Total Profit: Expected {expected}. Actual {Portfolio.TotalProfit}");
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp };

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public long DataPoints => 16077;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 0;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new()
        {
            { "Total Trades", "1" },
            { "Average Win", "0%" },
            { "Average Loss", "0%" },
            { "Compounding Annual Return", "2.354%" },
            { "Drawdown", "28.200%" },
            { "Expectancy", "0" },
            { "Net Profit", "20.462%" },
            { "Sharpe Ratio", "0.238" },
            { "Probabilistic Sharpe Ratio", "0.462%" },
            { "Loss Rate", "0%" },
            { "Win Rate", "0%" },
            { "Profit-Loss Ratio", "0" },
            { "Alpha", "-0.004" },
            { "Beta", "0.521" },
            { "Annual Standard Deviation", "0.083" },
            { "Annual Variance", "0.007" },
            { "Information Ratio", "-0.328" },
            { "Tracking Error", "0.076" },
            { "Treynor Ratio", "0.038" },
            { "Total Fees", "$2.56" },
            { "Estimated Strategy Capacity", "$24000000.00" },
            { "Lowest Capacity Asset", "SPY R735QTJ8XC9X" },
            { "Fitness Score", "0" },
            { "Kelly Criterion Estimate", "0" },
            { "Kelly Criterion Probability Value", "0" },
            { "Sortino Ratio", "0.355" },
            { "Return Over Maximum Drawdown", "0.083" },
            { "Portfolio Turnover", "0" },
            { "Total Insights Generated", "0" },
            { "Total Insights Closed", "0" },
            { "Total Insights Analysis Completed", "0" },
            { "Long Insight Count", "0" },
            { "Short Insight Count", "0" },
            { "Long/Short Ratio", "100%" },
            { "Estimated Monthly Alpha Value", "$0" },
            { "Total Accumulated Estimated Alpha Value", "$0" },
            { "Mean Population Estimated Insight Value", "$0" },
            { "Mean Population Direction", "0%" },
            { "Mean Population Magnitude", "0%" },
            { "Rolling Averaged Population Direction", "0%" },
            { "Rolling Averaged Population Magnitude", "0%" },
            { "OrderListHash", "e60d1af5917a9a4d7b41197ce665b296" }
        };
    }
}
