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

using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Configuration;
using QuantConnect.Packets;
using QuantConnect.Logging;
using Newtonsoft.Json.Linq;
using QuantConnect.Data.Market;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using QuantConnect.Interfaces;
using NodaTime;
using System.Globalization;
using System.IO;
using static QuantConnect.StringExtensions;
using QuantConnect.Util;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using QuantConnect.ToolBox.IEX.Response;

namespace QuantConnect.ToolBox.IEX
{
    /// <summary>
    /// IEX live data handler.
    /// See more at https://iexcloud.io/docs/api/
    /// </summary>
    public class IEXDataQueueHandler : HistoryProviderBase, IDataQueueHandler
    {
        private static readonly TimeSpan SubscribeDelay = TimeSpan.FromMilliseconds(1500);
        private readonly IEXEventSourceCollection _clients;
        private readonly ManualResetEvent _refreshEvent = new ManualResetEvent(false);

        private readonly ConcurrentDictionary<string, Symbol> _symbols = new ConcurrentDictionary<string, Symbol>(StringComparer.InvariantCultureIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _tickLastTradeTime = new ConcurrentDictionary<string, long>();

        // only required for history requests to IEX Cloud
        private readonly string _apiKey = Config.Get("iex-cloud-api-key");

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified);
        private int _dataPointCount;
        private bool _isDisposing;

        private readonly IDataAggregator _aggregator = Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(
            Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"));
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        public bool IsConnected => _clients.IsConnected;


        /// <summary>
        /// Initializes a new instance of the <see cref="IEXDataQueueHandler"/> class.
        /// </summary>
        public IEXDataQueueHandler()
        {
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();

            _subscriptionManager.SubscribeImpl += (symbols, t) =>
            {
                symbols.DoForEach(symbol =>
                {
                    if (!_symbols.TryAdd(symbol.Value, symbol))
                    {
                        throw new Exception($"Invalid logic, SubscriptionManager tries to subscribe to existing symbol : {symbol.Value}");
                    }
                });

                Refresh();
                return true;
            };

            _subscriptionManager.UnsubscribeImpl += (symbols, t) =>
            {
                symbols.DoForEach(symbol =>
                {
                    Symbol tmp;
                    _symbols.TryRemove(symbol.Value, out tmp);
                });

                Refresh();
                return true;
            };

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new Exception("The IEX API key was not provided.");
            }

            // Set the sse-clients collection
            _clients = new IEXEventSourceCollection( ((o, args) =>
            {
                var message = args.Message.Data;
                ProcessJsonObject(message);

            }), _apiKey);

            // In this thread, we check at each interval whether the client needs to be updated
            // Subscription renewal requests may come in dozens and all at relatively same time - we cannot update them one by one when work with SSE
            var clientUpdateThread = new Thread(() =>
            {
                while (true)
                {
                    _refreshEvent.WaitOne();
                    Thread.Sleep(SubscribeDelay);

                    _clients.UpdateSubscription(_symbols.Keys.ToArray());
                    _refreshEvent.Reset();

                    if (_isDisposing)
                    {
                        break;
                    }
                }

            }) {IsBackground = true};
            clientUpdateThread.Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ProcessJsonObject(string json)
        {
            try
            {
                var dataList = JsonConvert.DeserializeObject<List<QuoteSSE>>(json);

                foreach (var item in dataList)
                {
                    var symbolString = item.Symbol;
                    Symbol symbol;
                    if (!_symbols.TryGetValue(symbolString, out symbol))
                    {
                        // If this called, then the symbol is not in the collection,
                        // then it was removed in Unsubscribe(), then no updates are needed
                        Log.Trace($"ProcessJsonObject(): Could not get the symbol {symbolString}");
                        continue;
                    }

                    var bidSize = item.IexBidSize;
                    var bidPrice = item.IexBidPrice;
                    var askSize = item.IexAskSize;
                    var askPrice = item.IexAskPrice;
                    var lastSalePrice = item.IexRealtimePrice;
                    var lastSaleSize = item.IexRealtimeSize;

                    var lastTradeTime = item.LastTradeTime;
                    var lastTradeDateTime = _unixEpoch.AddMilliseconds(lastTradeTime);
                    var lastUpdated = item.IexLastUpdated;
                    var lastUpdatedDatetime = _unixEpoch.AddMilliseconds(lastUpdated);

                    if (lastUpdated == -1)
                    {
                        // there were no trades on this day
                        return;
                    }

                    // By a strange circumstance, IEX sends the same ticks several times in a row, we must skip the same entries
                    long value;
                    if (_tickLastTradeTime.TryGetValue(symbolString, out value))
                    {
                        if (value == lastTradeTime) return;
                    }

                    // Otherwise update a dictionary and update a tick
                    _tickLastTradeTime[symbolString] = lastTradeTime;

                    var tick = new Tick()
                    {
                        Symbol = symbol,
                        Time = lastUpdatedDatetime.ConvertFromUtc(TimeZones.NewYork),
                        TickType = lastUpdatedDatetime == lastTradeDateTime ? TickType.Trade : TickType.Quote,
                        Exchange = "IEX",
                        BidSize = bidSize,
                        BidPrice = bidPrice,
                        AskSize = askSize,
                        AskPrice = askPrice,
                        Value = lastSalePrice,
                        Quantity = lastSaleSize
                    };

                    _aggregator.Update(tick);
                }
            }
            catch (Exception err)
            {
                Log.Error("IEXDataQueueHandler.ProcessJsonObject(): " + err.Message);
            }
        }

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (dataConfig.SecurityType != SecurityType.Equity)
            {
                return Enumerable.Empty<BaseData>().GetEnumerator();
            }

            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
        }

        private void Refresh()
        {
            _refreshEvent.Set();
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

        /// <summary>
        /// Dispose connection to IEX
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _aggregator.DisposeSafely();
            _cts.Cancel();

            _clients.Dispose();
            _isDisposing = true;

            Log.Trace("IEXDataQueueHandler.Dispose(): Disconnected from IEX live data");
        }

        ~IEXDataQueueHandler()
        {
            Dispose(false);
        }

        #region IHistoryProvider implementation

        /// <summary>
        /// Gets the total number of data points emitted by this history provider
        /// </summary>
        public override int DataPointCount => _dataPointCount;

        /// <summary>
        /// Initializes this history provider to work for the specified job
        /// </summary>
        /// <param name="parameters">The initialization parameters</param>
        public override void Initialize(HistoryProviderInitializeParameters parameters)
        {
        }

        /// <summary>
        /// Gets the history for the requested securities
        /// </summary>
        /// <param name="requests">The historical data requests</param>
        /// <param name="sliceTimeZone">The time zone used when time stamping the slice instances</param>
        /// <returns>An enumerable of the slices of data covering the span specified in each request</returns>
        public override IEnumerable<Slice> GetHistory(IEnumerable<Data.HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Log.Error("IEXDataQueueHandler.GetHistory(): History calls for IEX require an API key.");
                yield break;
            }

            foreach (var request in requests)
            {
                foreach (var slice in ProcessHistoryRequests(request))
                {
                    yield return slice;
                }
            }
        }

        /// <summary>
        /// Populate request data
        /// </summary>
        private IEnumerable<Slice> ProcessHistoryRequests(Data.HistoryRequest request)
        {
            var ticker = request.Symbol.ID.Symbol;
            var start = request.StartTimeUtc.ConvertFromUtc(TimeZones.NewYork);
            var end = request.EndTimeUtc.ConvertFromUtc(TimeZones.NewYork);

            if (request.Resolution == Resolution.Minute && start <= DateTime.Today.AddDays(-30))
            {
                Log.Error("IEXDataQueueHandler.GetHistory(): History calls with minute resolution for IEX available only for trailing 30 calendar days.");
                yield break;
            }

            if (request.Resolution != Resolution.Daily && request.Resolution != Resolution.Minute)
            {
                Log.Error("IEXDataQueueHandler.GetHistory(): History calls for IEX only support daily & minute resolution.");
                yield break;
            }

            if (start <= DateTime.Today.AddYears(-5))
            {
                Log.Error("IEXDataQueueHandler.GetHistory(): History calls for IEX only support a maximum of 5 years history.");
                yield break;
            }

            Log.Trace("IEXDataQueueHandler.ProcessHistoryRequests(): Submitting request: " +
                Invariant($"{request.Symbol.SecurityType}-{ticker}: {request.Resolution} {start}->{end}")
            );

            var span = end.Date - start.Date;
            var suffixes = new List<string>();
            if (span.Days < 30 && request.Resolution == Resolution.Minute)
            {
                var begin = start;
                while (begin < end)
                {
                    suffixes.Add("date/" + begin.ToStringInvariant("yyyyMMdd"));
                    begin = begin.AddDays(1);
                }
            }
            else if (span.Days < 30)
            {
                suffixes.Add("1m");
            }
            else if (span.Days < 3 * 30)
            {
                suffixes.Add("3m");
            }
            else if (span.Days < 6 * 30)
            {
                suffixes.Add("6m");
            }
            else if (span.Days < 12 * 30)
            {
                suffixes.Add("1y");
            }
            else if (span.Days < 24 * 30)
            {
                suffixes.Add("2y");
            }
            else
            {
                suffixes.Add("5y");
            }
            // Download and parse data
            using (var client = new WebClient())
            {

                foreach (var suffix in suffixes)
                {
                    JArray parsedResponse;
                    try
                    {
                        var response = client.DownloadString("https://cloud.iexapis.com/v1/stock/" + ticker + "/chart/" + suffix + "?token=" + _apiKey);
                        parsedResponse = JArray.Parse(response);
                    }
                    catch (WebException webExc)
                    {
                        // To find a reason why does web exception occur need to retrieve additional details
                        if (webExc.Response != null)
                        {
                            var response = webExc.Response;
                            var dataStream = response.GetResponseStream();
                            if (dataStream != null)
                            {
                                var reader = new StreamReader(dataStream);
                                var details = reader.ReadToEnd();
                                
                                throw new Exception(details);
                            }
                        }

                        throw;
                    }

                    foreach (var item in parsedResponse.Children())
                    {
                        DateTime date;
                        if (item["minute"] != null)
                        {
                            date = DateTime.ParseExact(item["date"].Value<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                            var mins = TimeSpan.ParseExact(item["minute"].Value<string>(), "hh\\:mm", CultureInfo.InvariantCulture);
                            date += mins;
                        }
                        else
                        {
                            date = Parse.DateTime(item["date"].Value<string>());
                        }

                        if (date.Date < start.Date || date.Date > end.Date)
                        {
                            continue;
                        }

                        Interlocked.Increment(ref _dataPointCount);

                        if (item["open"].Type == JTokenType.Null)
                        {
                            continue;
                        }
                        var open = item["open"].Value<decimal>();
                        var high = item["high"].Value<decimal>();
                        var low = item["low"].Value<decimal>();
                        var close = item["close"].Value<decimal>();
                        var volume = item["volume"].Value<int>();

                        var tradeBar = new TradeBar(date, request.Symbol, open, high, low, close, volume);

                        yield return new Slice(tradeBar.EndTime, new[] {tradeBar});
                    }
                }
            }
        }

        #endregion
    }
}
