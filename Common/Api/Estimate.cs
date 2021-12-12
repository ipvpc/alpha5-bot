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

using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// Estimate response packet from the QuantConnect.com API.
    /// </summary>
    public class Estimate
    {
        /// <summary>
        /// Estimate id
        /// </summary>
        [JsonProperty(PropertyName = "estimateId")]
        public string EstimateId;

        /// <summary>
        /// Estimate time
        /// </summary>
        [JsonProperty(PropertyName = "time")]
        public int Time;

        /// <summary>
        /// Estimate balance
        /// </summary>
        [JsonProperty(PropertyName = "balance")]
        public int Balance;
    }

    /// <summary>
    /// Wrapper class for Optimizations/* endpoints JSON response
    /// Currently used by Optimizations/Estimate
    /// </summary>
    public class EstimateResponseWrapper : RestResponse
    {
        /// <summary>
        /// Estimate object
        /// </summary>
        [JsonProperty(PropertyName = "estimate")]
        public Estimate Estimate;
    }
}
