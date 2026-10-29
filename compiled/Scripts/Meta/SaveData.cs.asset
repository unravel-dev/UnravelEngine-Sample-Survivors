using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Player settings kept between sessions.</summary>
    public sealed class GameSettings
    {
        /// <summary>Master volume, 0..1.</summary>
        public float masterVolume = 1.0f;
        /// <summary>Music bus volume, 0..1.</summary>
        public float musicVolume = 0.7f;
        /// <summary>Sound effects and UI bus volume, 0..1.</summary>
        public float sfxVolume = 0.9f;
        /// <summary>0..1; 0 turns shake off.</summary>
        public float screenShake = 1.0f;
        /// <summary>0 off, 1 crits and big hits, 2 all.</summary>
        public int damageNumbers = 2;
        /// <summary>Opacity of the hero's own effects: 1, 0.6 or 0.3 (late-game clutter).</summary>
        public float effectsOpacity = 1.0f;
        /// <summary>Small health bars over elites.</summary>
        public bool showEliteBars = true;
        /// <summary>The touch joystick sits on the right half instead of the left.</summary>
        public bool joystickOnRight = false;
        /// <summary>Multiplies the UI size the screen gets: 0.8, 1.0, 1.2 or 1.4.</summary>
        public float uiScale = 1.0f;

        /// <summary>Brings every value back into its range (hand-edited or older files).</summary>
        public void Sanitize()
        {
            masterVolume = Math.Clamp(Finite(masterVolume, 1.0f), 0.0f, 1.0f);
            musicVolume = Math.Clamp(Finite(musicVolume, 0.7f), 0.0f, 1.0f);
            sfxVolume = Math.Clamp(Finite(sfxVolume, 0.9f), 0.0f, 1.0f);
            screenShake = Math.Clamp(Finite(screenShake, 1.0f), 0.0f, 1.0f);
            damageNumbers = Math.Clamp(damageNumbers, 0, 2);
            effectsOpacity = Math.Clamp(Finite(effectsOpacity, 1.0f), 0.3f, 1.0f);
            uiScale = Math.Clamp(Finite(uiScale, 1.0f), 0.8f, 1.4f);
        }

        private static float Finite(float value, float fallback) => float.IsFinite(value) ? value : fallback;
    }

    /// <summary>Best results of one hero class.</summary>
    public sealed class HeroRecord
    {
        public int runs;
        public int victories;
        /// <summary>Longest run, seconds.</summary>
        public float bestTime;
        public int bestLevel;
        public int bestKills;
        /// <summary>Highest Dread level won with this hero; -1 when none.</summary>
        public int highestDreadCleared = -1;
    }

    /// <summary>
    /// The save file: gold, meta ranks, unlocks, best results and settings, as versioned JSON at
    /// Application.persistentDataPath/barrowlight_save.json. A missing file starts fresh; an unreadable or corrupt
    /// one is kept aside as barrowlight_save.corrupt-[time].json and a fresh save starts. Fields the file lacks keep
    /// their defaults and fields this build no longer has are skipped, so older saves load as they are. Writes go to
    /// a temporary file first, then replace the save, so a crash mid-write never loses it. Main thread.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>The format this build writes.</summary>
        public const int CURRENT_VERSION = 1;
        public const string FILE_NAME = "barrowlight_save.json";
        /// <summary>The save of builds made before the game took its name; read once when the new file does not exist yet.</summary>
        public const string LEGACY_FILE_NAME = "gravetide_save.json";
        /// <summary>Level the Mage needs in one run.</summary>
        public const int MAGE_UNLOCK_LEVEL = 20;
        public const int MAX_DREAD = 5;
        public const string BOSS_GRAVEWARDEN = "gravewarden";
        public const string BOSS_ARCHMAGE = "hollow_archmage";
        /// <summary>The final boss: its fall wins the run, which <see cref="victories"/> counts; it unlocks no hero.</summary>
        public const string BOSS_HOLLOW_KING = "hollow_king";
        /// <summary>A best time counts as new only when it beats the old one by more than this (seconds).</summary>
        private const float BEST_TIME_MARGIN = 0.5f;
        private const int HERO_COUNT = (int)HeroClass.Count;

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
        };

        public int version = CURRENT_VERSION;
        /// <summary>Gold banked for the Armory.</summary>
        public int gold;
        /// <summary>Gold spent in the Armory since the last refund (the refund pays it back).</summary>
        public int goldSpent;
        /// <summary>Armory ranks by upgrade id (MetaShop).</summary>
        public Dictionary<string, int> metaRanks = new Dictionary<string, int>();
        public GameSettings settings = new GameSettings();
        public bool gravewardenDefeated;
        public bool archmageDefeated;
        public int runs;
        public int victories;
        public int bestLevel;
        /// <summary>Dread chosen on the hero select screen for the next run (0 = off).</summary>
        public int selectedDread;
        /// <summary>The class picked last (hero select starts on it).</summary>
        public int lastHero;
        public HeroRecord[] heroes = CreateHeroRecords();

        /// <summary>Heroes this save unlocked at the end of the last run, for the results screen.</summary>
        [JsonIgnore]
        public readonly List<HeroClass> newlyUnlocked = new List<HeroClass>();
        /// <summary>Records the last run beat, for the results screen ("New best time").</summary>
        [JsonIgnore]
        public readonly List<string> newRecords = new List<string>();
        /// <summary>Boss kills of the running run; applied after the end of run snapshot so their unlocks read as new.</summary>
        private readonly HashSet<string> bossKillsThisRun = new HashSet<string>();

        /// <summary>Dread can be chosen once the boss fell at least once.</summary>
        [JsonIgnore]
        public bool isDreadUnlocked => victories > 0;

        /// <summary>The save path (main thread: reads Application.persistentDataPath).</summary>
        public static string GetPath() => Path.Combine(Application.persistentDataPath, FILE_NAME);

        /// <summary>Reads the save, or starts a fresh one when it is missing, unreadable or from a newer build.</summary>
        public static SaveData Load()
        {
            string path;
            try
            {
                path = GetPath();
            }
            catch (Exception ex)
            {
                Log.Warning($"Save: no data path ({ex.Message}); playing without a save file.");
                return new SaveData();
            }
            if (!File.Exists(path))
            {
                string legacy = Path.Combine(Path.GetDirectoryName(path) ?? "", LEGACY_FILE_NAME);
                if (!File.Exists(legacy))
                {
                    return new SaveData();
                }
                path = legacy;
            }
            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonSerializer.Deserialize<SaveData>(json, jsonOptions);
                if (data == null)
                {
                    throw new InvalidDataException("empty document");
                }
                if (data.version > CURRENT_VERSION)
                {
                    throw new InvalidDataException($"version {data.version} is newer than {CURRENT_VERSION}");
                }
                data.Sanitize();
                return data;
            }
            catch (Exception ex)
            {
                string backup = Path.Combine(Path.GetDirectoryName(path) ?? "", $"barrowlight_save.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                try
                {
                    File.Copy(path, backup, true);
                }
                catch (Exception copyError)
                {
                    Log.Warning($"Save: could not back up the unreadable save: {copyError.Message}");
                }
                Log.Warning($"Save: {path} could not be read ({ex.Message}); kept it as {backup} and started fresh.");
                return new SaveData();
            }
        }

        /// <summary>Writes the save (temporary file, then replace). Errors are logged, never thrown.</summary>
        public void Save()
        {
            try
            {
                string path = GetPath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                version = CURRENT_VERSION;
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(this, jsonOptions));
                File.Move(temporary, path, true);
            }
            catch (Exception ex)
            {
                Log.Error($"Save: writing failed: {ex.Message}");
            }
        }

        /// <summary>Whether the save unlocked a class: Knight always; Barbarian after the Gravewarden fell; Mage after
        /// level 20 in one run; Rogue after the Hollow Archmage fell.</summary>
        public bool IsHeroUnlocked(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Knight:
                    return true;
                case HeroClass.Barbarian:
                    return gravewardenDefeated;
                case HeroClass.Mage:
                    return bestLevel >= MAGE_UNLOCK_LEVEL;
                case HeroClass.Rogue:
                    return archmageDefeated;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The progress line of a hero whose unlock is a level goal ("Best level so far: 12."), shown under its unlock
        /// hint while it is locked; empty for heroes unlocked by other means.
        /// </summary>
        public string GetLevelGoalProgress(HeroClass heroClass)
        {
            return heroClass == HeroClass.Mage ? $"Best level so far: {bestLevel}." : "";
        }

        /// <summary>Records a boss kill as it happens (ids: gravewarden, hollow_archmage, hollow_king); the unlock is
        /// written at the end of the run.</summary>
        public void RecordBossDefeated(string bossId) => bossKillsThisRun.Add(bossId);

        private void ApplyBossKills()
        {
            foreach (string bossId in bossKillsThisRun)
            {
                ApplyBossKill(bossId);
            }
            bossKillsThisRun.Clear();
        }

        private void ApplyBossKill(string bossId)
        {
            switch (bossId)
            {
                case BOSS_GRAVEWARDEN:
                    gravewardenDefeated = true;
                    break;
                case BOSS_ARCHMAGE:
                    archmageDefeated = true;
                    break;
            }
        }

        public HeroRecord GetHero(HeroClass heroClass)
        {
            int index = (int)heroClass;
            return index >= 0 && index < heroes.Length ? heroes[index] : new HeroRecord();
        }

        /// <summary>Banks the run's gold and records unlocks and bests. Fills <see cref="newlyUnlocked"/> and
        /// <see cref="newRecords"/> for the results screen.</summary>
        public void OnRunEnded(RunRecord record, HeroClass heroClass)
        {
            newRecords.Clear();
            bool[] wasUnlocked = GetUnlocks();
            ApplyBossKills();
            gold = SafeAdd(gold, Math.Max(0, record.goldEarned));
            runs++;
            bestLevel = Math.Max(bestLevel, record.levelReached);
            HeroRecord hero = GetHero(heroClass);
            hero.runs++;
            RecordBests(hero, record);
            if (record.isVictory)
            {
                victories++;
                hero.victories++;
                hero.highestDreadCleared = Math.Max(hero.highestDreadCleared, selectedDread);
            }
            CollectNewUnlocks(wasUnlocked);
        }

        public int GetRank(string upgradeId) => upgradeId != null && metaRanks.TryGetValue(upgradeId, out int rank) ? rank : 0;

        public void SetRank(string upgradeId, int rank)
        {
            if (rank <= 0)
            {
                metaRanks.Remove(upgradeId);
                return;
            }
            metaRanks[upgradeId] = rank;
        }

        private void Sanitize()
        {
            settings ??= new GameSettings();
            settings.Sanitize();
            metaRanks ??= new Dictionary<string, int>();
            gold = Math.Max(0, gold);
            goldSpent = Math.Max(0, goldSpent);
            selectedDread = Math.Clamp(selectedDread, 0, MAX_DREAD);
            lastHero = Math.Clamp(lastHero, 0, HERO_COUNT - 1);
            if (heroes == null || heroes.Length != HERO_COUNT)
            {
                HeroRecord[] fixedHeroes = CreateHeroRecords();
                for (int i = 0; heroes != null && i < Math.Min(heroes.Length, HERO_COUNT); i++)
                {
                    fixedHeroes[i] = heroes[i] ?? new HeroRecord();
                }
                heroes = fixedHeroes;
            }
            for (int i = 0; i < HERO_COUNT; i++)
            {
                heroes[i] ??= new HeroRecord();
            }
            MetaShop.SanitizeRanks(this);
        }

        private bool[] GetUnlocks()
        {
            var unlocked = new bool[HERO_COUNT];
            for (int i = 0; i < HERO_COUNT; i++)
            {
                unlocked[i] = IsHeroUnlocked((HeroClass)i);
            }
            return unlocked;
        }

        private void CollectNewUnlocks(bool[] wasUnlocked)
        {
            newlyUnlocked.Clear();
            for (int i = 0; i < HERO_COUNT; i++)
            {
                if (!wasUnlocked[i] && IsHeroUnlocked((HeroClass)i))
                {
                    newlyUnlocked.Add((HeroClass)i);
                }
            }
        }

        /// <summary>Updates a hero's best time, level and kills; a beaten best is news only after the hero's first run.</summary>
        private void RecordBests(HeroRecord hero, RunRecord record)
        {
            bool isNews = hero.runs > 1;
            if (record.timeSurvived > hero.bestTime + BEST_TIME_MARGIN)
            {
                AddNewRecord(isNews, "New best time");
                hero.bestTime = record.timeSurvived;
            }
            if (record.levelReached > hero.bestLevel)
            {
                AddNewRecord(isNews, "New best level");
                hero.bestLevel = record.levelReached;
            }
            if (record.kills > hero.bestKills)
            {
                AddNewRecord(isNews, "New most kills");
                hero.bestKills = record.kills;
            }
        }

        private void AddNewRecord(bool isNews, string text)
        {
            if (isNews)
            {
                newRecords.Add(text);
            }
        }

        private static HeroRecord[] CreateHeroRecords()
        {
            var records = new HeroRecord[HERO_COUNT];
            for (int i = 0; i < HERO_COUNT; i++)
            {
                records[i] = new HeroRecord();
            }
            return records;
        }

        private static int SafeAdd(int a, int b) => (int)Math.Min((long)a + b, int.MaxValue);
    }
}
