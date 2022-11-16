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

using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Class implements Fed's US primary credit rate as risk free rate, implementing <see cref="IQLRiskFreeRateEstimator"/>.
    /// </summary>
    /// <remarks>
    /// Board of Governors of the Federal Reserve System (US), Primary Credit Rate - Historical Dates of Changes and Rates for Federal Reserve District 8: St. Louis [PCREDIT8]
    /// retrieved from FRED, Federal Reserve Bank of St. Louis; https://fred.stlouisfed.org/series/PCREDIT8
    /// </remarks>
    public class FedRateQLRiskFreeRateEstimator : IQLRiskFreeRateEstimator
    {
        private InterestRateProvider _interestRateProvider = new InterestRateProvider();

        /// <summary>
        /// Returns current flat estimate of the risk free rate
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>The estimate</returns>
        public decimal Estimate(Security security, Slice slice, OptionContract contract)
        {
            if (slice == null)
                return 0.01m;
            
            return _interestRateProvider.GetInterestRate(slice.Time.Date);
        }
    }
}
