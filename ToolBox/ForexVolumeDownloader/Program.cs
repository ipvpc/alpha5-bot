﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using javax.print.attribute.standard;
using org.apache.log4j;
using QuantConnect.Configuration;
using QuantConnect.Logging;

namespace QuantConnect.ToolBox.FxVolumeDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: ForexVolumeDownloader SYMBOLS RESOLUTION FROMDATE TODATE");
                Console.WriteLine("SYMBOLS = eg EURUSD,USDJPY\n" +
                                  "\tAvailable pairs:\n" +
                                  "\tEURUSD, USDJPY, GBPUSD, USDCHF, EURCHF, AUDUSD, USDCAD,\n" +
                                  "\tNZDUSD, EURGBP, EURJPY, GBPJPY, EURAUD, EURCAD, AUDJPY");
                Console.WriteLine("RESOLUTION = Minute/Hour/Daily/All");
                Console.WriteLine("FROMDATE = yyyymmdd");
                Console.WriteLine("TODATE = yyyymmdd");
#if DEBUG
                Console.WriteLine("Press enter to close...");
                Console.ReadLine();
                args = new string[] { "EURUSD", "All", "20140501", "20140515" };
#else
                args = new string[] { "EURUSD", "Minute", "20140101", "20150101" };
                //Environment.Exit(1);
#endif
            }

            try
            {
                var timer = DateTime.Now;
                Log.DebuggingEnabled = true;
                var logHandlers = new ILogHandler[] { new ConsoleLogHandler(), new FileLogHandler("FxcmFxVolumeDownloader.log", false) };

                // Load settings from command line
                var tickers = args[0].Split(',');
                var resolutions = new[] { Resolution.Daily};

                if (args[1].ToLower() == "all")
                {
                    resolutions = new[] { Resolution.Daily, Resolution.Hour , Resolution.Minute };
                }
                else
                {
                    resolutions[0] = (Resolution) Enum.Parse(typeof(Resolution), args[1]);
                }

                var startDate = DateTime.ParseExact(args[2], "yyyyMMdd", CultureInfo.InvariantCulture);
                var endDate = DateTime.ParseExact(args[3], "yyyyMMdd", CultureInfo.InvariantCulture);

                // Load settings from config.json

                var dataDirectory = Config.Get("data-directory", "../../../Data");
                //var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TestingFXVolumeData");

                // Download the data
                Market.Add("FXCMForexVolume", identifier: 20);

                var downloader = new ForexVolumeDownloader(dataDirectory);
                foreach (var ticker in tickers)
                {
                    var symbol = Symbol.Create(ticker, SecurityType.Base, Market.Decode(code: 20));
                    foreach (var resolution in resolutions)
                    {
                        Log.Trace(string.Format("Requesting {0} volume data with {1} resolution.", symbol.Value, resolution.ToString()));
                        var data = downloader.Get(symbol, resolution, startDate, endDate);
                        Log.Trace(string.Format("\t=> {0} observations retrieved.", data.Count()));
                        var writer = new LeanDataWriter(resolution, symbol, dataDirectory);
                        writer.Write(data);
                        Log.Trace("\t=> Successfully saved!");
                    }
                }
                Console.WriteLine("\n => Timer: {0} milliseconds.", (DateTime.Now - timer).TotalMilliseconds);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }
    }
}
