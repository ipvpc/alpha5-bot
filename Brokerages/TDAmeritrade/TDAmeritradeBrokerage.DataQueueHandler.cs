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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using RestSharp;
using TDAmeritradeApi.Client.Models.MarketData;
using TDAmeritradeApi.Client.Models.Streamer;

namespace QuantConnect.Brokerages.TDAmeritrade
{
    /// <summary>
    /// Tradier Class: IDataQueueHandler implementation
    /// </summary>
    public partial class TDAmeritradeBrokerage : IDataQueueHandler
    {
        #region IDataQueueHandler implementation

        private bool _isDataQueueHandlerInitialized;

        private readonly ConcurrentDictionary<string, Symbol> _subscribedTickers = new ConcurrentDictionary<string, Symbol>();

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
            //set once
            tdClient.LiveMarketDataStreamer.MarketData.DataReceived += OnMarketDateReceived;
        }

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return Enumerable.Empty<BaseData>().GetEnumerator();
            }

            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        private static bool CanSubscribe(Symbol symbol)
        {
            return TDAmeritradeBrokerageModel.DefaultMarketMap.ContainsKey(symbol.ID.SecurityType) && !symbol.Value.Contains("universe", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }

        private bool Subscribe(IEnumerable<Symbol> symbols, TickType tickType)
        {
            var symbolsAdded = false;

            foreach (var symbol in symbols)
            {
                if (!symbol.Value.Contains("universe", StringComparison.InvariantCultureIgnoreCase))
                {
                    var ticker = TDAmeritradeToLeanMapper.GetBrokerageSymbol(symbol);
                    if (!_subscribedTickers.ContainsKey(ticker))
                    {
                        _subscribedTickers.TryAdd(ticker, symbol);
                        symbolsAdded = true;
                    }
                }
            }

            if (symbolsAdded)
            {
                SubscribeTo(_subscribedTickers.ToList());
            }

            return true;
        }

        private bool Unsubscribe(IEnumerable<Symbol> symbols, TickType tickType)
        {
            var symbolsRemoved = false;

            var tickers = symbols.Select(symbol => TDAmeritradeToLeanMapper.GetBrokerageSymbol(symbol)).ToList();

            //Remove options too because the symbol will not come through
            foreach (var ticker in tickers.ToArray())
            {
                //add derivative symbols
                tickers.AddRange(_subscribedTickers.Where(kvp => kvp.Key != kvp.Key && kvp.Key.Contains(ticker, StringComparison.InvariantCultureIgnoreCase))
                                                    .Select(kvp => kvp.Key));
            }

            foreach (var ticker in tickers)
            {
                if (_subscribedTickers.ContainsKey(ticker))
                {
                    Symbol removedSymbol;
                    _subscribedTickers.TryRemove(ticker, out removedSymbol);
                    symbolsRemoved = true;
                }
            }

            if (symbolsRemoved)
            {
                tdClient.LiveMarketDataStreamer.UnsubscribeAsync(TDAmeritradeApi.Client.Models.Streamer.MarketDataType.LevelOneQuotes, tickers.ToArray()).Wait();
            }

            return true;
        }

        private void SubscribeTo(List<KeyValuePair<string, Symbol>> brokerageSymbolToLeanSymbolsSubscribeList)
        {
            foreach (var brokerageSymbolToLeanSymbolToSubscribe in brokerageSymbolToLeanSymbolsSubscribeList)
            {
                SecurityType securityType;
                if (brokerageSymbolToLeanSymbolToSubscribe.Value.HasUnderlying && brokerageSymbolToLeanSymbolToSubscribe.Key == brokerageSymbolToLeanSymbolToSubscribe.Value.Underlying.Value)
                    securityType = brokerageSymbolToLeanSymbolToSubscribe.Value.Underlying.SecurityType;
                else
                    securityType = brokerageSymbolToLeanSymbolToSubscribe.Value.SecurityType;

                switch (securityType)
                {
                    case SecurityType.Index:
                    case SecurityType.Equity:
                        tdClient.LiveMarketDataStreamer.SubscribeToLevelOneQuoteDataAsync(QuoteType.Equity, brokerageSymbolToLeanSymbolToSubscribe.Key).Wait();
                        break;
                    case SecurityType.IndexOption:
                    case SecurityType.Option:
                        tdClient.LiveMarketDataStreamer.SubscribeToLevelOneQuoteDataAsync(QuoteType.Option, brokerageSymbolToLeanSymbolToSubscribe.Key).Wait();
                        break;
                    case SecurityType.Forex:
                        tdClient.LiveMarketDataStreamer.SubscribeToLevelOneQuoteDataAsync(QuoteType.Forex, brokerageSymbolToLeanSymbolToSubscribe.Key).Wait();
                        break;
                    case SecurityType.Future:
                        tdClient.LiveMarketDataStreamer.SubscribeToLevelOneQuoteDataAsync(QuoteType.Futures, brokerageSymbolToLeanSymbolToSubscribe.Key).Wait();
                        break;
                    case SecurityType.FutureOption:
                        tdClient.LiveMarketDataStreamer.SubscribeToLevelOneQuoteDataAsync(QuoteType.FuturesOptions, brokerageSymbolToLeanSymbolToSubscribe.Key).Wait();
                        break;
                        //default:
                        //    break;
                }
            }
        }

        private void OnMarketDateReceived(object _, TDAmeritradeApi.Client.Models.Streamer.MarketDataType e)
        {
            if (e == TDAmeritradeApi.Client.Models.Streamer.MarketDataType.LevelOneQuotes)
            {
                var dataDictionary = tdClient.LiveMarketDataStreamer.MarketData[e].ToList()
                    .OrderBy(kvp =>
                    {
                        var item = kvp.Value.IndividualItemType;
                        if (item is EquityLevelOneQuote)
                            return 0;
                        else if (item is OptionLevelOneQuote)
                            return 1;
                        else if (item is FutureMarketQuote)
                            return 2;
                        else if (item is FutureOptionsMarketQuote)
                            return 3;
                        else
                            return 4;
                    }).ToList();

                foreach (var item in dataDictionary)
                {
                    string brokerageSymbol = item.Key;
                    var data = item.Value;

                    AddTickData(data);
                }
            }
        }

        private void AddTickData(StoredData data)
        {
            ConcurrentQueue<LevelOneQuote> queue = data.Data;
            while (queue.TryDequeue(out LevelOneQuote quote))
            {
                if (quote.HasQuotes)
                {
                    var tick = GetQuote(quote);

                    if (tick != null)
                    {
                        _aggregator.Update(tick);
                    }
                }

                if (quote.HasTrades)
                {
                    var tick = GetTrade(quote);

                    if (tick != null)
                    {
                        _aggregator.Update(tick);
                    }
                }

                if( quote is OptionLevelOneQuote optionQuote)
                {
                    var openInterest = GetOpenInterest(optionQuote);

                    if (openInterest != null)
                    {
                        _aggregator.Update(openInterest);
                    }
                }
            }
        }

        private OpenInterest GetOpenInterest(OptionLevelOneQuote optionQuote)
        {
            Symbol symbol;
            if (!_subscribedTickers.TryGetValue(optionQuote.Symbol, out symbol))
            {
                // Not subscribed to this symbol.
                return null;
            }

            // Tradier trades are US NY time only. Convert local server time to NY Time:
            var utc = optionQuote.QuoteTime;

            // Convert the timestamp to exchange timezone and pass into algorithm
            var time = utc.DateTime.ConvertTo(DateTimeZone.Utc, TimeZones.NewYork);

            return new OpenInterest(time, symbol, optionQuote.OpenInterest);
        }

        /// <summary>
        /// Get quote from td stream
        /// </summary>
        /// <param name="marketQuote">TD stream data object</param>
        /// <returns>LEAN Tick object</returns>
        private Tick GetQuote(LevelOneQuote marketQuote)
        {
            Symbol symbol;
            if (!_subscribedTickers.TryGetValue(marketQuote.Symbol, out symbol))
            {
                // Not subscribed to this symbol.
                return null;
            }

            // Tradier trades are US NY time only. Convert local server time to NY Time:
            var utc = marketQuote.QuoteTime;

            // Convert the timestamp to exchange timezone and pass into algorithm
            var time = utc.DateTime.ConvertTo(DateTimeZone.Utc, TimeZones.NewYork);

            return new Tick(time, symbol, string.Empty, marketQuote.PrimaryListingExchangeName, (decimal)marketQuote.BidSize, (decimal)marketQuote.BidPrice, (decimal)marketQuote.AskSize, (decimal)marketQuote.AskPrice);
        }

        /// <summary>
        /// Get quote from td stream
        /// </summary>
        /// <param name="marketQuote">TD stream data object</param>
        /// <returns>LEAN Tick object</returns>
        private Tick GetTrade(LevelOneQuote marketQuote)
        {
            Symbol symbol;
            if (!_subscribedTickers.TryGetValue(marketQuote.Symbol, out symbol))
            {
                // Not subscribed to this symbol.
                return null;
            }

            // Tradier trades are US NY time only. Convert local server time to NY Time:
            var utc = marketQuote.TradeTime;

            // Convert the timestamp to exchange timezone and pass into algorithm
            var time = utc.DateTime.ConvertTo(DateTimeZone.Utc, TimeZones.NewYork);

            return new Tick(time, symbol, string.Empty, marketQuote.LastTradeExchange, (decimal)marketQuote.LastSize, (decimal)marketQuote.LastPrice);
        }

        #endregion
    }
}
