﻿/*
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

using Newtonsoft.Json;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.FutureOption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuantConnect.Brokerages.InteractiveBrokers
{
    /// <summary>
    /// Provides the mapping between Lean symbols and InteractiveBrokers symbols.
    /// </summary>
    public class InteractiveBrokersSymbolMapper : ISymbolMapper
    {
        private readonly IMapFileProvider _mapFileProvider;

        // we have a special treatment of futures, because IB renamed several exchange tickers (like GBP instead of 6B). We fix this:
        // We map those tickers back to their original names using the map below
        private readonly Dictionary<string, string> _ibNameMap = new Dictionary<string, string>();

        /// <summary>
        /// Constructs InteractiveBrokersSymbolMapper. Default parameters are used.
        /// </summary>
        public InteractiveBrokersSymbolMapper(IMapFileProvider mapFileProvider) :
            this(Path.Combine("InteractiveBrokers", "IB-symbol-map.json"))
        {
            _mapFileProvider = mapFileProvider;
        }

        /// <summary>
        /// Constructs InteractiveBrokersSymbolMapper
        /// </summary>
        /// <param name="ibNameMap">New names map (IB -> LEAN)</param>
        public InteractiveBrokersSymbolMapper(Dictionary<string, string> ibNameMap)
        {
            _ibNameMap = ibNameMap;
        }

        /// <summary>
        /// Constructs InteractiveBrokersSymbolMapper
        /// </summary>
        /// <param name="ibNameMapFullName">Full file name of the map file</param>
        public InteractiveBrokersSymbolMapper(string ibNameMapFullName)
        {
            if (File.Exists(ibNameMapFullName))
            {
                _ibNameMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(ibNameMapFullName));
            }
        }
        /// <summary>
        /// Converts a Lean symbol instance to an InteractiveBrokers symbol
        /// </summary>
        /// <param name="symbol">A Lean symbol instance</param>
        /// <returns>The InteractiveBrokers symbol</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol?.Value))
                throw new ArgumentException("Invalid symbol: " + (symbol == null ? "null" : symbol.ToString()));

            var ticker = GetMappedTicker(symbol);

            if (string.IsNullOrWhiteSpace(ticker))
                throw new ArgumentException("Invalid symbol: " + symbol.ToString());

            if (symbol.ID.SecurityType != SecurityType.Forex &&
                symbol.ID.SecurityType != SecurityType.Equity &&
                symbol.ID.SecurityType != SecurityType.Option &&
                symbol.ID.SecurityType != SecurityType.FutureOption &&
                symbol.ID.SecurityType != SecurityType.Future)
                throw new ArgumentException("Invalid security type: " + symbol.ID.SecurityType);

            if (symbol.ID.SecurityType == SecurityType.Forex && ticker.Length != 6)
                throw new ArgumentException("Forex symbol length must be equal to 6: " + symbol.Value);

            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Option:
                    // Final case is for equities. We use the mapped value to select
                    // the equity we want to trade.
                    return GetMappedTicker(symbol.Underlying);

                case SecurityType.FutureOption:
                    // We use the underlying Future Symbol since IB doesn't use
                    // the Futures Options' ticker, but rather uses the underlying's
                    // Symbol, mapped to the brokerage.
                    return GetBrokerageSymbol(symbol.Underlying);

                case SecurityType.Future:
                    return GetBrokerageRootSymbol(symbol.ID.Symbol);

                case SecurityType.Equity:
                    return ticker.Replace(".", " ");
            }

            return ticker;
        }

        /// <summary>
        /// Converts an InteractiveBrokers symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The InteractiveBrokers symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default(DateTime), decimal strike = 0, OptionRight optionRight = 0)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException("Invalid symbol: " + brokerageSymbol);

            if (securityType != SecurityType.Forex &&
                securityType != SecurityType.Equity &&
                securityType != SecurityType.Option &&
                securityType != SecurityType.Future &&
                securityType != SecurityType.FutureOption)
                throw new ArgumentException("Invalid security type: " + securityType);

            try
            {
                switch (securityType)
                {
                    case SecurityType.Future:
                        return Symbol.CreateFuture(GetLeanRootSymbol(brokerageSymbol), market, expirationDate);

                    case SecurityType.Option:
                        return Symbol.CreateOption(brokerageSymbol, market, OptionStyle.American, optionRight, strike, expirationDate);

                    case SecurityType.FutureOption:
                        var canonicalFutureSymbol = Symbol.Create(GetLeanRootSymbol(brokerageSymbol), SecurityType.Future, market);
                        var futureContractMonth = FuturesOptionsExpiryFunctions.GetFutureContractMonth(canonicalFutureSymbol, expirationDate);
                        var futureExpiry = FuturesExpiryFunctions.FuturesExpiryFunction(canonicalFutureSymbol)(futureContractMonth);

                        return Symbol.CreateOption(
                            Symbol.CreateFuture(
                                brokerageSymbol,
                                market,
                                futureExpiry),
                            market,
                            OptionStyle.American,
                            optionRight,
                            strike,
                            expirationDate);

                    case SecurityType.Equity:
                        brokerageSymbol = brokerageSymbol.Replace(" ", ".");
                        break;
                }

                return Symbol.Create(brokerageSymbol, securityType, market);
            }
            catch (Exception)
            {
                throw new ArgumentException($"Invalid symbol: {brokerageSymbol}, security type: {securityType}, market: {market}.");
            }
        }

        /// <summary>
        /// IB specific versions of the symbol mapping (GetBrokerageRootSymbol) for future root symbols
        /// </summary>
        /// <param name="rootSymbol">LEAN root symbol</param>
        /// <returns></returns>
        public string GetBrokerageRootSymbol(string rootSymbol)
        {
            var brokerageSymbol = _ibNameMap.FirstOrDefault(kv => kv.Value == rootSymbol);

            return brokerageSymbol.Key ?? rootSymbol;
        }

        /// <summary>
        /// IB specific versions of the symbol mapping (GetLeanRootSymbol) for future root symbols
        /// </summary>
        /// <param name="brokerageRootSymbol">IB Brokerage root symbol</param>
        /// <returns></returns>
        public string GetLeanRootSymbol(string brokerageRootSymbol)
        {
            return _ibNameMap.ContainsKey(brokerageRootSymbol) ? _ibNameMap[brokerageRootSymbol] : brokerageRootSymbol;
        }

        private string GetMappedTicker(Symbol symbol)
        {
            var ticker = symbol.Value;
            if (symbol.ID.SecurityType == SecurityType.Equity)
            {
                var mapFile = _mapFileProvider.Get(symbol.ID.Market).ResolveMapFile(symbol.ID.Symbol, symbol.ID.Date);
                ticker = mapFile.GetMappedSymbol(DateTime.UtcNow, symbol.Value);
            }

            return ticker;
        }
    }
}
