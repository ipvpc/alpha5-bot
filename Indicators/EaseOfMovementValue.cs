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

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the n-period Ease of Movement Value in three values using the following:
    /// MID = (high_n + low_n)/2 - (high_0 + low_0)/2 
    /// RATIO = (currentVolume/10000) / (high_n - low_n)
    /// EMV = MID/ratio
    /// </summary>
    public class EaseOfMovement : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        
        public IndicatorBase<IndicatorDataPoint> High { get; }

        public IndicatorBase<IndicatorDataPoint> Low { get; }

        public IndicatorBase<IndicatorDataPoint> Volume { get; }

        /// <summary>
        /// Creates a new RateOfChange indicator with the specified period
        /// </summary>
        /// <param name="period">The period over which to perform to computation</param>
        public EaseOfMovement(int period)
            : base($"EMV({period})", period)
        {
        }

        /// <summary>
        /// Creates a new RateOfChange indicator with the specified period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period over which to perform to computation</param>
        public EaseOfMovement(string name, int period)
            : base(name)
        {
            WarmUpPeriod = period;
            High = new Identity(name + "_High");
            Low = new Identity(name + "_Low");
            Volume = new Identity(name + "_Volume");
        }

        public override bool IsReady => High.IsReady && Low.IsReady && Volume.IsReady;

        public int WarmUpPeriod { get; }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // if we're not ready just grab the first input point in the window
            High.Update(input.Time, Math.Max(input.High, Math.Max(Open, Close)));
            Low.Update(input.Time, Math.Min(input.Low, Math.Min(Open, Close)));
            
            var previous = window.Samples <= window.Size ? window[window.Count - 1] : window.MostRecentlyRemoved;

            if (previous == 0)
            {
                return 0;
            }

            var value = base.ComputeNextValue(input);

            var mid = ((input.High + input.Low)/2) - ((previous.High + previous.Low) / 2);
            var ratio = (input.Volume/10000) / (input.High - input.Low);
            var emvValue = mid / ratio;

            return emvValue;
        }
    }
}