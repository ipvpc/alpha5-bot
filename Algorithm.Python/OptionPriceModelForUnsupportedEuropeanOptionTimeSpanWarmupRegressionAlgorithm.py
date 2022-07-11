# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from AlgorithmImports import *
from OptionPriceModelForUnsupportedEuropeanOptionRegressionAlgorithm import OptionPriceModelForUnsupportedEuropeanOptionRegressionAlgorithm

### <summary>
### Regression algorithm excersizing an equity covered European style option, using an option price model
### that does not support European style options and asserting that the option price model is not used.
### </summary>
class OptionPriceModelForUnsupportedEuropeanOptionTimeSpanWarmupRegressionAlgorithm(OptionPriceModelForUnsupportedEuropeanOptionRegressionAlgorithm):
    def Initialize(self):
        OptionPriceModelForUnsupportedEuropeanOptionRegressionAlgorithm.Initialize(self)
        
        self.SetWarmup(TimeSpan.FromHours(24 * 9 + 23))
