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
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.Results
{
    /// <summary>
    /// Monitors and reports the progress of a backtest
    /// </summary>
    public class BacktestProgressMonitor
    {
        private IAlgorithm _algorithm;

        private readonly DateTime _initialTime;

        /// <summary>
        /// The total days the algorithm will run
        /// </summary>
        public int TotalDays { get; private set; }

        /// <summary>
        /// The current days the algorithm has been running for
        /// </summary>
        public int ProcessedDays { get; private set; }

        /// <summary>
        /// Gets the current progress of the backtest
        /// </summary>
        public decimal Progress
        {
            get { return (decimal)ProcessedDays / TotalDays; }
        }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="algorithm">The algorithm the backtest is running for</param>
        public BacktestProgressMonitor(IAlgorithm algorithm)
        {
            _algorithm = algorithm;
            _initialTime = _algorithm.Time;
            TotalDays = Convert.ToInt32((_algorithm.EndDate.Date - _initialTime.Date).TotalDays) + 1;
        }

        /// <summary>
        /// Recalculates backtest passed/processed days
        /// </summary>
        /// <returns>The processed days count after recalculation</returns>
        public int RecalculateProcessedDays()
        {
            // We use 'int' so it's thread safe
            ProcessedDays = (int)(_algorithm.Time - _initialTime).TotalDays;
            return ProcessedDays;
        }
    }
}
