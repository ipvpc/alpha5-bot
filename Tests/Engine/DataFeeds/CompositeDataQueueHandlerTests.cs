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

using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Packets;
using QuantConnect.Queues;
using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Tests.Engine.DataFeeds
{
    [TestFixture]
    public class CompositeDataQueueHandlerTests
    {
        [TestCase("ZerodhaBrokerage")]
        [TestCase("TradierBrokerage")]
        [TestCase("QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersBrokerage")]
        [TestCase("OandaBrokerage")]
        [TestCase("GDAXDataQueueHandler")]
        [TestCase("BitfinexBrokerage")] 
        [TestCase("BinanceBrokerage")]
        public void GetFactoryFromDataQueueHandler(string dataQueueHandler)
        {
            var factory = JobQueue.GetFactoryFromDataQueueHandler(dataQueueHandler);
            Assert.NotNull(factory);
        }

        [Test]
        public void SetJob()
        {
            //Array IDQH
            var dataHandlers = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { "FakeDataQueue" });
            var jobWithArrayIDQH = new LiveNodePacket
            {
                Brokerage = "ZerodhaBrokerage",
                DataQueueHandler = dataHandlers
            };
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            compositeDataQueueHandler.SetJob(jobWithArrayIDQH);
            compositeDataQueueHandler.Dispose();
        }

        [Test]
        public void SubscribeReturnsNull()
        {
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            var enumerator = compositeDataQueueHandler.Subscribe(GetConfig(), (_, _) => {});
            Assert.Null(enumerator);
            compositeDataQueueHandler.Dispose();
        }

        [Test]
        public void SubscribeReturnsNotNull()
        {
            var dataHandlers = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { "FakeDataQueue" });
            var job = new LiveNodePacket
            {
                Brokerage = "ZerodhaBrokerage",
                DataQueueHandler = dataHandlers
            };
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            compositeDataQueueHandler.SetJob(job);
            var enumerator = compositeDataQueueHandler.Subscribe(GetConfig(), (_, _) => {});
            Assert.NotNull(enumerator);
            compositeDataQueueHandler.Dispose();
            enumerator.Dispose();
        }

        [Test]
        public void Unsubscribe()
        {
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            compositeDataQueueHandler.Unsubscribe(GetConfig());
            compositeDataQueueHandler.Dispose();
        }

        [Test]
        public void IsNotUniverseProvider()
        {
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            Assert.IsFalse(compositeDataQueueHandler.HasUniverseProvider);
            Assert.Throws<NotSupportedException>(() => compositeDataQueueHandler.LookupSymbols(Symbols.ES_Future_Chain, false));
            Assert.Throws<NotSupportedException>(() => compositeDataQueueHandler.CanPerformSelection());
            compositeDataQueueHandler.Dispose();
        }

        [Test]
        public void DoubleSubscribe()
        {
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            compositeDataQueueHandler.SetJob(new LiveNodePacket { Brokerage = "ZerodhaBrokerage", DataQueueHandler = "[ \"TestDataHandler\" ]" });

            var dataConfig = GetConfig();
            var enumerator = compositeDataQueueHandler.Subscribe(dataConfig, (_, _) => {});
            var enumerator2 = compositeDataQueueHandler.Subscribe(dataConfig, (_, _) => {});
            compositeDataQueueHandler.Unsubscribe(dataConfig);
            compositeDataQueueHandler.Unsubscribe(dataConfig);
            compositeDataQueueHandler.Unsubscribe(dataConfig);

            Assert.AreEqual(2, TestDataHandler.UnsubscribeCounter);

            TestDataHandler.UnsubscribeCounter = 0;
            compositeDataQueueHandler.Dispose();
        }

        [Test]
        public void SingleSubscribe()
        {
            var compositeDataQueueHandler = new CompositeDataQueueHandler();
            compositeDataQueueHandler.SetJob(new LiveNodePacket { Brokerage = "ZerodhaBrokerage", DataQueueHandler = "[ \"TestDataHandler\" ]" });

            var dataConfig = GetConfig();
            var enumerator = compositeDataQueueHandler.Subscribe(dataConfig, (_, _) => {});
            compositeDataQueueHandler.Unsubscribe(dataConfig);
            compositeDataQueueHandler.Unsubscribe(dataConfig);
            compositeDataQueueHandler.Unsubscribe(dataConfig);

            Assert.AreEqual(1, TestDataHandler.UnsubscribeCounter);

            TestDataHandler.UnsubscribeCounter = 0;
            compositeDataQueueHandler.Dispose();
        }

        private static SubscriptionDataConfig GetConfig()
        {
            return new SubscriptionDataConfig(typeof(TradeBar), Symbols.SPY, Resolution.Minute, TimeZones.NewYork, TimeZones.NewYork,
                false, false, false, false, TickType.Trade, false);
        }

        private class TestDataHandler : IDataQueueHandler
        {
            public static int UnsubscribeCounter { get; set; }
            public void Dispose()
            {
            }

            public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
            {
                return Enumerable.Empty<BaseData>().GetEnumerator();
            }

            public void Unsubscribe(SubscriptionDataConfig dataConfig)
            {
                UnsubscribeCounter++;
            }

            public void SetJob(LiveNodePacket job)
            {
            }

            public bool IsConnected { get; }
        }
    }
}
