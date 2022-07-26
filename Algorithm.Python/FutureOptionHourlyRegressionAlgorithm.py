﻿# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
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
# limitations under the License

from AlgorithmImports import *

### <summary>
### This regression algorithm tests using FutureOptions daily resolution
### </summary>
class FutureOptionHourlyRegressionAlgorithm(QCAlgorithm):

    def Initialize(self):
        self.SetStartDate(2012, 1, 3)
        self.SetEndDate(2012, 1, 4)
        resolution = Resolution.Hour

        # Add our underlying future contract
        self.dc = self.AddFutureContract(
            Symbol.CreateFuture(
                Futures.Dairy.ClassIIIMilk,
                Market.CME,
                datetime(2012, 4, 1)
            ),
            resolution,
            extendedMarketHours=True).Symbol

        # Attempt to fetch a specific ITM future option contract
        dcOptions = [
            self.AddFutureOptionContract(x, resolution, extendedMarketHours=True).Symbol for x in (self.OptionChainProvider.GetOptionContractList(self.dc, self.Time)) if x.ID.StrikePrice == 17 and x.ID.OptionRight == OptionRight.Call
        ]
        self.dcOption = dcOptions[0]

        # Validate it is the expected contract
        expectedContract = Symbol.CreateOption(self.dc, Market.CME, OptionStyle.American, OptionRight.Call, 17, datetime(2012, 4, 1))
        if self.dcOption != expectedContract:
            raise AssertionError(f"Contract {self.dcOption} was not the expected contract {expectedContract}")

        # Schedule a purchase of this contract at noon
        self.Schedule.On(self.DateRules.Today, self.TimeRules.Noon, self.ScheduleCallbackBuy)

        # Schedule liquidation at 2pm
        self.Schedule.On(self.DateRules.Today, self.TimeRules.At(14,0,0), self.ScheduleCallbackLiquidate)

    def ScheduleCallbackBuy(self):
        self.ticket = self.MarketOrder(self.dcOption, 1)

    def OnData(self, slice):
        # Assert we are only getting data at 7PM (12AM UTC)
        if slice.Time.minute != 0:
            raise AssertionError(f"Expected data only on hourly intervals; instead was {slice.Time}")

    def ScheduleCallbackLiquidate(self):
        self.Liquidate()

    def OnEndOfAlgorithm(self):
        if self.Portfolio.Invested:
            raise AssertionError(f"Expected no holdings at end of algorithm, but are invested in: {', '.join([str(i.ID) for i in self.Portfolio.Keys])}")

        if self.ticket.Status != OrderStatus.Filled:
            raise AssertionError("Future option order failed to fill correctly")
