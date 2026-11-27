using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal sealed class UpdateManifest
{
    public string launcherVersion { get; set; }
    public string launcherUrl { get; set; }
    public string launcherSha256 { get; set; }
    public string familyVersion { get; set; }
    public string familyUrl { get; set; }
    public string familySha256 { get; set; }
    public string battleVersion { get; set; }
    public string battleUrl { get; set; }
    public string battleSha256 { get; set; }
}

internal static class CoopUpdates
{
    internal const string LauncherVersion = "0.6.2";
    private const string ManifestUrl = "https://nick-chagas.github.io/coop-launcher/updates-v3.json";

    internal static UpdateManifest FetchManifest()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        using (var client = new WebClient())
        {
            client.Headers[HttpRequestHeader.UserAgent] = "CoopLauncher/" + LauncherVersion;
            string json = client.DownloadString(ManifestUrl);
            var manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(json);
            Version parsed;
            if (manifest == null || !Version.TryParse(manifest.launcherVersion, out parsed) ||
                !Version.TryParse(manifest.familyVersion, out parsed))
                throw new InvalidDataException("O servidor retornou um manifesto inválido.");
            return manifest;
        }
    }

    internal static bool IsNewer(string available, string installed)
    {
        Version a, b;
        return Version.TryParse(available, out a) && Version.TryParse(installed, out b) && a > b;
    }

    internal static bool ValidAsset(string url, string hash)
    {
        Uri uri;
        return Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
            Regex.IsMatch(hash ?? "", "^[a-fA-F0-9]{64}$");
    }

    internal static string FamilyVersion()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoopLauncher", "family.version");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "0.2.0";
    }

    internal static string BattleVersion()
    {
        return String.IsNullOrEmpty(BattleBlockMod.PackageVersion) ? "1.0.0" : BattleBlockMod.PackageVersion;
    }

    internal static void LoadBattlePackage()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoopLauncher", "battleblock.mod.xml");
        string checksum = path + ".sha256";
        if (!File.Exists(path) || !File.Exists(checksum)) return;
        using (var sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            if (!actual.Equals(File.ReadAllText(checksum).Trim(), StringComparison.OrdinalIgnoreCase)) return;
        }
        BattleBlockMod.LoadPackage(path);
    }

    internal static void UpdateBattle(UpdateManifest manifest, string exe)
    {
        if (Process.GetProcessesByName("BattleBlockTheater").Length != 0)
            throw new InvalidOperationException("Feche o BattleBlock Theater antes de atualizar o mod.");
        string downloaded = DownloadVerified(manifest.battleUrl, manifest.battleSha256);
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoopLauncher");
        string path = Path.Combine(folder, "battleblock.mod.xml");
        try
        {
            BattleBlockMod.LoadPackage(downloaded);
            ModState state = BattleBlockMod.Check(exe);
            if (state == ModState.Unsupported || (state != ModState.Missing && !BattleBlockMod.PackageSupports(exe)))
                throw new InvalidOperationException("Esta atualização não suporta o executável escolhido.");
            if (state == ModState.Installed) BattleBlockMod.Restore(exe);
            Directory.CreateDirectory(folder);
            File.Copy(downloaded, path, true);
            File.WriteAllText(path + ".sha256", manifest.battleSha256.ToUpperInvariant());
            if (state == ModState.Installed || state == ModState.Original) BattleBlockMod.Install(exe);
        }
        catch
        {
            if (File.Exists(path)) LoadBattlePackage();
            throw;
        }
        finally { File.Delete(downloaded); }
    }

    private static string DownloadVerified(string url, string hash)
    {
        if (!ValidAsset(url, hash)) throw new InvalidDataException("Endereço ou SHA-256 inválido no manifesto.");
        string temp = Path.Combine(Path.GetTempPath(), "CoopLauncher-" + Guid.NewGuid().ToString("N") + ".download");
        try
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "CoopLauncher/" + LauncherVersion;
                client.DownloadFile(url, temp);
            }
            using (var stream = File.OpenRead(temp))
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!actual.Equals(hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O arquivo baixado não corresponde ao SHA-256 publicado.");
            }
            return temp;
        }
        catch { if (File.Exists(temp)) File.Delete(temp); throw; }
    }

    internal static void UpdateFamily(UpdateManifest manifest, string gameRoot)
    {
        if (Process.GetProcessesByName("hollow_knight").Length != 0)
            throw new InvalidOperationException("Feche o Hollow Knight antes de atualizar o mod.");
        string downloaded = DownloadVerified(manifest.familyUrl, manifest.familySha256);
        string destination = Path.Combine(gameRoot, "CoopProfiles", "family", "HollowKnightLocal8.dll");
        string backup = destination + ".previous";
        try
        {
            if (!File.Exists(destination)) throw new FileNotFoundException("Perfil Family Coop não encontrado.", destination);
            File.Copy(destination, backup, true);
            try
            {
                ApplyFamilyPatch(downloaded, destination);
                string selector = Path.Combine(gameRoot, "Escolher-Hollow-Knight.ps1");
                var start = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -STA -File \"" + selector + "\" -Mode family -NoLaunch");
                start.WorkingDirectory = gameRoot;
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                using (var process = Process.Start(start))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0) throw new InvalidOperationException("Não foi possível ativar o mod atualizado.");
                }
                string versionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoopLauncher", "family.version");
                Directory.CreateDirectory(Path.GetDirectoryName(versionPath));
                File.WriteAllText(versionPath, manifest.familyVersion);
            }
            catch { File.Copy(backup, destination, true); throw; }
        }
        finally { File.Delete(downloaded); }
    }

    private static void ApplyFamilyPatch(string patch, string destination)
    {
        using (var input = new BinaryReader(File.OpenRead(patch)))
        {
            if (Encoding.ASCII.GetString(input.ReadBytes(4)) != "FCP1")
                throw new InvalidDataException("Formato do patch do Family Coop inválido.");
            int length = input.ReadInt32();
            if (length <= 0 || length > 30000000) throw new InvalidDataException("Tamanho de patch inválido.");
            byte[] baseHash = input.ReadBytes(32);
            byte[] targetHash = input.ReadBytes(32);
            byte[] original = File.ReadAllBytes(destination);
            if (original.Length != length) throw new InvalidDataException("Esta instalação precisa da versão anterior do Family Coop.");
            using (var sha = SHA256.Create())
            {
                if (!EqualHash(sha.ComputeHash(original), baseHash))
                    throw new InvalidDataException("O mod instalado foi alterado; não é seguro aplicar este patch.");
            }
            byte[] delta = new byte[length];
            using (var gzip = new GZipStream(input.BaseStream, CompressionMode.Decompress, true))
            {
                int offset = 0;
                while (offset < length)
                {
                    int count = gzip.Read(delta, offset, length - offset);
                    if (count == 0) throw new InvalidDataException("Patch incompleto.");
                    offset += count;
                }
                if (gzip.ReadByte() != -1) throw new InvalidDataException("Patch maior que o esperado.");
            }
            for (int i = 0; i < length; i++) original[i] ^= delta[i];
            using (var sha = SHA256.Create())
            {
                if (!EqualHash(sha.ComputeHash(original), targetHash))
                    throw new InvalidDataException("O patch não produziu a versão esperada.");
            }
            File.WriteAllBytes(destination, original);
        }
    }

    private static bool EqualHash(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int difference = 0;
        for (int i = 0; i < a.Length; i++) difference |= a[i] ^ b[i];
        return difference == 0;
    }

    internal static void UpdateLauncher(UpdateManifest manifest)
    {
        string downloaded = DownloadVerified(manifest.launcherUrl, manifest.launcherSha256);
        string destination = Process.GetCurrentProcess().MainModule.FileName;
        string script = Path.Combine(Path.GetTempPath(), "CoopLauncher-update-" + Guid.NewGuid().ToString("N") + ".ps1");
        int processId = Process.GetCurrentProcess().Id;
        string quote = "'";
        Func<string, string> ps = value => quote + value.Replace(quote, "''") + quote;
        string body = "$ErrorActionPreference = 'Stop'\r\n" +
            "Wait-Process -Id " + processId + " -ErrorAction SilentlyContinue\r\n" +
            "Start-Sleep -Milliseconds 500\r\n" +
            "Copy-Item -LiteralPath " + ps(destination) + " -Destination " + ps(destination + ".previous") + " -Force\r\n" +
            "Copy-Item -LiteralPath " + ps(downloaded) + " -Destination " + ps(destination) + " -Force\r\n" +
            "Remove-Item -LiteralPath " + ps(downloaded) + " -Force\r\n" +
            "Start-Process -FilePath " + ps(destination) + "\r\n" +
            "Remove-Item -LiteralPath $PSCommandPath -Force\r\n";
        File.WriteAllText(script, body, new UTF8Encoding(false));
        var start = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"");
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        Process.Start(start);
        Environment.Exit(0);
    }
}
