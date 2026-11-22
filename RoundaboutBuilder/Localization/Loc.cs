using ColossalFramework;
using ColossalFramework.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/* Roundabout Builder - localization core (Loc).

 * Translations live next to the mod dll in a "Translations" folder:
 *     Translations/lang.txt          (English, also the template for translators)
 *     Translations/lang_zh-cn.txt    (Simplified Chinese)
 *     Translations/lang_<code>.txt   (any other language)
 *
 * File format - one entry per line, key and value separated by a single space:
 *     RAB_BTN_BUILD Build
 * Literal "\n" inside a value is converted to a real line break. Lines starting
 * with '#' are comments. "key=value" is also accepted.
 *
 * If anything goes wrong (folder missing, file unreadable, key missing) the
 * hard-coded English fallback passed to Loc.L() is used, so the mod never shows
 * raw keys to the player.
 */

namespace RoundaboutBuilder
{
    public class LanguageEntry
    {
        public string Code { get; set; }
        public string NativeName { get; set; }
    }

    public static class Loc
    {
        public const string DefaultLanguage = "en";

        private const string TranslationsFolderName = "Translations";
        private const string EnglishFileName = "lang.txt";
        private const string FilePrefix = "lang_";
        private const string LanguageCodeKey = "RAB_LANGUAGE_CODE";
        private const string LanguageNameKey = "RAB_LANGUAGE_NAME";
        private const string SettingsKey = "language";

        /* Used to find the mod folder when Assembly.Location is empty (the game
         * loads mod assemblies from memory). */
        private const string ModAssemblyName = "RoundaboutBuilder";
        private const ulong WorkshopFileId = 1625704117UL;

        private static Dictionary<string, string> s_current = new Dictionary<string, string>();
        private static Dictionary<string, string> s_english = new Dictionary<string, string>();
        private static List<LanguageEntry> s_available = new List<LanguageEntry>();
        private static string s_currentCode = DefaultLanguage;
        private static SavedString s_setting;
        private static bool s_initialized;
        private static bool s_loadFailed;

        /* SavedString is created lazily: the settings file is only guaranteed to
         * exist after the IUserMod constructor has run. */
        public static SavedString Setting
        {
            get
            {
                if (s_setting == null)
                {
                    try
                    {
                        s_setting = new SavedString(SettingsKey, RoundAboutBuilder.settingsFileName, string.Empty, true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[RoundaboutBuilder] Could not create the language setting.");
                        Debug.LogWarning(e);
                    }
                }
                return s_setting;
            }
        }

        public static string CurrentLanguageCode
        {
            get { return s_currentCode; }
        }

        public static List<LanguageEntry> AvailableLanguages
        {
            get
            {
                EnsureLoaded();
                return s_available;
            }
        }

        /* Safe to call many times; the work is done once. If the first attempt
         * happened while the plugin list was not ready yet, a later explicit call
         * (OnEnabled / OnSettingsUI) retries. */
        public static void Initialize()
        {
            if (s_initialized && !s_loadFailed) return;
            s_initialized = true;
            Reload();
        }

        /* Used by the lookup helpers: loads once and never retries, so that a
         * missing folder cannot cause a file scan on every UI refresh. */
        private static void EnsureLoaded()
        {
            if (s_initialized) return;
            s_initialized = true;
            Reload();
        }

        /* Re-reads every translation file and applies the configured language. */
        public static void Reload()
        {
            s_current = new Dictionary<string, string>();
            s_english = new Dictionary<string, string>();
            s_available = new List<LanguageEntry>();
            s_currentCode = DefaultLanguage;
            s_loadFailed = false;

            try
            {
                string directory = GetTranslationsDirectory();
                if (directory == null)
                {
                    s_loadFailed = true;
                    Debug.LogWarning("[RoundaboutBuilder] Translations folder not found - using built-in English text.");
                    return;
                }

                Dictionary<string, Dictionary<string, string>> files = ReadAllTranslationFiles(directory);

                Dictionary<string, string> english;
                if (files.TryGetValue(DefaultLanguage, out english))
                    s_english = english;

                BuildAvailableLanguages(files);
                s_currentCode = ResolveLanguageCode(GetDesiredLanguageCode(), files);

                Dictionary<string, string> current;
                if (files.TryGetValue(s_currentCode, out current))
                    s_current = current;

                Debug.Log("[RoundaboutBuilder] Language set to '" + s_currentCode + "' (" + s_current.Count + " entries).");
            }
            catch (Exception e)
            {
                s_loadFailed = true;
                Debug.LogWarning("[RoundaboutBuilder] Loading translations failed - using built-in English text.");
                Debug.LogWarning(e);
            }
        }

        /* Stores the chosen language (empty string = follow the system language). */
        public static void SetLanguage(string code)
        {
            SavedString setting = Setting;
            if (setting != null)
            {
                try
                {
                    setting.value = code ?? string.Empty;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[RoundaboutBuilder] Could not save the language setting.");
                    Debug.LogWarning(e);
                }
            }
            Reload();
        }

        /* Empty string means "follow the system language". */
        public static string GetSavedLanguageCode()
        {
            SavedString setting = Setting;
            if (setting == null) return string.Empty;
            try
            {
                return setting.value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetLanguageName(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            for (int i = 0; i < s_available.Count; i++)
            {
                if (s_available[i].Code == code)
                    return s_available[i].NativeName;
            }
            return code;
        }

        public static bool LoadFailed
        {
            get
            {
                EnsureLoaded();
                return s_loadFailed;
            }
        }

        /* Main lookup. Never returns an empty string: falls back to English, then to the literal. */
        public static string L(string key, string fallback)
        {
            EnsureLoaded();

            string value;
            if (s_current.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return value;
            if (s_english.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return value;
            return fallback;
        }

        /* Same as L(), but the value may contain {0}, {1}, ... placeholders. */
        public static string L(string key, string fallback, params object[] args)
        {
            string text = L(key, fallback);
            if (args == null || args.Length == 0)
                return text;
            try
            {
                return string.Format(text, args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Bad format string for key '" + key + "'.");
                Debug.LogWarning(e);
                return text;
            }
        }

        /* ------------------------------ internals ------------------------------ */

        /* The game loads mod assemblies from memory, so Assembly.Location is empty
         * inside Cities: Skylines. Several sources are therefore tried in turn:
         * assembly location / code base, the plugin list (modPath), and finally the
         * well-known local Mods folder. */
        private static string GetTranslationsDirectory()
        {
            string[] candidates = new string[3];
            string tried = string.Empty;

            try
            {
                candidates[0] = GetDirectoryFromAssembly();
                tried += "assembly=" + (candidates[0] ?? "<null>") + "; ";

                candidates[1] = GetDirectoryFromPluginManager();
                tried += "plugin=" + (candidates[1] ?? "<null>") + "; ";

                candidates[2] = GetDirectoryFromModsFolder();
                tried += "mods=" + (candidates[2] ?? "<null>");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Could not resolve the Translations folder.");
                Debug.LogWarning(e);
                return null;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrEmpty(candidates[i]))
                    continue;

                string translations = Path.Combine(candidates[i], TranslationsFolderName);
                if (Directory.Exists(translations))
                {
                    Debug.Log("[RoundaboutBuilder] Translations folder: " + translations);
                    return translations;
                }
            }

            Debug.LogWarning("[RoundaboutBuilder] Translations folder not found - using built-in English text. Tried: " + tried);
            return null;
        }

        private static string GetDirectoryFromAssembly()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string path = assembly.Location;
            if (string.IsNullOrEmpty(path))
            {
                Uri codeBase;
                if (Uri.TryCreate(assembly.CodeBase, UriKind.Absolute, out codeBase) && codeBase.IsFile)
                    path = codeBase.LocalPath;
            }

            if (string.IsNullOrEmpty(path))
                return null;

            return Path.GetDirectoryName(path);
        }

        private static string GetDirectoryFromPluginManager()
        {
            if (PluginManager.instance == null)
                return null;

            foreach (PluginManager.PluginInfo info in PluginManager.instance.GetPluginsInfo())
            {
                if (info == null)
                    continue;

                string modPath = info.modPath;
                if (string.IsNullOrEmpty(modPath))
                    continue;

                bool match = false;
                try
                {
                    match = info.publishedFileID.AsUInt64 == WorkshopFileId;
                }
                catch
                {
                }

                if (!match && !string.IsNullOrEmpty(info.name) &&
                    info.name.IndexOf(ModAssemblyName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = true;
                }

                if (!match && modPath.IndexOf(ModAssemblyName, StringComparison.OrdinalIgnoreCase) >= 0)
                    match = true;

                if (match)
                    return modPath;
            }

            return null;
        }

        private static string GetDirectoryFromModsFolder()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
                return null;

            return Path.Combine(Path.Combine(Path.Combine(Path.Combine(localAppData,
                "Colossal Order"), "Cities_Skylines"), Path.Combine("Addons", "Mods")), ModAssemblyName);
        }

        private static Dictionary<string, Dictionary<string, string>> ReadAllTranslationFiles(string directory)
        {
            Dictionary<string, Dictionary<string, string>> result =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            string englishPath = Path.Combine(directory, EnglishFileName);
            if (File.Exists(englishPath))
                result[DefaultLanguage] = ReadFile(englishPath);

            foreach (string path in Directory.GetFiles(directory, FilePrefix + "*.txt"))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName == null || !fileName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string code = NormalizeCode(fileName.Substring(FilePrefix.Length));
                if (string.IsNullOrEmpty(code) || result.ContainsKey(code))
                    continue;

                result[code] = ReadFile(path);
            }

            return result;
        }

        private static Dictionary<string, string> ReadFile(string path)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line == null) continue;

                if (i == 0 && line.Length > 0 && line[0] == '\uFEFF')
                    line = line.Substring(1);

                line = line.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#' || line.StartsWith("//")) continue;

                string key;
                string value;

                int separator = line.IndexOf(' ');
                int equals = line.IndexOf('=');

                if (equals >= 0 && (separator < 0 || equals < separator))
                {
                    key = line.Substring(0, equals).Trim();
                    value = line.Substring(equals + 1).Trim();
                }
                else if (separator > 0)
                {
                    key = line.Substring(0, separator).Trim();
                    value = line.Substring(separator + 1).Trim();
                }
                else
                {
                    continue;
                }

                if (key.Length == 0) continue;

                value = value.Replace("\\n", "\n");
                entries[key] = value;
            }

            return entries;
        }

        private static void BuildAvailableLanguages(Dictionary<string, Dictionary<string, string>> files)
        {
            s_available = new List<LanguageEntry>();

            foreach (KeyValuePair<string, Dictionary<string, string>> pair in files)
            {
                string code = pair.Key;
                string declaredCode;
                if (pair.Value.TryGetValue(LanguageCodeKey, out declaredCode) && !string.IsNullOrEmpty(declaredCode))
                    code = NormalizeCode(declaredCode);

                string nativeName;
                if (!pair.Value.TryGetValue(LanguageNameKey, out nativeName) || string.IsNullOrEmpty(nativeName))
                    nativeName = code;

                if (ContainsLanguage(code))
                    continue;

                LanguageEntry entry = new LanguageEntry();
                entry.Code = code;
                entry.NativeName = nativeName;
                s_available.Add(entry);
            }

            s_available.Sort((a, b) =>
            {
                if (a.Code == DefaultLanguage) return -1;
                if (b.Code == DefaultLanguage) return 1;
                return string.Compare(a.NativeName, b.NativeName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool ContainsLanguage(string code)
        {
            for (int i = 0; i < s_available.Count; i++)
            {
                if (s_available[i].Code == code) return true;
            }
            return false;
        }

        private static string ResolveLanguageCode(string desired, Dictionary<string, Dictionary<string, string>> files)
        {
            string code = NormalizeCode(desired);

            switch (code)
            {
                case "zh-hans":
                case "zh-chs":
                case "zh-sg":
                    code = "zh-cn";
                    break;
                case "zh-hant":
                case "zh-cht":
                case "zh-hk":
                case "zh-mo":
                    code = "zh-tw";
                    break;
            }

            if (files.ContainsKey(code))
                return code;

            int dash = code.IndexOf('-');
            if (dash > 0)
            {
                string primary = code.Substring(0, dash);
                if (files.ContainsKey(primary))
                    return primary;
            }

            return DefaultLanguage;
        }

        private static string GetDesiredLanguageCode()
        {
            string saved = GetSavedLanguageCode();
            if (!string.IsNullOrEmpty(saved))
                return saved;

            string game = DetectGameLanguage();
            if (!string.IsNullOrEmpty(game))
                return game;

            string system = MapSystemLanguage(Application.systemLanguage);
            return string.IsNullOrEmpty(system) ? DefaultLanguage : system;
        }

        /* Tries to read the language currently used by the game. The exact API name
         * differs between game versions, so this is done via reflection and is
         * allowed to fail silently. */
        private static string DetectGameLanguage()
        {
            try
            {
                Type localeType = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type candidate = assemblies[i].GetType("ColossalFramework.Globalization.Locale", false);
                    if (candidate != null)
                    {
                        localeType = candidate;
                        break;
                    }
                }

                if (localeType == null) return null;

                string[] methodNames = { "GetLocale", "GetCurrentLanguage", "GetLanguage" };
                for (int i = 0; i < methodNames.Length; i++)
                {
                    MethodInfo method = localeType.GetMethod(
                        methodNames[i],
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        Type.EmptyTypes,
                        null);

                    if (method == null || method.ReturnType != typeof(string)) continue;

                    string value = method.Invoke(null, null) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Game language detection failed.");
                Debug.LogWarning(e);
            }

            return null;
        }

        /* Used only when the game does not expose its language. */
        private static string MapSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.ChineseSimplified: return "zh-cn";
                case SystemLanguage.ChineseTraditional: return "zh-tw";
                case SystemLanguage.Chinese: return "zh-cn";
                case SystemLanguage.English: return "en";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Danish: return "da";
                case SystemLanguage.Norwegian: return "no";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Hungarian: return "hu";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Bulgarian: return "bg";
                case SystemLanguage.Vietnamese: return "vi";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Icelandic: return "is";
                default: return null;
            }
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            return code.Trim().ToLowerInvariant().Replace('_', '-');
        }
    }
}
