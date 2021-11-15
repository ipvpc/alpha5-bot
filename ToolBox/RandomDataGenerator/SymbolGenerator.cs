using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.ToolBox.RandomDataGenerator
{
    public abstract class SymbolGenerator
    {
        protected IRandomValueGenerator Random { get; }
        protected RandomDataGeneratorSettings Settings { get; }

        protected MarketHoursDatabase MarketHoursDatabase { get; }
        protected SymbolPropertiesDatabase SymbolPropertiesDatabase { get; }

        // used to prevent generating duplicates, but also caps
        // the memory allocated to checking for duplicates
        private readonly FixedSizeHashQueue<Symbol> _symbols;

        protected SymbolGenerator(RandomDataGeneratorSettings settings, IRandomValueGenerator random)
        {
            Settings = settings;
            Random = random;
            _symbols = new FixedSizeHashQueue<Symbol>(1000);
            SymbolPropertiesDatabase = SymbolPropertiesDatabase.FromDataFolder();
            MarketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        }

        public IEnumerable<Symbol> GenerateRandomSymbols()
        {
            for (int i = 0; i < Settings.SymbolCount; i++)
            {
                yield return GenerateSingle();
            }
        }

        protected abstract Symbol GenerateSingle();

        public virtual Symbol NextSymbol(SecurityType securityType, string market)
        {
            if (securityType == SecurityType.Option || securityType == SecurityType.Future)
            {
                throw new ArgumentException("Please use NextOption or NextFuture for SecurityType.Option and SecurityType.Future respectively.");
            }

            string ticker;

            // we must return a symbol matching an entry in the symbol properties database
            // if there is a wildcard entry, we can generate a truly random symbol
            // if there is no wildcard entry, the symbols we can generate are limited by the entries in the database
            if (SymbolPropertiesDatabase.ContainsKey(market, SecurityDatabaseKey.Wildcard, securityType))
            {
                // let's make symbols all have 3 chars as it's acceptable for all security types with wildcard entries
                ticker = NextUpperCaseString(3, 3);
            }
            else
            {
                ticker = NextTickerFromSymbolPropertiesDatabase(securityType, market);
            }

            // by chance we may generate a ticker that actually exists, and if map files exist that match this
            // ticker then we'll end up resolving the first trading date for use in the SID, otherwise, all
            // generated symbol will have a date equal to SecurityIdentifier.DefaultDate
            var symbol = Symbol.Create(ticker, securityType, market);
            if (_symbols.Add(symbol))
            {
                return symbol;
            }

            // lo' and behold, we created a duplicate --recurse to find a unique value
            // this is purposefully done as the last statement to enable the compiler to
            // unroll this method into a tail-recursion loop :)
            return NextSymbol(securityType, market);
        }

        protected string NextTickerFromSymbolPropertiesDatabase(SecurityType securityType, string market)
        {
            // prevent returning a ticker matching any previously generated symbol
            var existingTickers = _symbols
                .Where(sym => sym.ID.Market == market && sym.ID.SecurityType == securityType)
                .Select(sym => sym.Value);

            // get the available tickers from the symbol properties database and remove previously generated tickers
            var availableTickers = Enumerable.Except(SymbolPropertiesDatabase.GetSymbolPropertiesList(market, securityType)
                    .Select(kvp => kvp.Key.Symbol), existingTickers)
                .ToList();

            // there is a limited number of entries in the symbol properties database so we may run out of tickers
            if (availableTickers.Count == 0)
            {
                throw new NoTickersAvailableException(securityType, market);
            }

            return availableTickers[Random.NextInt(availableTickers.Count)];
        }

        protected DateTime GetRandomExpiration(SecurityExchangeHours marketHours, DateTime minExpiry, DateTime maxExpiry)
        {
            // generate a random expiration date on a friday
            var expiry = Random.NextDate(minExpiry, maxExpiry, DayOfWeek.Friday);

            // check to see if we're open on this date and if not, back track until we are
            // we're using the equity market hours as a proxy since we haven't generated the option symbol yet
            while (!marketHours.IsDateOpen(expiry))
            {
                expiry = expiry.AddDays(-1);
            }

            return expiry;
        }

        /// <summary>
        /// Generates a random <see cref="string"/> within the specified lengths.
        /// </summary>
        /// <param name="minLength">The minimum length, inclusive</param>
        /// <param name="maxLength">The maximum length, inclusive</param>
        /// <returns>A new upper case string within the specified lengths</returns>
        public virtual string NextUpperCaseString(int minLength, int maxLength)
        {
            var str = string.Empty;
            var length = Random.NextInt(minLength, maxLength);
            for (int i = 0; i < length; i++)
            {
                // A=65 - inclusive lower bound
                // Z=90 - inclusive upper bound
                var c = (char)Random.NextInt(65, 91);
                str += c;
            }

            return str;
        }

        /// <summary>
        /// Returns the number of symbols with the specified parameters can be generated.
        /// Returns int.MaxValue if there is no limit for the given parameters.
        /// </summary>
        /// <param name="securityType">The security type of the generated symbols</param>
        /// <param name="market">The market of the generated symbols</param>
        /// <returns>The number of available symbols for the given parameters, or int.MaxValue if no limit</returns>
        public virtual int GetAvailableSymbolCount(SecurityType securityType, string market)
        {
            // there is no limit to the number of option/future contracts we can generate
            if (securityType == SecurityType.Option || securityType == SecurityType.Future)
            {
                return int.MaxValue;
            }

            // check the symbol properties database to determine how many symbols we can generate
            // if there is a wildcard entry, we can generate as many symbols as we want
            // if there is no wildcard entry, we can only generate as many symbols as there are entries
            return SymbolPropertiesDatabase.ContainsKey(market, SecurityDatabaseKey.Wildcard, securityType)
                ? int.MaxValue
                : SymbolPropertiesDatabase.GetSymbolPropertiesList(market, securityType).Count();
        }
    }
}
