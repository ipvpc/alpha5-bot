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
using System.IO;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Configuration;
using QuantConnect.Util;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Provides an implementation of <see cref="IMapFileProvider"/> that reads from a local zip file
    /// </summary>
    public class LocalZipMapFileProvider : IMapFileProvider
    {
        private readonly Dictionary<string, MapFileResolver> _cache;
        private IDataProvider _dataProvider;

        /// <summary>
        /// Initializes a new instance of <see cref="LocalDiskMapFileProvider"/> that uses configuration
        /// to resolve an instance of <see cref="IDataProvider"/> from the <see cref="Composer.Instance"/>
        /// </summary>s
        public LocalZipMapFileProvider()
            : this(Composer.Instance.GetExportedValueByTypeName<IDataProvider>(Config.Get("data-provider", "DefaultDataProvider")))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDiskFactorFileProvider"/> using the specified
        /// data provider
        /// </summary>
        /// <param name="dataProvider">The data provider to use to fetch the files</param>
        public LocalZipMapFileProvider(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
            _cache = new Dictionary<string, MapFileResolver>();
        }

        /// <summary>
        /// Initializes our MapFileProvider by supplying our dataProvider
        /// </summary>
        /// <param name="dataProvider">DataProvider to use</param>
        public void Initialize(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        /// <summary>
        /// Gets a <see cref="MapFileResolver"/> representing all the map files for the specified market
        /// </summary>
        /// <param name="market">The equity market, for example, 'usa'</param>
        /// <returns>A <see cref="MapFileResolver"/> containing all map files for the specified market</returns>
        public MapFileResolver Get(string market)
        {
            market = market.ToLowerInvariant();
            MapFileResolver result;
            // we use a lock so that only 1 thread loads the map file resolver while the rest wait
            // else we could have multiple threads loading the map file resolver at the same time!
            lock (_cache)
            {
                if (!_cache.TryGetValue(market, out result))
                {
                    _cache[market] = result = GetMapFileResolver(market);
                }
            }
            return result;
        }

        private MapFileResolver GetMapFileResolver(string market)
        {
            var timestamp = DateTime.UtcNow.ConvertFromUtc(TimeZones.NewYork);
            var todayNewYork = timestamp.Date;
            var yesterdayNewYork = todayNewYork.AddDays(-1);

            // start the search with yesterday, today's file will be available tomorrow
            var count = 0;
            var date = yesterdayNewYork;
            do
            {
                var zipFileName = Path.Combine(Globals.DataFolder, MapFileZipHelper.GetMapFileZipFileName(market, date));

                // Fetch a stream for our zip from our data provider
                var stream = _dataProvider.Fetch(zipFileName);

                // If we found a file we can read it 
                if (stream != null)
                {
                    Log.Trace("LocalZipMapFileProvider.Get({0}): Fetched map files for: {1} NY", market, date.ToShortDateString());
                    return new MapFileResolver(MapFileZipHelper.ReadMapFileZip(stream));
                }

                // prevent infinite recursion if something is wrong
                if (count++ > 30)
                {
                    throw new InvalidOperationException($"LocalZipMapFileProvider couldn't find any map files going all the way back to {date}");
                }

                date = date.AddDays(-1);
            }
            while (true);
        }
    }
}
