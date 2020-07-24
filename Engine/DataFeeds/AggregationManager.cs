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

using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;
using QuantConnect.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Aggregates ticks and bars based on given subscriptions. 
    /// Current implementation is based on <see cref="IDataConsolidator"/> that consolidates ticks and put them into enumerator.
    /// </summary>
    public class AggregationManager : IDataAggregator
    {
        private readonly ConcurrentDictionary<SecurityIdentifier, ConcurrentDictionary<SubscriptionDataConfig, ScannableEnumerator<BaseData>>> _enumerators
            = new ConcurrentDictionary<SecurityIdentifier, ConcurrentDictionary<SubscriptionDataConfig, ScannableEnumerator<BaseData>>>();

        /// <summary>
        /// Continuous UTC time provider
        /// </summary>
        protected ITimeProvider TimeProvider { get; set; } = new RealTimeProvider();

        /// <summary>
        /// Add new subscription to current <see cref="IDataAggregator"/> instance
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Add(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            IDataConsolidator consolidator;
            var period = dataConfig.Resolution.ToTimeSpan();
            var isPeriodBased = false;
            switch (dataConfig.Type.Name)
            {
                case nameof(QuoteBar):
                    isPeriodBased = dataConfig.Resolution != Resolution.Tick;
                    consolidator = new TickQuoteBarConsolidator(period);
                    break;

                case nameof(TradeBar):
                    isPeriodBased = dataConfig.Resolution != Resolution.Tick;
                    consolidator = new TickConsolidator(period);
                    break;

                case nameof(OpenInterest):
                    isPeriodBased = dataConfig.Resolution != Resolution.Tick;
                    consolidator = new OpenInterestConsolidator(period);
                    break;

                case nameof(Tick):
                    consolidator = FilteredIdentityDataConsolidator.ForTickType(dataConfig.TickType);
                    break;

                default:
                    // streaming custom data subscriptions can pass right through
                    consolidator = new FilteredIdentityDataConsolidator<BaseData>(data => data.GetType() == dataConfig.Type);
                    break;
            }

            var enumerator = new ScannableEnumerator<BaseData>(consolidator, dataConfig.ExchangeTimeZone, TimeProvider, newDataAvailableHandler, isPeriodBased);

            _enumerators.AddOrUpdate(
                dataConfig.Symbol.ID,
                new ConcurrentDictionary<SubscriptionDataConfig, ScannableEnumerator<BaseData>> { [dataConfig] = enumerator },
                (k, v) => { v.AddOrUpdate(dataConfig, enumerator); return v; });

            return enumerator;
        }

        /// <summary>
        /// Removes the handler with the specified identifier
        /// </summary>
        /// <param name="dataConfig">Subscription data configuration to be removed</param>
        public bool Remove(SubscriptionDataConfig dataConfig)
        {
            ConcurrentDictionary<SubscriptionDataConfig, ScannableEnumerator<BaseData>> enumerators;
            if (_enumerators.TryGetValue(dataConfig.Symbol.ID, out enumerators))
            {
                if (enumerators.Count == 1)
                {
                    ConcurrentDictionary<SubscriptionDataConfig, ScannableEnumerator<BaseData>> output;
                    return _enumerators.TryRemove(dataConfig.Symbol.ID, out output);
                }
                else
                {
                    ScannableEnumerator<BaseData> output;
                    return enumerators.TryRemove(dataConfig, out output);
                }
            }
            else
            {
                Log.Debug($"AggregationManager.Update(): IDataConsolidator for symbol ({dataConfig.Symbol.Value}) was not found.");
                return false;
            }
        }

        /// <summary>
        /// Add new data to aggregator
        /// </summary>
        /// <param name="input">The new data</param>
        public void Update(BaseData input)
        {
            try
            {
                ConcurrentDictionary<SubscriptionDataConfig, ScannableEnumerator<BaseData>> enumerators;
                if (_enumerators.TryGetValue(input.Symbol.ID, out enumerators))
                {
                    foreach (var kvp in enumerators)
                    {
                        // for non tick resolution subscriptions drop suspicious ticks
                        if (kvp.Key.Resolution != Resolution.Tick && input.DataType == MarketDataType.Tick)
                        {
                            var tick = input as Tick;
                            if (tick != null && tick.Suspicious)
                            {
                                continue;
                            }
                        }

                        kvp.Value.Update(input);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        /// <summary>
        /// Dispose of the aggregation manager.
        /// </summary>
        public void Dispose() { }
    }
}
