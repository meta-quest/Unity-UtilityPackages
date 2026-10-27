// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Meta.Utilities.Editor
{
    public static class VulkanCacheEditor
    {
#if UNITY_6000_0_OR_NEWER
        // Unity 6 keeps its Vulkan pipeline cache in the app's internal cache dir (Context.getCacheDir()),
        // and only writes it when the app pauses or exits normally (not on a force-stop).
        private const string INTERNAL_CACHE_FILE = "cache/vulkan_pso_cache.bin";

        public static async void SaveVulkanCache(string suffix)
        {
            var packageName = PlayerSettings.applicationIdentifier;
            var targetCachePath = $"{Application.dataPath}/psocache.androidlib/assets/vulkan_pso_cache_{suffix}.bin";
            if (!Directory.Exists(Path.GetDirectoryName(targetCachePath)))
            {
                Debug.LogError($"Failed to save Vulkan PSO Cache: the folder {Path.GetDirectoryName(targetCachePath)} doesn't exist. This tool saves the cache into the project's psocache.androidlib Android library.");
                return;
            }

            // run-as works without root on debuggable (Development) builds; reading the path directly needs adbd running as root.
            var attempts = new[]
            {
                $"exec-out run-as {packageName} cat {INTERNAL_CACHE_FILE}",
                $"exec-out cat /data/user/0/{packageName}/{INTERNAL_CACHE_FILE}",
            };
            var processMessages = "";
            foreach (var arguments in attempts)
            {
                var (data, error) = await RunAdb(arguments);
                processMessages += $"\nadb {arguments}: {data.Length} bytes{(string.IsNullOrWhiteSpace(error) ? "" : $", error: {error.Trim()}")}";
                if (!TryReadCacheUnityVersion(data, out var cacheUnityVersion))
                {
                    continue;
                }

                File.WriteAllBytes(targetCachePath, data);
                Debug.Log($"Vulkan PSO Cache ({data.Length} bytes, made by Unity {cacheUnityVersion}) successfully saved to: {targetCachePath}\n{processMessages}");
                if (Regex.Match(Application.unityVersion, @"^\d+\.\d+\.\d+").Value != cacheUnityVersion)
                {
                    Debug.LogWarning($"The saved Vulkan PSO Cache was made by Unity {cacheUnityVersion}, but this Editor is {Application.unityVersion}. Unity ignores a cache made by another Unity version.");
                }
                return;
            }

            Debug.LogError($"Failed to save Vulkan PSO Cache to: {targetCachePath}\nNo valid cache found at /data/user/0/{packageName}/{INTERNAL_CACHE_FILE}. Run the app once on the device and exit it normally (not with a force-stop) so Unity writes the cache, then save again. Reading the cache needs a Development (debuggable) build, or adb running as root.\n{processMessages}");
        }

        private static async Task<(byte[] data, string error)> RunAdb(string arguments)
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "adb";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            try
            {
                _ = process.Start();
            }
            catch (Exception e)
            {
                return (Array.Empty<byte>(), $"could not start adb ({e.Message}); make sure adb is on the PATH");
            }

            // Read stdout as raw bytes (the cache is binary), and read both streams while adb runs so it can't block on a full pipe.
            using var output = new MemoryStream();
            var copyOutput = process.StandardOutput.BaseStream.CopyToAsync(output);
            var readError = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(copyOutput, readError);
            process.WaitForExit();
            return (output.ToArray(), readError.Result);
        }

        // Unity's cache file starts with a 32-byte Unity header (format, then the Unity major, minor and patch version),
        // followed by the standard Vulkan pipeline cache header (header size 32, header version 1).
        private static bool TryReadCacheUnityVersion(byte[] data, out string unityVersion)
        {
            unityVersion = null;
            if (data.Length < 64 || BitConverter.ToUInt32(data, 32) != 32 || BitConverter.ToUInt32(data, 36) != 1)
            {
                return false;
            }

            unityVersion = $"{BitConverter.ToUInt32(data, 4)}.{BitConverter.ToUInt32(data, 8)}.{BitConverter.ToUInt32(data, 12)}";
            return true;
        }
#else
        public static async void SaveVulkanCache(string suffix)
        {
            var packageName = PlayerSettings.applicationIdentifier;
            var androidFilePath = $"/sdcard/Android/data/{packageName}/cache/vulkan_pso_cache.bin";
            var targetCachePath = $"{Application.dataPath}/psocache.androidlib/assets/vulkan_pso_cache_{suffix}.bin";
            var adbCommand = $"adb pull {androidFilePath} {targetCachePath}";
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {adbCommand}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            _ = process.Start();
            process.WaitForExit();

            var processMessages = $"\nADB Output: {await process.StandardOutput.ReadToEndAsync()}\nADB Error: {await process.StandardError.ReadToEndAsync()}";

            if (File.Exists(targetCachePath))
            {
                Debug.Log($"Vulkan PSO Cache successfully saved to: {targetCachePath}\n{processMessages}");
            }
            else
            {
                Debug.LogError($"Failed to save Vulkan PSO Cache to: {targetCachePath}\n{processMessages}");
            }
        }
#endif

        [MenuItem("Tools/PSO Cache/Save Vulkan PSO Cache (Quest 2)", priority = BuildTools.MENU_PRIORITY)]
        public static void SaveVulkanCache_Quest2()
        {
            SaveVulkanCache("quest2");
        }

        [MenuItem("Tools/PSO Cache/Save Vulkan PSO Cache (Quest 3)", priority = BuildTools.MENU_PRIORITY)]
        public static void SaveVulkanCache_Quest3()
        {
            SaveVulkanCache("quest3");
        }
    }
}
