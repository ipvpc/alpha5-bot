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

namespace QuantConnect.Indicators
{
/// <summary>
/// Represents an indicator capable of predicting new values given previous data from a window.
/// Source: https://tulipindicators.org/tsf
/// </summary>
public class TimeSeriesForecast : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
{
    /// <summary>
    /// Creates a new TimeSeriesForecast indicator with the specified period
    /// </summary>
    /// <param name="name">The name of this indicator</param>
    /// <param name="period">The period over which to look back</param>
    public TimeSeriesForecast(string name, int period)
        : base(name, period)
    {
    }

    /// <summary>
    /// Creates a new TimeSeriesForecast indicator with the specified period
    /// </summary>
    /// <param name="period">The period over which to look back</param>
    public TimeSeriesForecast(int period)
        : base($"TSF{period})", period)
    {
    }

    /// <inheritdoc />
    protected override decimal ComputeNextValue(
        IReadOnlyWindow<IndicatorDataPoint> window,
        IndicatorDataPoint input
        )
    {
        if (window.Count < Period)
        {
            throw new ArgumentException("Length of data is smaller than period.");
        }
        
        decimal x1 = Period;
        decimal x2 = Period * Period;
        decimal xy = 0;
        decimal y = 0;

        for (var i = 0; i < Period; i++)
        {
            x1 += i + 1;
            x2 += (i + 1) * (i + 1);
            xy += window[i].Value * (i + 1);
            y += window[i].Value;
        }

        var bd = 1 / (Period * x2 - x1 * x1);
        var b = (Period * xy - x1 * y) * bd;
        var a = (y - b * x1) * Period;
        
        return a + b * Period + 1;
    }
}
}
