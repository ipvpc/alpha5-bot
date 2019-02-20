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

'''
    
In a perfect market, you could buy 100 EUR worth of USD, sell 100 EUR worth of GBP,
and then use the GBP to buy USD and wind up with the same amount in USD as you received when
you bought them with EUR. This relationship is expressed by the Triangle Exchange Rate, which is

    Triangle Exchange Rate = (A/B) * (B/C) * (C/A)
    
where (A/B) is the exchange rate of A-to-B. In a perfect market, TER = 1, and so when
there is a mispricing in the market, then TER will not be 1 and there exists an arbitrage opportunity.

This Alpha Model is an implementation of this theory.



This alpha is part of the Benchmark Alpha Series created by QuantConnect which are open
sourced so the community and client funds can see an example of an alpha.
    
'''

from clr import AddReference
AddReference("System")
AddReference("QuantConnect.Algorithm")
AddReference("QuantConnect.Common")

from System import *
from QuantConnect import *
from QuantConnect.Algorithm import *
from QuantConnect.Algorithm.Framework import *
from QuantConnect.Algorithm.Framework.Risk import *
from QuantConnect.Orders.Fees import ConstantFeeModel
from QuantConnect.Algorithm.Framework.Alphas import *
from QuantConnect.Algorithm.Framework.Selection import *
from QuantConnect.Algorithm.Framework.Execution import *
from QuantConnect.Algorithm.Framework.Portfolio import PortfolioTarget, EqualWeightingPortfolioConstructionModel

from datetime import datetime, timedelta

class TriangleArbitrageAlgorithm(QCAlgorithmFramework):

    def Initialize(self):
        
        self.SetStartDate(2019, 1, 1)   #Set Start Date
        self.SetCash(100000)           #Set Strategy Cash
        
        ## Select trio of currencies to trade where
        ## Currency A = USD
        ## Currency B = EUR
        ## Currency C = GBP
        currencies = ['EURUSD','EURGBP','GBPUSD']
        symbols = [ Symbol.Create(currency, SecurityType.Forex, Market.Oanda) for currency in currencies]

        ## Manual universe selection with tick-resolution data
        self.Universe.Resolution = Resolution.Tick
        self.SetUniverseSelection( ManualUniverseSelectionModel(symbols) )

        ## Set $0 fees
        self.SetSecurityInitializer(lambda security: security.SetFeeModel(ConstantFeeModel(0)))

        ## Set custom Alpha Model
        self.SetAlpha(ForexTriangleArbitrageAlphaModel(currencies))
        
        self.SetPortfolioConstruction(EqualWeightingPortfolioConstructionModel())
        
        self.SetExecution(ImmediateExecutionModel())
        
        self.SetRiskManagement(NullRiskManagementModel())
        
    
    
class ForexTriangleArbitrageAlphaModel:
    
    def __init__(self, currencies):
        self.TriangleRate = 0
        self.currency_a = currencies[0]
        self.currency_b = currencies[1]
        self.currency_c = currencies[2]

    def Update(self, algorithm, data):
        insights = []
        
        ## Extract QuoteBars for all three Forex securities
        bar_a = data[self.currency_a]
        bar_b = data[self.currency_b]
        bar_c = data[self.currency_c]

        ## Calculate the triangle exchange rate
        self.TriangleRate = self.CalculateTriangleRate(bar_a, bar_b, bar_c)
        algorithm.Log(str(self.TriangleRate))
        
        ## If the triangle rate is significantly different than 1, then emit insights
        if self.TriangleRate > 1.00015:
            insights.append(Insight(self.currency_a, timedelta(seconds=5), InsightType.Price, InsightDirection.Up, 0.0001, None))
            insights.append(Insight(self.currency_b, timedelta(seconds=5), InsightType.Price, InsightDirection.Down, 0.0001, None))
            insights.append(Insight(self.currency_c, timedelta(seconds=5), InsightType.Price, InsightDirection.Up, 0.0001, None))
        
        return insights

    def CalculateTriangleRate(self, bar_a, bar_b, bar_c):
        
        ## Bid(Currency A -> Currency B) * Bid(Currency B -> Currency C) * Bid(Currency C -> Currency A)
        ## If exchange rates are priced perfectly, then this yield 1. If it is different than 1, then an arbitrage opportunity exists
        return bar_a.Bid.Close * (1/bar_b.Bid.Close) * (1/bar_c.Bid.Close)
        
    def OnSecuritiesChanged(self, algorithm, changes):
        
        ## Set fees = 0 tom better mimic HFT
        for security in changes.AddedSecurities:
            security.FeeModel = ConstantFeeModel(0)