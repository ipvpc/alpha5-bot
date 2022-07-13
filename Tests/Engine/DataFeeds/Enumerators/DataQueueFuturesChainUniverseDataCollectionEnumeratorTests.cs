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
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Collections.Generic;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;

namespace QuantConnect.Tests.Engine.DataFeeds.Enumerators.Factories
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class DataQueueFuturesChainUniverseDataCollectionEnumeratorTests
    {
        [TestCase(Resolution.Tick)]
        [TestCase(Resolution.Second)]
        [TestCase(Resolution.Minute)]
        [TestCase(Resolution.Hour)]
        [TestCase(Resolution.Daily)]
        public void RefreshesFutureChainUniverseOnDateChange(Resolution resolution)
        {
            var startTime = new DateTime(2018, 10, 17, 5, 0, 0);
            var timeProvider = new ManualTimeProvider(startTime);

            var symbolUniverse = new TestDataQueueUniverseProvider(timeProvider);

            var canonicalSymbol = Symbol.Create(Futures.Indices.VIX, SecurityType.Future, Market.CFE, "/VX");

            var request = GetRequest(canonicalSymbol, startTime, resolution);
            var enumerator = new DataQueueFuturesChainUniverseDataCollectionEnumerator(request, symbolUniverse, timeProvider);

            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsNotNull(enumerator.Current);
            Assert.AreEqual(1, symbolUniverse.TotalLookupCalls);
            var data = enumerator.Current;
            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Data.Count);

            timeProvider.Advance(Time.OneSecond);

            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsNull(enumerator.Current);
            Assert.AreEqual(1, symbolUniverse.TotalLookupCalls);

            timeProvider.Advance(Time.OneMinute);

            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsNull(enumerator.Current);
            Assert.AreEqual(1, symbolUniverse.TotalLookupCalls);

            timeProvider.Advance(Time.OneDay);

            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsNotNull(enumerator.Current);
            Assert.AreEqual(2, symbolUniverse.TotalLookupCalls);
            data = enumerator.Current;
            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Data.Count);

            timeProvider.Advance(Time.OneMinute);

            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsNull(enumerator.Current);
            Assert.AreEqual(2, symbolUniverse.TotalLookupCalls);

            enumerator.Dispose();
            request.Universe.Dispose();
        }

        private static SubscriptionRequest GetRequest(Symbol canonicalSymbol, DateTime startTime, Resolution resolution)
        {
            var entry = MarketHoursDatabase.FromDataFolder().GetEntry(canonicalSymbol.ID.Market, canonicalSymbol, canonicalSymbol.SecurityType);
            var config = new SubscriptionDataConfig(
                typeof(ZipEntryName),
                canonicalSymbol,
                resolution,
                entry.DataTimeZone,
                entry.ExchangeHours.TimeZone,
                true,
                false,
                false,
                false,
                TickType.Quote,
                false,
                DataNormalizationMode.Raw
            );

            var algo = new AlgorithmStub();
            var future = algo.AddFuture(canonicalSymbol.Value);

            var universeSettings = new UniverseSettings(resolution, 0, true, false, TimeSpan.Zero);
            var universe = new FuturesChainUniverse(future, universeSettings);
            return new SubscriptionRequest(true, universe, future, config, startTime, Time.EndOfTime);
        }

        private class TestDataQueueUniverseProvider : IDataQueueUniverseProvider
        {
            private readonly Symbol[] _symbolList1 =
            {
                Symbol.CreateFuture(Futures.Indices.VIX, Market.CFE, new DateTime(2018, 10, 31))
            };
            private readonly Symbol[] _symbolList2 =
            {
                Symbol.CreateFuture(Futures.Indices.VIX, Market.CFE, new DateTime(2018, 10, 31)),
                Symbol.CreateFuture(Futures.Indices.VIX, Market.CFE, new DateTime(2018, 11, 30)),
            };

            private readonly ITimeProvider _timeProvider;

            public int TotalLookupCalls { get; set; }

            public TestDataQueueUniverseProvider(ITimeProvider timeProvider)
            {
                _timeProvider = timeProvider;
            }

            public IEnumerable<Symbol> LookupSymbols(Symbol symbol, bool includeExpired, string securityCurrency = null)
            {
                TotalLookupCalls++;
                return _timeProvider.GetUtcNow().Date.Day >= 18 ? _symbolList2 : _symbolList1;
            }

            public bool CanPerformSelection() => true;
        }
    }
}
