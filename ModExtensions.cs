using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace DifficultyCalculator
{
    public static class ModExtensions
    {
        public static LegacyMods ToLegacy(this Mod[] mods)
        {
            var value = LegacyMods.None;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ManiaModFadeIn _:
                        value |= LegacyMods.FadeIn;
                        break;

                    case ModNoFail _:
                        value |= LegacyMods.NoFail;
                        break;

                    case ModEasy _:
                        value |= LegacyMods.Easy;
                        break;

                    case ModHidden _:
                        value |= LegacyMods.Hidden;
                        break;

                    case ModHardRock _:
                        value |= LegacyMods.HardRock;
                        break;

                    case ModSuddenDeath _:
                        value |= LegacyMods.SuddenDeath;
                        break;

                    case ModDoubleTime _:
                        value |= LegacyMods.DoubleTime;
                        break;

                    case ModRelax _:
                        value |= LegacyMods.Relax;
                        break;

                    case OsuModAutopilot _:
                        value |= LegacyMods.Autopilot;
                        break;

                    case ModHalfTime _:
                        value |= LegacyMods.HalfTime;
                        break;

                    case ModFlashlight _:
                        value |= LegacyMods.Flashlight;
                        break;

                    case ManiaModKey1 _:
                        value |= LegacyMods.Key1;
                        break;

                    case ManiaModKey2 _:
                        value |= LegacyMods.Key2;
                        break;

                    case ManiaModKey3 _:
                        value |= LegacyMods.Key3;
                        break;

                    case ManiaModKey4 _:
                        value |= LegacyMods.Key4;
                        break;

                    case ManiaModKey5 _:
                        value |= LegacyMods.Key5;
                        break;

                    case ManiaModKey6 _:
                        value |= LegacyMods.Key6;
                        break;

                    case ManiaModKey7 _:
                        value |= LegacyMods.Key7;
                        break;

                    case ManiaModKey8 _:
                        value |= LegacyMods.Key8;
                        break;

                    case ManiaModKey9 _:
                        value |= LegacyMods.Key9;
                        break;

                    case ManiaModMirror _:
                        value |= LegacyMods.Mirror;
                        break;

                    case ManiaModDualStages _:
                        value |= LegacyMods.KeyCoop;
                        break;
                }
            }

            return value;
        }
    }
}
