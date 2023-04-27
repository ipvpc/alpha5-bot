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

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Future holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class FutureHolding : SecurityHolding
    {
        /// <summary>
        /// The current settlement price
        /// </summary>
        public decimal SettlementPrice { get; set; }

        /// <summary>
        /// The cash settled profit for the current open position
        /// </summary>
        public virtual decimal SettledProfit { get; set; }

        /// <summary>
        /// Unsettled profit for the current open position <see cref="SettledProfit"/>
        /// </summary>
        public virtual decimal UnsettledProfit
        {
            get
            {
                return TotalCloseProfit() - SettledProfit;
            }
        }

        /// <summary>
        /// Future Holding Class constructor
        /// </summary>
        /// <param name="security">The future security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public FutureHolding(Security security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }

        /// <summary>
        /// Update local copy of closing price value.
        /// </summary>
        /// <param name="closingPrice">Price of the underlying asset to be used for calculating market price / portfolio value</param>
        public override void UpdateMarketPrice(decimal closingPrice)
        {
            base.UpdateMarketPrice(closingPrice);

            // simple settlement price logic, we just use the latest price. This could potentially be replaced by a settlement price algorithm or data feed
            SettlementPrice = closingPrice;
        }
    }
}
