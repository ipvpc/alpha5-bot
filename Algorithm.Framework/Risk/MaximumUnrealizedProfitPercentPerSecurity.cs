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
using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Portfolio;

namespace QuantConnect.Algorithm.Framework.Risk
{
    /// <summary>
    /// Provides an implementation of <see cref="IRiskManagementModel"/> that limits the unrealized profit
    /// per holding to the specified percentage
    /// </summary>
    public class MaximumUnrealizedProfitPercentPerSecurity : RiskManagementModel
    {
        private readonly decimal _maximumUnrealizedProfitPercent;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaximumUnrealizedProfitPercentPerSecurity"/> class
        /// </summary>
        /// <param name="maximumUnrealizedProfitPercent">The maximum percentage unrealized profit allowed for any single security holding,
        /// defaults to 5% drawdown per security</param>
        public MaximumUnrealizedProfitPercentPerSecurity(
            decimal maximumUnrealizedProfitPercent = 0.05m
            )
        {
            _maximumUnrealizedProfitPercent = Math.Abs(maximumUnrealizedProfitPercent);
        }

        /// <summary>
        /// Manages the algorithm's risk at each time step
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The current portfolio targets to be assessed for risk</param>
        public override IEnumerable<IPortfolioTarget> ManageRisk(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            foreach (var kvp in algorithm.Securities)
            {
                var security = kvp.Value;

                if (!security.Invested)
                {
                    continue;
                }

                var pnl = security.Holdings.UnrealizedProfitPercent;
                if (pnl > _maximumUnrealizedProfitPercent)
                {
                    // Cancel insights
                    var insights = algorithm.Insights.GetActiveInsights(algorithm.UtcTime);
                    foreach (var insight in insights)
                    {
                        insight.CloseTimeUtc = algorithm.UtcTime.AddSeconds(-1);
                        algorithm.Insights.Remove(insight);
                    }

                    // liquidate
                    yield return new PortfolioTarget(security.Symbol, 0);
                }
            }
        }
    }
}
