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
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Defines a custom alpha model that uses MACD crossovers. The MACD signal line is
    /// used to generate up/down insights if it's stronger than the bounce threshold.
    /// If the MACD signal is within the bounce threshold then a flat price insight is returned.
    /// </summary>
    public class MacdAlphaModel : AlphaModel
    {
        private readonly int _fastPeriod;
        private readonly int _slowPeriod;
        private readonly int _signalPeriod;
        private readonly MovingAverageType _movingAverageType;
        private readonly Resolution _resolution;
        private readonly TimeSpan _insightPeriod;
        private const decimal BounceThresholdPercent = 0.01m;
        protected readonly Dictionary<Symbol, SymbolData> _symbolData = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="MacdAlphaModel"/> class
        /// </summary>
        /// <param name="fastPeriod">The MACD fast period</param>
        /// <param name="slowPeriod">The MACD slow period</param>
        /// <param name="signalPeriod">The smoothing period for the MACD signal</param>
        /// <param name="movingAverageType">The type of moving average to use in the MACD</param>
        /// <param name="resolution">The resolution of data sent into the MACD indicator</param>
        public MacdAlphaModel(
            int fastPeriod = 12,
            int slowPeriod = 26,
            int signalPeriod = 9,
            MovingAverageType movingAverageType = MovingAverageType.Exponential,
            Resolution resolution = Resolution.Daily
            )
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _signalPeriod = signalPeriod;
            _movingAverageType = movingAverageType;
            _resolution = resolution;
            _insightPeriod = _resolution.ToTimeSpan().Multiply(_fastPeriod);
            Name = $"{nameof(MacdAlphaModel)}({fastPeriod},{slowPeriod},{signalPeriod},{movingAverageType},{resolution})";
        }

        /// <summary>
        /// Determines an insight for each security based on it's current MACD signal
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="data">The new data available</param>
        /// <returns>The new insights generated</returns>
        public override IEnumerable<Insight> Update(QCAlgorithm algorithm, Slice data)
        {
            // Reset indicators when corporate actions occur
            foreach (var symbol in data.Splits.Keys.Union(data.Dividends.Keys))
            {
                if (_symbolData.TryGetValue(symbol, out var symbolData))
                {
                    algorithm.Log($"MacdAlphaModel: Reset {symbol} MACD due to corporate action.");
                    symbolData.Reset();
                }
            }

            // Only emit insights when there is quote data, not when a corporate action occurs (at midnight)
            if (data.QuoteBars.Count == 0) 
            {
                yield break;
            }

            foreach (var sd in _symbolData.Values)
            {
                if (sd.Security.Price == 0)
                {
                    continue;
                }

                var direction = InsightDirection.Flat;
                var normalizedSignal = sd.MACD.Signal / sd.Security.Price;
                if (normalizedSignal > BounceThresholdPercent)
                {
                    direction = InsightDirection.Up;
                }
                else if (normalizedSignal < -BounceThresholdPercent)
                {
                    direction = InsightDirection.Down;
                }

                // ignore signal for same direction as previous signal
                if (direction == sd.PreviousDirection)
                {
                    continue;
                }

                var insight = Insight.Price(sd.Security.Symbol, _insightPeriod, direction);
                sd.PreviousDirection = direction;
                yield return insight;
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed.
        /// This initializes the MACD for each added security and cleans up the indicator for each removed security.
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
            foreach (var added in changes.AddedSecurities)
            {
                if (_symbolData.ContainsKey(added.Symbol))
                {
                    continue;
                }
                _symbolData.Add(added.Symbol, new SymbolData(algorithm, added, _fastPeriod, _slowPeriod, _signalPeriod, _movingAverageType, _resolution));
            }

            foreach (var removed in changes.RemovedSecurities)
            {
                if (_symbolData.Remove(removed.Symbol, out var data))
                {
                    // clean up our consolidator and indicator
                    data.Dispose();
                }
            }
        }

        public class SymbolData : IDisposable
        {
            private readonly QCAlgorithm _algorithm;
            private readonly Resolution _resolution;
            public InsightDirection? PreviousDirection { get; set; }
            public readonly Security Security;
            public readonly IDataConsolidator Consolidator;
            public readonly MovingAverageConvergenceDivergence MACD;

            public SymbolData(QCAlgorithm algorithm, Security security, int fastPeriod, int slowPeriod, int signalPeriod, MovingAverageType movingAverageType, Resolution resolution)
            {
                _algorithm = algorithm;
                _resolution = resolution;
                Security = security;

                MACD = new MovingAverageConvergenceDivergence(fastPeriod, slowPeriod, signalPeriod, movingAverageType);
                Consolidator = algorithm.ResolveConsolidator(security.Symbol, resolution);
                
                algorithm.RegisterIndicator(security.Symbol, MACD, Consolidator);
                algorithm.SubscriptionManager.AddConsolidator(security.Symbol, Consolidator);
                algorithm.WarmUpIndicator(security.Symbol, MACD, resolution);
            }

            public void Reset()
            {
                // reset & warm up indicator & consolidator by updating
                MACD.Reset();
                var history = _algorithm.History<TradeBar>(Security.Symbol, MACD.WarmUpPeriod, _resolution);
                foreach (var bar in history)
                {
                    Consolidator.Update(bar);
                }
            }

            public void Dispose()
            {
                MACD.Reset();
                _algorithm.SubscriptionManager.RemoveConsolidator(Security.Symbol, Consolidator);
            }
        }
    }
}
