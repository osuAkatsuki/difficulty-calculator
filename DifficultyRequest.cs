﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API;

namespace DifficultyCalculator
{
    public class DifficultyRequest : IEquatable<DifficultyRequest>
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId { get; init; }

        [JsonProperty("beatmap_md5")]
        public string? BeatmapMd5 { get; init; }

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; init; }

        [JsonProperty("mods")]
        public JArray? Mods { get; init; }

        public List<APIMod> GetMods()
        {
            var apiMods = new List<APIMod>(Mods?.ToObject<APIMod[]>()?.OrderBy(m => m.Acronym).ToArray() ?? Array.Empty<APIMod>());

            // Hacks for some stable-specific mods.
            apiMods.RemoveAll(m =>
            {
                string? acronym = m.Acronym?.ToUpper();

                if (string.IsNullOrWhiteSpace(acronym))
                    return true;

                switch (acronym)
                {
                    case "SCOREV2":
                    case "CINEMA":
                    case "AUTO":
                        return true;
                }

                return false;
            });

            // Stable provides an unexpected acronym for dual stages.
            foreach (var m in apiMods)
            {
                if (m.Acronym == "2P")
                    m.Acronym = "DS";
            }

            return apiMods;
        }

        public bool Equals(DifficultyRequest? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return BeatmapMd5 == other.BeatmapMd5 && RulesetId == other.RulesetId && new JTokenEqualityComparer().Equals(Mods!, other.Mods!);
        }

        public override bool Equals(object? obj)
            => obj is DifficultyRequest other && Equals(other);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            hashCode.Add(BeatmapMd5);
            hashCode.Add(RulesetId);
            hashCode.Add(new JTokenEqualityComparer().GetHashCode(Mods!));

            return hashCode.ToHashCode();
        }
    }
}