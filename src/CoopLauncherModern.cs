using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xml;
using Microsoft.Win32;

internal static class CoopLauncherModern
{
    private static Window window;
    private static ScrollViewer explorePage;
    private static ScrollViewer libraryPage;
    private static Button exploreNav;
    private static Button libraryNav;
    private static Button addButton;
    private static Button primary;
    private static Button restore;
    private static Button battleOriginalButton;
    private static TextBlock gamePath;
    private static TextBlock statusTitle;
    private static TextBlock statusDetail;
    private static System.Windows.Shapes.Ellipse statusDot;
    private static FrameworkElement emptyLibrary;
    private static FrameworkElement libraryGame;
    private static FrameworkElement hollowLibraryGame;
    private static FrameworkElement rhythmLibraryGame;
    private static Button rhythmPrimaryButton;
    private static Button rhythmUpdateButton;
    private static Button rhythmOriginalButton;
    private static TextBlock rhythmStatus;
    private static TextBlock rhythmGamePath;
    private static Button hollowAddButton;
    private static Button hollowPrimaryButton;
    private static Button hollowSkinButton;
    private static Button hollowUpdateButton;
    private static Button battleUpdateButton;
    private static Button updatesButton;
    private static TextBlock updatesStatus;
    private static Button hollowOriginalButton;
    private static TextBlock hollowGamePath;
    private static TextBlock hollowStatusTitle;
    private static TextBlock hollowStatusDetail;
    private static TextBlock hollowModBadge;
    private static TextBlock battleModVersion;
    private static string selectedExe;
    private static string hollowExe;
    private static string rhythmExe;
    private static bool added;
    private static bool hollowAdded;
    private static bool rhythmAdded;
    private static ModState state;

    [STAThread]
    private static void Main()
    {
        var app = new Application();
        app.DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CoopLauncher.xaml"))
        using (XmlReader reader = XmlReader.Create(stream)) window = (Window)XamlReader.Load(reader);
        explorePage = Find<ScrollViewer>("ExplorePage");
        libraryPage = Find<ScrollViewer>("LibraryPage");
        exploreNav = Find<Button>("ExploreNav");
        libraryNav = Find<Button>("LibraryNav");
        addButton = Find<Button>("AddButton");
        primary = Find<Button>("PrimaryButton");
        restore = Find<Button>("RestoreButton");
        battleOriginalButton = Find<Button>("BattleOriginalButton");
        gamePath = Find<TextBlock>("GamePath");
        statusTitle = Find<TextBlock>("StatusTitle");
        statusDetail = Find<TextBlock>("StatusDetail");
        statusDot = Find<System.Windows.Shapes.Ellipse>("StatusDot");
        emptyLibrary = Find<FrameworkElement>("EmptyLibrary");
        libraryGame = Find<FrameworkElement>("LibraryGame");
        hollowLibraryGame = Find<FrameworkElement>("HollowLibraryGame");
        rhythmLibraryGame = Find<FrameworkElement>("RhythmLibraryGame");
        hollowAddButton = Find<Button>("HollowAddButton");
        hollowPrimaryButton = Find<Button>("HollowPrimaryButton");
        hollowSkinButton = Find<Button>("HollowSkinButton");
        hollowUpdateButton = Find<Button>("HollowUpdateButton");
        battleUpdateButton = Find<Button>("BattleUpdateButton");
        updatesButton = Find<Button>("UpdatesButton");
        updatesStatus = Find<TextBlock>("UpdatesStatus");
        hollowOriginalButton = Find<Button>("HollowOriginalButton");
        hollowGamePath = Find<TextBlock>("HollowGamePath");
        hollowStatusTitle = Find<TextBlock>("HollowStatusTitle");
        hollowStatusDetail = Find<TextBlock>("HollowStatusDetail");
        hollowModBadge = Find<TextBlock>("HollowModBadge");
        battleModVersion = Find<TextBlock>("BattleModVersion");
        rhythmPrimaryButton = Find<Button>("RhythmPrimaryButton");
        rhythmUpdateButton = Find<Button>("RhythmUpdateButton");
        rhythmOriginalButton = Find<Button>("RhythmOriginalButton");
        rhythmStatus = Find<TextBlock>("RhythmStatus");
        rhythmGamePath = Find<TextBlock>("RhythmGamePath");
        LoadArtwork();
        exploreNav.Click += (s, e) => ShowExplore();
        libraryNav.Click += (s, e) => ShowLibrary();
        Find<Button>("AboutNav").Click += (s, e) => MessageBox.Show(window,
            "Coop Launcher 0.7.0\n\nAdicione um jogo, instale o mod compatível e use Atualizações para receber novas versões.",
            "Sobre o Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
        addButton.Click += (s, e) => AddToLibrary();
        Find<Button>("EmptyExploreButton").Click += (s, e) => ShowExplore();
        Find<Button>("ChooseGameButton").Click += (s, e) => ChooseGame();
        primary.Click += (s, e) => PrimaryAction();
        restore.Click += (s, e) => RestoreAction();
        battleOriginalButton.Click += (s, e) => LaunchBattleOriginal();
        hollowAddButton.Click += (s, e) => AddHollowToLibrary();
        hollowPrimaryButton.Click += (s, e) => HollowPrimaryAction();
        hollowOriginalButton.Click += (s, e) => LaunchHollow("original");
        hollowSkinButton.Click += (s, e) => AddHollowSkin();
        hollowUpdateButton.Click += (s, e) => HollowModAction();
        battleUpdateButton.Click += (s, e) => CheckUpdates(true, false, true);
        Find<Button>("HollowChooseButton").Click += (s, e) => ChooseHollow();
        Find<Button>("RhythmAddButton").Click += (s, e) => AddRhythmToLibrary();
        Find<Button>("RhythmChooseButton").Click += (s, e) => ChooseRhythm();
        rhythmPrimaryButton.Click += (s, e) => RhythmPrimaryAction();
        rhythmOriginalButton.Click += (s, e) => LaunchRhythm(false);
        rhythmUpdateButton.Click += (s, e) => RhythmModAction();
        updatesButton.Click += (s, e) => CheckUpdates(true);
        selectedExe = LoadSavedPath();
        if (string.IsNullOrEmpty(selectedExe) || !File.Exists(selectedExe)) selectedExe = DefaultSteamPath();
        try { CoopUpdates.LoadBattlePackage(); } catch { }
        added = LoadAdded();
        hollowExe = LoadHollowPath();
        hollowAdded = File.Exists(Path.Combine(ConfigDirectory(), "hollowknight.added"));
        rhythmExe = LoadRhythmPath();
        rhythmAdded = File.Exists(Path.Combine(ConfigDirectory(), "rhythmdoctor.added"));
        Refresh();
        if (hollowAdded && HollowReady()) CreateHollowShortcuts();
        if (added || hollowAdded || rhythmAdded) ShowLibrary(); else ShowExplore();
        window.ContentRendered += (s, e) => CheckUpdates(false);
        app.Run(window);
    }

    private static T Find<T>(string name) where T : class
    {
        T control = window.FindName(name) as T;
        if (control == null) throw new InvalidOperationException("Elemento visual ausente: " + name);
        return control;
    }

    private static void LoadArtwork()
    {
        var battleblock = Artwork("BattleBlock-cover.jpg");
        Find<Image>("ExploreCover").Source = battleblock;
        Find<Image>("LibraryCover").Source = battleblock;
        var hollow = Artwork("HollowKnight-header.jpg");
        Find<Image>("ExploreHollowHero").Source = hollow;
        Find<Image>("HollowLibraryCover").Source = hollow;
    }

    private static BitmapImage Artwork(string resource)
    {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {
            if (stream == null) throw new InvalidOperationException("Arte não encontrada: " + resource);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }

    private static string ConfigDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoopLauncher");
    }

    private static void CheckUpdates(bool manual, bool familyOnly = false, bool battleOnly = false)
    {
        if (familyOnly && !HollowGameExists())
        {
            MessageBox.Show(window, "Localize primeiro o Hollow Knight.", "Atualizar Family Coop", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (battleOnly && (string.IsNullOrEmpty(selectedExe) || !File.Exists(selectedExe)))
        {
            MessageBox.Show(window, "Localize primeiro o BattleBlock Theater.", "Atualizar mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        updatesButton.IsEnabled = false;
        hollowUpdateButton.IsEnabled = false;
        battleUpdateButton.IsEnabled = false;
        rhythmUpdateButton.IsEnabled = false;
        updatesStatus.Text = "Buscando versões...";
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                UpdateManifest manifest = CoopUpdates.FetchManifest();
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    updatesButton.IsEnabled = true;
                    hollowUpdateButton.IsEnabled = true;
                    battleUpdateButton.IsEnabled = true;
                    rhythmUpdateButton.IsEnabled = RhythmDoctorMod.Exists(rhythmExe);
                    bool appUpdate = !familyOnly && !battleOnly && CoopUpdates.IsNewer(manifest.launcherVersion, CoopUpdates.LauncherVersion) && CoopUpdates.ValidAsset(manifest.launcherUrl, manifest.launcherSha256);
                    bool modUpdate = !battleOnly && HollowGameExists() && HollowModPresent() &&
                        CoopUpdates.ValidAsset(manifest.familyFullUrl, manifest.familyFullSha256) &&
                        ((manual && familyOnly) || CoopUpdates.IsNewer(manifest.familyVersion, CoopUpdates.FamilyVersion(Path.GetDirectoryName(hollowExe))));
                    bool battleUpdate = !familyOnly && !String.IsNullOrEmpty(selectedExe) &&
                        CoopUpdates.ValidAsset(manifest.battleUrl, manifest.battleSha256) &&
                        ((manual && battleOnly) || (state == ModState.Installed &&
                        (CoopUpdates.IsNewer(manifest.battleVersion, CoopUpdates.BattleVersion()) || !BattleBlockMod.LatestInstalled(selectedExe))));
                    string installedRhythm = RhythmDoctorMod.InstalledVersion(rhythmExe);
                    bool rhythmUpdate = !familyOnly && !battleOnly && rhythmAdded &&
                        installedRhythm != "0.0.0" && installedRhythm != "outra versão" &&
                        CoopUpdates.ValidAsset(manifest.rhythmUrl, manifest.rhythmSha256) &&
                        CoopUpdates.IsNewer(manifest.rhythmVersion, installedRhythm);
                    updatesStatus.Text = appUpdate || modUpdate || battleUpdate || rhythmUpdate ? "Atualização disponível" : "Tudo atualizado";
                    if (appUpdate && MessageBox.Show(window, "Nova versão do Coop Launcher: " + manifest.launcherVersion + ". Atualizar agora?", "Atualização do app", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        InstallUpdate(() => CoopUpdates.UpdateLauncher(manifest));
                        return;
                    }
                    if (modUpdate && MessageBox.Show(window, "Nova versão do Family Coop: " + manifest.familyVersion + ". Feche o jogo e atualize agora?", "Atualização do mod", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        InstallUpdate(() => CoopUpdates.InstallFamilyFull(manifest, Path.GetDirectoryName(hollowExe)));
                    if (battleUpdate && MessageBox.Show(window, "Nova versão do mod BattleBlock: " + manifest.battleVersion + ". Atualizar agora?", "Atualização do mod", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        InstallUpdate(() => CoopUpdates.UpdateBattle(manifest, selectedExe));
                    if (rhythmUpdate && MessageBox.Show(window, "Nova versão do Pulso em Equipe: " + manifest.rhythmVersion + ". Feche o jogo e atualize agora?", "Atualização do mod", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        InstallUpdate(() => CoopUpdates.UpdateRhythm(manifest, rhythmExe));
                    if (manual && !appUpdate && !modUpdate && !battleUpdate && !rhythmUpdate) MessageBox.Show(window, familyOnly && !HollowModPresent() ? "Não encontrei o mod do Hollow Knight nessa instalação. Confira o caminho do jogo e o Modding API." : "Nenhuma atualização publicada no momento.", "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            }
            catch (Exception ex)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    updatesButton.IsEnabled = true;
                    hollowUpdateButton.IsEnabled = true;
                    battleUpdateButton.IsEnabled = true;
                    rhythmUpdateButton.IsEnabled = RhythmDoctorMod.Exists(rhythmExe);
                    updatesStatus.Text = "Sem conexão com o servidor";
                    if (manual) MessageBox.Show(window, ex.Message, "Verificar atualizações", MessageBoxButton.OK, MessageBoxImage.Warning);
                }));
            }
        });
    }

    private static void InstallUpdate(Action action)
    {
        updatesStatus.Text = "Baixando e verificando...";
        updatesButton.IsEnabled = false;
        hollowUpdateButton.IsEnabled = false;
        battleUpdateButton.IsEnabled = false;
        rhythmUpdateButton.IsEnabled = false;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                action();
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    updatesStatus.Text = "Atualização instalada";
                    updatesButton.IsEnabled = true;
                    hollowUpdateButton.IsEnabled = true;
                    battleUpdateButton.IsEnabled = true;
                    rhythmUpdateButton.IsEnabled = RhythmDoctorMod.Exists(rhythmExe);
                    Refresh();
                    MessageBox.Show(window, "Pacote instalado. Abra o jogo para usar a versão nova.", "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            }
            catch (Exception ex)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    updatesButton.IsEnabled = true;
                    hollowUpdateButton.IsEnabled = true;
                    battleUpdateButton.IsEnabled = true;
                    rhythmUpdateButton.IsEnabled = RhythmDoctorMod.Exists(rhythmExe);
                    updatesStatus.Text = "Falha na atualização";
                    MessageBox.Show(window, ex.Message, "Atualização", MessageBoxButton.OK, MessageBoxImage.Warning);
                }));
            }
        });
    }

    private static string LoadSavedPath()
    {
        string path = Path.Combine(ConfigDirectory(), "battleblock.path");
        try { return File.Exists(path) ? File.ReadAllText(path).Trim() : null; }
        catch { return null; }
    }

    private static bool LoadAdded()
    {
        try { return File.Exists(Path.Combine(ConfigDirectory(), "battleblock.added")); }
        catch { return false; }
    }

    private static string LoadHollowPath()
    {
        string saved = Path.Combine(ConfigDirectory(), "hollowknight.path");
        try
        {
            string path = File.Exists(saved) ? File.ReadAllText(saved).Trim() : null;
            if (HollowGameExists(path)) return path;
        }
        catch { }
        string known = @"C:\Games\Hollow Knight - SteamGG.NET\Hollow Knight - SteamGG.NET\hollow_knight.exe";
        return HollowGameExists(known) ? known : null;
    }

    private static bool HollowGameExists(string exe = null)
    {
        exe = exe ?? hollowExe;
        return !string.IsNullOrEmpty(exe) && File.Exists(exe) &&
            string.Equals(Path.GetFileName(exe), "hollow_knight.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HollowReady(string exe = null)
    {
        exe = exe ?? hollowExe;
        if (!HollowGameExists(exe)) return false;
        string folder = Path.GetDirectoryName(exe);
        return File.Exists(Path.Combine(folder, "Escolher-Hollow-Knight.ps1")) &&
            File.Exists(Path.Combine(folder, "Adicionar-Skin.cmd")) &&
            File.Exists(Path.Combine(folder, "CoopProfiles", "family", "HollowKnightLocal8.dll"));
    }

    private static bool HollowModPresent()
    {
        if (!HollowGameExists()) return false;
        string folder = Path.GetDirectoryName(hollowExe);
        return File.Exists(Path.Combine(folder, "CoopProfiles", "family", "HollowKnightLocal8.dll")) || HollowActiveModPresent();
    }

    private static bool HollowActiveModPresent()
    {
        return HollowGameExists() && File.Exists(Path.Combine(Path.GetDirectoryName(hollowExe),
            "hollow_knight_Data", "Managed", "Mods", "HollowKnightLocal8", "HollowKnightLocal8.dll"));
    }

    private static void AddHollowToLibrary()
    {
        try
        {
            if (!HollowGameExists()) { ChooseHollow(); if (!HollowGameExists()) return; }
            Directory.CreateDirectory(ConfigDirectory());
            File.WriteAllText(Path.Combine(ConfigDirectory(), "hollowknight.path"), hollowExe);
            File.WriteAllText(Path.Combine(ConfigDirectory(), "hollowknight.added"), "1");
            hollowAdded = true;
            CreateHollowShortcuts();
            Refresh();
            ShowLibrary();
        }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static void ChooseHollow()
    {
        var dialog = new OpenFileDialog { Title = "Escolha hollow_knight.exe", Filter = "Hollow Knight (hollow_knight.exe)|hollow_knight.exe|Executáveis (*.exe)|*.exe" };
        if (dialog.ShowDialog(window) != true) return;
        hollowExe = dialog.FileName;
        if (HollowGameExists())
        {
            Directory.CreateDirectory(ConfigDirectory());
            File.WriteAllText(Path.Combine(ConfigDirectory(), "hollowknight.path"), hollowExe);
            if (hollowAdded) CreateHollowShortcuts();
        }
        Refresh();
    }

    private static void RefreshHollow()
    {
        bool ready = HollowReady();
        bool modPresent = HollowModPresent();
        bool gameExists = HollowGameExists();
        string familyVersion = gameExists ? CoopUpdates.FamilyVersion(Path.GetDirectoryName(hollowExe)) : "0.0.0";
        hollowLibraryGame.Visibility = hollowAdded ? Visibility.Visible : Visibility.Collapsed;
        hollowAddButton.Content = hollowAdded ? "Abrir" : "＋  Adicionar";
        hollowGamePath.Text = string.IsNullOrEmpty(hollowExe) ? "Jogo ainda não escolhido" : hollowExe;
        bool playable = familyVersion != "0.0.0" && (ready || HollowActiveModPresent());
        hollowPrimaryButton.Content = playable ? "▶  Jogar com Family Coop" : "Instalar Family Coop";
        hollowPrimaryButton.IsEnabled = gameExists;
        hollowUpdateButton.Content = modPresent ? "↻  Atualizar Family Coop" : "↓  Instalar mod incluído";
        hollowOriginalButton.IsEnabled = gameExists;
        hollowSkinButton.IsEnabled = ready;
        hollowStatusTitle.Text = modPresent ? "Mod encontrado" : gameExists ? "Jogo original disponível" : "Localize o jogo";
        hollowStatusDetail.Text = ready ? "Family Coop e jogo original disponíveis." : modPresent ? "Mod encontrado. Use Atualizar Family Coop para instalar nossa versão." : gameExists ? "Você já pode jogar sem mod. Family Coop ainda não está instalado." : "Escolha o executável do Hollow Knight.";
        hollowModBadge.Text = modPresent ? (familyVersion == "0.0.0" ? "MOD ENCONTRADO · ATUALIZE" : "MOD INSTALADO · V" + familyVersion) : "MOD DISPONÍVEL";
    }

    private static void HollowPrimaryAction()
    {
        if (!HollowGameExists()) { ChooseHollow(); return; }
        if (CoopUpdates.FamilyVersion(Path.GetDirectoryName(hollowExe)) == "0.0.0" ||
            (!HollowReady() && !HollowActiveModPresent()))
        {
            InstallUpdate(() => CoopUpdates.InstallFamilyBundled(Path.GetDirectoryName(hollowExe)));
            return;
        }
        LaunchHollow("family");
    }

    private static void HollowModAction()
    {
        if (!HollowGameExists()) { ChooseHollow(); return; }
        if (!HollowModPresent())
            InstallUpdate(() => CoopUpdates.InstallFamilyBundled(Path.GetDirectoryName(hollowExe)));
        else CheckUpdates(true, true);
    }

    private static void LaunchHollow(string mode)
    {
        try
        {
            if (!HollowGameExists()) { ChooseHollow(); return; }
            if (mode == "family" && CoopUpdates.FamilyVersion(Path.GetDirectoryName(hollowExe)) == "0.0.0")
                throw new InvalidOperationException("Use Atualizar Family Coop para instalar nossa versão do mod.");
            if (mode == "original" && !HollowReady())
            {
                Process.Start(new ProcessStartInfo(hollowExe) { WorkingDirectory = Path.GetDirectoryName(hollowExe) });
                return;
            }
            if (!HollowReady())
            {
                if (mode == "family" && HollowActiveModPresent() && CoopUpdates.FamilyVersion(Path.GetDirectoryName(hollowExe)) != "0.0.0")
                {
                    Process.Start(new ProcessStartInfo(hollowExe) { WorkingDirectory = Path.GetDirectoryName(hollowExe) });
                    return;
                }
                throw new InvalidOperationException("Instale o Family Coop para jogar neste modo.");
            }
            string script = Path.Combine(Path.GetDirectoryName(hollowExe), "Escolher-Hollow-Knight.ps1");
            var start = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -STA -File \"" + script + "\" -Mode " + mode);
            start.WorkingDirectory = Path.GetDirectoryName(hollowExe);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("Não foi possível ativar o modo escolhido. Feche o jogo antes de trocar de modo.");
            }
        }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Hollow Knight", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static void AddHollowSkin()
    {
        try
        {
            if (!HollowReady()) { ChooseHollow(); return; }
            Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(hollowExe), "Adicionar-Skin.cmd"))
            { WorkingDirectory = Path.GetDirectoryName(hollowExe), UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Adicionar skin", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static void CreateShortcut(string destination, string target, string arguments, string icon, string description)
    {
        Type type = Type.GetTypeFromProgID("WScript.Shell");
        if (type == null) throw new InvalidOperationException("Windows Script Host indisponível para criar atalhos.");
        object shell = Activator.CreateInstance(type);
        object shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { destination });
        Type linkType = shortcut.GetType();
        linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
        linkType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
        linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(icon) });
        linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icon + ",0" });
        linkType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
        linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, new object[0]);
    }

    private static void CreateHollowShortcuts()
    {
        if (!HollowReady()) return;
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string folder = Path.GetDirectoryName(hollowExe);
        string selector = Path.Combine(folder, "Escolher-Hollow-Knight.ps1");
        CreateShortcut(Path.Combine(desktop, "Hollow Knight - Family Coop.lnk"), "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -STA -File \"" + selector + "\" -Mode family", hollowExe,
            "Abrir Hollow Knight com nosso Family Coop");
        CreateShortcut(Path.Combine(desktop, "Adicionar skin - Hollow Knight.lnk"),
            Path.Combine(folder, "Adicionar-Skin.cmd"), "", hollowExe, "Instalar skin no Family Coop");
    }

    private static string DefaultSteamPath()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "BattleBlock Theater", BattleBlockMod.ExecutableName);
        return File.Exists(path) ? path : null;
    }

    private static void AddToLibrary()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory());
            File.WriteAllText(Path.Combine(ConfigDirectory(), "battleblock.added"), "1");
            added = true;
            Refresh();
            ShowLibrary();
        }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static void ShowExplore()
    {
        explorePage.Visibility = Visibility.Visible;
        libraryPage.Visibility = Visibility.Collapsed;
        explorePage.ScrollToTop();
        exploreNav.Background = Brush("#243B49");
        exploreNav.Foreground = Brush("#A5EFF5");
        libraryNav.Background = Brushes.Transparent;
        libraryNav.Foreground = Brush("#F1EDFA");
    }

    private static void ShowLibrary()
    {
        explorePage.Visibility = Visibility.Collapsed;
        libraryPage.Visibility = Visibility.Visible;
        libraryPage.ScrollToTop();
        libraryNav.Background = Brush("#243B49");
        libraryNav.Foreground = Brush("#A5EFF5");
        exploreNav.Background = Brushes.Transparent;
        exploreNav.Foreground = Brush("#F1EDFA");
    }

    private static void ChooseGame()
    {
        var dialog = new OpenFileDialog { Title = "Escolha BattleBlockTheater.exe", Filter = "Executáveis (*.exe)|*.exe" };
        if (dialog.ShowDialog(window) != true) return;
        selectedExe = dialog.FileName;
        if (string.Equals(Path.GetFileName(selectedExe), BattleBlockMod.ExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(ConfigDirectory());
            File.WriteAllText(Path.Combine(ConfigDirectory(), "battleblock.path"), selectedExe);
        }
        Refresh();
    }

    private static void Refresh()
    {
        try { state = BattleBlockMod.Check(selectedExe); }
        catch { state = ModState.Unsupported; }
        emptyLibrary.Visibility = (added || hollowAdded) ? Visibility.Collapsed : Visibility.Visible;
        libraryGame.Visibility = added ? Visibility.Visible : Visibility.Collapsed;
        addButton.Content = added ? "Abrir" : "＋  Adicionar";
        gamePath.Text = string.IsNullOrEmpty(selectedExe) ? "Executável ainda não escolhido" : selectedExe;
        battleModVersion.Text = state == ModState.Installed ? "Mod v" + CoopUpdates.BattleVersion() + " ativo" : "Campanha coop local";
        restore.Visibility = state == ModState.Installed ? Visibility.Visible : Visibility.Collapsed;
        battleOriginalButton.IsEnabled = state == ModState.Original || state == ModState.Installed;
        switch (state)
        {
            case ModState.Installed:
                primary.Content = "▶   Jogar com nosso mod";
                statusTitle.Text = "Pronto para jogar";
                statusDetail.Text = "Mod v" + CoopUpdates.BattleVersion() + " ativo. Pronto para abrir o jogo.";
                statusDot.Fill = Brush("#80DCC1");
                break;
            case ModState.Original:
                primary.Content = "Instalar mod";
                statusTitle.Text = "Jogo encontrado";
                statusDetail.Text = "Build compatível. Instale o mod para liberar as quatro vagas.";
                statusDot.Fill = Brush("#F2B86B");
                break;
            case ModState.Unsupported:
                primary.Content = "Trocar executável";
                statusTitle.Text = "Versão não compatível";
                statusDetail.Text = "Build não compatível. SHA-256: " + BattleBlockMod.ExecutableHash(selectedExe);
                statusDot.Fill = Brush("#EA8F9B");
                break;
            default:
                primary.Content = "Escolher .exe";
                statusTitle.Text = "Escolha o jogo";
                statusDetail.Text = "Selecione BattleBlockTheater.exe da instalação do jogo.";
                statusDot.Fill = Brush("#F2B86B");
                break;
        }
        RefreshHollow();
        RefreshRhythm();
    }

    private static string LoadRhythmPath()
    {
        string saved = Path.Combine(ConfigDirectory(), "rhythmdoctor.path");
        try
        {
            if (File.Exists(saved) && RhythmDoctorMod.Exists(File.ReadAllText(saved).Trim()))
                return File.ReadAllText(saved).Trim();
        }
        catch { }
        string local = @"C:\Games\Rhythm Doctor\Rhythm Doctor.exe";
        return RhythmDoctorMod.Exists(local) ? local : null;
    }

    private static void AddRhythmToLibrary()
    {
        if (!RhythmDoctorMod.Exists(rhythmExe)) ChooseRhythm();
        if (!RhythmDoctorMod.Exists(rhythmExe)) return;
        Directory.CreateDirectory(ConfigDirectory());
        File.WriteAllText(Path.Combine(ConfigDirectory(), "rhythmdoctor.path"), rhythmExe);
        File.WriteAllText(Path.Combine(ConfigDirectory(), "rhythmdoctor.added"), "1");
        rhythmAdded = true;
        Refresh();
        ShowLibrary();
    }

    private static void ChooseRhythm()
    {
        var dialog = new OpenFileDialog { Title = "Escolha Rhythm Doctor.exe",
            Filter = "Rhythm Doctor|Rhythm Doctor.exe|Executáveis (*.exe)|*.exe" };
        if (dialog.ShowDialog(window) != true) return;
        if (!RhythmDoctorMod.Exists(dialog.FileName))
        {
            MessageBox.Show(window, "Selecione Rhythm Doctor.exe.", "Rhythm Doctor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        rhythmExe = dialog.FileName;
        Directory.CreateDirectory(ConfigDirectory());
        File.WriteAllText(Path.Combine(ConfigDirectory(), "rhythmdoctor.path"), rhythmExe);
        Refresh();
    }

    private static void RefreshRhythm()
    {
        bool found = RhythmDoctorMod.Exists(rhythmExe);
        string version = found ? RhythmDoctorMod.InstalledVersion(rhythmExe) : "0.0.0";
        rhythmLibraryGame.Visibility = rhythmAdded ? Visibility.Visible : Visibility.Collapsed;
        Find<Button>("RhythmAddButton").Content = rhythmAdded ? "Abrir" : "＋  Adicionar";
        rhythmGamePath.Text = found ? rhythmExe : "Jogo ainda não localizado";
        bool latest = version != "0.0.0" && version != "outra versão" &&
            !CoopUpdates.IsNewer(RhythmDoctorMod.Version, version);
        rhythmStatus.Text = !found ? "Localize o jogo para instalar o mod incluído." :
            latest ? "Pulso em Equipe v" + version + " instalado · pronto para jogar." :
            version == "0.0.0" ? "Jogo encontrado · instale o mod incluído." :
            "Atualização do Pulso em Equipe disponível no app.";
        rhythmPrimaryButton.Content = !found ? "Localizar jogo" :
            latest ? "▶  Jogar com mod" : "↓  Instalar mod incluído";
        rhythmOriginalButton.IsEnabled = found;
        rhythmUpdateButton.IsEnabled = found;

    }

    private static void RhythmPrimaryAction()
    {
        if (!RhythmDoctorMod.Exists(rhythmExe)) { ChooseRhythm(); return; }
        string installed = RhythmDoctorMod.InstalledVersion(rhythmExe);
        if (installed == "0.0.0" || installed == "outra versão" ||
            CoopUpdates.IsNewer(RhythmDoctorMod.Version, installed))
        {
            InstallUpdate(() => RhythmDoctorMod.InstallBundled(rhythmExe));
            return;
        }
        LaunchRhythm(true);
    }

    private static void LaunchRhythm(bool withMod)
    {
        try { RhythmDoctorMod.Launch(rhythmExe, withMod); RefreshRhythm(); }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Rhythm Doctor", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static void RhythmModAction()
    {
        if (!RhythmDoctorMod.Exists(rhythmExe)) { ChooseRhythm(); return; }
        string installed = RhythmDoctorMod.InstalledVersion(rhythmExe);
        if (installed == "0.0.0" || installed == "outra versão" ||
            CoopUpdates.IsNewer(RhythmDoctorMod.Version, installed))
        {
            InstallUpdate(() => RhythmDoctorMod.InstallBundled(rhythmExe));
            return;
        }
        rhythmUpdateButton.IsEnabled = false;
        updatesStatus.Text = "Buscando versões...";
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                UpdateManifest manifest = CoopUpdates.FetchManifest();
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    rhythmUpdateButton.IsEnabled = true;
                    if (CoopUpdates.ValidAsset(manifest.rhythmUrl, manifest.rhythmSha256) &&
                        CoopUpdates.IsNewer(manifest.rhythmVersion, installed))
                        InstallUpdate(() => CoopUpdates.UpdateRhythm(manifest, rhythmExe));
                    else
                    {
                        updatesStatus.Text = "Tudo atualizado";
                        MessageBox.Show(window, "Pulso em Equipe já está atualizado.", "Rhythm Doctor",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }));
            }
            catch (Exception ex)
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    rhythmUpdateButton.IsEnabled = true;
                    updatesStatus.Text = "Sem conexão com o servidor";
                    MessageBox.Show(window, ex.Message, "Atualizar Pulso em Equipe",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }));
            }
        });
    }

    private static SolidColorBrush Brush(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static void PrimaryAction()
    {
        try
        {
            if (state == ModState.Missing || state == ModState.Unsupported) { ChooseGame(); return; }
            if (state == ModState.Original) { BattleBlockMod.Install(selectedExe); Refresh(); return; }
            if (state == ModState.Installed)
            {
                Process.Start(new ProcessStartInfo(selectedExe) { WorkingDirectory = Path.GetDirectoryName(selectedExe) });
            }
        }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Warning); Refresh(); }
    }

    private static void RestoreAction()
    {
        try { BattleBlockMod.Restore(selectedExe); Refresh(); }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "Coop Launcher", MessageBoxButton.OK, MessageBoxImage.Warning); Refresh(); }
    }

    private static void LaunchBattleOriginal()
    {
        try
        {
            if (state == ModState.Installed) BattleBlockMod.Restore(selectedExe);
            if (BattleBlockMod.Check(selectedExe) != ModState.Original)
                throw new InvalidOperationException("O jogo original não pôde ser confirmado.");
            Refresh();
            Process.Start(new ProcessStartInfo(selectedExe) { WorkingDirectory = Path.GetDirectoryName(selectedExe) });
        }
        catch (Exception ex) { MessageBox.Show(window, ex.Message, "BattleBlock Theater", MessageBoxButton.OK, MessageBoxImage.Warning); Refresh(); }
    }
}
