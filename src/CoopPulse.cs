using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CoopLauncher.RhythmDoctor
{
    [BepInPlugin("nickchagas.cooplauncher.rhythmdoctor", "Coop Pulse", "0.4.0")]
    public sealed class CoopPulse : BaseUnityPlugin
    {
        private static CoopPulse instance;
        private Harmony harmony;
        private ConfigEntry<int> players;
        private ConfigEntry<bool> coopEnabled;
        private readonly KeyCode[] defaultKeys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        private readonly ConfigEntry<KeyCode>[] keys = new ConfigEntry<KeyCode>[4];
        private readonly ConfigEntry<int>[] pads = new ConfigEntry<int>[4];
        private readonly ConfigEntry<int>[] buttons = new ConfigEntry<int>[4];
        private readonly ConfigEntry<int>[] characters = new ConfigEntry<int>[4];
        private readonly ConfigEntry<int>[] hearts = new ConfigEntry<int>[4];
        private readonly string[] characterNames = { "IAN", "NICOLE", "PAIGE", "SAMURAI",
            "MINEIRO", "ATLETA", "BARISTA", "POLITICO" };
        private readonly string[] characterTextures = { "RWL_icon_Ian", "RWL_icon_Nicole_1",
            "RWL_icon_Paige", "RWL_icon_Samurai", "RWL_icon_Miner", "RWL_icon_Athlete",
            "RWL_icon_SmokinBarista", "RWL_icon_Politician" };
        private Texture2D[] portraits;
        private bool portraitsLoaded;
        private readonly int[] hits = new int[4];
        private readonly int[] misses = new int[4];
        private readonly float[] feedbackUntil = new float[4];
        private readonly bool[] feedbackGood = new bool[4];
        private readonly float[] inputFlashUntil = new float[4];
        private sealed class LaneState
        {
            public Beat Target;
            public double TargetTime;
            public readonly bool[] Pressed = new bool[4];
            public readonly double[] PressTimes = new double[4];
            public readonly HashSet<Beat> Consumed = new HashSet<Beat>();
            public bool Completed;
            public bool Holding;
        }
        private readonly LaneState[] lanes = { new LaneState(), new LaneState() };
        private readonly LaneState sharedLane = new LaneState();
        private readonly Color[] colors = { new Color(0.31f, 0.81f, 1f), new Color(1f, 0.58f, 0.55f),
            new Color(0.58f, 1f, 0.62f), new Color(1f, 0.83f, 0.38f) };
        private scnGame lastGame;
        private LevelBase lastLevel;
        private double previousAudioPos = Double.NegativeInfinity;
        private bool dualHeartLevel;
        private bool lastP1Press;
        private bool lastP2Press;
        private int lastFrame = -1;
        private bool menuOpen;
        private int menuTab = 1;
        private int listening = -1;
        private string menuMessage = "APERTE F6 PARA CONFIGURAR OS JOGADORES";
        private Texture2D pixel;
        private GUIStyle titleStyle;
        private GUIStyle rowStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private bool loggedGui;
        private bool loggedGame;
        private bool loggedInput;

        private void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            instance = this;
            players = Config.Bind("Coop", "Players", 4, "1 a 4 jogadores locais.");
            coopEnabled = Config.Bind("Coop", "Enabled", true, "F7 liga ou desliga o pulso coletivo.");
            for (int i = 0; i < 4; i++)
            {
                int n = i + 1;
                keys[i] = Config.Bind("Jogador " + n, "Tecla", defaultKeys[i], "Tecla individual.");
                pads[i] = Config.Bind("Jogador " + n, "Controle", 0, "0 = teclado; 1 a 8 = número do controle.");
                buttons[i] = Config.Bind("Jogador " + n, "Botao", 0, "Botão de pulso no controle.");
                characters[i] = Config.Bind("Jogador " + n, "Personagem", i, "Personagem exibido no painel do mod.");
                hearts[i] = Config.Bind("Jogador " + n, "Coracao", i < 2 ? 0 : 1, "0 = coração de cima; 1 = coração de baixo.");
            }
            BalanceHearts(-1);
            harmony = new Harmony("nickchagas.cooplauncher.rhythmdoctor");
            harmony.PatchAll(typeof(CoopPulse));
            Logger.LogInfo("Coop Pulse carregado: " + Players + " jogadores");
        }

        private void OnDestroy()
        {
            Logger.LogInfo("Objeto visual do Coop Pulse encerrado; hooks de entrada preservados");
        }

        private int Players { get { return Math.Max(1, Math.Min(4, players.Value)); } }

        [HarmonyPatch(typeof(RDInput), "Update")]
        [HarmonyPostfix]
        private static void AfterInput()
        {
            if (!System.Object.ReferenceEquals(instance, null))
            {
                if (!instance.loggedInput) { instance.Logger.LogInfo("Hook de entrada ativo"); instance.loggedInput = true; }
                instance.ProcessInput();
            }
        }

        [HarmonyPatch(typeof(scnGame), "OnGUI")]
        [HarmonyPostfix]
        private static void AfterGameGui()
        {
            if (!System.Object.ReferenceEquals(instance, null)) instance.OnGUI();
        }

        [HarmonyPatch(typeof(scnBase), "OnGUI")]
        [HarmonyPostfix]
        private static void AfterMenuGui()
        {
            if (!System.Object.ReferenceEquals(instance, null) && scnGame.instance == null) instance.OnGUI();
        }

        private void ProcessInput()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (Input.GetKeyDown(KeyCode.F6))
            {
                menuOpen = !menuOpen;
                listening = -1;
                ResetTarget(false);
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                coopEnabled.Value = !coopEnabled.Value;
                ResetTarget(false);
                Logger.LogInfo("Coop Pulse " + (coopEnabled.Value ? "ligado" : "desligado"));
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                players.Value = Players == 4 ? 1 : Players + 1;
                BalanceHearts(-1);
                ResetTarget(false);
            }
            if (menuOpen && listening >= 0) ListenForBinding();
            for (int i = 0; i < Players; i++)
                if (GetDown(i)) inputFlashUntil[i] = Time.unscaledTime + 0.35f;
            if (!coopEnabled.Value) return;

            scnGame game = scnGame.instance;
            if (game == null || !game.started || game.currentLevel == null)
            {
                ResetTarget(false);
                return;
            }
            if (!loggedGame) { Logger.LogInfo("Processando entradas na fase"); loggedGame = true; }
            if (!System.Object.ReferenceEquals(game, lastGame) ||
                !System.Object.ReferenceEquals(game.currentLevel, lastLevel))
            {
                lastGame = game;
                lastLevel = game.currentLevel;
                ClearConsumed();
                Array.Clear(hits, 0, hits.Length);
                Array.Clear(misses, 0, misses.Length);
                previousAudioPos = Double.NegativeInfinity;
                dualHeartLevel = false;
                ResetTarget(false);
            }
            double now = scrConductor.instance.audioPos;
            if (now + 1.0 < previousAudioPos)
            {
                ClearConsumed();
                ResetTarget(false);
            }
            previousAudioPos = now;
            DetectDualHeartLevel(game);
            ClearNativeInput();
            if (menuOpen) return;

            if (dualHeartLevel)
            {
                ProcessLane(game, now, lanes[0], 0, false);
                ProcessLane(game, now, lanes[1], 1, false);
                ProcessLane(game, now, sharedLane, -1, true);
            }
            else
                ProcessLane(game, now, sharedLane, -1, false);
            RDInput.anyPlayerPress = RDInput.p1Press || RDInput.p2Press;
            RDInput.anyPlayerIsPressed = RDInput.p1IsPressed || RDInput.p2IsPressed;
            RDInput.anyPlayerRelease = RDInput.p1Release || RDInput.p2Release;
        }

        private void DetectDualHeartLevel(scnGame game)
        {
            if (dualHeartLevel || game.beats == null) return;
            bool top = false, bottom = false;
            foreach (Beat beat in game.beats)
            {
                if (beat == null || beat.player == RDPlayer.CPU) continue;
                top |= beat.player == RDPlayer.P1;
                bottom |= beat.player == RDPlayer.P2;
                if (top && bottom)
                {
                    dualHeartLevel = true;
                    ResetTarget(false);
                    Logger.LogInfo("Fase com dois corações detectada");
                    return;
                }
            }
        }

        private void ClearNativeInput()
        {
            bool topHolding = dualHeartLevel ? lanes[0].Holding || sharedLane.Holding : sharedLane.Holding;
            bool bottomHolding = dualHeartLevel ? lanes[1].Holding || sharedLane.Holding : sharedLane.Holding;
            RDInput.p1Press = false;
            RDInput.p2Press = false;
            RDInput.p1IsPressed = false;
            RDInput.p2IsPressed = false;
            RDInput.p1Release = lastP1Press && !topHolding;
            RDInput.p2Release = lastP2Press && !bottomHolding;
            RDInput.anyPlayerPress = false;
            RDInput.anyPlayerIsPressed = false;
            RDInput.anyPlayerRelease = RDInput.p1Release || RDInput.p2Release;
            lastP1Press = false;
            lastP2Press = false;
        }

        private void ProcessLane(scnGame game, double now, LaneState state, int lane, bool bothOnly)
        {
            double margin = Math.Max(0.14, scnGame.GetHitMargin(lane == 1 ? RDPlayer.P2 : RDPlayer.P1));
            Beat next = state.Target;
            if (next == null || (!state.Holding && now > state.TargetTime + margin) || next.dead)
                next = FindBeat(game, now, state, lane, bothOnly);
            if (!System.Object.ReferenceEquals(next, state.Target))
            {
                if (state.Target != null && !state.Completed) FinishTarget(state, lane);
                state.Target = next;
                state.TargetTime = next == null ? 0 : next.inputTime;
                state.Holding = false;
                state.Completed = false;
                Array.Clear(state.Pressed, 0, state.Pressed.Length);
            }
            if (state.Target == null) return;

            bool top = lane == 0 || (lane == -1 && state.Target.player != RDPlayer.P2);
            bool bottom = lane == 1 || (lane == -1 && state.Target.player != RDPlayer.P1);
            if (state.Holding)
            {
                bool held = true;
                for (int i = 0; i < Players; i++)
                    if (lane == -1 || Heart(i) == lane) held &= GetHeld(i);
                if (top) RDInput.p1IsPressed |= held;
                if (bottom) RDInput.p2IsPressed |= held;
                if (!held)
                {
                    if (top) RDInput.p1Release = true;
                    if (bottom) RDInput.p2Release = true;
                    state.Holding = false;
                    state.Target = null;
                }
                return;
            }

            int groupCount = 0;
            bool all = true;
            double earliest = Double.MaxValue, latest = Double.MinValue;
            for (int i = 0; i < Players; i++)
            {
                if (lane != -1 && Heart(i) != lane) continue;
                groupCount++;
                if (GetDown(i))
                {
                    bool good = Math.Abs(now - state.TargetTime) <= margin && !state.Pressed[i];
                    feedbackGood[i] = good;
                    feedbackUntil[i] = Time.unscaledTime + 0.45f;
                    if (good)
                    {
                        state.Pressed[i] = true;
                        state.PressTimes[i] = now;
                        hits[i]++;
                    }
                }
                if (!state.Pressed[i]) all = false;
                else
                {
                    earliest = Math.Min(earliest, state.PressTimes[i]);
                    latest = Math.Max(latest, state.PressTimes[i]);
                }
            }
            bool ready = groupCount == 0 ? now >= state.TargetTime && now < state.TargetTime + 0.06 :
                all && latest - earliest <= 0.18 && Math.Abs(now - state.TargetTime) <= margin;
            if (!ready) return;
            if (game.beats != null)
                foreach (Beat beat in game.beats)
                    if (beat != null && !beat.dead && !beat.unhittable &&
                        Math.Abs(beat.inputTime - state.TargetTime) <= 0.025 &&
                        MatchLane(beat, lane, bothOnly)) state.Consumed.Add(beat);
            if (top)
            {
                RDInput.p1Press = true;
                RDInput.p1IsPressed = true;
                lastP1Press = true;
            }
            if (bottom)
            {
                RDInput.p2Press = true;
                RDInput.p2IsPressed = true;
                lastP2Press = true;
            }
            state.Completed = true;
            state.Holding = state.Target.hasHeldPulses || state.Target.isHeldClap;
            if (!state.Holding) state.Target = null;
        }

        private Beat FindBeat(scnGame game, double now, LaneState state, int lane, bool bothOnly)
        {
            Beat best = null;
            double distance = 0.38;
            if (game.beats == null) return null;
            foreach (Beat beat in game.beats)
            {
                if (beat == null || beat.dead || beat.unhittable || state.Consumed.Contains(beat) ||
                    !MatchLane(beat, lane, bothOnly)) continue;
                double delta = Math.Abs(beat.inputTime - now);
                if (delta < distance)
                {
                    distance = delta;
                    best = beat;
                }
            }
            return best;
        }

        private bool MatchLane(Beat beat, int lane, bool bothOnly)
        {
            if (beat.player == RDPlayer.CPU) return false;
            if (lane == 0) return beat.player == RDPlayer.P1;
            if (lane == 1) return beat.player == RDPlayer.P2;
            return !bothOnly || beat.player == RDPlayer.BOTH;
        }

        private void FinishTarget(LaneState state, int lane)
        {
            for (int i = 0; i < Players; i++)
                if (lane == -1 || Heart(i) == lane) misses[i]++;
        }

        private void ClearConsumed()
        {
            foreach (LaneState lane in lanes) lane.Consumed.Clear();
            sharedLane.Consumed.Clear();
        }

        private void ResetTarget(bool countMisses)
        {
            foreach (LaneState lane in lanes) ResetLane(lane, countMisses);
            ResetLane(sharedLane, countMisses);
            lastP1Press = false;
            lastP2Press = false;
        }

        private void ResetLane(LaneState state, bool countMisses)
        {
            if (countMisses && state.Target != null && !state.Completed)
                FinishTarget(state, System.Object.ReferenceEquals(state, sharedLane) ? -1 :
                    System.Object.ReferenceEquals(state, lanes[0]) ? 0 : 1);
            state.Target = null;
            state.TargetTime = 0;
            state.Holding = false;
            state.Completed = false;
            Array.Clear(state.Pressed, 0, state.Pressed.Length);
        }

        private int Heart(int slot) { return hearts[slot].Value == 1 ? 1 : 0; }

        private void SetHeart(int slot, int heart)
        {
            hearts[slot].Value = heart == 1 ? 1 : 0;
            BalanceHearts(slot);
            ResetTarget(false);
            menuMessage = "JOGADOR " + (slot + 1) + " NO CORACAO DE " + (Heart(slot) == 0 ? "CIMA" : "BAIXO");
        }

        private void BalanceHearts(int preferredSlot)
        {
            int capacity = Players == 1 ? 1 : Players == 2 ? 1 : 2;
            for (int heart = 0; heart < 2; heart++)
            {
                int count = 0;
                for (int i = 0; i < Players; i++) if (Heart(i) == heart) count++;
                while (count > capacity)
                {
                    int move = -1;
                    for (int i = Players - 1; i >= 0; i--)
                        if (i != preferredSlot && Heart(i) == heart) { move = i; break; }
                    if (move < 0) break;
                    hearts[move].Value = 1 - heart;
                    count--;
                }
            }
        }

        private KeyCode PadKey(int pad, int button)
        {
            return (KeyCode)((int)KeyCode.Joystick1Button0 + (pad - 1) * 20 + button);
        }

        private bool GetDown(int i)
        {
            int pad = pads[i].Value;
            return pad > 0 && pad <= 8
                ? Input.GetKeyDown(PadKey(pad, Math.Max(0, Math.Min(19, buttons[i].Value))))
                : Input.GetKeyDown(keys[i].Value);
        }

        private bool GetHeld(int i)
        {
            int pad = pads[i].Value;
            return pad > 0 && pad <= 8
                ? Input.GetKey(PadKey(pad, Math.Max(0, Math.Min(19, buttons[i].Value))))
                : Input.GetKey(keys[i].Value);
        }

        private void ListenForBinding()
        {
            for (int pad = 1; pad <= 8; pad++)
                for (int button = 0; button < 20; button++)
                    if (Input.GetKeyDown(PadKey(pad, button)))
                    {
                        BindPad(listening, pad, button);
                        return;
                    }
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
                if (Input.GetKeyDown(key)) { BindKey(listening, key); return; }
            for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
                if (Input.GetKeyDown(key)) { BindKey(listening, key); return; }
            KeyCode[] extra = { KeyCode.Space, KeyCode.Return, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2,
                KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7,
                KeyCode.Keypad8, KeyCode.Keypad9 };
            foreach (KeyCode key in extra)
                if (Input.GetKeyDown(key)) { BindKey(listening, key); return; }
        }

        private void BindPad(int slot, int pad, int button)
        {
            for (int i = 0; i < 4; i++)
                if (i != slot && pads[i].Value == pad && buttons[i].Value == button)
                    pads[i].Value = 0;
            pads[slot].Value = pad;
            buttons[slot].Value = button;
            menuMessage = "JOGADOR " + (slot + 1) + " CONECTADO AO CONTROLE " + pad;
            inputFlashUntil[slot] = Time.unscaledTime + 0.8f;
            listening = -1;
            ResetTarget(false);
        }

        private void BindKey(int slot, KeyCode key)
        {
            KeyCode replacement = pads[slot].Value == 0 ? keys[slot].Value : KeyCode.None;
            for (int i = 0; i < 4; i++)
                if (i != slot && pads[i].Value == 0 && keys[i].Value == key)
                    keys[i].Value = replacement == key ? KeyCode.None : replacement;
            keys[slot].Value = key;
            pads[slot].Value = 0;
            menuMessage = "JOGADOR " + (slot + 1) + " CONECTADO A TECLA " + key;
            inputFlashUntil[slot] = Time.unscaledTime + 0.8f;
            listening = -1;
            ResetTarget(false);
        }

        private void AutoBindPads()
        {
            string[] names = Input.GetJoystickNames();
            int found = 0;
            for (int pad = 1; pad <= Math.Min(8, names.Length) && found < Players; pad++)
            {
                if (String.IsNullOrWhiteSpace(names[pad - 1])) continue;
                pads[found].Value = pad;
                buttons[found].Value = 0;
                found++;
            }
            for (int i = found; i < Players; i++)
            {
                pads[i].Value = 0;
                keys[i].Value = defaultKeys[i];
            }
            menuMessage = found == 0 ? "NENHUM CONTROLE DETECTADO. USE CONECTAR." :
                found + " CONTROLE(S) CONECTADO(S). TESTE CADA ENTRADA.";
            listening = -1;
            ResetTarget(false);
        }

        private void OnGUI()
        {
            if (!loggedGui) { Logger.LogInfo("OnGUI ativo"); loggedGui = true; }
            EnsureStyles();
            DrawHud();
            if (menuOpen) DrawMenu();
        }

        private void EnsureStyles()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = Color.white;
            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft };
            rowStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft };
            smallStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = null;
            buttonStyle.hover.background = null;
            buttonStyle.active.background = null;
            buttonStyle.focused.background = null;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
        }

        private int CharacterIndex(int slot)
        {
            int value = characters[slot].Value % characterNames.Length;
            return value < 0 ? value + characterNames.Length : value;
        }

        private void EnsurePortraits()
        {
            if (portraitsLoaded) return;
            portraitsLoaded = true;
            portraits = new Texture2D[characterNames.Length];
            for (int i = 0; i < portraits.Length; i++)
                portraits[i] = Resources.Load<Texture2D>(characterTextures[i]);
            Texture2D[] loaded = Resources.FindObjectsOfTypeAll<Texture2D>();
            for (int i = 0; i < portraits.Length; i++)
                if (portraits[i] == null)
                    foreach (Texture2D texture in loaded)
                        if (texture != null && texture.name == characterTextures[i])
                        {
                            portraits[i] = texture;
                            break;
                        }
            int found = 0;
            foreach (Texture2D portrait in portraits) if (portrait != null) found++;
            Logger.LogInfo("Retratos do jogo disponíveis: " + found + "/" + portraits.Length);
        }

        private void DrawPortrait(Rect rect, int slot)
        {
            EnsurePortraits();
            Texture2D portrait = portraits[CharacterIndex(slot)];
            Fill(rect, new Color32(23, 42, 68, 255));
            if (portrait != null)
                GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit, true);
            else
                Text(rect, characterNames[CharacterIndex(slot)].Substring(0, 1), rowStyle, colors[slot]);
        }

        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }

        private void Text(Rect rect, string value, GUIStyle style, Color color)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(rect, value, style);
            GUI.contentColor = old;
        }

        private bool Button(Rect rect, string value, Color fill)
        {
            Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color32(114, 166, 185, 255));
            Fill(rect, fill);
            return GUI.Button(rect, value, buttonStyle);
        }

        private void DrawHud()
        {
            bool playing = scnGame.instance != null && scnGame.instance.started;
            float left = Screen.width - 335f;
            float height = playing ? 88f + Players * 30f : 112f;
            Fill(new Rect(left - 3f, 15f, 320f, height + 6f), new Color32(81, 193, 208, 255));
            Fill(new Rect(left, 18f, 314f, height), new Color32(15, 24, 43, 248));
            Fill(new Rect(left, 18f, 314f, 7f), new Color32(242, 95, 139, 255));
            Text(new Rect(left + 15f, 29f, 283f, 30f), "PULSO EM EQUIPE", titleStyle, new Color32(244, 235, 255, 255));
            Text(new Rect(left + 16f, 59f, 285f, 22f),
                Players + " JOGADOR" + (Players == 1 ? "" : "ES") + (coopEnabled.Value ? "  ·  LIGADO" : "  ·  DESLIGADO"),
                smallStyle, new Color32(94, 232, 233, 255));
            if (playing)
                for (int i = 0; i < Players; i++)
                {
                    float y = 87f + i * 30f;
                    Color color = feedbackUntil[i] > Time.unscaledTime ?
                        (feedbackGood[i] ? new Color32(117, 241, 183, 255) : new Color32(255, 113, 143, 255)) : colors[i];
                    Fill(new Rect(left + 13f, y + 7f, 7f, 15f), color);
                    DrawPortrait(new Rect(left + 27f, y + 2f, 34f, 25f), i);
                    Text(new Rect(left + 69f, y, 110f, 27f), "J" + (i + 1) + " " + characterNames[CharacterIndex(i)], smallStyle, color);
                    Text(new Rect(left + 176f, y, 25f, 27f), Heart(i) == 0 ? "▲" : "▼", rowStyle,
                        Heart(i) == 0 ? new Color32(255, 113, 143, 255) : new Color32(87, 222, 236, 255));
                    Text(new Rect(left + 204f, y + 1f, 99f, 25f), hits[i] + " AC · " + misses[i] + " ER", smallStyle,
                        new Color32(198, 204, 229, 255));
                }
            float buttonY = playing ? 87f + Players * 30f : 84f;
            if (Button(new Rect(left + 13f, buttonY, 286f, 23f), "F6  CONFIGURAR JOGADORES", new Color32(50, 62, 93, 255)))
            {
                menuOpen = !menuOpen;
                listening = -1;
            }
        }

        private string ShortBinding(int i)
        {
            return pads[i].Value > 0 ? "PAD " + pads[i].Value + " / B" + buttons[i].Value :
                keys[i].Value == KeyCode.None ? "SEM TECLA" : keys[i].Value.ToString().ToUpperInvariant();
        }

        private string PadName(int pad)
        {
            string[] names = Input.GetJoystickNames();
            if (pad < 1 || pad > names.Length || String.IsNullOrWhiteSpace(names[pad - 1]))
                return "CONTROLE " + pad + " DESCONECTADO";
            string name = names[pad - 1].Trim().ToUpperInvariant();
            if (name.Length > 22) name = name.Substring(0, 22) + "...";
            return "CONTROLE " + pad + " · " + name;
        }

        private void DrawMenu()
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.04f, 0.12f, 0.88f));
            float scale = Mathf.Min(1.18f, Mathf.Min(Screen.width / 950f, Screen.height / 720f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 900f * scale) / 2f,
                (Screen.height - 665f * scale) / 2f, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            Color navy = new Color32(18, 23, 48, 255);
            Color cyan = new Color32(78, 224, 232, 255);
            Color pink = new Color32(252, 100, 149, 255);
            Color muted = new Color32(175, 186, 216, 255);
            Fill(new Rect(-6, -6, 912, 677), cyan);
            Fill(new Rect(0, 0, 900, 665), navy);
            Fill(new Rect(0, 0, 900, 13), pink);
            Fill(new Rect(28, 39, 7, 63), cyan);
            Text(new Rect(52, 31, 680, 43), "PULSO EM EQUIPE", titleStyle, Color.white);
            Text(new Rect(54, 72, 400, 26), "JOGADORES, CORACOES E CONTROLES", smallStyle, muted);
            if (Button(new Rect(425, 77, 130, 32), "CONTROLES", menuTab == 0 ? pink : new Color32(43, 54, 86, 255)))
            {
                menuTab = 0;
                menuMessage = "APERTE CONECTAR E USE A TECLA OU CONTROLE DE CADA JOGADOR";
            }
            if (Button(new Rect(565, 77, 110, 32), "CORAÇÕES", menuTab == 1 ? pink : new Color32(43, 54, 86, 255)))
            {
                menuTab = 1;
                menuMessage = "ESCOLHA QUEM JOGA NO CORACAO DE CIMA OU DE BAIXO";
            }
            if (Button(new Rect(685, 77, 150, 32), "PERSONAGENS", menuTab == 2 ? pink : new Color32(43, 54, 86, 255)))
            {
                menuTab = 2;
                menuMessage = "ESCOLHA UM PERSONAGEM PARA CADA VAGA";
            }
            if (Button(new Rect(777, 37, 91, 46), "FECHAR", new Color32(79, 49, 86, 255)))
            {
                menuOpen = false;
                listening = -1;
            }
            Fill(new Rect(28, 119, 844, 3), new Color32(67, 84, 119, 255));
            Text(new Rect(31, 135, 125, 25), "JOGADORES", smallStyle, cyan);
            for (int n = 1; n <= 4; n++)
            {
                if (Button(new Rect(165 + (n - 1) * 66, 132, 54, 35), n.ToString(),
                    n == Players ? pink : new Color32(43, 54, 86, 255)))
                {
                    players.Value = n;
                    BalanceHearts(-1);
                    ResetTarget(false);
                    menuMessage = n + " JOGADOR" + (n == 1 ? " ATIVO" : "ES ATIVOS");
                }
            }
            if (menuTab == 2)
            {
                Fill(new Rect(572, 132, 294, 35), new Color32(33, 64, 83, 255));
                Text(new Rect(591, 135, 275, 29), "8 PERSONAGENS DO JOGO", smallStyle, cyan);
            }
            else if (menuTab == 1)
            {
                int topCount = 0;
                for (int i = 0; i < Players; i++) if (Heart(i) == 0) topCount++;
                Fill(new Rect(572, 132, 294, 35), new Color32(33, 64, 83, 255));
                Text(new Rect(590, 135, 276, 29), topCount + " CIMA  ·  " + (Players - topCount) + " BAIXO", smallStyle, cyan);
            }
            else if (Button(new Rect(572, 132, 294, 35), "CONECTAR AUTOMATICAMENTE", new Color32(42, 105, 125, 255)))
                AutoBindPads();
            for (int i = 0; i < 4; i++)
            {
                float y = 190 + i * 88;
                Color tone = i < Players ? colors[i] : new Color32(95, 103, 129, 255);
                Fill(new Rect(28, y, 844, 78), new Color32(30, 38, 71, 255));
                Fill(new Rect(28, y, 7, 78), tone);
                Text(new Rect(48, y + 9, 135, 28), "JOGADOR " + (i + 1), rowStyle, tone);
                Text(new Rect(49, y + 38, 135, 22), i < Players ? "VAGA ATIVA" : "VAGA RESERVA", smallStyle, muted);
                if (menuTab == 2)
                {
                    DrawPortrait(new Rect(192, y + 7, 75, 64), i);
                    Text(new Rect(280, y + 9, 325, 29), characterNames[CharacterIndex(i)], rowStyle, Color.white);
                    Text(new Rect(281, y + 38, 325, 22), "PERSONAGEM DO RHYTHM DOCTOR", smallStyle, muted);
                    if (Button(new Rect(650, y + 17, 85, 43), "<", new Color32(60, 76, 112, 255)))
                    {
                        characters[i].Value = (CharacterIndex(i) + characterNames.Length - 1) % characterNames.Length;
                        menuMessage = "JOGADOR " + (i + 1) + " ESCOLHEU " + characterNames[CharacterIndex(i)];
                    }
                    if (Button(new Rect(749, y + 17, 85, 43), ">", new Color32(60, 76, 112, 255)))
                    {
                        characters[i].Value = (CharacterIndex(i) + 1) % characterNames.Length;
                        menuMessage = "JOGADOR " + (i + 1) + " ESCOLHEU " + characterNames[CharacterIndex(i)];
                    }
                }
                else if (menuTab == 1)
                {
                    DrawPortrait(new Rect(192, y + 7, 75, 64), i);
                    Text(new Rect(281, y + 7, 327, 30), Heart(i) == 0 ? "CORACAO DE CIMA" : "CORACAO DE BAIXO",
                        rowStyle, Heart(i) == 0 ? pink : cyan);
                    Text(new Rect(283, y + 39, 320, 22), characterNames[CharacterIndex(i)] + " · " + ShortBinding(i),
                        smallStyle, muted);
                    if (Button(new Rect(625, y + 17, 107, 43), "▲ CIMA",
                        Heart(i) == 0 ? new Color32(172, 55, 100, 255) : new Color32(60, 76, 112, 255)))
                        SetHeart(i, 0);
                    if (Button(new Rect(745, y + 17, 107, 43), "▼ BAIXO",
                        Heart(i) == 1 ? new Color32(41, 127, 145, 255) : new Color32(60, 76, 112, 255)))
                        SetHeart(i, 1);
                }
                else
                {
                    Text(new Rect(193, y + 6, 420, 27), pads[i].Value > 0 ? PadName(pads[i].Value) :
                        "TECLADO · " + ShortBinding(i), rowStyle, Color.white);
                    Text(new Rect(195, y + 38, 405, 22),
                        pads[i].Value > 0 ? "BOTAO " + buttons[i].Value + " · APERTE PARA TESTAR" : "APERTE A TECLA PARA TESTAR",
                        smallStyle, muted);
                    bool lit = Time.unscaledTime < inputFlashUntil[i] || GetHeld(i);
                    Fill(new Rect(607, y + 20, 17, 17), lit ? new Color32(111, 241, 178, 255) : new Color32(67, 79, 108, 255));
                    Text(new Rect(633, y + 12, 72, 29), lit ? "OK" : "TESTE", smallStyle, lit ? cyan : muted);
                    if (Button(new Rect(714, y + 18, 141, 40),
                        listening == i ? "AGUARDANDO..." : "CONECTAR", new Color32(60, 76, 112, 255)))
                    {
                        listening = i;
                        menuMessage = "JOGADOR " + (i + 1) + ": APERTE UMA TECLA OU BOTAO DO CONTROLE";
                    }
                }
            }
            Fill(new Rect(28, 554, 844, 3), new Color32(67, 84, 119, 255));
            Text(new Rect(34, 566, 836, 25), menuMessage, smallStyle, cyan);
            if (Button(new Rect(30, 602, 190, 39), coopEnabled.Value ? "MOD LIGADO · F7" : "MOD DESLIGADO · F7",
                coopEnabled.Value ? new Color32(35, 109, 102, 255) : new Color32(105, 58, 73, 255)))
            {
                coopEnabled.Value = !coopEnabled.Value;
                ResetTarget(false);
            }
            Text(new Rect(239, 603, 630, 35), "F6 FECHA  ·  F8 MUDA VAGAS  ·  CONFIGURE ANTES DA MUSICA", smallStyle, muted);
            GUI.matrix = previous;
        }
    }
}
