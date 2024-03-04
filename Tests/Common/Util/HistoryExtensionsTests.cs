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
using System.Linq;
using NUnit.Framework;
using QuantConnect.Util;
using QuantConnect.Data;
using QuantConnect.Tests.Brokerages;

namespace QuantConnect.Tests.Common.Util
{
    [TestFixture]
    public class HistoryExtensionsTests
    {
        [TestCase("GOOGL", "2010/01/01", "2014/02/04", 1)]
        [TestCase("GOOGL", "2010/01/01", "2015/02/16", 2)]
        [TestCase("GOOGL", "2020/01/01", "2024/01/01", 1)]
        [TestCase("SPWR", "2007/11/17", "2023/01/01", 3)]
        [TestCase("SPWR", "2011/11/17", "2023/01/01", 1)]
        [TestCase("AAPL", "2008/02/01", "2024/03/01", 1)]
        [TestCase("NFLX", "2022/02/01", "2024/03/01", 1)]
        public void GetSplitHistoricalRequestWithTheSameSymbolButDifferentTicker(string ticker, DateTime startDateTime, DateTime endDateTime, int expectedAmount)
        {
            var symbol = Symbol.Create(ticker, SecurityType.Equity, Market.USA);

            var historyRequest = TestsHelpers.GetHistoryRequest(symbol, startDateTime, endDateTime, Resolution.Daily, TickType.Trade);

            var historyRequests = historyRequest.SplitHistoryRequestWithUpdatedMappedSymbol(TestGlobals.MapFileProvider).ToList();

            Assert.IsNotNull(historyRequests);
            Assert.IsNotEmpty(historyRequests);
            Assert.That(historyRequests.Count, Is.EqualTo(expectedAmount));

            if (expectedAmount >= 2)
            {
                var (firstHistoryRequest, secondHistoryRequest) = (historyRequests[0], historyRequests[1]);

                Assert.IsTrue(firstHistoryRequest.Symbol.Value != secondHistoryRequest.Symbol.Value);

                Assert.That(startDateTime, Is.EqualTo(firstHistoryRequest.StartTimeUtc));
                Assert.That(startDateTime, Is.Not.EqualTo(secondHistoryRequest.StartTimeUtc));

                Assert.That(endDateTime, Is.Not.EqualTo(firstHistoryRequest.EndTimeUtc));
                Assert.That(endDateTime, Is.EqualTo(historyRequests[expectedAmount - 1].EndTimeUtc));

                Assert.That(firstHistoryRequest.StartTimeUtc, Is.Not.EqualTo(secondHistoryRequest.StartTimeUtc));
                Assert.That(firstHistoryRequest.EndTimeUtc, Is.Not.EqualTo(secondHistoryRequest.EndTimeUtc));
                Assert.That(firstHistoryRequest.StartTimeLocal, Is.Not.EqualTo(secondHistoryRequest.StartTimeLocal));
                Assert.That(firstHistoryRequest.EndTimeLocal, Is.Not.EqualTo(secondHistoryRequest.EndTimeLocal));
            }
        }
    }
}