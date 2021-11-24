using System;
using System.Linq;
using QuantConnect.Securities;

namespace QuantConnect.ToolBox.RandomDataGenerator
{
    /// <summary>
    /// Generates a new random <see cref="Symbol"/> object of the specified security type.
    /// All returned symbols have a matching entry in the Symbol properties database.
    /// </summary>
    /// <remarks>
    /// A valid implementation will keep track of generated Symbol objects to ensure duplicates
    /// are not generated.
    /// </remarks>
    /// <exception cref="ArgumentException">Throw when specifying <see cref="SecurityType.Option"/> or
    /// <see cref="SecurityType.Future"/>. To generate symbols for the derivative security types, please
    /// use <see cref="NextOption"/> and <see cref="NextFuture"/> respectively</exception>
    /// <exception cref="NoTickersAvailableException">Thrown when there are no tickers left to use for new symbols.</exception>
    /// <param name="securityType">The security type of the generated Symbol</param>
    /// <param name="market">The market of the generated Symbol</param>
    /// <returns>A new Symbol object of the specified security type</returns>
    public class SpotSymbolGenerator : SymbolGenerator
    {
        private readonly string _market;
        private readonly SecurityType _securityType;

        public SpotSymbolGenerator(RandomDataGeneratorSettings settings, IRandomValueGenerator random)
            : base(settings, random)
        {
            _market = settings.Market;
            _securityType = settings.SecurityType;
        }

        protected override Symbol GenerateSymbol()
            => NextSymbol(Settings.SecurityType, Settings.Market);

        protected override ITickGenerator CreateTickGenerator(Symbol symbol)
            => new TickGenerator(Settings, Random, symbol);

        public override int GetAvailableSymbolCount()
        {
            // check the Symbol properties database to determine how many symbols we can generate
            // if there is a wildcard entry, we can generate as many symbols as we want
            // if there is no wildcard entry, we can only generate as many symbols as there are entries
            return SymbolPropertiesDatabase.ContainsKey(_market, SecurityDatabaseKey.Wildcard, _securityType)
                ? int.MaxValue
                : SymbolPropertiesDatabase.GetSymbolPropertiesList(_market, _securityType).Count();
        }
    }
}
