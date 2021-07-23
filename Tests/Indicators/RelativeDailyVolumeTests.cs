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
using NUnit.Framework;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class RelativeDailyVolumeTests : CommonIndicatorTests<TradeBar>
    {
        protected override IndicatorBase<TradeBar> CreateIndicator()
        {
            return new RelativeDailyVolume(14, Resolution.Hour);
        }

        protected override string TestFileName => "spy_rdv.txt";

        protected override string TestColumnName => "RDV";

        protected override Action<IndicatorBase<TradeBar>, double> Assertion
        {
            get { return (indicator, expected) => Assert.AreEqual(expected, (double)indicator.Current.Value, 0.001); }
        }

        [Test]
        public override void ResetsProperly()
        {
            var rdv = new RelativeDailyVolume(2); /// Default resolution is daily
            var reference = System.DateTime.Today;

            rdv.Update(new TradeBar() { Symbol = Symbols.AAPL, Low = 1, High = 2, Volume = 100, Time = reference.AddDays(1) });
            rdv.Update(new TradeBar() { Symbol = Symbols.AAPL, Low = 3, High = 4, Volume = 200, Time = reference.AddDays(2) });
            rdv.Update(new TradeBar() { Symbol = Symbols.AAPL, Low = 5, High = 6, Volume = 300, Time = reference.AddDays(3) });
            Assert.IsTrue(rdv.IsReady);
            Assert.AreNotEqual(0m, rdv.Current.Value);

            rdv.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(rdv);
        }

        [Test]
        public void ResolutionTest()
        {
            Assert.Throws<ArgumentException>(() => new RelativeDailyVolume(6, Resolution.Hour));
            Assert.Throws<ArgumentException>(() => new RelativeDailyVolume(20, Resolution.Minute));
            Assert.Throws<ArgumentException>(() => new RelativeDailyVolume(2000, Resolution.Second));
        }
    }
}
