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

using Newtonsoft.Json.Linq;
using QuantConnect.Logging;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.ToolBox.BitfinexDownloader
{
    /// <summary>
    /// Bitfinex implementation of <see cref="IExchangeInfoDownloader"/>
    /// </summary>
    public class BitfinexExchangeInfoDownloader : IExchangeInfoDownloader
    {
        /// <summary>
        /// Market name
        /// </summary>
        public string Market => QuantConnect.Market.Bitfinex;

        /// <summary>
        /// Pulling data from a remote source
        /// </summary>
        /// <returns>Enumerable of exchange info</returns>
        public IEnumerable<string> Get()
        {
            const string tradingPairsUrl = "https://api-pub.bitfinex.com/v2/conf/pub:list:pair:exchange";
            const string currenciesUrl = "https://api-pub.bitfinex.com/v2/conf/pub:list:currency";
            const string pairInfosUrl = "https://api-pub.bitfinex.com/v2/conf/pub:info:pair";
            const string pairLabelUrl = "https://api-pub.bitfinex.com/v2/conf/pub:map:currency:label";
            Dictionary<string, string> headers = new() { { "User-Agent", ".NET Framework Test Client" } };

            // Fetch trading pairs
            var json = tradingPairsUrl.DownloadData(headers);
            var tradingPairs = JToken.Parse(json).First.ToObject<List<string>>();

            // Fetch currencies
            json = currenciesUrl.DownloadData(headers);
            var currencies = JToken.Parse(json).First.ToObject<List<string>>();

            // Fetch pair info
            Dictionary<string, List<string>> pairsInfo = new();
            json = pairInfosUrl.DownloadData(headers);
            var jObject = JToken.Parse(json);
            foreach (var kvp in jObject.First)
            {
                pairsInfo[kvp[0].ToString()] = kvp[1].ToObject<List<string>>();
            }

            // Fetch trading label
            Dictionary<string, string> currencyLabel = new();
            json = pairLabelUrl.DownloadData(headers);
            jObject = JToken.Parse(json);
            foreach (var kvp in jObject.First)
            {
                currencyLabel[kvp[0].ToString()] = kvp[1].ToString();
            }

            foreach (var tradingPair in tradingPairs)
            {
                // market,symbol,type,description,quote_currency,contract_multiplier,minimum_price_variation,lot_size,market_ticker,minimum_order_size
                var symbol = tradingPair.Replace(":", string.Empty);
                var quoteCurrency = currencies.Where(x => tradingPair.EndsWith(x)).OrderByDescending(s => s.Length).First();
                var baseCurrency = symbol.RemoveFromEnd(quoteCurrency);
                if (!currencyLabel.TryGetValue(quoteCurrency, out string quoteLabel))
                {
                    Log.Trace($"BitfinexExchangeInfoDownloader.Get(): missing label value for quote currency {quoteCurrency}");
                    continue;
                }
                if (!currencyLabel.TryGetValue(baseCurrency, out string baseLabel))
                {
                    Log.Trace($"BitfinexExchangeInfoDownloader.Get(): missing label value for base currency {baseCurrency}");
                    continue;
                }
                var description = $"{baseLabel}-{quoteLabel}";
                var contractMultiplier = 1;
                var minimum_price_variation = "missing";
                var lot_size = "missing";
                var marketTicker = "t" + tradingPair;
                var minimum_order_size = pairsInfo[tradingPair][3];
                yield return $"{Market},{symbol},crypto,{description},{quoteCurrency},{contractMultiplier},{minimum_price_variation},{lot_size},{marketTicker},{minimum_order_size}";
            }
        }
    }
}
