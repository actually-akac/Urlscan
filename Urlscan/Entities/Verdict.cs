﻿using System.Text.Json.Serialization;

namespace Urlscan
{
    public class VerdictParameters
    {
        [JsonPropertyName("verdict")]
        public VerdictType Verdict { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        [JsonPropertyName("threatTypes")]
        public ThreatType[] ThreatTypes { get; set; }

        [JsonPropertyName("brands")]
        public string[] Brands { get; set; }

        [JsonPropertyName("scope")]
        public VerdictScope Scope { get; set; }

        [JsonPropertyName("scopeValue")]
        public string ScopeValue { get; set; }

        [JsonPropertyName("uuid")]
        public string UUID { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; } = "verdict";
    }
}