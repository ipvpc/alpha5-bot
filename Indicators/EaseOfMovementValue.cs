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
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the n-period Ease of Movement Value using the following:
    /// MID = (high_1 + low_1)/2 - (high_0 + low_0)/2 
    /// RATIO = (currentVolume/10000) / (high_1 - low_1)
    /// EMV = MID/ratio
    /// </summary>
    public class EaseOfMovementValue : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {

        private decimal _previousHighMaximum;
        private decimal _previousLowMinimum;

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples >= 2;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => 2;


        public override void Reset()
        {
            _previousHighMaximum = 0.0m;
            _previousLowMinimum = 0.0m;
            base.Reset();
        }

        /// <summary>
        /// Initializeds a new instance of the EaseOfMovement class using the specufued period
        /// </summary>
        /// <param name="period">The period over which to perform to computation</param>
        public EaseOfMovementValue()
            : this("EMV()")
        {
        }
        /// <summary>
        /// Creates a new EaseOfMovement indicator with the specified period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        public EaseOfMovementValue(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A a value for this indicator</returns>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            if (_previousHighMaximum == null) _previousHighMaximum = input.High;
            if (_previousLowMinimum == null) _previousLowMinimum = input.Low;
            if (input.Volume == 0 || input.High == input.Low)
            {
                return 0;
            }
            var midValue = ((input.High + input.Low) / 2) - ((_previousHighMaximum + _previousLowMinimum) / 2);
            var midRatio = ((input.Volume / 10000) / (input.High - input.Low));
            _previousHighMaximum = input.High;
            _previousLowMinimum = input.Low;
            return midValue / midRatio;
        }
    }
}