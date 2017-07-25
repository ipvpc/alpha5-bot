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
using QuantConnect.Data.Custom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace QuantConnect.ToolBox.FxVolumeDownloader
{
    /// <summary>
    ///     FXCM Real FOREX Volume/Transactions Downloader Toolbox Project For LEAN Algorithmic Trading Engine.
    /// </summary>
    /// <seealso cref="QuantConnect.ToolBox.IDataDownloader" />
    public class ForexVolumeDownloader : IDataDownloader
    {
        private enum FxcmSymbolId
        {
            EURUSD = 1,
            USDJPY = 2,
            GBPUSD = 3,
            USDCHF = 4,
            EURCHF = 5,
            AUDUSD = 6,
            USDCAD = 7,
            NZDUSD = 8,
            EURGBP = 9,
            EURJPY = 10,
            GBPJPY = 11,
            EURAUD = 14,
            EURCAD = 15,
            AUDJPY = 17
        }

        #region Fields

        /// <summary>
        ///     Integer representing client version.
        /// </summary>
        private readonly int _ver = 1;

        /// <summary>
        ///     FXCM session id.
        /// </summary>
        private readonly string _sid = "quantconnect";

        /// <summary>
        ///     The columns index which should be added to obtain the transactions.
        /// </summary>
        private readonly long[] _transactionsIdx = { 27, 29, 31, 33 };

        /// <summary>
        ///     The columns index which should be added to obtain the volume.
        /// </summary>
        private readonly int[] _volumeIdx = { 26, 28, 30, 32 };

        /// <summary>
        ///     The request base URL.
        /// </summary>
        private readonly string _baseUrl = " http://marketsummary2.fxcorporate.com/ssisa/servlet?RT=SSI";

        private readonly string _dataDirectory;

        #endregion Fields

        #region Public Methods

        /// <summary>
        ///     Initializes a new instance of the <see cref="ForexVolumeDownloader" /> class.
        /// </summary>
        /// <param name="dataDirectory">The Lean data directory.</param>
        public ForexVolumeDownloader(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
        }

        /// <summary>
        ///     Method in charge of making all the steps to save the data. It makes the request, parses the data and saves it.
        ///     This method takes into account the size limitation of the responses, slicing big request into smaller ones.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="resolution">The resolution.</param>
        /// <param name="startUtc">The start UTC.</param>
        /// <param name="endUtc">The end UTC.</param>
        public void Run(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc)
        {
            var data = new List<BaseData>();
            var requestDayInterval = 0;
            var writer = new LeanDataWriter(resolution, symbol, _dataDirectory);
            var intermediateStartDate = startUtc;
            var intermediateEndDate = endUtc;

            // As the responses has a Limit of 10000 lines, hourly data the minute data request should be sliced.
            if (resolution == Resolution.Minute && (endUtc - startUtc).TotalMinutes > 10000)
            {
                // Six days are 8640 minute observations, 7 days are 10080.
                requestDayInterval = 6;
            }
            else if (resolution == Resolution.Hour && (endUtc - startUtc).TotalHours > 10000)
            {
                // 410 days x 24 hr = 9840 hr.
                requestDayInterval = 410;
            }

            var counter = 0;
            do
            {
                if (requestDayInterval != 0)
                {
                    if (counter++ != 0)
                    {
                        intermediateStartDate = intermediateEndDate.AddDays(value: 1);
                    }
                    intermediateEndDate = intermediateStartDate.AddDays(requestDayInterval);
                    if (intermediateEndDate > endUtc) intermediateEndDate = endUtc;
                }
                data.AddRange(Get(symbol, resolution, intermediateStartDate, intermediateEndDate));
                // If we have 300k observations in memory, write it.
                if (resolution == Resolution.Minute && counter % 30 == 0)
                {
                    writer.Write(data);
                    data.Clear();
                }
            } while (intermediateEndDate != endUtc);
            // Seems the data has som duplicate values! this makes the writer throw an error.
            var duplicates = data.GroupBy(x => x.Time)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count != 0)
            {
                foreach (var duplicate in duplicates)
                {
                    data.RemoveAll(o => o.Time == duplicate);
                }
            }
            writer.Write(data);
        }

        /// <summary>
        ///     Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="symbol">Symbol for the data we're looking for.</param>
        /// <param name="resolution">Resolution of the data request</param>
        /// <param name="startUtc">Start time of the data in UTC</param>
        /// <param name="endUtc">End time of the data in UTC</param>
        /// <returns>
        ///     Enumerable of base data for this symbol
        /// </returns>
        public IEnumerable<BaseData> Get(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc)
        {
            startUtc = startUtc.AddDays(value: -1);
            var requestedData = new List<BaseData>();
            var lines = RequestData(symbol, resolution, startUtc, endUtc);
            foreach (var line in lines)
            {
                var obs = line.Split(';');
                // Skip the first line
                if (obs.Length != 35) continue;
                var stringDate = obs[0].Substring(startIndex: 3);
                var obsTime = DateTime.ParseExact(stringDate, "yyyyMMddHHmm",
                    DateTimeFormatInfo.InvariantInfo);
                var volume = _volumeIdx.Select(x => long.Parse(obs[x])).Sum();

                var transactions = _transactionsIdx.Select(x => int.Parse(obs[x])).Sum();
                requestedData.Add(new ForexVolume
                {
                    Symbol = symbol,
                    Time = obsTime,
                    Value = volume,
                    Transactions = transactions
                });
            }
            return requestedData;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        ///     Gets the FXCM identifier from a FOREX pair symbol.
        /// </summary>
        /// <param name="symbol">The pair symbol.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">Volume data is not available for the selected symbol. - symbol</exception>
        private int GetFxcmIDFromSymbol(Symbol symbol)
        {
            int symbolId;
            try
            {
                symbolId = (int)Enum.Parse(typeof(FxcmSymbolId), symbol.Value);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("Volume data is not available for the selected symbol.", "symbol");
            }
            return symbolId;
        }

        /// <summary>
        ///     Gets the string interval representation from the resolution.
        /// </summary>
        /// <param name="resolution">The requested resolution.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        ///     resolution - tick or second resolution are not supported for Forex
        ///     Volume.
        /// </exception>
        private string GetIntervalFromResolution(Resolution resolution)
        {
            string interval;
            switch (resolution)
            {
                case Resolution.Minute:
                    interval = "M1";
                    break;

                case Resolution.Hour:
                    interval = "H1";
                    break;

                case Resolution.Daily:
                    interval = "D1";
                    break;

                default:
                    throw new ArgumentOutOfRangeException("resolution", resolution,
                        "tick or second resolution are not supported for Forex Volume.");
            }
            return interval;
        }

        /// <summary>
        ///     Generates the API Requests.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="resolution">The resolution.</param>
        /// <param name="startUtc">The start date in UTC.</param>
        /// <param name="endUtc">The end date in UTC.</param>
        /// <returns></returns>
        private string[] RequestData(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc)
        {
            var symbolId = GetFxcmIDFromSymbol(symbol);
            var interval = GetIntervalFromResolution(resolution);
            var startDate = startUtc.ToString("yyyyMMdd") + "2100";
            var endDate = endUtc.ToString("yyyyMMdd") + "2100";

            var request = string.Format("{0}&ver={1}&sid={2}&interval={3}&offerID={4}&timeFrom={5}&timeTo={6}",
                _baseUrl, _ver, _sid, interval, symbolId, startDate, endDate);

            string[] lines;
            using (var client = new WebClient())
            {
                var data = client.DownloadString(request);
                lines = data.Split('\n');
            }
            return lines;
        }

        #endregion Private Methods
    }
}