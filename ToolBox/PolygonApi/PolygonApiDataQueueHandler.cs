﻿using System;
using System.Collections.Generic;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Packets;

namespace QuantConnect.ToolBox.PolygonApi
{
    /// <summary>
    /// An implementation of <see cref="IDataQueueHandler"/> for PolygonAPI
    /// </summary>
    public class PolygonApiDataQueueHandler : IDataQueueHandler, IDisposable
    {

        private const string WebSocketUrl = "wss://socket.polygon.io/stocks";
        private const string restUrl = "https://api.polygon.io/";

        private readonly string _apiKey = Config.Get("polygon-api-key");
        private readonly WebSocketWrapper _webSocket = new WebSocketWrapper();
        private readonly object _locker = new object();
        private readonly List<Tick> _ticks = new List<Tick>();
        private readonly DefaultConnectionHandler _connectionHandler = new DefaultConnectionHandler();

        public PolygonApiDataQueueHandler()
        {
            _connectionHandler.ConnectionLost += OnConnectionLost;
            _connectionHandler.ConnectionRestored += OnConnectionRestored;
            _connectionHandler.ReconnectRequested += OnReconnectRequested;

            _connectionHandler.Initialize(string.Empty);

            _webSocket.Initialize(WebSocketUrl);

            _webSocket.Message += (s, m) => EnqueueMessage(m);

            _webSocket.Connect();

            //new Thread(new ThreadStart(MessagesProcessorThread)).Start();
        }

        private void EnqueueMessage(WebSocketMessage m)
        {
            throw new NotImplementedException();
        }

        private void OnReconnectRequested(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnConnectionRestored(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {

            if (_webSocket.IsOpen)
            {
                _webSocket.Close();
            }
        }

        public IEnumerable<BaseData> GetNextTicks()
        {
            throw new NotImplementedException();
        }

        public void Subscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }
    }
}
