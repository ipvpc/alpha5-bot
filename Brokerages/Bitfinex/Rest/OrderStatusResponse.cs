﻿// Generated by Xamasoft JSON Class Generator
// http://www.xamasoft.com/json-class-generator

using Newtonsoft.Json;

namespace QuantConnect.Brokerages.Bitfinex.Rest
{
#pragma warning disable 1591

    public class OrderStatusResponse
    {
        //Corresponds to Order.BrokerId
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("exchange")]
        public string Exchange { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("avg_execution_price")]
        public string AvgExecutionPrice { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("is_cancelled")]
        public bool IsCancelled { get; set; }

        [JsonProperty("was_forced")]
        public bool WasForced { get; set; }

        [JsonProperty("original_amount")]
        public decimal? OriginalAmount { get; set; }

        [JsonProperty("remaining_amount")]
        public decimal? RemainingAmount { get; set; }

        [JsonProperty("executed_amount")]
        public decimal? ExecutedAmount { get; set; }
    }
#pragma warning restore 1591
}