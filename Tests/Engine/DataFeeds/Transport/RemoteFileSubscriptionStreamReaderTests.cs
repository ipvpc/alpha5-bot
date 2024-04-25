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
 *
*/

using System.Collections.Generic;
using System.IO;
using System.Net;
using NUnit.Framework;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.DataFeeds.Transport;
using QuantConnect.Util;

namespace QuantConnect.Tests.Engine.DataFeeds.Transport
{
    [TestFixture]
    public class RemoteFileSubscriptionStreamReaderTests
    {
        [SetUp]
        public void SetUp()
        {
            RemoteFileSubscriptionStreamReader.SetDownloadProvider(new TestDownloadProvider());
            TestDownloadProvider.DownloadCount = 0;
            // create cache directory if not existing
            if (!Directory.Exists(Globals.Cache))
            {
                Directory.CreateDirectory(Globals.Cache);
            }
            else
            {
                // clean old files out of the cache
                Directory.Delete(Globals.Cache, true);
                Directory.CreateDirectory(Globals.Cache);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SingleEntryDataCacheProviderEphemeralDataIsRespected(bool isDataEphemeral)
        {
            using var dataProvider = new DefaultDataProvider();
            using var cacheProvider = new SingleEntryDataCacheProvider(dataProvider, isDataEphemeral: isDataEphemeral);
            using var remoteReader = new RemoteFileSubscriptionStreamReader(
                cacheProvider,
                @"https://www.quantconnect.com/api/v2/proxy/quandl/api/v3/datasets/BCHARTS/BITSTAMPUSD.csv?order=asc&api_key=WyAazVXnq7ATy_fefTqm",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);
            Assert.AreEqual(1, TestDownloadProvider.DownloadCount);

            using var dataProvider2 = new DefaultDataProvider();
            using var cacheProvider2 = new SingleEntryDataCacheProvider(dataProvider2, isDataEphemeral: isDataEphemeral);
            using var remoteReader2 = new RemoteFileSubscriptionStreamReader(
                cacheProvider2,
                @"https://www.quantconnect.com/api/v2/proxy/quandl/api/v3/datasets/BCHARTS/BITSTAMPUSD.csv?order=asc&api_key=WyAazVXnq7ATy_fefTqm",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);
            Assert.AreEqual(isDataEphemeral ? 2 : 1, TestDownloadProvider.DownloadCount);

            remoteReader.Dispose();
            remoteReader2.Dispose();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ZipDataCacheProviderEphemeralDataIsRespected(bool isDataEphemeral)
        {
            using var dataProvider = new DefaultDataProvider();
            using var cacheProvider = new ZipDataCacheProvider(dataProvider, isDataEphemeral: isDataEphemeral);
            using var remoteReader = new RemoteFileSubscriptionStreamReader(
                cacheProvider,
                @"https://www.quantconnect.com/api/v2/proxy/quandl/api/v3/datasets/BCHARTS/BITSTAMPUSD.csv?order=asc&api_key=WyAazVXnq7ATy_fefTqm",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);
            Assert.AreEqual(1, TestDownloadProvider.DownloadCount);

            using var dataProvider2 = new DefaultDataProvider();
            using var cacheProvider2 = new ZipDataCacheProvider(dataProvider2, isDataEphemeral: isDataEphemeral);
            using var remoteReader2 = new RemoteFileSubscriptionStreamReader(
                cacheProvider2,
                @"https://www.quantconnect.com/api/v2/proxy/quandl/api/v3/datasets/BCHARTS/BITSTAMPUSD.csv?order=asc&api_key=WyAazVXnq7ATy_fefTqm",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);
            Assert.AreEqual(isDataEphemeral ? 2 : 1, TestDownloadProvider.DownloadCount);

            remoteReader.Dispose();
            remoteReader2.Dispose();
            cacheProvider.DisposeSafely();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ZipDataCacheProviderEphemeralDataIsRespectedForZippedData(bool isDataEphemeral)
        {
            using var dataProvider = new DefaultDataProvider();
            using var cacheProvider = new ZipDataCacheProvider(dataProvider, isDataEphemeral: isDataEphemeral);
            using var remoteReader = new RemoteFileSubscriptionStreamReader(
                cacheProvider,
                @"https://mba.tuck.dartmouth.edu/pages/faculty/ken.french/ftp/F-F_Momentum_Factor_daily_CSV.zip",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);
            Assert.AreEqual(1, TestDownloadProvider.DownloadCount);

            using var remoteReader2 = new RemoteFileSubscriptionStreamReader(
                cacheProvider,
                @"https://mba.tuck.dartmouth.edu/pages/faculty/ken.french/ftp/F-F_Momentum_Factor_daily_CSV.zip",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);
            Assert.AreEqual(isDataEphemeral ? 2 : 1, TestDownloadProvider.DownloadCount);

            remoteReader.Dispose();
            remoteReader2.Dispose();
            cacheProvider.DisposeSafely();
        }

        [Test]
        public void InvalidDataSource()
        {
            using var dataProvider = new DefaultDataProvider();
            using var cacheProvider = new ZipDataCacheProvider(dataProvider);
            using var remoteReader = new RemoteFileSubscriptionStreamReader(
                cacheProvider,
                @"http://quantconnect.com",
                Globals.Cache,
                null);

            Assert.IsFalse(remoteReader.EndOfStream);

            // Fails to get quantconnect.com, missing http://
            Assert.Throws<WebException>(() => new RemoteFileSubscriptionStreamReader(
                    new SingleEntryDataCacheProvider(new DefaultDataProvider()),
                    @"quantconnect.com",
                    Globals.Cache,
                    null),
                "Api.Download(): Failed to download data from quantconnect.com. Please verify the source for missing http:// or https://"
            );

            remoteReader.DisposeSafely();
        }

        private class TestDownloadProvider : QuantConnect.Api.Api
        {
            public static int DownloadCount { get; set; }
            static TestDownloadProvider()
            {
                DownloadCount = 0;
            }
            public override Stream DownloadBytes(string address, IEnumerable<KeyValuePair<string, string>> headers, string userName, string password)
            {
                DownloadCount++;
                return base.DownloadBytes(address, headers, userName, password);
            }
        }
    }
}
