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

namespace QuantConnect
{
    /// <summary>
    /// Single chart series point/bar data.
    /// </summary>
    public interface ISeriesPoint
    {
        /// <summary>
        /// Time of this chart series point
        /// </summary>
        DateTime Time { get; set; }

        /// <summary>
        /// List of values for this chart series point
        /// </summary>
        /// <remarks>
        /// A single (x, y) value is represented as a list of length 1, with x being the <see cref="Time"/> and y being the value.
        /// On the other hand, a candlestick is represented as a list of length 4, with the values being (open, high, low, close).
        /// </remarks>
        List<decimal> Values { get; }

        /// <summary>
        /// Clone implementation for ISeriesPoint
        /// </summary>
        /// <returns>Clone of the series</returns>
        ISeriesPoint Clone();
    }
}
