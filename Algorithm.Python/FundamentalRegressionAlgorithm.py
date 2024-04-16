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
### Demonstration of how to define a universe using the fundamental data
### </summary>
### <meta name="tag" content="using data" />
### <meta name="tag" content="universes" />
### <meta name="tag" content="coarse universes" />
### <meta name="tag" content="regression test" />
class FundamentalRegressionAlgorithm(QCAlgorithm):

    def initialize(self):
        self.set_start_date(2014, 3, 26)
        self.set_end_date(2014, 4, 7)

        self.universe_settings.resolution = Resolution.DAILY

        self._universe = self.add_universe(self.selection_function)

        # before we add any symbol
        self.assert_fundamental_universe_data()

        self.add_equity("SPY")
        self.add_equity("AAPL")

        # Request fundamental data for symbols at current algorithm time
        ibm = Symbol.create("IBM", SecurityType.EQUITY, Market.USA)
        ibmFundamental = self.fundamentals(ibm)
        if self.time != self.start_date or self.time != ibmFundamental.end_time:
            raise ValueError(f"Unexpected Fundamental time {ibmFundamental.end_time}")
        if ibmFundamental.price == 0:
            raise ValueError(f"Unexpected Fundamental IBM price!")
        nb = Symbol.create("NB", SecurityType.EQUITY, Market.USA)
        fundamentals = self.fundamentals([ nb, ibm ])
        if len(fundamentals) != 2:
            raise ValueError(f"Unexpected Fundamental count {len(fundamentals)}! Expected 2")

        # Request historical fundamental data for symbols
        history = self.history(Fundamental, TimeSpan(2, 0, 0, 0))
        if len(history) != 4:
            raise ValueError(f"Unexpected Fundamental history count {len(history)}! Expected 4")

        for ticker in [ "AAPL", "SPY" ]:
            data = history.loc[ticker]
            if data["value"][0] == 0:
                raise ValueError(f"Unexpected {data} fundamental data")
        if Object.reference_equals(data.earningreports.iloc[0], data.earningreports.iloc[1]):
            raise ValueError(f"Unexpected fundamental data instance duplication")
        if data.earningreports.iloc[0]._time_provider.get_utc_now() == data.earningreports.iloc[1]._time_provider.get_utc_now():
            raise ValueError(f"Unexpected fundamental data instance duplication")

        self.assert_fundamental_universe_data()

        self.changes = None
        self.number_of_symbols_fundamental = 2

    def assert_fundamental_universe_data(self):
        # Case A
        universeDataPerTime = self.history(self._universe.data_type, [self._universe.symbol], TimeSpan(2, 0, 0, 0))
        if len(universeDataPerTime) != 2:
            raise ValueError(f"Unexpected Fundamentals history count {len(universeDataPerTime)}! Expected 2")

        for universeDataCollection in universeDataPerTime:
            self.assert_fundamental_enumerator(universeDataCollection, "A")

        # Case B (sugar on A)
        universeDataPerTime = self.history(self._universe, TimeSpan(2, 0, 0, 0))
        if len(universeDataPerTime) != 2:
            raise ValueError(f"Unexpected Fundamentals history count {len(universeDataPerTime)}! Expected 2")

        for universeDataCollection in universeDataPerTime:
            self.assert_fundamental_enumerator(universeDataCollection, "B")

        # Case C: Passing through the unvierse type and symbol
        enumerableOfDataDictionary = self.history[self._universe.data_type]([self._universe.symbol], 100)
        for selectionCollectionForADay in enumerableOfDataDictionary:
            self.assert_fundamental_enumerator(selectionCollectionForADay[self._universe.symbol], "C")

    def assert_fundamental_enumerator(self, enumerable, caseName):
        dataPointCount = 0
        for fundamental in enumerable:
            dataPointCount += 1
            if type(fundamental) is not Fundamental:
                raise ValueError(f"Unexpected Fundamentals data type {type(fundamental)} case {caseName}! {str(fundamental)}")
        if dataPointCount < 7000:
            raise ValueError(f"Unexpected historical Fundamentals data count {dataPointCount} case {caseName}! Expected > 7000")

    # return a list of three fixed symbol objects
    def selection_function(self, fundamental):
        # sort descending by daily dollar volume
        sortedByDollarVolume = sorted([x for x in fundamental if x.price > 1],
            key=lambda x: x.dollar_volume, reverse=True)

        # sort descending by P/E ratio
        sortedByPeRatio = sorted(sortedByDollarVolume, key=lambda x: x.valuation_ratios.pe_ratio, reverse=True)

        # take the top entries from our sorted collection
        return [ x.symbol for x in sortedByPeRatio[:self.number_of_symbols_fundamental] ]

    def on_data(self, data):
        # if we have no changes, do nothing
        if self.changes is None: return

        # liquidate removed securities
        for security in self.changes.removed_securities:
            if security.invested:
                self.liquidate(security.symbol)
                self.debug("Liquidated Stock: " + str(security.symbol.value))

        # we want 50% allocation in each security in our universe
        for security in self.changes.added_securities:
            self.set_holdings(security.symbol, 0.02)

        self.changes = None

    # this event fires whenever we have changes to our universe
    def on_securities_changed(self, changes):
        self.changes = changes
