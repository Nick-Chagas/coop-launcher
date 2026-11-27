using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Xml;

internal enum ModState { Missing, Original, Installed, Unsupported }

internal static class BattleBlockMod
{
    public const string ExecutableName = "BattleBlockTheater.exe";
    public const string BackupSuffix = ".story4-original.bak";
    private const string OriginalHash = "011BB60826A798BADE8D1C39EEDB115597DFC845F3586CBF1665EF72E2A802EE";
    private const string UpdatedHash = "0E8D634BA3BABF9635A7F01ED285BEC801F77E3AF943E47872F6372EBEA153F3";

    private sealed class Patch
    {
        public int Offset;
        public byte[] Before;
        public byte[] After;
        public Patch(int offset, string before, string after)
        {
            Offset = offset;
            Before = Hex(before);
            After = Hex(after);
            if (Before.Length != After.Length) throw new InvalidOperationException("Patch inválido.");
        }
    }

    private static readonly Patch[] Patches = {
        new Patch(0x3B3648, "C787C405000004000000", "E934DDE6FF9090909090"),
        new Patch(0x221381, "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC", "C787C405000004000000C787B805000004000000C787E001000000000000E9AE221900CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"),
        new Patch(0x249729, "B802000000", "E923ACE3FF"),
        new Patch(0x84351, "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC", "B802000000837D0803740C837D0800750B837D0CFF7505B804000000E9BC531C00CCCCCCCCCCCCCCCCCCCCCCCCCCCC")
    };

    private static readonly Patch[] UpdatedPatches = {
        new Patch(0x3E3E78, "C787C405000004000000", "E9B583E4FF9090909090"),
        new Patch(0x22C232, new string('C', 124), "C787C405000004000000C787B805000004000000C787E001000000000000E92D7C1B00CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"),
        new Patch(0x2538D9, "B802000000", "E9734AE0FF"),
        new Patch(0x58351, new string('C', 94), "B802000000837D0803740C837D0800750B837D0CFF7505B804000000E96CB51F00CCCCCCCCCCCCCCCCCCCCCCCCCCCC")
    };

    private static readonly Dictionary<string, Patch[]> PackagePatches = new Dictionary<string, Patch[]>();
    private sealed class AssetPatch
    {
        public string OriginalHash;
        public Patch[] Patches;
    }
    private static AssetPatch LightingAsset;
    private static AssetPatch PreviousLightingAsset;
    public static string PackageVersion { get; private set; }

    public static void LoadPackage(string path)
    {
        var doc = new XmlDocument();
        doc.XmlResolver = null;
        doc.Load(path);
        if (doc.DocumentElement == null || doc.DocumentElement.Name != "mod") throw new InvalidOperationException("Pacote do mod inválido.");
        string version = doc.DocumentElement.GetAttribute("version");
        Version.Parse(version);
        var builds = new Dictionary<string, Patch[]>();
        foreach (XmlNode node in doc.DocumentElement.SelectNodes("build"))
        {
            var build = (XmlElement)node;
            string hash = build.GetAttribute("sha256").ToUpperInvariant();
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidOperationException("Hash de build inválido.");
            Patch[] patches = ReadPatches(build);
            if (builds.ContainsKey(hash)) throw new InvalidOperationException("Build do mod duplicada.");
            builds.Add(hash, patches);
        }
        if (builds.Count == 0) throw new InvalidOperationException("Pacote sem builds.");
        AssetPatch lighting = null;
        XmlElement asset = doc.DocumentElement.SelectSingleNode("asset") as XmlElement;
        if (asset != null)
        {
            if (asset.GetAttribute("path") != "data/shaders.pak") throw new InvalidOperationException("Arquivo adicional não permitido.");
            string hash = asset.GetAttribute("sha256").ToUpperInvariant();
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidOperationException("Hash de shader inválido.");
            lighting = new AssetPatch { OriginalHash = hash, Patches = ReadPatches(asset) };
        }
        PackagePatches.Clear();
        foreach (var build in builds) PackagePatches.Add(build.Key, build.Value);
        PreviousLightingAsset = LightingAsset;
        LightingAsset = lighting;
        PackageVersion = version;
    }

    private static Patch[] ReadPatches(XmlElement parent)
    {
        var patches = new List<Patch>();
        foreach (XmlNode item in parent.SelectNodes("patch"))
        {
            var element = (XmlElement)item;
            int offset = Convert.ToInt32(element.GetAttribute("offset"), 16);
            var patch = new Patch(offset, element.GetAttribute("before"), element.GetAttribute("after"));
            if (patch.Before.Length == 0 || patch.Offset < 0) throw new InvalidOperationException("Patch vazio ou deslocamento inválido.");
            patches.Add(patch);
        }
        if (patches.Count == 0) throw new InvalidOperationException("Pacote sem patches.");
        patches = patches.OrderBy(p => p.Offset).ToList();
        for (int i = 1; i < patches.Count; i++)
            if (patches[i - 1].Offset + patches[i - 1].Before.Length > patches[i].Offset)
                throw new InvalidOperationException("Patches sobrepostos.");
        return patches.ToArray();
    }

    public static bool PackageSupports(string exe)
    {
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
        string backup = exe + BackupSuffix;
        string original = File.Exists(backup) && Check(exe) == ModState.Installed ? backup : exe;
        string hash = Hash(File.ReadAllBytes(original));
        Patch[] patches;
        if (!PackagePatches.TryGetValue(hash, out patches)) return false;
        Apply(File.ReadAllBytes(original), patches);
        PreparedLighting(exe);
        return true;
    }

    public static bool LatestInstalled(string exe)
    {
        if (Check(exe) != ModState.Installed) return false;
        string backup = exe + BackupSuffix;
        byte[] original = File.ReadAllBytes(backup);
        Patch[] patches;
        return PackagePatches.TryGetValue(Hash(original), out patches) &&
            File.ReadAllBytes(exe).SequenceEqual(Apply(original, patches)) && LightingInstalled(exe);
    }

    private static bool Supported(string hash) { return hash == OriginalHash || hash == UpdatedHash || PackagePatches.ContainsKey(hash); }

    public static ModState Check(string exe)
    {
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return ModState.Missing;
        if (!string.Equals(Path.GetFileName(exe), ExecutableName, StringComparison.OrdinalIgnoreCase)) return ModState.Unsupported;
        byte[] current = File.ReadAllBytes(exe);
        string currentHash = Hash(current);
        if (Supported(currentHash)) return ModState.Original;
        string backup = exe + BackupSuffix;
        if (File.Exists(backup))
        {
            byte[] original = File.ReadAllBytes(backup);
            if (Supported(Hash(original)) && KnownPatched(original, current)) return ModState.Installed;
        }
        return ModState.Unsupported;
    }

    public static string ExecutableHash(string exe)
    {
        return File.Exists(exe) ? Hash(File.ReadAllBytes(exe)) : null;
    }

    public static void Install(string exe)
    {
        RequireClosed();
        if (Check(exe) != ModState.Original) throw new InvalidOperationException("O executável não corresponde à versão suportada.");
        string backup = exe + BackupSuffix;
        string temp = exe + ".cooplauncher.tmp";
        byte[] original = File.ReadAllBytes(exe);
        if (File.Exists(backup) && Hash(File.ReadAllBytes(backup)) != Hash(original))
        {
            string archived = backup + ".old-build";
            if (Hash(File.ReadAllBytes(backup)) != OriginalHash || File.Exists(archived))
                throw new InvalidOperationException("Já existe um backup diferente. Nenhum arquivo foi alterado.");
            File.Move(backup, archived);
        }
        byte[] modified = Patched(original);
        byte[] lighting = PreparedLighting(exe);
        if (!File.Exists(backup)) File.WriteAllBytes(backup, original);
        if (Hash(File.ReadAllBytes(backup)) != Hash(original)) throw new InvalidOperationException("Não foi possível verificar o backup.");
        bool exeReplaced = false;
        try
        {
            File.WriteAllBytes(temp, modified);
            InstallLighting(exe, lighting);
            File.Replace(temp, exe, null);
            exeReplaced = true;
            if (Check(exe) != ModState.Installed) throw new InvalidOperationException("Instalação não verificada; backup preservado.");
        }
        catch
        {
            if (exeReplaced)
            {
                File.WriteAllBytes(temp, original);
                File.Replace(temp, exe, null);
            }
            if (lighting != null) RestoreLighting(exe);
            throw;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Restore(string exe)
    {
        RequireClosed();
        if (Check(exe) != ModState.Installed) throw new InvalidOperationException("Patch ou backup diferente do esperado.");
        string backup = exe + BackupSuffix;
        string temp = exe + ".cooplauncher.tmp";
        byte[] original = File.ReadAllBytes(backup);
        try
        {
            RestoreLighting(exe);
            File.WriteAllBytes(temp, original);
            File.Replace(temp, exe, null);
            if (Hash(File.ReadAllBytes(exe)) != Hash(original)) throw new InvalidOperationException("Restauração não verificada; backup preservado.");
            File.Delete(backup);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string LightingPath(string exe)
    {
        return Path.Combine(Path.GetDirectoryName(exe), "data", "shaders.pak");
    }

    private static byte[] PreparedLighting(string exe)
    {
        if (LightingAsset == null) return null;
        string path = LightingPath(exe);
        string backup = path + ".cooplauncher-original.bak";
        if (!File.Exists(path)) throw new InvalidOperationException("Arquivo de iluminação não encontrado.");
        byte[] current = File.ReadAllBytes(path);
        byte[] original = File.Exists(backup) ? File.ReadAllBytes(backup) : current;
        if (Hash(original) != LightingAsset.OriginalHash)
            throw new InvalidOperationException("Versão do arquivo de iluminação não suportada.");
        byte[] patched = Apply(original, LightingAsset.Patches);
        bool previous = PreviousLightingAsset != null && PreviousLightingAsset.OriginalHash == LightingAsset.OriginalHash &&
            current.SequenceEqual(Apply(original, PreviousLightingAsset.Patches));
        if (!current.SequenceEqual(original) && !current.SequenceEqual(patched) && !previous)
            throw new InvalidOperationException("Arquivo de iluminação foi modificado por outro programa.");
        return patched;
    }

    private static bool LightingInstalled(string exe)
    {
        if (LightingAsset == null) return true;
        string path = LightingPath(exe);
        if (!File.Exists(path)) return false;
        try { return File.ReadAllBytes(path).SequenceEqual(PreparedLighting(exe)); }
        catch { return false; }
    }

    private static void InstallLighting(string exe, byte[] patched)
    {
        if (patched == null) return;
        string path = LightingPath(exe);
        string backup = path + ".cooplauncher-original.bak";
        string temp = path + ".cooplauncher.tmp";
        if (File.ReadAllBytes(path).SequenceEqual(patched)) return;
        if (!File.Exists(backup)) File.Copy(path, backup);
        if (Hash(File.ReadAllBytes(backup)) != LightingAsset.OriginalHash)
            throw new InvalidOperationException("Backup da iluminação inválido.");
        try
        {
            File.WriteAllBytes(temp, patched);
            File.Replace(temp, path, null);
            if (!File.ReadAllBytes(path).SequenceEqual(patched)) throw new InvalidOperationException("Falha ao verificar iluminação.");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void RestoreLighting(string exe)
    {
        string path = LightingPath(exe);
        string backup = path + ".cooplauncher-original.bak";
        if (!File.Exists(backup)) return;
        byte[] original = File.ReadAllBytes(backup);
        if (LightingAsset != null && Hash(original) != LightingAsset.OriginalHash)
            throw new InvalidOperationException("Backup da iluminação inválido.");
        string temp = path + ".cooplauncher.tmp";
        try
        {
            File.WriteAllBytes(temp, original);
            File.Replace(temp, path, null);
            if (!File.ReadAllBytes(path).SequenceEqual(original)) throw new InvalidOperationException("Falha ao restaurar iluminação.");
            File.Delete(backup);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void RequireClosed()
    {
        if (Process.GetProcessesByName("BattleBlockTheater").Length != 0)
            throw new InvalidOperationException("Feche o jogo antes de instalar ou restaurar o mod.");
    }

    private static byte[] Patched(byte[] original)
    {
        string hash = Hash(original);
        if (!Supported(hash)) throw new InvalidOperationException("Build não suportado.");
        Patch[] patches;
        if (!PackagePatches.TryGetValue(hash, out patches)) patches = hash == OriginalHash ? Patches : UpdatedPatches;
        return Apply(original, patches);
    }

    private static bool KnownPatched(byte[] original, byte[] current)
    {
        string hash = Hash(original);
        Patch[] patches;
        if (PackagePatches.TryGetValue(hash, out patches) && current.SequenceEqual(Apply(original, patches))) return true;
        if (hash == OriginalHash && current.SequenceEqual(Apply(original, Patches))) return true;
        return hash == UpdatedHash && current.SequenceEqual(Apply(original, UpdatedPatches));
    }

    private static byte[] Apply(byte[] original, Patch[] patches)
    {
        byte[] output = (byte[])original.Clone();
        foreach (Patch patch in patches)
        {
            if (patch.Offset > original.Length - patch.Before.Length) throw new InvalidOperationException("Patch fora do executável.");
            if (!original.Skip(patch.Offset).Take(patch.Before.Length).SequenceEqual(patch.Before))
                throw new InvalidOperationException("Bytes originais inesperados.");
            Buffer.BlockCopy(patch.After, 0, output, patch.Offset, patch.After.Length);
        }
        return output;
    }

    private static string Hash(byte[] data)
    {
        using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
    }

    private static byte[] Hex(string value)
    {
        byte[] result = new byte[value.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return result;
    }
}
