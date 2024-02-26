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
using QuantConnect.Util;
using QuantConnect.Logging;
using System.Threading.Tasks;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Data.Auxiliary;

namespace QuantConnect.Data
{
    /// <summary>
    /// Estimated annualized continuous dividend yield at given date
    /// </summary>
    public class DividendYieldProvider : IDividendYieldModel
    {
        protected static Dictionary<Symbol, Dictionary<DateTime, decimal>> _dividendYieldRateProvider;
        protected static Task _cacheClearTask;
        private static readonly object _lock = new();

        private readonly DateTime _firstDividendYieldDate = Time.Start;
        private decimal _lastDividendYield;
        private readonly Symbol _symbol;

        /// <summary>
        /// Default no dividend payout
        /// </summary>
        public static readonly decimal DefaultDividendYieldRate = 0.0m;

        /// <summary>
        /// The cached refresh period for the dividend yield rate
        /// </summary>
        /// <remarks>Exposed for testing</remarks>
        protected virtual TimeSpan CacheRefreshPeriod
        {
            get
            {
                var dueTime = Time.GetNextLiveAuxiliaryDataDueTime();
                if (dueTime > TimeSpan.FromMinutes(10))
                {
                    // Clear the cache before the auxiliary due time to avoid race conditions with consumers
                    return dueTime - TimeSpan.FromMinutes(10);
                }
                return dueTime;
            }
        }

        /// <summary>
        /// Instantiates a <see cref="DividendYieldProvider"/> with the specified Symbol
        /// </summary>
        public DividendYieldProvider(Symbol symbol)
        {
            _symbol = symbol;

            if (_cacheClearTask == null)
            {
                lock (_lock)
                {
                    // only the first triggers the expiration task check
                    if (_cacheClearTask == null)
                    {
                        StartExpirationTask(CacheRefreshPeriod);
                    }
                }
            }
        }

        /// <summary>
        /// Helper method that will clear any cached dividend rate in a daily basis, this is useful for live trading
        /// </summary>
        private static void StartExpirationTask(TimeSpan cacheRefreshPeriod)
        {
            lock (_lock)
            {
                // we clear the dividend yield rate cache so they are reloaded
                _dividendYieldRateProvider = new();
            }
            _cacheClearTask = Task.Delay(cacheRefreshPeriod).ContinueWith(_ => StartExpirationTask(cacheRefreshPeriod));
        }

        /// <summary>
        /// Get dividend yield by a given date of a given symbol
        /// </summary>
        /// <param name="date">The date</param>
        /// <returns>Dividend yield on the given date of the given symbol</returns>
        public decimal GetDividendYield(DateTime date)
        {
            Dictionary<DateTime, decimal> symbolDividend;
            lock (_lock)
            {
                if (!_dividendYieldRateProvider.TryGetValue(_symbol, out symbolDividend))
                {
                    // load the symbol factor if it is the first encounter
                    symbolDividend = _dividendYieldRateProvider[_symbol] = LoadDividendYieldProvider(_symbol);
                }
            }

            if (!symbolDividend.TryGetValue(date.Date, out var dividendYield))
            {
                return date < _firstDividendYieldDate
                    ? DefaultDividendYieldRate
                    : _lastDividendYield;
            }

            return dividendYield;
        }

        /// <summary>
        /// Generate the daily historical dividend yield
        /// </summary>
        /// <remarks>Exposed for testing</remarks>
        protected virtual Dictionary<DateTime, decimal> LoadDividendYieldProvider(Symbol symbol)
        {
            var factorFileProvider = Composer.Instance.GetPart<IFactorFileProvider>();
            var corporateFactors = factorFileProvider
                .Get(symbol)
                .Select(factorRow => factorRow as CorporateFactorRow)
                .Where(corporateFactor => corporateFactor != null);

            var symbolDividends = FromCorporateFactorRow(corporateFactors, symbol);
            if (symbolDividends.Count == 0)
            {
                return new();
            }

            // Sparse the discrete data points into continuous data for every day
            _lastDividendYield = DefaultDividendYieldRate;
            for (var date = _firstDividendYieldDate; date <= symbolDividends.Keys.Max(); date = date.AddDays(1))
            {
                if (!symbolDividends.TryGetValue(date, out var currentRate))
                {
                    symbolDividends[date] = _lastDividendYield;
                    continue;
                }
                _lastDividendYield = currentRate;
            }

            return symbolDividends;
        }

        /// <summary>
        /// Returns a dictionary of historical dividend yield from collection of corporate factor rows
        /// </summary>
        /// <param name="corporateFactors">The corporate factor rows containing factor data</param>
        /// <param name="symbol">The target symbol</param>
        /// <returns>Dictionary of historical annualized continuous dividend yield data</returns>
        public static Dictionary<DateTime, decimal> FromCorporateFactorRow(IEnumerable<CorporateFactorRow> corporateFactors, Symbol symbol)
        {
            var dividendYieldProvider = new Dictionary<DateTime, decimal>();

            // calculate the dividend rate from each payout
            var subsequentRate = 0m;
            foreach (var row in corporateFactors.OrderByDescending(corporateFactor => corporateFactor.Date))
            {
                var dividendYield = 1 / row.PriceFactor - 1 - subsequentRate;
                dividendYieldProvider[row.Date] = dividendYield;
                subsequentRate = dividendYield;
            }

            // cumulative sum by year, since we'll use yearly payouts for estimation
            var yearlyDividendYieldProvider = new Dictionary<DateTime, decimal>();
            foreach (var date in dividendYieldProvider.Keys.OrderBy(x => x))
            {
                // 15 days window from 1y to avoid overestimation from last year value
                var yearlyDividend = dividendYieldProvider.Where(kvp => kvp.Key <= date && kvp.Key > date.AddDays(-350)).Sum(kvp => kvp.Value);
                // discrete to continuous: LN(1 + i)
                yearlyDividendYieldProvider[date] = Convert.ToDecimal(Math.Log(1d + (double)yearlyDividend));
            }

            if (yearlyDividendYieldProvider.Count == 0)
            {
                Log.Error($"DividendYieldProvider.FromCsvFile({symbol}): no dividend were loaded");
            }

            return yearlyDividendYieldProvider;
        }
    }
}
