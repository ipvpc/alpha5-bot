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

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Test algorithm using <see cref="QCAlgorithm.AddAlphaModel(IAlphaModel)"/>
    /// </summary>
    public class AddAlphaModelAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _spy;
        private Symbol _fb;
        private Symbol _ibm;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            UniverseSettings.Resolution = Resolution.Daily;

            _spy = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);
            _fb = QuantConnect.Symbol.Create("FB", SecurityType.Equity, Market.USA);
            _ibm = QuantConnect.Symbol.Create("IBM", SecurityType.Equity, Market.USA);
            SetUniverseSelection(new ManualUniverseSelectionModel(_spy, _fb, _ibm));
            SetPortfolioConstruction(new EqualWeightingPortfolioConstructionModel());
            SetExecution(new ImmediateExecutionModel());

            AddAlpha(new OneTimeAlphaModel(_spy));
            AddAlpha(new OneTimeAlphaModel(_fb));
            AddAlpha(new OneTimeAlphaModel(_ibm));

            InsightsGenerated += OnInsightsGeneratedVerifier;
        }

        private void OnInsightsGeneratedVerifier(IAlgorithm algorithm,
            GeneratedInsightsCollection insightsCollection)
        {
            if (insightsCollection.Insights.Count(insight => insight.Symbol == _fb) != 1
                || insightsCollection.Insights.Count(insight => insight.Symbol == _spy) != 1
                || insightsCollection.Insights.Count(insight => insight.Symbol == _ibm) != 1)
            {
                throw new Exception("Unexpected insights were emitted");
            }
        }

        private class OneTimeAlphaModel : AlphaModel
        {
            private readonly Symbol _symbol;
            private bool _triggered;

            public OneTimeAlphaModel(Symbol symbol)
            {
                _symbol = symbol;
            }

            public override IEnumerable<Insight> Update(QCAlgorithm algorithm, Slice data)
            {
                if (!_triggered)
                {
                    _triggered = true;
                    yield return Insight.Price(
                        _symbol,
                        Resolution.Daily,
                        1,
                        InsightDirection.Down
                        );
                }
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
            {"Total Trades", "9"},
            {"Average Win", "0.89%"},
            {"Average Loss", "-0.27%"},
            {"Compounding Annual Return", "196.304%"},
            {"Drawdown", "1.700%"},
            {"Expectancy", "1.854"},
            {"Net Profit", "1.499%"},
            {"Sharpe Ratio", "4.265"},
            {"Probabilistic Sharpe Ratio", "60.408%"},
            {"Loss Rate", "33%"},
            {"Win Rate", "67%"},
            {"Profit-Loss Ratio", "3.28"},
            {"Alpha", "1.579"},
            {"Beta", "-0.284"},
            {"Annual Standard Deviation", "0.277"},
            {"Annual Variance", "0.077"},
            {"Information Ratio", "-0.586"},
            {"Tracking Error", "0.369"},
            {"Treynor Ratio", "-4.159"},
            {"Total Fees", "$14.46"},
            {"Estimated Strategy Capacity", "$45000000.00"},
            {"Lowest Capacity Asset", "IBM R735QTJ8XC9X"},
            {"Fitness Score", "0.408"},
            {"Kelly Criterion Estimate", "16.438"},
            {"Kelly Criterion Probability Value", "0.314"},
            {"Sortino Ratio", "13.495"},
            {"Return Over Maximum Drawdown", "117.2"},
            {"Portfolio Turnover", "0.411"},
            {"Total Insights Generated", "3"},
            {"Total Insights Closed", "3"},
            {"Total Insights Analysis Completed", "3"},
            {"Long Insight Count", "0"},
            {"Short Insight Count", "3"},
            {"Long/Short Ratio", "0%"},
            {"Estimated Monthly Alpha Value", "$20692960.6599"},
            {"Total Accumulated Estimated Alpha Value", "$3563787.6692"},
            {"Mean Population Estimated Insight Value", "$1187929.2231"},
            {"Mean Population Direction", "100%"},
            {"Mean Population Magnitude", "0%"},
            {"Rolling Averaged Population Direction", "100%"},
            {"Rolling Averaged Population Magnitude", "0%"},
            {"OrderListHash", "4e0e07a4b92e6d23d681220125617e62"}
        };
    }
}
