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
 *
*/

using System;
using NodaTime;
using System.IO;
using QuantConnect.Util;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Margin interest rate data source
    /// </summary>
    /// <remarks>This is useful to model margin costs</remarks>
    public class MarginInterestRate : BaseData
    {
        /// <summary>
        /// The interest rate value
        /// </summary>
        public decimal InterestRate { get; set; }

        /// <summary>
        /// Reader converts each line of the data source into BaseData objects. Each data type creates its own factory method, and returns a new instance of the object
        /// each time it is called. The returned object is assumed to be time stamped in the config.ExchangeTimeZone.
        /// </summary>
        /// <param name="config">Subscription data config setup object</param>
        /// <param name="stream">The data stream</param>
        /// <param name="date">Date of the requested data</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>Instance of the T:BaseData object generated by this line of the CSV</returns>
        public override BaseData Reader(SubscriptionDataConfig config, StreamReader stream, DateTime date, bool isLiveMode)
        {
            var dateTime = stream.GetDateTime("yyyyMMdd HH:mm:ss");
            var interestRate = stream.GetDecimal();
            return new MarginInterestRate {
                Time = dateTime,
                InterestRate = Value = interestRate,
                Symbol = config.Symbol,
                DataType = MarketDataType.Auxiliary
            };
        }

        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>String URL of source file.</returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            var identifier = config.Symbol.ID;
            var source = Path.Combine(Globals.DataFolder,
                identifier.SecurityType.SecurityTypeToLower(),
                identifier.Market.ToLowerInvariant(),
                "margin_interest",
                $"{identifier.Symbol.ToLowerInvariant()}.csv"
            );

            return new SubscriptionDataSource(source, SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
        }

        /// <summary>
        /// Specifies the data time zone for this data type. This is useful for custom data types
        /// </summary>
        public override DateTimeZone DataTimeZone()
        {
            return TimeZones.Utc;
        }

        /// <summary>
        /// Formats a string with the symbol and value.
        /// </summary>
        public override string ToString()
        {
            return $"{Symbol}: Rate {InterestRate}";
        }
    }
}