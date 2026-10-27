// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            var (device, deviceError) = await SelectDevice(suffix);
            if (deviceError != null)
            {
                Debug.LogError($"Failed to save Vulkan PSO Cache: {deviceError}");
                return;
            }

            // run-as works without root on debuggable (Development) builds; reading the path directly needs adbd running as root.
            var deviceArgument = device == null ? "" : $"-s {device.Serial} ";
            var attempts = new[]
            {
                $"{deviceArgument}exec-out run-as {packageName} cat {INTERNAL_CACHE_FILE}",
                $"{deviceArgument}exec-out cat /data/user/0/{packageName}/{INTERNAL_CACHE_FILE}",
            };
            var processMessages = $"\nDevice: {device?.ToString() ?? $"ANDROID_SERIAL={Environment.GetEnvironmentVariable("ANDROID_SERIAL")}"}";
            foreach (var arguments in attempts)
            {
                var (data, error) = await RunAdb(arguments);
                processMessages += $"\nadb {arguments}: {DescribeOutput(data)}{(string.IsNullOrWhiteSpace(error) ? "" : $", error: {error.Trim()}")}";
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

        private sealed class AdbDevice
        {
            public string Serial, State, Codename, Model;
            public override string ToString() => $"{Model ?? Codename ?? "unknown"} ({Serial}{(State == "device" ? "" : $", {State}")})";
        }

        // The device codename (the "device:" field of `adb devices -l`) of the headset each menu item saves the cache for.
        private static readonly Dictionary<string, string> s_deviceCodenames = new()
        {
            ["quest2"] = "hollywood",
            ["quest3"] = "eureka",
        };

        // Picks the connected headset matching the menu item. A cache made on another headset model is for another GPU, so Unity
        // would ignore it: never save one under the wrong name unless ANDROID_SERIAL explicitly selects that headset.
        // Returns a null device when ANDROID_SERIAL is set, since adb then uses it.
        private static async Task<(AdbDevice device, string error)> SelectDevice(string suffix)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_SERIAL")))
            {
                return (null, null);
            }

            var (data, error) = await RunAdb("devices -l");
            var devices = ParseDevices(Encoding.UTF8.GetString(data));
            var ready = devices.Where(d => d.State == "device").ToList();
            if (ready.Count == 0)
            {
                return (null, $"no headset is connected and authorized.{(devices.Count == 0 ? "" : $" adb devices: {string.Join(", ", devices)}.")}{(string.IsNullOrWhiteSpace(error) ? "" : $" adb error: {error.Trim()}")}");
            }

            return ChooseDevice(ready, suffix);
        }

        private static (AdbDevice device, string error) ChooseDevice(List<AdbDevice> ready, string suffix)
        {
            _ = s_deviceCodenames.TryGetValue(suffix, out var codename);
            var matching = ready.Where(d => d.Codename == codename).ToList();
            if (matching.Count == 1)
            {
                return (matching[0], null);
            }

            var connected = ready.Count == 1 ? $"the connected headset is {ready[0]}, not" : $"{ready.Count} headsets are connected ({string.Join(", ", ready)}) and {(matching.Count == 0 ? "none of them is" : $"{matching.Count} of them are")}";
            var fix = matching.Count == 0
                ? $"Connect a {suffix}, or start the Editor with ANDROID_SERIAL set to a headset's serial to save its cache as {suffix} anyway (a cache made on another headset model is ignored by Unity)."
                : "Connect only the headset to read from, or start the Editor with ANDROID_SERIAL set to its serial.";
            return (null, $"{connected} a {suffix} ({codename}). {fix}");
        }

        private static List<AdbDevice> ParseDevices(string devicesOutput)
        {
            var devices = new List<AdbDevice>();
            foreach (var line in devicesOutput.Split('\n'))
            {
                var fields = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 2 || line.StartsWith("List of devices") || line.StartsWith("*"))
                {
                    continue;
                }

                string Field(string key) => fields.Skip(2).Where(f => f.StartsWith(key + ":")).Select(f => f.Substring(key.Length + 1)).FirstOrDefault();
                devices.Add(new AdbDevice { Serial = fields[0], State = fields[1], Codename = Field("device"), Model = Field("model") });
            }
            return devices;
        }

        // adb prints its errors (e.g. "run-as: package not debuggable") on stdout with exec-out, so show short text output.
        private static string DescribeOutput(byte[] data)
        {
            var isShortText = data.Length is > 0 and <= 512 && data.All(b => b is >= 0x20 and < 0x7f or (byte)'\n' or (byte)'\r' or (byte)'\t');
            return isShortText ? $"{data.Length} bytes: \"{Encoding.ASCII.GetString(data).Trim()}\"" : $"{data.Length} bytes";
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
