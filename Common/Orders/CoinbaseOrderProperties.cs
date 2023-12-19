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

using QuantConnect.Interfaces;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Contains additional properties and settings for an order submitted to Coinbase brokerage
    /// </summary>
    public class CoinbaseOrderProperties : OrderProperties
    {
        /// <summary>
        /// This flag will ensure the order executes only as a maker (no fee) order.
        /// If part of the order results in taking liquidity rather than providing,
        /// it will be rejected and no part of the order will execute.
        /// Note: this flag is only applied to Limit orders.
        /// </summary>
        public bool PostOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether self-trade prevention is enabled for this order.
        /// Self-trade prevention helps prevent an order from crossing against the same user, 
        /// reducing the risk of unintentional trades within the same account.
        /// </summary>
        public bool SelfTradePreventionId { get; set; }

        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>
        public override IOrderProperties Clone()
        {
            return (CoinbaseOrderProperties)MemberwiseClone();
        }
    }
}
