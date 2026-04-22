using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Schema;

namespace PZAutoBackUpAndRestore
{
    public class Program
    {
        private const string AppId = "108600";
        private const string GameDirName = "ProjectZomboid";
        private const string cfgPath = "config.txt";

        static void Main()
        {
            string currSave = "";
            string saveName = "";
            string saveCat = "";
            string savePath = "";
            string savesRoot = "";
            string steamPath = GetSteamInstallPath();
            using var watcher = new FileSystemWatcher();
            bool usingAutosave = false;
            double saveInterval = 15;
            var timer = new TimerService(1, backupSave);
            DateTime _lastWriteTime = DateTime.MinValue;


            if (string.IsNullOrEmpty(steamPath))
            {
                Console.WriteLine("Steam is not installed on this system.");
                Console.ReadKey();
                return;
            }

            //Console.WriteLine($"Steam found at: {steamPath}");
            List<string> libraries = GetSteamLibraries(steamPath);
            string installPath = FindGameInLibraries(libraries, AppId);

            if (installPath != null)
            {
                //Console.WriteLine("Status: Installed");
                Console.WriteLine($"Location: {installPath}");

                string exePath = Path.Combine(installPath, "ProjectZomboid64.exe");
                if (File.Exists(exePath))
                {
                    Console.WriteLine($"Executable path: {exePath}");
                }
            }
            else
            {
                Console.WriteLine("Do you even Zomboid ? - No game found");
                Console.ReadKey();
                return;
            }

            if (File.Exists(cfgPath))
            {
                string[] lines = File.ReadAllLines(cfgPath);

                // Parse the first line as a boolean
                usingAutosave = bool.Parse(lines[0]);

                // Parse the second line as a double
                saveInterval = double.Parse(lines[1]);
                timer = new TimerService(saveInterval, backupSave);
                if (usingAutosave)
                {
                    timer.Start();
                }
                //Console.WriteLine($"Autosave: {usingAutosave}, Every: {saveInterval} mins");
            }

            savePath = GetZomboidSavePath(steamPath);

            if (Directory.Exists(savePath))
            {
                Console.WriteLine("Save Directory Found:");
                Console.WriteLine(savePath);
                string lastestSave = Path.Combine(savePath, "latestSave.ini");
                if (File.Exists(lastestSave))
                {
                    string[] lines = File.ReadAllLines(lastestSave);
                    saveName = lines[0];
                    saveCat = lines[1];
                }
                savesRoot = Path.Combine(savePath, "Saves");
                if (Directory.Exists(savesRoot))
                {
                    currSave = Path.Combine(savesRoot, saveCat, saveName);
                    Console.WriteLine($"Current save is {currSave}.");
                    watcher.Path = savePath;
                    watcher.Filter = "latestSave.ini";
                    watcher.NotifyFilter = NotifyFilters.LastWrite;
                    watcher.Changed += OnChanged;
                    watcher.EnableRaisingEvents = true;
                    // PZ saves are nested __ UNUSED
                    //string[] categories = Directory.GetDirectories(savesRoot);
                    //int totalWorlds = 0;
                    //foreach (string cat in categories)
                    //{
                    //    totalWorlds += Directory.GetDirectories(cat).Length;
                    //    foreach (var dic in Directory.GetDirectories(cat).Select(path => Path.GetFileName(path)))
                    //    {
                    //        Console.WriteLine($"Found {dic} save inside {cat}.");
                    //    }
                    //}
                    //Console.WriteLine($"Found {totalWorlds} total worlds across all save categories.");
                }
            }
            else
            {
                Console.WriteLine("Do you even Zomboid ? - No save folder found");
                Console.ReadKey();
                return;
            }
            Console.WriteLine($"Autosave is currently {(usingAutosave ? $"on, interval: {saveInterval} minutes" : "off")}");
            Console.WriteLine("Press esc to exit");
            Console.WriteLine("Press 1 to backup the save now");
            Console.WriteLine("Press 2 to change the autosave frequency");
            Console.WriteLine("Press 3 to toggle autosave");
            Console.WriteLine("Press 4 to restore to previous save");
            Console.WriteLine("Press 5 to to run game via Steam");

            var input = new ConsoleKeyInfo();
            do
            {
                input = Console.ReadKey(true);
                if (input.Key == ConsoleKey.D1)
                {
                    resetDisplay();
                    backupSave();
                }
                else if (input.Key == ConsoleKey.D3)
                {
                    timer.Stop();
                    usingAutosave = !usingAutosave;
                    if (usingAutosave) { timer.Start(); }
                    //Console.WriteLine($"Autosave is currently {(usingAutosave ? "on" : "off")}");
                    saveCfg();
                    resetDisplay();
                }

                else if (input.Key == ConsoleKey.D2)
                {
                    timer.Stop();
                    var inputDur = "";
                    do
                    {
                        Console.WriteLine("Enter the number (minutes) between saves: (If this line re-appears, you input the minutes wrong)");
                        inputDur = Console.ReadLine();
                    } while (!double.TryParse(inputDur.ToString(), out saveInterval));
                    //Console.WriteLine($"Autosave interval: {saveInterval} minutes");
                    saveCfg();
                    resetDisplay();
                    timer = new TimerService(saveInterval, backupSave);
                    timer.Start();
                }

                else if (input.Key == ConsoleKey.D4) restoreSave();

                else if (input.Key == ConsoleKey.D5) LaunchGame();
            } while (input.Key != ConsoleKey.Escape);

            //Console.WriteLine("\nPress any key to exit...");
            //Console.ReadKey();
            void saveCfg()
            {
                string[] contents = { usingAutosave.ToString(), saveInterval.ToString() };
                File.WriteAllLines("config.txt", contents);
            }

            void OnChanged(object sender, FileSystemEventArgs e)
            {

                DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);

                if (lastWriteTime != _lastWriteTime)
                {
                    _lastWriteTime = lastWriteTime;
                    string[] lines = File.ReadAllLines(e.FullPath);
                    saveName = lines[0];
                    saveCat = lines[1];
                    if (Path.Combine(savesRoot, saveCat, saveName).Equals(currSave)) return;
                    resetDisplay();
                    Console.WriteLine($"Current save changed: {saveName} in {saveCat}");
                    currSave = Path.Combine(savesRoot, saveCat, saveName);
                }
            }

            void resetDisplay()
            {
                Console.Clear();
                Console.WriteLine($"Active save: {saveName} - {saveCat}");
                Console.WriteLine($"Autosave is currently {(usingAutosave ? $"on, interval: {saveInterval} minutes" : "off")}");
                Console.WriteLine("Press esc to exit");
                Console.WriteLine("Press 1 to backup the save now");
                Console.WriteLine("Press 2 to change the autosave frequency");
                Console.WriteLine("Press 3 to toggle autosave");
                Console.WriteLine("Press 4 to restore to previous save");
                Console.WriteLine("Press 5 to to run game via Steam");
            }

            void backupSave()
            {
                //Only copy changed file if existed
                if (Directory.Exists(currSave + "_backup"))
                {
                    foreach (string newPath in Directory.GetFiles(currSave, "*.*", System.IO.SearchOption.AllDirectories))
                    {
                        string destPath = newPath.Replace(currSave, currSave + "_backup");
                        string destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        // Only copy if the file doesn't exist OR the source is newer
                        if (!File.Exists(destPath) || File.GetLastWriteTime(newPath) > File.GetLastWriteTime(destPath))
                        {
                            File.Copy(newPath, destPath, true);
                        }
                    }
                }
                //Copy all
                else FileSystem.CopyDirectory(currSave, currSave + "_backup", true);
                Console.WriteLine($"Saved to {currSave + "_backup"}.");
            }

            void restoreSave()
            {
                if (Directory.Exists(currSave + "_backup"))
                {
                    try
                    {
                        Directory.Delete(currSave, true);
                        FileSystem.CopyDirectory(currSave + "_backup", currSave, true);
                        resetDisplay();
                        Console.WriteLine($"The {saveName} save has been restored from the backup version.");
                    }
                    catch (IOException e) {
                        Console.WriteLine($"The {saveName} can't be restored, exit to menu or close the game first.");
                    }

                }
                else Console.WriteLine("No backup save folder found.");
            }
        }

        static void LaunchGame()
        {
            string steamUri = $"steam://run/{AppId}";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                // This usually happens if Steam isn't installed
                Console.WriteLine($"Could not launch game: {e.Message}");
            }
        }

        static string GetZomboidSavePath(string steamPath)
        {
            // 1. Check for the default location
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid");

            if (Directory.Exists(defaultPath))
            {
                return defaultPath;
            }

            // 2. Fallback to custom launch option
            Console.WriteLine("Default save folder not found. Checking Steam launch options for custom path...");
            string customPath = GetCustomCacheDir(steamPath);

            if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
            {
                return customPath;
            }

            return defaultPath;
        }

        static string GetCustomCacheDir(string steamPath)
        {
            string userdataPath = Path.Combine(steamPath, "userdata");
            if (!Directory.Exists(userdataPath)) return null;

            foreach (string userFolder in Directory.GetDirectories(userdataPath))
            {
                string vdfPath = Path.Combine(userFolder, "config", "localconfig.vdf");
                if (!File.Exists(vdfPath)) continue;

                try
                {
                    string content = File.ReadAllText(vdfPath);
                    string blockA = GetNestedBlock(content, "Software");
                    // We read for each block inside the vdf file
                    if (blockA != null)
                    {
                        string blockB = GetNestedBlock(blockA, "Valve");

                        if (blockB != null)
                        {
                            string blockC = GetNestedBlock(blockB, "Steam");

                            if (blockC != null)
                            {
                                string blockD = GetNestedBlock(blockC, "apps");

                                if (blockD != null)
                                {
                                    string blockE = GetNestedBlock(blockD, "108600");
                                    //Find the LaunchOption line
                                    string keyToFind = "\"LaunchOptions\"";
                                    int keyIndex = blockE.IndexOf("\"LaunchOptions\"", StringComparison.OrdinalIgnoreCase);
                                    if (keyIndex != -1)
                                    {
                                        //End of line
                                        int lineEnd = blockE.IndexOf('\n', keyIndex);

                                        //Use the end of the string if no new line
                                        if (lineEnd == -1) lineEnd = blockE.Length;

                                        //Get full line
                                        string fullLine = blockE.Substring(keyIndex, lineEnd - keyIndex);
                                        //Get the path
                                        var cleanParts = fullLine.Split('"')
                                                              .Select(p => p.Trim())
                                                              .Where(p => !string.IsNullOrEmpty(p))
                                                              .ToList();
                                        return cleanParts[2];
                                    }
                                }

                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // File might be locked by Steam, skip this user
                    continue;
                }
            }
            return null;
        }

        static string GetNestedBlock(string text, string blockName)
        {
            string key = $"\"{blockName}\"";
            int start = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start == -1) return null;

            int openBrace = text.IndexOf('{', start);
            if (openBrace == -1) return null;

            int depth = 0;
            for (int i = openBrace; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}') depth--;

                if (depth == 0)
                {
                    // Returns everything inside the { }
                    return text.Substring(openBrace + 1, i - openBrace - 1);
                }
            }
            return null;
        }

        static string GetSteamInstallPath()
        {
            // Check the Registry for Steam's installation directory
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        }

        static List<string> GetSteamLibraries(string steamPath)
        {
            List<string> libraries = new List<string>();
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(vdfPath)) return libraries;

            string vdfContent = File.ReadAllText(vdfPath);
            MatchCollection matches = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""");

            foreach (Match match in matches)
            {
                string path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(path))
                {
                    libraries.Add(path);
                }
            }

            return libraries;
        }

        static string FindGameInLibraries(List<string> libraries, string appId)
        {
            foreach (string library in libraries)
            {
                // Check for the manifest file
                string manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");

                if (File.Exists(manifestPath))
                {
                    // If manifest exists, the game folder is in 'common'
                    return Path.Combine(library, "steamapps", "common", GameDirName);
                }
            }
            return null;
        }
    }
}