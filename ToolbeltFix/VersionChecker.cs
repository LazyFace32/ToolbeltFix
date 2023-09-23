using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Collections;
using UnityModManagerNet;
using HarmonyLib;
using UnityEngine.Events;
using System.Collections.Generic;
using MEC;

namespace ToolbeltFix
{
    internal class VersionChecker
    {
        internal static void CheckVersion(UnityModManager.ModEntry modEntry)
        {
            TextWriter backupOut = Console.Out;
            Console.SetOut(TextWriter.Null);

            bool hasNetowkrConnection = UnityModManager.HasNetworkConnection();

            Console.SetOut(backupOut);

            if (!hasNetowkrConnection && HasNetworkConnection() && modEntry.NewestVersion == null)
            {
                Timing.RunCoroutine(CheckVersion_Sequence(modEntry));
            }
        }

        private static IEnumerator<float> CheckVersion_Sequence(UnityModManager.ModEntry modEntry)
        {
            if (!string.IsNullOrEmpty(modEntry.Info.Repository))
            {
                while (!UnityModManager.UI.Instance)
                {
                    yield return 0f;
                }

                string url = modEntry.Info.Repository;

                if (UnityModManager.unityVersion < new Version(5, 4))
                {
                    UnityModManager.UI.Instance.StartCoroutine((IEnumerator)AccessTools.Method(typeof(UnityModManager), "DownloadString_5_3").Invoke(null, new object[] { url, new UnityAction<string, string>(ParseRepository) }));
                }
                else
                {
                    UnityModManager.UI.Instance.StartCoroutine((IEnumerator)AccessTools.Method(typeof(UnityModManager), "DownloadString").Invoke(null, new object[] { url, new UnityAction<string, string>(ParseRepository) }));
                }
            }
        }

        private static void ParseRepository(string json, string url)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                UnityModManager.Repository repository = TinyJson.JSONParser.FromJson<UnityModManager.Repository>(json);
                if (repository != null && repository.Releases != null && repository.Releases.Length > 0)
                {
                    foreach (UnityModManager.Repository.Release release in repository.Releases)
                    {
                        if (!string.IsNullOrEmpty(release.Id) && !string.IsNullOrEmpty(release.Version))
                        {
                            UnityModManager.ModEntry modEntry = UnityModManager.FindMod(release.Id);
                            if (modEntry != null)
                            {
                                Version ver = UnityModManager.ParseVersion(release.Version);

                                if (modEntry.Version < ver && (modEntry.NewestVersion == null || modEntry.NewestVersion < ver))
                                {
                                    modEntry.NewestVersion = ver;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityModManager.Logger.Log(string.Format("Error checking mod updates on '{0}'.", url));
                UnityModManager.Logger.Log(e.Message);
            }
        }

        internal static bool HasNetworkConnection()
        {
            try
            {
                using (var ping = new Ping())
                {
                    return ping.Send("8.8.8.8", 3000).Status == IPStatus.Success;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
