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
using System.Linq;
using System.Threading;
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Handles brokerage data subscriptions with multiple websocket connections, with optional symbol weighting
    /// </summary>
    public class BrokerageMultiWebSocketSubscriptionManager : EventBasedDataQueueHandlerSubscriptionManager
    {
        private readonly string _webSocketUrl;
        private readonly int _maximumSymbolsPerWebSocket;
        private readonly int _maximumWebSocketConnections;
        private readonly Func<WebSocketClientWrapper> _webSocketFactory;
        private readonly Action<IWebSocket, Symbol, TickType> _subscribeFunc;
        private readonly Action<IWebSocket, Symbol, TickType> _unsubscribeFunc;
        private readonly BrokerageConcurrentMessageHandler<WebSocketMessage> _messageHandler;
        private readonly RateGate _connectionRateLimiter;

        private const int ConnectionTimeout = 30000;

        private readonly object _locker = new();
        private readonly List<BrokerageMultiWebSocketEntry> _webSocketEntries = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="BrokerageMultiWebSocketSubscriptionManager"/> class
        /// </summary>
        /// <param name="webSocketUrl">The URL for websocket connections</param>
        /// <param name="maximumSymbolsPerWebSocket">The maximum number of symbols per websocket connection</param>
        /// <param name="maximumWebSocketConnections">The maximum number of websocket connections allowed (if zero, symbol weighting is disabled)</param>
        /// <param name="connectionRateLimiter">The rate limiter for creating new websocket connections</param>
        /// <param name="symbolWeights">A dictionary for the symbol weights</param>
        /// <param name="webSocketFactory">A function which returns a new websocket instance</param>
        /// <param name="subscribeFunc">A function which subscribes a symbol</param>
        /// <param name="unsubscribeFunc">A function which unsubscribes a symbol</param>
        /// <param name="messageHandler">The websocket message handler</param>
        public BrokerageMultiWebSocketSubscriptionManager(
            string webSocketUrl,
            int maximumSymbolsPerWebSocket,
            int maximumWebSocketConnections,
            Dictionary<Symbol, int> symbolWeights,
            Func<WebSocketClientWrapper> webSocketFactory,
            Action<IWebSocket, Symbol, TickType> subscribeFunc,
            Action<IWebSocket, Symbol, TickType> unsubscribeFunc,
            BrokerageConcurrentMessageHandler<WebSocketMessage> messageHandler,
            RateGate connectionRateLimiter = null)
        {
            _webSocketUrl = webSocketUrl;
            _maximumSymbolsPerWebSocket = maximumSymbolsPerWebSocket;
            _maximumWebSocketConnections = maximumWebSocketConnections;
            _webSocketFactory = webSocketFactory;
            _subscribeFunc = subscribeFunc;
            _unsubscribeFunc = unsubscribeFunc;
            _messageHandler = messageHandler;
            _connectionRateLimiter = connectionRateLimiter;

            if (_maximumWebSocketConnections > 0)
            {
                // symbol weighting enabled, create all websocket instances
                for (var i = 0; i < _maximumWebSocketConnections; i++)
                {
                    _webSocketEntries.Add(new BrokerageMultiWebSocketEntry(symbolWeights, _webSocketFactory()));
                }
            }
        }

        /// <summary>
        /// Subscribes to the symbols
        /// </summary>
        /// <param name="symbols">Symbols to subscribe</param>
        /// <param name="tickType">Type of tick data</param>
        protected override bool Subscribe(IEnumerable<Symbol> symbols, TickType tickType)
        {
            Log.Trace($"BrokerageMultiWebSocketSubscriptionManager.Subscribe(): {string.Join(",", symbols.Select(x => x.Value))}");

            foreach (var symbol in symbols)
            {
                var webSocket = GetWebSocketForSymbol(symbol);

                _subscribeFunc(webSocket, symbol, tickType);
            }

            return true;
        }

        /// <summary>
        /// Unsubscribes from the symbols
        /// </summary>
        /// <param name="symbols">Symbols to subscribe</param>
        /// <param name="tickType">Type of tick data</param>
        protected override bool Unsubscribe(IEnumerable<Symbol> symbols, TickType tickType)
        {
            Log.Trace($"BrokerageMultiWebSocketSubscriptionManager.Unsubscribe(): {string.Join(",", symbols.Select(x => x.Value))}");

            foreach (var symbol in symbols)
            {
                var entry = GetWebSocketEntryBySymbol(symbol);
                if (entry != null)
                {
                    entry.RemoveSymbol(symbol);

                    _unsubscribeFunc(entry.WebSocket, symbol, tickType);
                }
            }

            return true;
        }

        private BrokerageMultiWebSocketEntry GetWebSocketEntryBySymbol(Symbol symbol)
        {
            lock (_locker)
            {
                foreach (var entry in _webSocketEntries.Where(entry => entry.Contains(symbol)))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a symbol to an existing or new websocket connection
        /// </summary>
        private IWebSocket GetWebSocketForSymbol(Symbol symbol)
        {
            lock (_locker)
            {
                if (_webSocketEntries.All(x => x.SymbolCount >= _maximumSymbolsPerWebSocket))
                {
                    if (_maximumWebSocketConnections > 0)
                    {
                        throw new NotSupportedException($"Maximum symbol count reached for the current configuration [MaxSymbolsPerWebSocket={_maximumSymbolsPerWebSocket}, MaxWebSocketConnections:{_maximumWebSocketConnections}]");
                    }

                    // symbol limit reached on all, create new websocket instance
                    var webSocket = _webSocketFactory();
                    _webSocketEntries.Add(new BrokerageMultiWebSocketEntry(webSocket));
                }

                // sort by weight ascending, taking into account the symbol limit per websocket
                _webSocketEntries.Sort((x, y) =>
                    x.SymbolCount >= _maximumSymbolsPerWebSocket
                    ? 1
                    : y.SymbolCount >= _maximumSymbolsPerWebSocket
                        ? -1
                        : Math.Sign(x.TotalWeight - y.TotalWeight));

                var entry = _webSocketEntries.First();

                if (!entry.WebSocket.IsOpen)
                {
                    Connect(entry.WebSocket);
                }

                entry.AddSymbol(symbol);

                Log.Trace($"BrokerageMultiWebSocketSubscriptionManager.GetWeightedWebSocketForSymbol(): added symbol: {symbol} to websocket: {entry.WebSocket.GetHashCode()} - Count: {entry.SymbolCount}");

                return entry.WebSocket;
            }
        }

        private void Connect(IWebSocket webSocket)
        {
            webSocket.Initialize(_webSocketUrl);
            webSocket.Message += (s, e) => _messageHandler.HandleNewMessage(e);

            var connectedEvent = new ManualResetEvent(false);
            EventHandler onOpenAction = (_, _) =>
            {
                connectedEvent.Set();
            };

            webSocket.Open += onOpenAction;

            if (_connectionRateLimiter != null && !_connectionRateLimiter.WaitToProceed(TimeSpan.Zero))
            {
                _connectionRateLimiter.WaitToProceed();
            }

            try
            {
                webSocket.Connect();

                if (!connectedEvent.WaitOne(ConnectionTimeout))
                {
                    throw new Exception("BrokerageMultiWebSocketSubscriptionManager.Connect(): WebSocket connection timeout.");
                }
            }
            finally
            {
                webSocket.Open -= onOpenAction;

                connectedEvent.DisposeSafely();
            }
        }
    }
}
