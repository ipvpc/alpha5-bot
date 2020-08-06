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
using System.IO;
using NUnit.Framework;
using QuantConnect.Logging;

namespace QuantConnect.Tests.Logging
{
    [TestFixture]
    public class FileLogHandlerTests
    {
        [Test]
        public void WritesMessageToFile()
        {
            const string file = "log2.txt";
            File.Delete(file);

            var debugMessage = "*debug message*" + DateTime.UtcNow.ToStringInvariant("o");
            using (var log = new FileLogHandler(file))
            {
                log.Debug(debugMessage);
            }

            var contents = File.ReadAllText(file);
            Assert.IsNotNull(contents);
            Assert.IsTrue(contents.Contains(debugMessage));

            File.Delete(file);
        }

        [Test]
        public void UsesGlobalFilePath()
        {
            var previous = Log.FilePath;
            Directory.CreateDirectory("filePathTest");
            Log.FilePath = Path.Combine("filePathTest", "log2.txt");
            File.Delete(Log.FilePath);

            var debugMessage = "*debug message*" + DateTime.UtcNow.ToStringInvariant("o");
            using (var log = new FileLogHandler())
            {
                log.Debug(debugMessage);
            }

            var contents = File.ReadAllText(Log.FilePath);
            Log.FilePath = previous;

            Assert.IsNotNull(contents);
            Assert.IsTrue(contents.Contains(debugMessage));

            File.Delete(Log.FilePath);
        }

        [Test]
        public void TestLoggingSpeeds()
        {
            var start = DateTime.Now;
            const string file = "log2.txt";

            //Delete it first, just to make sure it is not there.
            File.Delete(file);
            int math = 1;

            using (var log = new FileLogHandler(file))
            {
                //Log messages but also do other things in the meantime to test log threading.
                for (int i = 0; i < 1000000; i++)
                {
                    var debugMessage = "debug message " + i;
                    log.Debug(debugMessage);

                    for (int j = 0; j < 10000; j++)
                    {
                        math = math * j;
                    }

                    log.Debug(math.ToStringInvariant());
                }
            }


            var end = DateTime.Now;
            var time = start - end;

            Console.WriteLine(time);
        }
    }
}
