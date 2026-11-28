using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

internal static class RhythmDoctorMod
{
    internal const string Version = "0.4.0";
    private const string BundleHash = "F2C425DD72D042941FECA65318DE808198C228898F8CFFA3731B5545FF3F40E9";
    private const string PluginHash = "BD695AE2DA358FD14D0A9C981EF47F95632D48D144E57CAC139896B1828651C4";
    private const string PreviousPluginHash = "7AC4CDC9572804D42FBFCFA69A27A9D7E3C8E4721E5561142CC558C01820E437";
    private static readonly string[] DefaultKeys = { "D", "F", "J", "K" };

    internal static bool Exists(string exe)
    {
        return !String.IsNullOrWhiteSpace(exe) && File.Exists(exe) &&
            Path.GetFileName(exe).Equals("Rhythm Doctor.exe", StringComparison.OrdinalIgnoreCase);
    }

    internal static string Root(string exe) { return Path.GetDirectoryName(exe); }

    internal static string InstalledVersion(string exe)
    {
        if (!Exists(exe)) return "0.0.0";
        string plugin = Path.Combine(Root(exe), "BepInEx", "plugins", "CoopPulse", "CoopPulse.dll");
        if (!File.Exists(plugin)) return "0.0.0";
        string hash = Hash(File.ReadAllBytes(plugin));
        if (hash == PluginHash) return Version;
        if (hash == PreviousPluginHash) return "0.3.0";
        if (hash == "57B27820F2F6579729F19387FF7F107FD44D7D81F49DB87C9212FD509EE441D7") return "0.2.0";
        string marker = Path.Combine(Root(exe), "BepInEx", "plugins", "CoopPulse", "version.txt");
        if (File.Exists(marker))
        {
            string[] parts = File.ReadAllText(marker).Trim().Split('|');
            System.Version parsed;
            if (parts.Length == 2 && System.Version.TryParse(parts[0], out parsed) &&
                hash.Equals(parts[1], StringComparison.OrdinalIgnoreCase)) return parts[0];
        }
        return "outra versão";
    }

    internal static void InstallBundled(string exe)
    {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RhythmDoctorBundle.zip"))
        {
            if (stream == null) throw new InvalidDataException("Pacote do Rhythm Doctor ausente do app.");
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                InstallPackage(exe, memory.ToArray(), BundleHash, Version, PluginHash);
            }
        }
    }

    internal static void InstallPackage(string exe, byte[] bytes, string expectedHash, string version, string pluginHash)
    {
        if (!Exists(exe)) throw new FileNotFoundException("Localize Rhythm Doctor.exe antes de instalar o mod.");
        if (Process.GetProcessesByName("Rhythm Doctor").Length != 0)
            throw new InvalidOperationException("Feche o Rhythm Doctor antes de instalar ou atualizar o mod.");
        if (!Hash(bytes).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O pacote do mod não passou na verificação de integridade.");
        string root = Root(exe);
        using (var memory = new MemoryStream(bytes))
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Read))
        {
            ZipArchiveEntry loader = archive.GetEntry("winhttp.dll");
            if (loader == null) throw new InvalidDataException("Loader ausente do pacote.");
            ZipArchiveEntry plugin = archive.GetEntry("BepInEx/plugins/CoopPulse/CoopPulse.dll");
            if (plugin == null) throw new InvalidDataException("Plugin ausente do pacote.");
            using (var input = plugin.Open())
            using (var copy = new MemoryStream())
            {
                input.CopyTo(copy);
                if (!Hash(copy.ToArray()).Equals(pluginHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O plugin não corresponde ao SHA-256 publicado.");
            }
            string existing = Path.Combine(root, "winhttp.dll");
            if (File.Exists(existing))
            {
                using (var input = loader.Open())
                using (var copy = new MemoryStream())
                {
                    input.CopyTo(copy);
                    if (Hash(File.ReadAllBytes(existing)) != Hash(copy.ToArray()))
                        throw new InvalidOperationException("Já existe outro loader winhttp.dll no jogo. O app preservou essa instalação.");
                }
            }
            string absoluteRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (String.IsNullOrEmpty(entry.Name)) continue;
                string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string target = Path.GetFullPath(Path.Combine(root, relative));
                if (!target.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Caminho inválido no pacote do mod.");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var input = entry.Open())
                using (var output = File.Create(target)) input.CopyTo(output);
            }
        }
        string installedPlugin = Path.Combine(root, "BepInEx", "plugins", "CoopPulse", "CoopPulse.dll");
        if (!Hash(File.ReadAllBytes(installedPlugin)).Equals(pluginHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O mod copiado não corresponde à versão esperada.");
        File.WriteAllText(Path.Combine(root, "BepInEx", "plugins", "CoopPulse", "version.txt"),
            version + "|" + pluginHash);
    }

    internal static int Players(string exe)
    {
        int value;
        return Int32.TryParse(Get(exe, "Coop", "Players", "4"), out value) ? Math.Max(1, Math.Min(4, value)) : 4;
    }

    internal static int Pad(string exe, int slot)
    {
        int value;
        return Int32.TryParse(Get(exe, "Jogador " + slot, "Controle", "0"), out value) ? Math.Max(0, Math.Min(8, value)) : 0;
    }

    internal static string Key(string exe, int slot)
    {
        return Get(exe, "Jogador " + slot, "Tecla", DefaultKeys[slot - 1]);
    }

    internal static void SetPlayers(string exe, int count)
    {
        EnsureStopped(exe);
        Set(exe, "Coop", "Players", Math.Max(1, Math.Min(4, count)).ToString());
    }

    internal static void SetBinding(string exe, int slot, int pad)
    {
        EnsureStopped(exe);
        if (slot < 1 || slot > 4 || pad < 0 || pad > 8) throw new ArgumentOutOfRangeException();
        Set(exe, "Jogador " + slot, "Controle", pad.ToString());
        Set(exe, "Jogador " + slot, "Botao", "0");
        if (pad == 0) Set(exe, "Jogador " + slot, "Tecla", DefaultKeys[slot - 1]);
    }

    internal static void Launch(string exe, bool withMod)
    {
        if (!Exists(exe)) throw new FileNotFoundException("Localize Rhythm Doctor.exe.");
        string version = InstalledVersion(exe);
        if (withMod && (version == "0.0.0" || version == "outra versão")) InstallBundled(exe);
        if (InstalledVersion(exe) != "0.0.0") Set(exe, "Coop", "Enabled", withMod ? "true" : "false");
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Root(exe) });
    }

    private static void EnsureStopped(string exe)
    {
        if (!Exists(exe)) throw new FileNotFoundException("Localize Rhythm Doctor.exe.");
        if (Process.GetProcessesByName("Rhythm Doctor").Length != 0)
            throw new InvalidOperationException("Feche o jogo para configurar no app. Durante o jogo, use F6.");
    }

    private static string ConfigPath(string exe)
    {
        return Path.Combine(Root(exe), "BepInEx", "config", "nickchagas.cooplauncher.rhythmdoctor.cfg");
    }

    private static string Get(string exe, string section, string key, string fallback)
    {
        if (!Exists(exe) || !File.Exists(ConfigPath(exe))) return fallback;
        string current = "";
        foreach (string line in File.ReadAllLines(ConfigPath(exe)))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) current = trimmed.Substring(1, trimmed.Length - 2);
            else if (current == section && trimmed.StartsWith(key + " =", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring(trimmed.IndexOf('=') + 1).Trim();
        }
        return fallback;
    }

    private static void Set(string exe, string section, string key, string value)
    {
        string path = ConfigPath(exe);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
        string current = "";
        int sectionEnd = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                if (current == section && sectionEnd < 0) sectionEnd = i;
                current = trimmed.Substring(1, trimmed.Length - 2);
            }
            else if (current == section && trimmed.StartsWith(key + " =", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = key + " = " + value;
                File.WriteAllLines(path, lines.ToArray());
                return;
            }
        }
        if (current == section && sectionEnd < 0) sectionEnd = lines.Count;
        if (sectionEnd >= 0) lines.Insert(sectionEnd, key + " = " + value);
        else { lines.Add("[" + section + "]"); lines.Add(key + " = " + value); }
        File.WriteAllLines(path, lines.ToArray());
    }

    private static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
