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

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NodaTime;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.AlgorithmFactory.Python.Wrappers;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Custom;
using QuantConnect.Data.Custom.PsychSignal;
using QuantConnect.Data.Custom.SEC;
using QuantConnect.Data.Custom.TradingEconomics;
using QuantConnect.Data.Custom.USTreasury;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Securities;
using QuantConnect.Tests.Engine.DataFeeds;
using QuantConnect.Util;
using Bitcoin = QuantConnect.Algorithm.CSharp.LiveTradingFeaturesAlgorithm.Bitcoin;
using HistoryRequest = QuantConnect.Data.HistoryRequest;

namespace QuantConnect.Tests.Algorithm
{
    [TestFixture]
    public class AlgorithmAddDataTests
    {
        [Test]
        public void DefaultDataFeeds_CanBeOverwritten_Successfully()
        {
            Config.Set("security-data-feeds", "{ Forex: [\"Trade\"] }");
            var algo = new QCAlgorithm();
            algo.SubscriptionManager.SetDataManager(new DataManagerStub(algo));

            // forex defult - should be tradebar
            var forexTrade = algo.AddForex("EURUSD");
            Assert.IsTrue(forexTrade.Subscriptions.Count() == 1);
            Assert.IsTrue(GetMatchingSubscription(forexTrade, typeof(QuoteBar)) != null);

            // Change
            var dataFeedsConfigString = Config.Get("security-data-feeds");
            Dictionary<SecurityType, List<TickType>> dataFeeds = new Dictionary<SecurityType, List<TickType>>();
            if (dataFeedsConfigString != string.Empty)
            {
                dataFeeds = JsonConvert.DeserializeObject<Dictionary<SecurityType, List<TickType>>>(dataFeedsConfigString);
            }

            algo.SetAvailableDataTypes(dataFeeds);

            // new forex - should be quotebar
            var forexQuote = algo.AddForex("EURUSD");
            Assert.IsTrue(forexQuote.Subscriptions.Count() == 1);
            Assert.IsTrue(GetMatchingSubscription(forexQuote, typeof(TradeBar)) != null);
        }

        [Test]
        public void DefaultDataFeeds_AreAdded_Successfully()
        {
            var algo = new QCAlgorithm();
            algo.SubscriptionManager.SetDataManager(new DataManagerStub(algo));

            // forex
            var forex = algo.AddSecurity(SecurityType.Forex, "eurusd");
            Assert.IsTrue(forex.Subscriptions.Count() == 1);
            Assert.IsTrue(GetMatchingSubscription(forex, typeof(QuoteBar)) != null);

            // equity
            var equity = algo.AddSecurity(SecurityType.Equity, "goog");
            Assert.IsTrue(equity.Subscriptions.Count() == 1);
            Assert.IsTrue(GetMatchingSubscription(equity, typeof(TradeBar)) != null);

            // option
            var option = algo.AddSecurity(SecurityType.Option, "goog");
            Assert.IsTrue(option.Subscriptions.Count() == 1);
            Assert.IsTrue(GetMatchingSubscription(option, typeof(ZipEntryName)) != null);

            // cfd
            var cfd = algo.AddSecurity(SecurityType.Cfd, "abc");
            Assert.IsTrue(cfd.Subscriptions.Count() == 1);
            Assert.IsTrue(GetMatchingSubscription(cfd, typeof(QuoteBar)) != null);

            // future
            var future = algo.AddSecurity(SecurityType.Future, "ES");
            Assert.IsTrue(future.Subscriptions.Count() == 1);
            Assert.IsTrue(future.Subscriptions.FirstOrDefault(x => typeof(ZipEntryName).IsAssignableFrom(x.Type)) != null);

            // Crypto
            var crypto = algo.AddSecurity(SecurityType.Crypto, "btcusd", Resolution.Daily);
            Assert.IsTrue(crypto.Subscriptions.Count() == 2);
            Assert.IsTrue(GetMatchingSubscription(crypto, typeof(QuoteBar)) != null);
            Assert.IsTrue(GetMatchingSubscription(crypto, typeof(TradeBar)) != null);
        }


        [Test]
        public void CustomDataTypes_AreAddedToSubscriptions_Successfully()
        {
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            // Add a bitcoin subscription
            qcAlgorithm.AddData<Bitcoin>("BTC");
            var bitcoinSubscription = qcAlgorithm.SubscriptionManager.Subscriptions.FirstOrDefault(x => x.Type == typeof(Bitcoin));
            Assert.AreEqual(bitcoinSubscription.Type, typeof(Bitcoin));

            // Add a quandl subscription
            qcAlgorithm.AddData<Quandl>("EURCAD");
            var quandlSubscription = qcAlgorithm.SubscriptionManager.Subscriptions.FirstOrDefault(x => x.Type == typeof(Quandl));
            Assert.AreEqual(quandlSubscription.Type, typeof(Quandl));
        }

        [Test]
        public void OnEndOfTimeStepSeedsUnderlyingSecuritiesThatHaveNoData()
        {
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm, new MockDataFeed()));
            qcAlgorithm.SetLiveMode(true);
            var testHistoryProvider = new TestHistoryProvider();
            qcAlgorithm.HistoryProvider = testHistoryProvider;

            var option = qcAlgorithm.AddSecurity(SecurityType.Option, testHistoryProvider.underlyingSymbol);
            var option2 = qcAlgorithm.AddSecurity(SecurityType.Option, testHistoryProvider.underlyingSymbol2);
            Assert.IsFalse(qcAlgorithm.Securities.ContainsKey(option.Symbol.Underlying));
            Assert.IsFalse(qcAlgorithm.Securities.ContainsKey(option2.Symbol.Underlying));
            qcAlgorithm.OnEndOfTimeStep();
            var data = qcAlgorithm.Securities[testHistoryProvider.underlyingSymbol].GetLastData();
            var data2 = qcAlgorithm.Securities[testHistoryProvider.underlyingSymbol2].GetLastData();
            Assert.IsNotNull(data);
            Assert.IsNotNull(data2);
            Assert.AreEqual(data.Price, 2);
            Assert.AreEqual(data2.Price, 3);
        }

        [Test]
        public void OnEndOfTimeStepDoesNotThrowWhenSeedsSameUnderlyingForTwoSecurities()
        {
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm, new MockDataFeed()));
            qcAlgorithm.SetLiveMode(true);
            var testHistoryProvider = new TestHistoryProvider();
            qcAlgorithm.HistoryProvider = testHistoryProvider;
            var option = qcAlgorithm.AddOption(testHistoryProvider.underlyingSymbol);

            var symbol = Symbol.CreateOption(testHistoryProvider.underlyingSymbol, Market.USA, OptionStyle.American,
                OptionRight.Call, 1, new DateTime(2015, 12, 24));
            var symbol2 = Symbol.CreateOption(testHistoryProvider.underlyingSymbol, Market.USA, OptionStyle.American,
                OptionRight.Put, 1, new DateTime(2015, 12, 24));

            var optionContract = qcAlgorithm.AddOptionContract(symbol, Resolution.Daily);
            var optionContract2 = qcAlgorithm.AddOptionContract(symbol2, Resolution.Minute);

            qcAlgorithm.OnEndOfTimeStep();
            var data = qcAlgorithm.Securities[testHistoryProvider.underlyingSymbol].GetLastData();
            Assert.AreEqual(testHistoryProvider.LastResolutionRequest, Resolution.Minute);
            Assert.IsNotNull(data);
            Assert.AreEqual(data.Price, 2);
        }

        // C# Underlying symbol tests
        [TestCase("EURUSD", SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", SecurityType.Crypto, false, false)]
        [TestCase("CL", SecurityType.Future, false, false)]
        [TestCase("AAPL", SecurityType.Equity, true, true)]
        [TestCase("EURUSD", SecurityType.Forex, false, false)]
        public void AddDataSecuritySymbolWithUnderlying(string ticker, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<PsychSignalSentimentData>(asset.Symbol, Resolution.Daily);
            var customData = qcAlgorithm.AddData<PsychSignalSentimentData>(asset.Symbol, Resolution.Daily);

            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            Assert.IsFalse(customData.IsTradable);

            Assert.IsTrue(customData.Symbol.HasUnderlying, $"PsychSignalSentimentData added as {ticker} Symbol with SecurityType {securityType} does not have underlying");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol, $"Custom data underlying does not match {securityType} Symbol for {ticker}");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("EURUSD", SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", SecurityType.Crypto, false, false)]
        [TestCase("CL", SecurityType.Future, false, false)]
        [TestCase("FDTR", SecurityType.Equity, true, false)]
        [TestCase("EURUSD", SecurityType.Forex, false, false)]
        public void AddDataSecuritySymbolNoUnderlying(string ticker, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<TradingEconomicsCalendar>(asset.Symbol, Resolution.Daily);
            var customData = qcAlgorithm.AddData<TradingEconomicsCalendar>(asset.Symbol, Resolution.Daily);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"TradingEconomicsCalendar has underlying symbol {customData.Symbol.Underlying.Value} for SecurityType {securityType} with original ticker {ticker}");
            Assert.AreEqual(customData.Symbol.Underlying, null, $"TradingEconomicsCalendar - Custom data underlying Symbol for SecurityType {securityType} is not null");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("EURUSD", SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", SecurityType.Crypto, false, false)]
        [TestCase("CL", SecurityType.Future, false, false)]
        [TestCase("AAPL", SecurityType.Equity, true, true)]
        [TestCase("EURUSD", SecurityType.Forex, false, false)]
        public void AddDataSecurityTickerWithUnderlying(string ticker, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<PsychSignalSentimentData>(ticker, Resolution.Daily);
            var customData = qcAlgorithm.AddData<PsychSignalSentimentData>(ticker, Resolution.Daily);

            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            Assert.IsFalse(customData.IsTradable);

            Assert.IsTrue(customData.Symbol.HasUnderlying, $"PsychSignalSentimentData added as {ticker} Symbol with SecurityType {securityType} does not have underlying");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol, $"Custom data underlying does not match {securityType} Symbol for {ticker}");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("EURUSD", SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", SecurityType.Crypto, false, false)]
        [TestCase("CL", SecurityType.Future, false, false)]
        [TestCase("FDTR", SecurityType.Equity, true, false)]
        [TestCase("EURUSD", SecurityType.Forex, false, false)]
        public void AddDataSecurityTickerNoUnderlying(string ticker, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<TradingEconomicsCalendar>(ticker, Resolution.Daily);
            var customData = qcAlgorithm.AddData<TradingEconomicsCalendar>(ticker, Resolution.Daily);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"TradingEconomicsCalendar has underlying symbol for SecurityType {securityType} with ticker {ticker}");
            Assert.AreEqual(customData.Symbol.Underlying, null, $"TradingEconomicsCalendar - Custom data underlying Symbol for SecurityType {securityType} is not null");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL")]
        [TestCase("TWX")]
        [TestCase("FB")]
        [TestCase("NFLX")]
        public void AddDataOptionsSymbolHasChainedUnderlyingSymbols(string ticker)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<PsychSignalSentimentData>(asset.Symbol, Resolution.Daily);
            var customData = qcAlgorithm.AddData<PsychSignalSentimentData>(asset.Symbol, Resolution.Daily);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsTrue(customData.Symbol.HasUnderlying, $"PsychSignalSentimentData - {ticker} has no underlying Symbol");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying, asset.Symbol.Underlying);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying.Underlying, null);

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsTrue(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL")]
        [TestCase("FDTR")]
        [TestCase("FOOBAR")]
        public void AddDataOptionsSymbolHasNoChainedUnderlyingSymbols(string ticker)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<TradingEconomicsCalendar>(asset.Symbol, Resolution.Daily);
            var customData = qcAlgorithm.AddData<TradingEconomicsCalendar>(asset.Symbol, Resolution.Daily);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"TradingEconomicsCalendar has an underlying Symbol");

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsFalse(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL")]
        [TestCase("TWX")]
        [TestCase("FB")]
        [TestCase("NFLX")]
        public void AddDataOptionsTickerHasChainedUnderlyingSymbols(string ticker)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<PsychSignalSentimentData>(ticker, Resolution.Daily);
            var customData = qcAlgorithm.AddData<PsychSignalSentimentData>(ticker, Resolution.Daily);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsTrue(customData.Symbol.HasUnderlying, $"PsychSignalSentimentData - {ticker} has no underlying Symbol");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying, asset.Symbol.Underlying);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying.Underlying, null);

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsTrue(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL")]
        [TestCase("FDTR")]
        [TestCase("FOOBAR")]
        public void AddDataOptionsTickerHasNoChainedUnderlyingSymbols(string ticker)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData<TradingEconomicsCalendar>(ticker, Resolution.Daily);
            var customData = qcAlgorithm.AddData<TradingEconomicsCalendar>(ticker, Resolution.Daily);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"TradingEconomicsCalendar has an underlying Symbol");

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsFalse(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        // Python tests for AddData underlying symbol
        [TestCase("EURUSD", typeof(PsychSignalSentimentData), SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", typeof(PsychSignalSentimentData), SecurityType.Crypto, false, false)]
        [TestCase("CL", typeof(PsychSignalSentimentData), SecurityType.Future, false, false)]
        [TestCase("AAPL", typeof(PsychSignalSentimentData), SecurityType.Equity, true, true)]
        [TestCase("EURUSD", typeof(PsychSignalSentimentData), SecurityType.Forex, false, false)]
        public void AddDataSecuritySymbolWithUnderlyingPython(string ticker, Type customDataType, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            Assert.IsFalse(customData.IsTradable);

            Assert.IsTrue(customData.Symbol.HasUnderlying, $"{customDataType.Name} added as {ticker} Symbol with SecurityType {securityType} does not have underlying");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol, $"Custom data underlying does not match {securityType} Symbol for {ticker}");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("EURUSD", typeof(TradingEconomicsCalendar), SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", typeof(TradingEconomicsCalendar), SecurityType.Crypto, false, false)]
        [TestCase("CL", typeof(TradingEconomicsCalendar), SecurityType.Future, false, false)]
        [TestCase("FDTR", typeof(TradingEconomicsCalendar), SecurityType.Equity, true, false)]
        [TestCase("EURUSD", typeof(TradingEconomicsCalendar), SecurityType.Forex, false, false)]
        public void AddDataSecuritySymbolNoUnderlyingPython(string ticker, Type customDataType, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"{customDataType.Name} has underlying symbol {customData.Symbol.Underlying.Value} for SecurityType {securityType} with original ticker {ticker}");
            Assert.AreEqual(customData.Symbol.Underlying, null, $"{customDataType.Name} - Custom data underlying Symbol for SecurityType {securityType} is not null");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("EURUSD", typeof(PsychSignalSentimentData), SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", typeof(PsychSignalSentimentData), SecurityType.Crypto, false, false)]
        [TestCase("CL", typeof(PsychSignalSentimentData), SecurityType.Future, false, false)]
        [TestCase("AAPL", typeof(PsychSignalSentimentData), SecurityType.Equity, true, true)]
        [TestCase("EURUSD", typeof(PsychSignalSentimentData), SecurityType.Forex, false, false)]
        public void AddDataSecurityTickerWithUnderlyingPython(string ticker, Type customDataType, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            Assert.IsFalse(customData.IsTradable);

            Assert.IsTrue(customData.Symbol.HasUnderlying, $"Custom data added as {ticker} Symbol with SecurityType {securityType} does not have underlying");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol, $"Custom data underlying does not match {securityType} Symbol for {ticker}");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("EURUSD", typeof(TradingEconomicsCalendar), SecurityType.Cfd, false, false)]
        [TestCase("BTCUSD", typeof(TradingEconomicsCalendar), SecurityType.Crypto, false, false)]
        [TestCase("CL", typeof(TradingEconomicsCalendar), SecurityType.Future, false, false)]
        [TestCase("FDTR", typeof(TradingEconomicsCalendar), SecurityType.Equity, true, false)]
        [TestCase("EURUSD", typeof(TradingEconomicsCalendar), SecurityType.Forex, false, false)]
        public void AddDataSecurityTickerNoUnderlyingPython(string ticker, Type customDataType, SecurityType securityType, bool securityShouldBeMapped, bool customDataShouldBeMapped)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            Security asset;

            switch (securityType) {
                case SecurityType.Cfd:
                    asset = qcAlgorithm.AddCfd(ticker, Resolution.Daily);
                    break;
                case SecurityType.Crypto:
                    asset = qcAlgorithm.AddCrypto(ticker, Resolution.Daily);
                    break;
                case SecurityType.Equity:
                    asset = qcAlgorithm.AddEquity(ticker, Resolution.Daily);
                    break;
                case SecurityType.Forex:
                    asset = qcAlgorithm.AddForex(ticker, Resolution.Daily);
                    break;
                case SecurityType.Future:
                    asset = qcAlgorithm.AddFuture(ticker, Resolution.Daily);
                    break;
                default:
                    throw new Exception($"SecurityType {securityType} is not valid for this test");
            }

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"{customDataType.Name} has underlying symbol for SecurityType {securityType} with ticker {ticker}");
            Assert.AreEqual(customData.Symbol.Underlying, null, $"{customDataType.Name} - Custom data underlying Symbol for SecurityType {securityType} is not null");

            Assert.AreEqual(securityShouldBeMapped, asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.AreEqual(customDataShouldBeMapped, customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL", typeof(PsychSignalSentimentData))]
        [TestCase("TWX", typeof(PsychSignalSentimentData))]
        [TestCase("FB", typeof(PsychSignalSentimentData))]
        [TestCase("NFLX", typeof(PsychSignalSentimentData))]
        public void AddDataOptionsSymbolHasChainedUnderlyingSymbolsPython(string ticker, Type customDataType)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsTrue(customData.Symbol.HasUnderlying, $"{customDataType.Name} - {ticker} has no underlying Symbol");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying, asset.Symbol.Underlying);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying.Underlying, null);

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsTrue(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL", typeof(TradingEconomicsCalendar))]
        [TestCase("FDTR", typeof(TradingEconomicsCalendar))]
        public void AddDataOptionsSymbolHasNoChainedUnderlyingSymbolsPython(string ticker, Type customDataType)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, asset.Symbol, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"{customDataType.Name} has an underlying Symbol");

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsFalse(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL", typeof(PsychSignalSentimentData))]
        [TestCase("TWX", typeof(PsychSignalSentimentData))]
        [TestCase("FB", typeof(PsychSignalSentimentData))]
        [TestCase("NFLX", typeof(PsychSignalSentimentData))]
        public void AddDataOptionsTickerHasChainedUnderlyingSymbolsPython(string ticker, Type customDataType)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsTrue(customData.Symbol.HasUnderlying, $"{customDataType.Name} - {ticker} has no underlying Symbol");
            Assert.AreEqual(customData.Symbol.Underlying, asset.Symbol);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying, asset.Symbol.Underlying);
            Assert.AreEqual(customData.Symbol.Underlying.Underlying.Underlying, null);

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsTrue(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [TestCase("AAPL", typeof(TradingEconomicsCalendar))]
        [TestCase("FDTR", typeof(TradingEconomicsCalendar))]
        public void AddDataOptionsTickerHasNoChainedUnderlyingSymbolsPython(string ticker, Type customDataType)
        {
            SymbolCache.Clear();
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            var asset = qcAlgorithm.AddOption(ticker, Resolution.Daily);

            // Dummy here is meant to try to corrupt the SymbolCache. Ideally, SymbolCache should return non-custom data types with higher priority
            // in case we want to add two custom data types, but still have them associated with the equity from the cache if we're using it.
            // This covers the case where two idential data subscriptions are created.
            var dummy = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);
            var customData = qcAlgorithm.AddData(customDataType, ticker, Resolution.Daily, asset.Subscriptions.Single().DataTimeZone);

            // Ensures that the securities generated are distinct
            Assert.AreNotEqual(asset.GetType(), customData.GetType());
            //Assert.IsTrue(asset.IsTradable);
            // TODO: Should fail in master
            Assert.IsFalse(customData.IsTradable);

            // Check to see if we have an underlying symbol when we shouldn't
            Assert.IsFalse(customData.Symbol.HasUnderlying, $"{customDataType.Name} has an underlying Symbol");

            Assert.IsTrue(asset.Subscriptions.Single().TickerShouldBeMapped());
            Assert.IsFalse(customData.Subscriptions.Single().TickerShouldBeMapped());
        }

        [Test]
        public void PythonCustomDataTypes_AreAddedToSubscriptions_Successfully()
        {
            var qcAlgorithm = new AlgorithmPythonWrapper("Test_CustomDataAlgorithm");
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            // Initialize contains the statements:
            // self.AddData(Nifty, "NIFTY")
            // self.AddData(QuandlFuture, "SCF/CME_CL1_ON", Resolution.Daily)
            qcAlgorithm.Initialize();

            var niftySubscription = qcAlgorithm.SubscriptionManager.Subscriptions.FirstOrDefault(x => x.Symbol.Value == "NIFTY");
            Assert.IsNotNull(niftySubscription);

            var niftyFactory = (BaseData)ObjectActivator.GetActivator(niftySubscription.Type).Invoke(new object[] { niftySubscription.Type });
            Assert.DoesNotThrow(() => niftyFactory.GetSource(niftySubscription, DateTime.UtcNow, false));

            var quandlSubscription = qcAlgorithm.SubscriptionManager.Subscriptions.FirstOrDefault(x => x.Symbol.Value == "SCF/CME_CL1_ON");
            Assert.IsNotNull(quandlSubscription);

            var quandlFactory = (BaseData)ObjectActivator.GetActivator(quandlSubscription.Type).Invoke(new object[] { quandlSubscription.Type });
            Assert.DoesNotThrow(() => quandlFactory.GetSource(quandlSubscription, DateTime.UtcNow, false));
        }

        [Test]
        public void PythonCustomDataTypes_AreAddedToConsolidator_Successfully()
        {
            var qcAlgorithm = new AlgorithmPythonWrapper("Test_CustomDataAlgorithm");
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));

            // Initialize contains the statements:
            // self.AddData(Nifty, "NIFTY")
            // self.AddData(QuandlFuture, "SCF/CME_CL1_ON", Resolution.Daily)
            qcAlgorithm.Initialize();

            var niftyConsolidator = new DynamicDataConsolidator(TimeSpan.FromDays(2));
            Assert.DoesNotThrow(() => qcAlgorithm.SubscriptionManager.AddConsolidator("NIFTY", niftyConsolidator));

            var quandlConsolidator = new DynamicDataConsolidator(TimeSpan.FromDays(2));
            Assert.DoesNotThrow(() => qcAlgorithm.SubscriptionManager.AddConsolidator("SCF/CME_CL1_ON", quandlConsolidator));
        }

        [Test]
        public void AddingInvalidDataTypeThrows()
        {
            var qcAlgorithm = new QCAlgorithm();
            qcAlgorithm.SubscriptionManager.SetDataManager(new DataManagerStub(qcAlgorithm));
            Assert.Throws<ArgumentException>(() => qcAlgorithm.AddData(typeof(double),
                "double",
                Resolution.Daily,
                DateTimeZone.Utc));
        }

        private static SubscriptionDataConfig GetMatchingSubscription(Security security, Type type)
        {
            // find a subscription matchin the requested type with a higher resolution than requested
            return (from sub in security.Subscriptions.OrderByDescending(s => s.Resolution)
                    where type.IsAssignableFrom(sub.Type)
                    select sub).FirstOrDefault();
        }

        private class TestHistoryProvider : HistoryProviderBase
        {
            public string underlyingSymbol = "GOOG";
            public string underlyingSymbol2 = "AAPL";
            public override int DataPointCount { get; }
            public Resolution LastResolutionRequest;

            public override void Initialize(HistoryProviderInitializeParameters parameters)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
            {
                var now = DateTime.UtcNow;
                LastResolutionRequest = requests.First().Resolution;
                var tradeBar1 = new TradeBar(now, underlyingSymbol, 1, 1, 1, 1, 1, TimeSpan.FromDays(1));
                var tradeBar2 = new TradeBar(now, underlyingSymbol2, 3, 3, 3, 3, 3, TimeSpan.FromDays(1));
                var slice1 = new Slice(now, new List<BaseData> { tradeBar1, tradeBar2 },
                                    new TradeBars(now), new QuoteBars(),
                                    new Ticks(), new OptionChains(),
                                    new FuturesChains(), new Splits(),
                                    new Dividends(now), new Delistings(),
                                    new SymbolChangedEvents());
                var tradeBar1_2 = new TradeBar(now, underlyingSymbol, 2, 2, 2, 2, 2, TimeSpan.FromDays(1));
                var slice2 = new Slice(now, new List<BaseData> { tradeBar1_2 },
                    new TradeBars(now), new QuoteBars(),
                    new Ticks(), new OptionChains(),
                    new FuturesChains(), new Splits(),
                    new Dividends(now), new Delistings(),
                    new SymbolChangedEvents());
                return new[] { slice1, slice2 };
            }
        }
    }
}
