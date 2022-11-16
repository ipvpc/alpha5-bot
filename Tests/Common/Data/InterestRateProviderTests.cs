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
using System.IO;
using NUnit.Framework;
using QuantConnect.Data;

namespace QuantConnect.Tests.Common.Data
{
    [TestFixture]
    public class InterestRateProviderTests
    {
        [Test]
        public void Create()
        {
            var csvLine = "2020-01-01,2.5";
            var result = InterestRateProvider.Create(csvLine);

            var expected = new InterestRateProvider
            {
                Date = new DateTime(2020, 1, 1),
                InterestRate = 0.025m
            };

            AssertAreEqual(expected, result);
        }

        [TestCase("option/usa/interest-rate.csv", true)]
        [TestCase("non-existing.csv", false)]
        public void FromCsvFile(string dir, bool getResults)
        {
            var filePath = Path.Combine(Globals.DataFolder, dir);
            var result = InterestRateProvider.FromCsvFile(filePath);

            var expected = new Dictionary<DateTime, decimal>();
            if (getResults)
            {
                expected.Add(new DateTime(2020, 3, 9), 0.0175m);
                expected.Add(new DateTime(2020, 3, 11), 0.0025m);
            }
            else
            {
                expected.Add(DateTime.MinValue, 0.01m);
            }

            AssertAreEqual(expected, result);
        }

        private void AssertAreEqual(object expected, object result)
        {
            foreach (var fieldInfo in expected.GetType().GetFields())
            {
                Assert.AreEqual(fieldInfo.GetValue(expected), fieldInfo.GetValue(result));
            }
        }
    }
}
