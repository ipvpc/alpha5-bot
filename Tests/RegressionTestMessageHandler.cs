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

using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Packets;
using QuantConnect.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuantConnect.Tests
{
    /// <summary>
    /// Local/desktop implementation of messaging system for Lean Engine.
    /// </summary>
    public class RegressionTestMessageHandler : QuantConnect.Messaging.Messaging
    {
        private static readonly bool _updateRegressionStatistics = Config.GetBool("regression-update-statistics", true);
        private AlgorithmNodePacket _job;
        private AlgorithmManager _algorithmManager;

        /// <summary>
        /// Initialize the messaging system
        /// </summary>
        public void SetAlgorithmManager(AlgorithmManager algorithmManager)
        {
            _algorithmManager = algorithmManager;
        }

        /// <summary>
        /// Set the messaging channel
        /// </summary>
        public override void SetAuthentication(AlgorithmNodePacket job)
        {
            base.SetAuthentication(job);
            _job = job;
        }

        /// <summary>
        /// Send a generic base packet without processing
        /// </summary>
        public override void Send(Packet packet)
        {
            base.Send(packet);
            switch (packet.Type)
            {
                case PacketType.BacktestResult:
                    var result = (BacktestResultPacket)packet;

                    if (result.Progress == 1)
                    {
                        if (_updateRegressionStatistics && _job.Language == Language.CSharp)
                        {
                            UpdateRegressionStatisticsInSourceFile(result);
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        private void UpdateRegressionStatisticsInSourceFile(BacktestResultPacket result)
        {
            var algorithmSource = Directory.EnumerateFiles("../../../Algorithm.CSharp", $"{_job.AlgorithmId}.cs", SearchOption.AllDirectories).Single();
            var file = File.ReadAllLines(algorithmSource).ToList().GetEnumerator();
            var lines = new List<string>();
            while (file.MoveNext())
            {
                var line = file.Current;
                if (line == null)
                {
                    continue;
                }

                if (line.Contains("Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>")
                    || line.Contains("Dictionary<string, string> ExpectedStatistics => new()"))
                {
                    if (!result.Results.Statistics.Any())
                    {
                        lines.Add(line);
                        continue;
                    }

                    lines.Add(line);
                    lines.Add("        {");

                    foreach (var pair in result.Results.Statistics)
                    {
                        lines.Add($"            {{\"{pair.Key}\", \"{pair.Value}\"}},");
                    }

                    // remove trailing comma
                    var lastLine = lines[lines.Count - 1];
                    lines[lines.Count - 1] = lastLine.Substring(0, lastLine.Length - 1);

                    // now we skip existing expected statistics in file
                    while (file.MoveNext())
                    {
                        line = file.Current;
                        if (line != null && line.Contains("};"))
                        {
                            lines.Add(line);
                            break;
                        }
                    }
                }
                else if (line.Contains($"long DataPoints =>"))
                {
                    lines.Add(GetDataPointLine(line, _algorithmManager?.DataPoints.ToString()));
                }
                else if (line.Contains($"int AlgorithmHistoryDataPoints =>"))
                {
                    lines.Add(GetDataPointLine(line, _algorithmManager?.AlgorithmHistoryDataPoints.ToString()));
                }
                else
                {
                    lines.Add(line);
                }
            }

            file.DisposeSafely();
            File.WriteAllLines(algorithmSource, lines);
        }

        private static string GetDataPointLine(string currentLine, string count)
        {
            var dataParts = currentLine.Split(" ");
            dataParts[^1] = count + ";";
            return string.Join(" ", dataParts);
        }
    }
}
