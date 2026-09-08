using System.ComponentModel;
using System.Text.Json;
using ErrorOr;
using QuickMask.Core.Localization;
using QuickMask.Core.Utils;

namespace QuickMask.UI.Localization;

public sealed class Localizer : INotifyPropertyChanged
{
    private readonly List<Dictionary<string, string>> _map;
    private int _selectedLanguageIndex = -1;
    private bool IsValidIndex => _selectedLanguageIndex >= 0 && _selectedLanguageIndex < _map.Count;

    public static Localizer Instance { get; } = new();
    public event Action? LanguageChanged;

    public int CurrentLanguageIndex
    {
        get => _selectedLanguageIndex;
        private set
        {
            if (_selectedLanguageIndex != value)
            {
                _selectedLanguageIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                LanguageChanged?.Invoke();
            }
        }
    }

    public int LanguageCount => _map.Count;

    public event PropertyChangedEventHandler? PropertyChanged;

    private Localizer()
    {
        _map = [];
    }

    public void LoadFromFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        var languageMaps = new List<Dictionary<string, string>>();

        foreach (var filePath in Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly))
        {
            var deserializeResult = LoadLanguageFile(filePath);
            if (!deserializeResult.IsError && deserializeResult.Value.ContainsKey(Loc.LanguageName))
            {
                languageMaps.Add(deserializeResult.Value);
            }
        }

        _map.Clear();
        var sortedMaps = languageMaps.OrderBy(i => ValueParser.Int(i.TryGetValue(Loc.LanguagePriority, out string? value) ? value : string.Empty, int.MaxValue)).ToList();
        _map.AddRange(sortedMaps);
    }
    private ErrorOr<Dictionary<string, string>> LoadLanguageFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null) return Error.Failure(description: $"Failed to deserialize language file: '{Path.GetFileName(filePath)}'.");
            return dict;
        }
        catch
        {
            return Error.Failure(description: $"Failed to load language file: '{Path.GetFileName(filePath)}'.");
        }
    }

    public string[] GetLanguageList() => _map.Select(i => i[Loc.LanguageName]).ToArray();

    public void SetLanguage(int index) => CurrentLanguageIndex = GetValidLanguageIndex(index);

    private int GetValidLanguageIndex(int index)
    {
        if (_map.Count == 0) return -1;
        return Math.Clamp(index, 0, _map.Count - 1);
    }

    public string Get(string localizationKey) => this[localizationKey];
    public string Get(string localizationKey, string arg)
    {
        if (!IsValidIndex) return localizationKey;
        var localizedText = _map[_selectedLanguageIndex].TryGetValue(localizationKey, out var value) ? value : localizationKey;
        return string.Format(localizedText, arg);
    }
    public string Get(string localizationKey, string[] args)
    {
        if (!IsValidIndex) return localizationKey;
        var localizedText = _map[_selectedLanguageIndex].TryGetValue(localizationKey, out var value) ? value : localizationKey;
        return args.Length > 0 ? string.Format(localizedText, args) : localizedText;
    }

    public string this[string key]
    {
        get
        {
            if (!IsValidIndex) return key;
            return _map[_selectedLanguageIndex].TryGetValue(key, out string? value) ? value : key;
        }
    }

    public string? GetKey(string displayName)
    {
        if (!IsValidIndex) return null;
        return _map[_selectedLanguageIndex].FirstOrDefault(i => i.Value == displayName).Key;
    }
}
