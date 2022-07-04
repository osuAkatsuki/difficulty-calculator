using Dapper;
using DifficultyCalculator.Models;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.IO;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Difficulty;
using System.Reflection;
using static DifficultyCalculator.Globals;

namespace DifficultyCalculator
{
    public class DifficultyCache
    {
        private static readonly List<Ruleset> available_rulesets = getRulesets();
        private static readonly DifficultyAttributes empty_attributes = new DifficultyAttributes(Array.Empty<Mod>(), -1);

        private readonly Dictionary<DifficultyRequest, Task<DifficultyAttributes>> attributeTaskCache = new Dictionary<DifficultyRequest, Task<DifficultyAttributes>>();
        private readonly Dictionary<DifficultyRequest, DifficultyAttributes> attributeCache = new Dictionary<DifficultyRequest, DifficultyAttributes>();
        private readonly Dictionary<DifficultyRequest, double> difficultyCache = new Dictionary<DifficultyRequest, double>();

        private readonly ILogger logger;

        public DifficultyCache(ILogger<DifficultyCache> logger)
        {
            this.logger = logger;
        }

        private static long totalLookups;

        public async Task<double> GetDifficultyRating(DifficultyRequest request)
        {
            double sr;

            if (string.IsNullOrWhiteSpace(request.BeatmapMd5))
                return 0;

            if (difficultyCache.ContainsKey(request))
                return difficultyCache[request];

            var databaseDifficulty = await getDatabasedDifficulty(request);
            if (databaseDifficulty != default(float))
                sr = databaseDifficulty;
            else
                sr = (await computeAttributes(request)).StarRating;

            lock (difficultyCache)
                difficultyCache.Add(request, sr);

            return sr;
        }

        public async Task<DifficultyAttributes> GetAttributes(DifficultyRequest request)
        {
            DifficultyAttributes attrs;

            if (string.IsNullOrWhiteSpace(request.BeatmapMd5))
                return empty_attributes;

            if (attributeCache.ContainsKey(request))
                return attributeCache[request];

            var databaseAttributes = await getDatabasedAttributes(request);
            if (databaseAttributes != null)
                attrs = databaseAttributes;
            else
                attrs = await computeAttributes(request);

            lock (attributeCache)
                attributeCache.Add(request, attrs);

            return attrs;
        }

        private async Task<DifficultyAttributes?> getDatabasedAttributes(DifficultyRequest request)
        {
            int mods = getModBitwise(request.RulesetId, request.GetMods());

            BeatmapAttribute[] rawDifficultyAttributes;

            using (var conn = await Database.GetDatabaseConnection())
            {
                rawDifficultyAttributes = (await conn.QueryAsync<BeatmapAttribute>(
                    "SELECT * FROM beatmap_attributes WHERE beatmap_md5 = @BeatmapMd5 AND mode = @RulesetId AND mods = @ModValue", new
                    {
                        BeatmapMd5 = request.BeatmapMd5,
                        RulesetId = request.RulesetId,
                        ModValue = mods
                    })).ToArray();
            }

            if (rawDifficultyAttributes.Length == 0)
                return null;

            DifficultyAttributes attributes;

            switch (request.RulesetId)
            {
                case 0:
                    attributes = new OsuDifficultyAttributes();
                    break;

                case 1:
                    attributes = new TaikoDifficultyAttributes();
                    break;

                case 2:
                    attributes = new CatchDifficultyAttributes();
                    break;

                case 3:
                    attributes = new ManiaDifficultyAttributes();
                    break;

                default:
                    throw new InvalidOperationException($"Invalid ruleset: {request.RulesetId}");
            }
            
            var ruleset = available_rulesets.First(r => r.RulesetInfo.OnlineID == request.RulesetId);
            var workingBeatmap = await getBeatmap(request.BeatmapId);
            var beatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo);

            attributes.FromDatabaseAttributes(rawDifficultyAttributes.ToDictionary(a => (int)a.attribute_id, e => (double)e.attribute_value), beatmap.BeatmapInfo.OnlineInfo);

            return attributes;
        }

        private async Task<float> getDatabasedDifficulty(DifficultyRequest request)
        {
            int mods = getModBitwise(request.RulesetId, request.GetMods());

            if (Interlocked.Increment(ref totalLookups) % 1000 == 0)
            {
                logger.LogInformation("difficulty lookup for (beatmap: {BeatmapMd5}, ruleset: {RulesetId}, mods: {Mods})",
                    request.BeatmapMd5,
                    request.RulesetId,
                    mods);
            }

            using (var conn = await Database.GetDatabaseConnection())
            {
                return await conn.QueryFirstOrDefaultAsync<float>("SELECT diff from beatmap_difficulties WHERE beatmap_md5 = @BeatmapMd5 AND mode = @RulesetId and mods = @ModValue", new
                {
                    BeatmapMd5 = request.BeatmapMd5,
                    RulesetId = request.RulesetId,
                    ModValue = mods
                });
            }
        }

        private async Task<DifficultyAttributes> computeAttributes(DifficultyRequest request)
        {
            Task<DifficultyAttributes>? task;

            lock (attributeTaskCache)
            {
                if (!attributeTaskCache.TryGetValue(request, out task))
                {
                    attributeTaskCache[request] = task = Task.Run(async () =>
                    {
                        var apiMods = request.GetMods();

                        logger.LogInformation("Computing difficulty (beatmap: {BeatmapMd5}, ruleset: {RulesetId}, mods: {Mods})",
                            request.BeatmapMd5,
                            request.RulesetId,
                            apiMods.Select(m => m.ToString()));

                        try
                        {
                            var ruleset = available_rulesets.First(r => r.RulesetInfo.OnlineID == request.RulesetId);
                            var mods = apiMods.Select(m => m.ToMod(ruleset)).ToArray();
                            var beatmap = await getBeatmap(request.BeatmapId);

                            var difficultyCalculator = ruleset.CreateDifficultyCalculator(beatmap);
                            var attributes = difficultyCalculator.Calculate(mods);

                            // Trim a few members which we don't consume and only take up RAM.
                            attributes.Mods = Array.Empty<Mod>();

                            using (var conn = await Database.GetDatabaseConnection())
                            {
                                await conn.ExecuteAsync("INSERT INTO beatmap_difficulties (beatmap_md5, mode, mods, diff) VALUES (@BeatmapMd5, @RulesetId, @ModValue, @Diff) ON DUPLICATE KEY UPDATE `diff` = @Diff", new
                                {
                                    BeatmapMd5 = request.BeatmapMd5,
                                    RulesetId = request.RulesetId,
                                    ModValue = (int)attributes.Mods.ToLegacy(),
                                    Diff = attributes.StarRating
                                });

                                var parameters = new List<object>();

                                foreach (var mapping in attributes.ToDatabaseAttributes())
                                {
                                    parameters.Add(new
                                    {
                                        BeatmapMd5 = request.BeatmapMd5,
                                        Mode = request.RulesetId,
                                        Mods = (int)attributes.Mods.ToLegacy(),
                                        Attribute = mapping.attributeId,
                                        Value = Convert.ToSingle(mapping.value)
                                    });
                                }

                                await conn.ExecuteAsync(
                                    "INSERT INTO beatmap_attributes (beatmap_md5, mode, mods, attribute_id, attribute_value) "
                                    + "VALUES (@BeatmapMd5, @Mode, @Mods, @Attribute, @Value) "
                                    + "ON DUPLICATE KEY UPDATE attribute_value = VALUES(attribute_value)",
                                    parameters.ToArray()
                                );
                            }

                            return attributes;
                        }
                        catch (Exception e)
                        {
                            logger.LogWarning("Request failed with \"{Message}\"", e.Message);
                            return empty_attributes;
                        }
                    });
                }
            }

            return await task;
        }

        public void Purge(string beatmapMd5)
        {
            logger.LogInformation("Purging (beatmap: {BeatmapMd5})", beatmapMd5);

            lock (attributeTaskCache)
            {
                foreach (var req in attributeTaskCache.Keys.ToArray())
                {
                    if (req.BeatmapMd5 == beatmapMd5)
                        attributeTaskCache.Remove(req);
                }
            }

            lock (attributeCache)
            {
                foreach (var req in attributeCache.Keys.ToArray())
                {
                    if (req.BeatmapMd5 == beatmapMd5)
                        attributeCache.Remove(req);
                }
            }

            lock (difficultyCache)
            {
                foreach (var req in difficultyCache.Keys.ToArray())
                {
                    if (req.BeatmapMd5 == beatmapMd5)
                        difficultyCache.Remove(req);
                }
            }
        }

        private async Task<WorkingBeatmap> getBeatmap(int beatmapId)
        {
            logger.LogInformation("Downloading beatmap ({BeatmapId})", beatmapId);

            var beatmapFilePath = $"{Settings.BeatmapFolderPath}/{beatmapId}.osu";
            if (File.Exists(beatmapFilePath))
            {
                logger.LogInformation($"Retrieved {beatmapId}'s file from disk");
                return new LoaderWorkingBeatmap(new MemoryStream(File.ReadAllBytes(beatmapFilePath)));
            }

            var req = new WebRequest($"https://old.ppy.sh/osu/{beatmapId}")
            {
                AllowInsecureRequests = true
            };

            await req.PerformAsync();

            if (req.ResponseStream.Length == 0)
                throw new Exception($"Retrieved zero-length beatmap ({beatmapId})!");

            var responseBuffer = req.GetResponseData();
            File.WriteAllBytes(beatmapFilePath, responseBuffer);
            return new LoaderWorkingBeatmap(new MemoryStream(responseBuffer));
        }

        private static List<Ruleset> getRulesets()
        {
            const string ruleset_library_prefix = "osu.Game.Rulesets";

            var rulesetsToProcess = new List<Ruleset>();

            foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, $"{ruleset_library_prefix}.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    Type type = assembly.GetTypes().First(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset)));
                    rulesetsToProcess.Add((Ruleset)Activator.CreateInstance(type));
                }
                catch
                {
                    throw new Exception($"Failed to load ruleset ({file})");
                }
            }

            return rulesetsToProcess;
        }

        private static int getModBitwise(int rulesetId, List<APIMod> mods)
        {
            int val = 0;

            foreach (var mod in mods)
                val |= (int)getLegacyMod(mod);

            return val;

            LegacyMods getLegacyMod(APIMod mod)
            {
                switch (mod.Acronym)
                {
                    case "EZ": return LegacyMods.Easy;

                    case "HR": return LegacyMods.HardRock;

                    case "NC": return LegacyMods.DoubleTime;

                    case "DT": return LegacyMods.DoubleTime;

                    case "HT": return LegacyMods.HalfTime;

                    case "4K": return LegacyMods.Key4;

                    case "5K": return LegacyMods.Key5;

                    case "6K": return LegacyMods.Key6;

                    case "7K": return LegacyMods.Key7;

                    case "8K": return LegacyMods.Key8;

                    case "9K": return LegacyMods.Key9;

                    case "FL" when rulesetId == 0: return LegacyMods.Flashlight;

                    case "AP": return LegacyMods.Autopilot;

                    case "RX": return LegacyMods.Relax;
                }

                return 0;
            }
        }
    }
}