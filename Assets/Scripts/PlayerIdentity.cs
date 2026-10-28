using System;
using UnityEngine;

/// <summary>
/// Guest identity for HTTP leaderboards (CrazyGames Basic / local).
/// </summary>
public static class PlayerIdentity
{
    private const string IdKey = "QDG2.PlayerId";
    private const string NameKey = "QDG2.PlayerName";

    public static string PlayerId
    {
        get
        {
            string id = PlayerPrefs.GetString(IdKey, string.Empty);
            if (id.Length >= 8)
                return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(IdKey, id);
            PlayerPrefs.Save();
            return id;
        }
    }

    public static string Nickname
    {
        get => PlayerPrefs.GetString(NameKey, string.Empty);
        set
        {
            string clean = SanitizeName(value);
            if (string.IsNullOrEmpty(clean))
                return;

            PlayerPrefs.SetString(NameKey, clean);
            PlayerPrefs.Save();
        }
    }

    public static bool HasNickname => SanitizeName(Nickname).Length >= 2;

    public static string SanitizeName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var chars = raw.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c < 32 || c == '|')
                chars[i] = ' ';
        }

        string name = new string(chars);
        while (name.Contains("  "))
            name = name.Replace("  ", " ");
        name = name.Trim();
        if (name.Length < 2)
            return string.Empty;
        if (name.Length > 16)
            name = name.Substring(0, 16);
        return name;
    }
}
