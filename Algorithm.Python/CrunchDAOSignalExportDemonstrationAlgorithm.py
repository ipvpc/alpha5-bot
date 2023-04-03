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

### <summary>
### This algorithm sends a list of current portfolio targets to CrunchDAO API every time
### the ema indicators crosses between themselves.
### </summary>
### <meta name="tag" content="using data" />
### <meta name="tag" content="using quantconnect" />
### <meta name="tag" content="securities and portfolio" />
class CrunchDAOSignalExportDemonstrationAlgorithm(QCAlgorithm):

    def Initialize(self):
        ''' Initialize the date and add one equity and one index, as CrunchDAO only accepts stock and index symbols '''

        self.SetStartDate(2013, 10, 7)   #Set Start Date
        self.SetEndDate(2013, 10, 11)    #Set End Date
        self.SetCash(100000)             #Set Strategy Cash

        self.targets = []

        self.spy = self.AddEquity("SPY").Symbol;
        self.spx = self.AddIndex("SPX").Symbol;

        # Create a new PortfolioTarget for each symbol, assign it an initial quantity of 0.05 and save it self.targets list
        self.targets.append(PortfolioTarget(self.spy, 0.05))
        self.targets.append(PortfolioTarget(self.spx, 0.05))

        fastPeriod = 100
        slowPeriod = 200

        self.fast = self.EMA("SPY", fastPeriod)
        self.slow = self.EMA("SPY", slowPeriod)

        # Initialize these flags, to check when the ema indicators crosses between themselves
        self.emaFastIsNotSet = True;
        self.emaFastWasAbove = False;

        # Set the CrunchDAO signal export provider
        self.crunchDAOApiKey = "" # Replace this value with your CrunchDAO API key
        self.crunchDAOModel = "" # Replace this value with your model's name
        self.crunchDAOSubmissionName = "" # Replace this value with the name for your submission (Optional)
        self.crunchDAOComment = "" # Replace this value with a comment for your submission (Optional)
        self.SignalExport.AddSignalExportProviders(CrunchDAOSignalExport(self.crunchDAOApiKey, self.crunchDAOModel, self.crunchDAOSubmissionName, self.crunchDAOComment))

    def OnData(self, data):
        ''' Reduce the quantity of holdings for one security and increase the holdings to the another
        one when the EMA's indicators crosses between themselves, then send a signal to CrunchDAO API '''

        # Wait for our indicators to be ready
        if not self.fast.IsReady or not self.slow.IsReady:
            return

        fast = self.fast.Current.Value
        slow = self.slow.Current.Value

        # Set the value of flag _emaFastWasAbove, to know when the ema indicators crosses between themselves
        if self.emaFastIsNotSet == True:
            if fast > slow *1.001:
                self.emaFastWasAbove = True
            else:
                self.emaFastWasAbove = False
            self.emaFastIsNotSet = False;

        # Check whether ema fast and ema slow crosses. If they do, set holdings to SPY
        # or reduce its holdings,update their values in self.targets and send signals
        # to the CrunchDAO API from self.targets
        if fast > slow * 1.001 and (not self.emaFastWasAbove):
            self.SetHoldings("SPY", 0.1)
            self.SetHoldings("SPX", 0.01)
            self.targets[0] = PortfolioTarget(self.spy, 0.1)
            self.targets[1] = PortfolioTarget(self.spx, 0.01)
            self.SignalExport.SetTargetPortfolio(self.targets)
        elif fast < slow * 0.999 and (self.emaFastWasAbove):
            self.SetHoldings("SPY", 0.01)
            self.SetHoldings("SPX", 0.1)
            self.targets[0] = PortfolioTarget(self.spy, 0.01)
            self.targets[1] = PortfolioTarget(self.spx, 0.1)
            self.SignalExport.SetTargetPortfolio(self.targets)