using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>One row of the leaderboard. Public fields so JsonUtility can save it.</summary>
[Serializable]
public class LeaderboardEntry
{
    public string name;
    public int score;
    public int shots;
    public float accuracy;
    public long timeTicks; // when it was set (UTC); earlier wins a tie
}

/// <summary>
/// STAGE 9 - local top-10 leaderboard for the 60-second challenge.
///
///  - Ranking: higher score first; same score -> fewer shots first; still equal -> earlier first.
///  - Saved as JSON in Application.persistentDataPath/leaderboard.json, so it survives
///    closing the app. The file is written to a temp file first and then swapped in,
///    so a crash can't leave a half-written file. A damaged file is ignored, not fatal.
///  - Player names are cleaned (no rich-text tags, max 12 characters).
///
/// Plain C# class (not a MonoBehaviour): GameManager creates and owns it.
/// </summary>
public class Leaderboard
{
    public const int DefaultCapacity = 10;
    public const int MaxNameLength = 12;

    [Serializable]
    private class SaveData
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    private readonly List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

    public string FilePath { get; }
    public int Capacity { get; }
    public IReadOnlyList<LeaderboardEntry> Entries => entries;

    /// <summary>The leaderboard file used by the game.</summary>
    public static string DefaultPath => Path.Combine(Application.persistentDataPath, "leaderboard.json");

    public Leaderboard(string filePath, int capacity = DefaultCapacity)
    {
        FilePath = filePath;
        Capacity = Mathf.Max(1, capacity);
        Load();
    }

    /// <summary>Would this result get onto the board?</summary>
    public bool Qualifies(int score, int shots)
    {
        if (score <= 0) return false;
        if (entries.Count < Capacity) return true;
        var candidate = new LeaderboardEntry { score = score, shots = shots, timeTicks = NextTimeTicks() };
        return Compare(candidate, entries[entries.Count - 1]) < 0;
    }

    /// <summary>Adds a result and saves. Returns its rank (1 = best) or 0 if it didn't make the board.</summary>
    public int Submit(string playerName, int score, int shots, float accuracy)
    {
        if (!Qualifies(score, shots)) return 0;

        var entry = new LeaderboardEntry
        {
            name = SanitizeName(playerName),
            score = score,
            shots = Mathf.Max(0, shots),
            accuracy = Mathf.Clamp01(accuracy),
            timeTicks = NextTimeTicks(),
        };

        entries.Add(entry);
        entries.Sort(Compare);
        if (entries.Count > Capacity) entries.RemoveRange(Capacity, entries.Count - Capacity);

        int rank = entries.IndexOf(entry) + 1; // 0 if it was trimmed off
        Save();
        return rank;
    }

    /// <summary>Best score on the board, or 0 if it is empty.</summary>
    public int BestScore => entries.Count > 0 ? entries[0].score : 0;

    public void Clear()
    {
        entries.Clear();
        Save();
    }

    /// <summary>Removes rich-text tags and angle brackets, trims, limits length; empty -> "Player".</summary>
    public static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Player";
        string clean = Regex.Replace(name, "<[^>]*>", "").Replace("<", "").Replace(">", "").Trim();
        if (clean.Length > MaxNameLength) clean = clean.Substring(0, MaxNameLength).TrimEnd();
        return clean.Length == 0 ? "Player" : clean;
    }

    // ---------------------------------------------------------------- saving

    public void Load()
    {
        entries.Clear();
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (data?.entries == null) return;

            foreach (LeaderboardEntry e in data.entries)
            {
                if (e == null || e.score <= 0) continue;
                e.name = SanitizeName(e.name);
                entries.Add(e);
            }
            entries.Sort(Compare);
            if (entries.Count > Capacity) entries.RemoveRange(Capacity, entries.Count - Capacity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Could not read {FilePath} ({e.Message}). Starting a new leaderboard.");
            entries.Clear();
        }
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        try
        {
            string folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string json = JsonUtility.ToJson(new SaveData { entries = new List<LeaderboardEntry>(entries) }, true);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Could not save {FilePath}: {e.Message}");
        }
    }

    // ---------------------------------------------------------------- ranking

    private static int Compare(LeaderboardEntry a, LeaderboardEntry b)
    {
        int byScore = b.score.CompareTo(a.score);      // higher score first
        if (byScore != 0) return byScore;
        int byShots = a.shots.CompareTo(b.shots);      // fewer shots first
        if (byShots != 0) return byShots;
        return a.timeTicks.CompareTo(b.timeTicks);     // earlier first
    }

    /// <summary>Current time, but always later than every saved entry (keeps ties in order).</summary>
    private long NextTimeTicks()
    {
        long latest = 0;
        foreach (LeaderboardEntry e in entries) latest = Math.Max(latest, e.timeTicks);
        return Math.Max(DateTime.UtcNow.Ticks, latest + 1);
    }
}
