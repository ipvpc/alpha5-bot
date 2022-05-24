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
using System.Linq;
using Moq;
using NUnit.Framework;
using Python.Runtime;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.Framework.Risk;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;
using QuantConnect.Data.Market;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Tests.Algorithm.Framework.Risk
{
    [TestFixture]
    public class TrailingStopRiskManagementModelTests
    {
        [Test]
        [TestCase(Language.CSharp, 10, new[] { false, true, true }, new[] { 0d, 10d, -1d }, new[] { false, false, true })]
        [TestCase(Language.Python, 10, new[] { false, true, true }, new[] { 0d, 10d, -1d }, new[] { false, false, true })]
        [TestCase(Language.CSharp, 10, new[] { false, false, false}, new[] { 0d, 10d, -1d }, new[] { false, false, false })]
        [TestCase(Language.Python, 10, new[] { false, false, false }, new[] { 0d, 10d, -1d }, new[] { false, false, false })]
        [TestCase(Language.CSharp, 10, new[] { true, true, true, true }, new[] { 10d, 20d, 10d, 9d }, new[] { false, false, false, true })]
        [TestCase(Language.Python, 10, new[] { true, true, true, true }, new[] { 10d, 20d, 10d, 9d }, new[] { false, false, false, true })]
        public void ReturnsExpectedPortfolioTarget(
            Language language,
            decimal maxDrawdownPercent,
            bool[] investedArray,
            double[] uppInputsDouble,
            bool[] shouldLiquidateArray)
        {
            var unrealizedProfitPercentArray = System.Array.ConvertAll(uppInputsDouble, x => (decimal) x);
            var security = new Mock<Equity>(
                Symbols.AAPL,
                SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                new Cash(Currencies.USD, 0, 1),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null,
                new SecurityCache(),
                Exchange.UNKNOWN
            );

            var holding = new Mock<EquityHolding>(security.Object,
                new IdentityCurrencyConverter(Currencies.USD));

            var algorithm = new QCAlgorithm();
            algorithm.SetPandasConverter();
            algorithm.Securities.Add(Symbols.AAPL, security.Object);

            if (language == Language.Python)
            {
                using (Py.GIL())
                {
                    const string name = nameof(TrailingStopRiskManagementModel);
                    var instance = Py.Import(name).GetAttr(name).Invoke(maxDrawdownPercent.ToPython());
                    var model = new RiskManagementModelPythonWrapper(instance);
                    algorithm.SetRiskManagement(model);
                }
            }
            else
            {
                var model = new TrailingStopRiskManagementModel(maxDrawdownPercent);
                algorithm.SetRiskManagement(model);
            }

            for (int i = 0; i < investedArray.Length; i++)
            {
                var invested = investedArray[i];
                var unrealizedProfitPercent = unrealizedProfitPercentArray[i];
                var shouldLiquidate = shouldLiquidateArray[i];

                security.Setup(m => m.Invested).Returns(invested);
                holding.Setup(m => m.UnrealizedProfitPercent).Returns(unrealizedProfitPercent);
                security.Object.Holdings = holding.Object;

                var targets = algorithm.RiskManagement.ManageRisk(algorithm, null).ToList();

                if (shouldLiquidate)
                {
                    Assert.AreEqual(1, targets.Count);
                    Assert.AreEqual(Symbols.AAPL, targets[0].Symbol);
                    Assert.AreEqual(0, targets[0].Quantity);
                }
                else
                {
                    Assert.AreEqual(0, targets.Count);
                }
            }
        }

        [Test]
        [TestCase(Language.CSharp, 0.05, new[] { 1d, 100d, 99.95d, 99.94d, 95d, 94.99d }, new[] { false, false, false, false, false, true }, true)]
        [TestCase(Language.Python, 0.05, new[] { 1d, 100d, 99.95d, 99.94d, 95d, 94.99d }, new[] { false, false, false, false, false, true }, true)]
        [TestCase(Language.CSharp, 0.05, new[] { 1d, 100d, 99.95d, 99.94d, 95d, 94.99d }, new[] { false, false, false, false, false, true }, false)]
        [TestCase(Language.Python, 0.05, new[] { 1d, 100d, 99.95d, 99.94d, 95d, 94.99d }, new[] { false, false, false, false, false, true }, false)]
        public void LiquidatesWhenExpected(
            Language language,
            decimal maxDrawdownPercent,
            double[] prices,
            bool[] shouldLiquidateArray,
            bool longPosition)
        {
            var decimalPrices = System.Array.ConvertAll(prices, x => (decimal) x);

            var security = new Security(
                Symbols.AAPL,
                SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                new Cash(Currencies.USD, 1000, 1),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null,
                new SecurityCache()
            );
            security.FeeModel = new ConstantFeeModel(0);

            var holding = new SecurityHolding(security, new IdentityCurrencyConverter(Currencies.USD));
            var holdingsCost = decimalPrices[0];
            holding.SetHoldings(holdingsCost, longPosition ? 1m : -1m);
            security.Holdings = holding;

            var algorithm = new QCAlgorithm();
            algorithm.SetPandasConverter();
            algorithm.Securities.Add(Symbols.AAPL, security);

            if (language == Language.Python)
            {
                using (Py.GIL())
                {
                    const string name = nameof(TrailingStopRiskManagementModel);
                    var instance = Py.Import(name).GetAttr(name).Invoke(maxDrawdownPercent.ToPython());
                    var model = new RiskManagementModelPythonWrapper(instance);
                    algorithm.SetRiskManagement(model);
                }
            }
            else
            {
                var model = new TrailingStopRiskManagementModel(maxDrawdownPercent);
                algorithm.SetRiskManagement(model);
            }

            for (int i = 0; i < decimalPrices.Length; i++)
            {
                var price = decimalPrices[i];
                security.SetMarketPrice(new Tick(DateTime.Now, security.Symbol, price, price));

                var targets = algorithm.RiskManagement.ManageRisk(algorithm, null).ToList();
                var shouldLiquidate = shouldLiquidateArray[i];

                if (shouldLiquidate)
                {
                    Assert.AreEqual(1, targets.Count);
                    Assert.AreEqual(Symbols.AAPL, targets[0].Symbol);
                    Assert.AreEqual(0, targets[0].Quantity);
                }
                else
                {
                    Assert.AreEqual(0, targets.Count);
                }
            }
        }
    }
}
