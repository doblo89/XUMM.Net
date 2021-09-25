﻿using System.Text.Json.Serialization;

namespace XUMM.Net.Models.Misc.AppStorage
{
    public class XummStorageApplication
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("uuidv4")]
        public string Uuidv4 { get; set; } = default!;
    }
}
