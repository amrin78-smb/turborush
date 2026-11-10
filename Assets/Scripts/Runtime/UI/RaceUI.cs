using System.Text;
using TMPro;
using TurboRush.Core;
using TurboRush.Race;
using TurboRush.Save;
using UnityEngine;
using UnityEngine.UI;

namespace TurboRush.UI
{
    /// <summary>
    /// Race HUD (speed, position, lap, progress bar with every racer, pause button), countdown/lap/finish pop-ups,
    /// first-race steering hint, pause menu and results screen. HUD numbers update only when they change and use
    /// TMP SetText with numeric arguments (no per-frame string allocation).
    /// </summary>
    public sealed class RaceUI : MonoBehaviour
    {
        private RaceManager m_Race;
        private UISkin m_Skin;
        private Services m_Services;
        private RectTransform m_Safe;
        private GameObject m_Hud;

        private TextMeshProUGUI m_Speed, m_Unit, m_Pos, m_PosOf, m_Lap, m_TrackLabel;
        private RectTransform m_BarFill;
        private RectTransform[] m_Markers;
        private TextMeshProUGUI m_Center;
        private Pop m_CenterPop;
        private TextMeshProUGUI m_Banner;
        private Pop m_BannerPop;
        private TextMeshProUGUI m_Overtake;
        private Pop m_OvertakePop;
        private GameObject m_Hint;
        private GameObject m_PauseOverlay;
        private GameObject m_Results;
        private Button m_PauseButton;

        private int m_ShownSpeed = -1, m_ShownPos = -1, m_ShownLap = -1;
        private bool m_Mph;

        public RectTransform PauseButtonRect { get; private set; }
        public GameObject ResultsRoot => m_Results;
        public GameObject PauseRoot => m_PauseOverlay;
        public int ShownSpeed => m_ShownSpeed;
        public int ShownPosition => m_ShownPos;
        public float ShownProgress { get; private set; }

        public void Init(RaceManager race, UISkin skin)
        {
            m_Race = race;
            m_Skin = skin;
            m_Services = Services.Instance;
            UIKit.Skin = skin;
            m_Safe = UIKit.Canvas("RaceCanvas", 10, out _);
            BuildHud();
            BuildPause();
            m_Services.States.Changed += OnState;
            race.RaceReset += OnReset;
        }

        private void OnDestroy()
        {
            if (m_Services != null) m_Services.States.Changed -= OnState;
            if (m_Race != null) m_Race.RaceReset -= OnReset;
        }

        private void BuildHud()
        {
            m_Hud = UIKit.Rect(m_Safe, "HUD").gameObject;
            UIKit.Stretch((RectTransform)m_Hud.transform);
            Transform h = m_Hud.transform;
            int n = m_Race.Sim.Racers.Length;

            // Position (top-left)
            Image posBg = UIKit.Panel(h, "PositionPanel", m_Skin.panel);
            posBg.raycastTarget = false;
            UIKit.Place(posBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(300, 170));
            m_Pos = UIKit.Label(posBg.transform, "Pos", "6", 128, Color.white, TextAlignmentOptions.Right, true);
            UIKit.Place(m_Pos.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(165, 6), new Vector2(150, 160));
            m_PosOf = UIKit.Label(posBg.transform, "Of", "/" + n, 64, m_Skin.accent, TextAlignmentOptions.Left, true);
            UIKit.Place(m_PosOf.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170, -16), new Vector2(120, 90));
            Image flag = UIKit.Image(posBg.transform, "PosIcon", m_Skin.Get(Icon.Trophy), m_Skin.accent);
            UIKit.Place(flag.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(40, -40), new Vector2(54, 54));
            m_Overtake = UIKit.Label(h, "Overtake", "+1", 110, m_Skin.good, TextAlignmentOptions.Left, true);
            UIKit.Place(m_Overtake.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(316, -25), new Vector2(240, 130));
            m_Overtake.gameObject.AddComponent<CanvasGroup>();
            m_OvertakePop = m_Overtake.gameObject.AddComponent<Pop>();
            m_Overtake.gameObject.SetActive(false);

            // Progress (top-centre)
            RectTransform prog = UIKit.Place(UIKit.Rect(h, "Progress"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-20, -8), new Vector2(820, 150));
            m_TrackLabel = UIKit.Label(prog, "Track", m_Race.Track.displayName, 46, Color.white, TextAlignmentOptions.Left, true);
            UIKit.Place(m_TrackLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, 0), new Vector2(500, 60));
            m_Lap = UIKit.Label(prog, "Lap", "LAP 1/2", 46, m_Skin.accent, TextAlignmentOptions.Right, true);
            UIKit.Place(m_Lap.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, 0), new Vector2(300, 60));
            Image barBg = UIKit.Image(prog, "BarBg", m_Skin.rounded, new Color(0f, 0f, 0.1f, 0.55f));
            UIKit.Place(barBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(680, 34));
            Image fill = UIKit.Image(barBg.transform, "Fill", m_Skin.rounded, m_Skin.primary);
            m_BarFill = fill.rectTransform;
            m_BarFill.anchorMin = new Vector2(0, 0);
            m_BarFill.anchorMax = new Vector2(0, 1);
            m_BarFill.pivot = new Vector2(0, 0.5f);
            m_BarFill.offsetMin = new Vector2(4, 4);
            m_BarFill.offsetMax = new Vector2(4, -4);
            Image start = UIKit.Image(prog, "StartIcon", m_Skin.circle, Color.white);
            UIKit.Place(start.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-370, -87), new Vector2(34, 34));
            Image finish = UIKit.Image(prog, "FinishIcon", m_Skin.Get(Icon.Flag), Color.white);
            UIKit.Place(finish.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(375, -84), new Vector2(56, 56));
            m_Markers = new RectTransform[n];
            for (int i = n - 1; i >= 0; i--)
            {
                bool player = i == m_Race.Sim.PlayerIndex;
                Image dot = UIKit.Image(barBg.transform, "Racer" + i, m_Skin.circle, player ? Color.white : m_Race.RacerCars[i].paint);
                float s = player ? 46 : 26;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0, 0.5f);
                dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                dot.rectTransform.sizeDelta = new Vector2(s, s);
                if (player)
                {
                    Image inner = UIKit.Image(dot.transform, "Inner", m_Skin.circle, m_Race.PlayerCar.paint);
                    UIKit.Stretch(inner.rectTransform, 7);
                }
                m_Markers[i] = dot.rectTransform;
            }
            m_Markers[m_Race.Sim.PlayerIndex].SetAsLastSibling();

            // Pause (top-right)
            m_PauseButton = UIKit.Button(h, "PauseButton", null, Icon.Pause, m_Skin.secondary, new Vector2(150, 150), () => m_Race.Pause());
            PauseButtonRect = (RectTransform)m_PauseButton.transform;
            UIKit.Place(PauseButtonRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(150, 150));

            // Speed (bottom-right)
            Image spBg = UIKit.Panel(h, "SpeedPanel", m_Skin.panel);
            spBg.raycastTarget = false;
            UIKit.Place(spBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(340, 170));
            m_Speed = UIKit.Label(spBg.transform, "Speed", "0", 120, Color.white, TextAlignmentOptions.Right, true);
            UIKit.Place(m_Speed.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(225, 8), new Vector2(220, 150));
            m_Unit = UIKit.Label(spBg.transform, "Unit", "km/h", 40, m_Skin.accent, TextAlignmentOptions.Left, true);
            UIKit.Place(m_Unit.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(232, -30), new Vector2(110, 60));

            // Centre pop-ups
            m_Center = UIKit.Label(h, "Center", "3", 300, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(m_Center.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1200, 400));
            m_Center.gameObject.AddComponent<CanvasGroup>();
            m_CenterPop = m_Center.gameObject.AddComponent<Pop>();
            m_Center.gameObject.SetActive(false);
            m_Banner = UIKit.Label(h, "Banner", "", 110, m_Skin.accent, TextAlignmentOptions.Center, true);
            UIKit.Place(m_Banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1400, 180));
            m_Banner.gameObject.AddComponent<CanvasGroup>();
            m_BannerPop = m_Banner.gameObject.AddComponent<Pop>();
            m_Banner.gameObject.SetActive(false);

            BuildHint(h);
        }

        private void BuildHint(Transform h)
        {
            m_Hint = UIKit.Rect(h, "SteerHint").gameObject;
            var hr = (RectTransform)m_Hint.transform;
            UIKit.Stretch(hr);
            for (int side = 0; side < 2; side++)
            {
                Image zone = UIKit.Image(hr, side == 0 ? "LeftZone" : "RightZone", m_Skin.rounded, new Color(1f, 1f, 1f, 0.12f));
                zone.rectTransform.anchorMin = new Vector2(side == 0 ? 0f : 0.5f, 0f);
                zone.rectTransform.anchorMax = new Vector2(side == 0 ? 0.5f : 1f, 0.55f);
                zone.rectTransform.offsetMin = new Vector2(side == 0 ? 0 : 20, 190);
                zone.rectTransform.offsetMax = new Vector2(side == 0 ? -20 : 0, 0);
                Image arrow = UIKit.Image(zone.transform, "Arrow", m_Skin.Get(side == 0 ? Icon.ArrowLeft : Icon.ArrowRight), new Color(1f, 1f, 1f, 0.9f));
                UIKit.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
            }
            TextMeshProUGUI t = UIKit.Label(hr, "Text", "Touch left or right to steer", 60, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1400, 100));
            m_Hint.SetActive(false);
        }

        private void BuildPause()
        {
            m_PauseOverlay = UIKit.Overlay(m_Safe, "PauseOverlay", 0.6f);
            Transform root = m_PauseOverlay.transform;
            RectTransform safe = UIKit.Rect(root, "Safe");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Image panel = UIKit.Panel(safe, "Panel", m_Skin.panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 820));
            TextMeshProUGUI title = UIKit.Label(panel.transform, "Title", "PAUSED", 100, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(800, 130));
            Button resume = UIKit.Button(panel.transform, "Resume", "Resume", Icon.Play, m_Skin.good, new Vector2(640, 150), () => m_Race.Resume(), 64);
            UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(640, 150));
            Button restart = UIKit.Button(panel.transform, "Restart", "Restart", Icon.Restart, m_Skin.secondary, new Vector2(640, 130), () => m_Race.Restart());
            UIKit.Place((RectTransform)restart.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -370), new Vector2(640, 130));
            Button menu = UIKit.Button(panel.transform, "Menu", "Menu", Icon.Home, m_Skin.primary, new Vector2(640, 130), () => m_Race.QuitToMenu());
            UIKit.Place((RectTransform)menu.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -530), new Vector2(640, 130));
            Button music = null, sound = null;
            music = UIKit.Button(panel.transform, "Music", null, Icon.Music, ToggleColor(m_Services.Settings.music), new Vector2(120, 120), () =>
            {
                m_Services.Settings.music = !m_Services.Settings.music;
                m_Services.ApplySettings();
                m_Services.Persist();
                music.GetComponent<Image>().color = ToggleColor(m_Services.Settings.music);
            });
            UIKit.Place((RectTransform)music.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-80, 30), new Vector2(120, 120));
            sound = UIKit.Button(panel.transform, "Sound", null, Icon.Sound, ToggleColor(m_Services.Settings.sfx), new Vector2(120, 120), () =>
            {
                m_Services.Settings.sfx = !m_Services.Settings.sfx;
                m_Services.ApplySettings();
                m_Services.Persist();
                sound.GetComponent<Image>().color = ToggleColor(m_Services.Settings.sfx);
            });
            UIKit.Place((RectTransform)sound.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(80, 30), new Vector2(120, 120));
            m_PauseOverlay.SetActive(false);
        }

        private Color ToggleColor(bool on) => on ? m_Skin.good : m_Skin.locked;

        private void OnReset()
        {
            if (m_Results != null) { Destroy(m_Results); m_Results = null; }
            m_ShownSpeed = m_ShownPos = m_ShownLap = -1;
            m_Center.gameObject.SetActive(false);
            m_Banner.gameObject.SetActive(false);
            m_Overtake.gameObject.SetActive(false);
            m_Hud.SetActive(true);
            m_PauseButton.gameObject.SetActive(true);
            m_PauseOverlay.SetActive(false);
            m_Hint.SetActive(false);
            Refresh();
        }

        private void OnState(GameState from, GameState to)
        {
            m_PauseOverlay.SetActive(to == GameState.Paused);
            m_PauseButton.gameObject.SetActive(to == GameState.Countdown || to == GameState.Racing);
            if (to == GameState.Results) m_Hud.SetActive(false);
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (m_Race == null || m_Race.Sim == null) return;
            RaceSimulation sim = m_Race.Sim;
            RacerState p = sim.Player;
            bool mph = m_Services.Settings.units == SpeedUnit.Mph;
            if (mph != m_Mph || m_ShownSpeed < 0)
            {
                m_Mph = mph;
                m_Unit.SetText(mph ? "mph" : "km/h");
                m_ShownSpeed = -2;
            }
            int speed = Mathf.RoundToInt(mph ? p.Speed * 2.23694f : p.SpeedKmh);
            if (speed != m_ShownSpeed) { m_ShownSpeed = speed; m_Speed.SetText("{0}", speed); }
            if (p.Position != m_ShownPos) { m_ShownPos = p.Position; m_Pos.SetText("{0}", p.Position); }
            if (p.Lap != m_ShownLap) { m_ShownLap = p.Lap; m_Lap.SetText("LAP {0}/{1}", p.Lap, sim.Track.Laps); }

            float width = ((RectTransform)m_BarFill.parent).rect.width - 8f;
            float prog = sim.Progress(sim.PlayerIndex);
            ShownProgress = prog;
            m_BarFill.sizeDelta = new Vector2(Mathf.Max(0f, width * prog), m_BarFill.sizeDelta.y);
            for (int i = 0; i < m_Markers.Length; i++)
                m_Markers[i].anchoredPosition = new Vector2(4f + width * sim.Progress(i), 0f);
        }

        public void ShowCountdown(int n)
        {
            m_Center.color = n == 3 ? m_Skin.primary : n == 2 ? m_Skin.accent : m_Skin.good;
            m_Center.SetText("{0}", n);
            m_CenterPop.Play(0.95f);
        }

        public void ShowGo()
        {
            m_Center.color = m_Skin.good;
            m_Center.SetText("GO!");
            m_CenterPop.Play(1.1f);
        }

        public void ShowLap(int lap, int laps)
        {
            m_Banner.SetText(lap == laps ? "FINAL LAP!" : "LAP {0}/{1}", lap, laps);
            m_BannerPop.Play(1.8f);
        }

        public void ShowFinish(int position)
        {
            m_Center.color = m_Skin.accent;
            m_Center.fontSize = 220;
            m_Center.SetText("FINISH!");
            m_CenterPop.Play(2.6f);
            m_Center.fontSize = 220;
        }

        public void OnPositionChanged(int oldPos, int newPos)
        {
            if (newPos >= oldPos) return;
            m_Overtake.SetText("+{0}", oldPos - newPos);
            m_OvertakePop.Play(1.1f);
        }

        public void ShowJump()
        {
            m_Banner.SetText("BIG JUMP!");
            m_BannerPop.Play(1.0f);
        }

                public void SetSteerHint(bool on) => m_Hint.SetActive(on);

        public void ShowResults(RaceReward reward)
        {
            m_Center.fontSize = 300;
            m_Center.gameObject.SetActive(false);
            if (m_Results != null) Destroy(m_Results);
            RaceSimulation sim = m_Race.Sim;
            RacerState p = sim.Player;
            int pos = p.FinishOrder > 0 ? p.FinishOrder : p.Position;

            m_Results = UIKit.Overlay(m_Safe, "Results", 0.55f);
            RectTransform safe = UIKit.Rect(m_Results.transform, "Safe");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Image panel = UIKit.Panel(safe, "Panel", m_Skin.panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 900));
            Transform pt = panel.transform;

            // Left column: placement, time, stars, unlocks.
            Color gold = pos == 1 ? m_Skin.accent : pos <= 3 ? new Color(0.85f, 0.9f, 1f) : Color.white;
            Image trophy = UIKit.Image(pt, "Trophy", m_Skin.Get(Icon.Trophy), gold);
            UIKit.Place(trophy.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(170, -150), new Vector2(170, 170));
            TextMeshProUGUI place = UIKit.Label(pt, "Place", UIKit.Ordinal(pos) + "!", 170, gold, TextAlignmentOptions.Left, true);
            UIKit.Place(place.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(280, -150), new Vector2(560, 220));
            TextMeshProUGUI track = UIKit.Label(pt, "Track", m_Race.Track.displayName, 50, Color.white, TextAlignmentOptions.Left, true);
            UIKit.Place(track.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(90, -290), new Vector2(700, 70));
            TextMeshProUGUI time = UIKit.Label(pt, "Time", "Time  " + UIKit.FormatTime(p.FinishTime), 64, Color.white, TextAlignmentOptions.Left, true);
            UIKit.Place(time.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(90, -370), new Vector2(700, 80));
            if (reward != null && reward.newBestTime)
            {
                Image badge = UIKit.Panel(pt, "Best", m_Skin.good);
                UIKit.Place(badge.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(560, -370), new Vector2(240, 70));
                TextMeshProUGUI bt = UIKit.Label(badge.transform, "T", "NEW BEST!", 38, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Stretch(bt.rectTransform);
            }
            int stars = reward != null ? reward.stars : Progression.StarsFor(pos);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < stars;
                Image st = UIKit.Image(pt, "Star" + i, m_Skin.Get(on ? Icon.Star : Icon.StarEmpty), on ? m_Skin.accent : new Color(1, 1, 1, 0.35f));
                UIKit.Place(st.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(180 + i * 160, -500), new Vector2(140, 140));
                if (on) st.gameObject.AddComponent<Pop>().Play();
            }
            if (reward != null && (reward.unlockedCars.Count > 0 || reward.unlockedTracks.Count > 0))
            {
                var sb = new StringBuilder("Unlocked: ");
                for (int i = 0; i < reward.unlockedCars.Count; i++) sb.Append(i > 0 ? ", " : "").Append(reward.unlockedCars[i]);
                for (int i = 0; i < reward.unlockedTracks.Count; i++) sb.Append(reward.unlockedCars.Count + i > 0 ? ", " : "").Append(reward.unlockedTracks[i]);
                TextMeshProUGUI un = UIKit.Label(pt, "Unlocked", sb.ToString(), 46, m_Skin.accent, TextAlignmentOptions.Left, true);
                UIKit.Place(un.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(90, -620), new Vector2(760, 70));
                un.textWrappingMode = TextWrappingModes.Normal;
                un.enableAutoSizing = true;
                un.fontSizeMin = 28;
                un.fontSizeMax = 46;
            }

            // Right column: standings.
            TextMeshProUGUI st2 = UIKit.Label(pt, "StandingsTitle", "Results", 52, m_Skin.accent, TextAlignmentOptions.Left, true);
            UIKit.Place(st2.rectTransform, new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(-760, -60), new Vector2(600, 70));
            for (int row = 0; row < sim.Order.Length; row++)
            {
                RacerState r = sim.Racers[sim.Order[row]];
                bool you = r.IsPlayer;
                Image rowBg = UIKit.Image(pt, "Row" + row, m_Skin.rounded, you ? new Color(1f, 1f, 1f, 0.22f) : new Color(1f, 1f, 1f, 0.06f));
                UIKit.Place(rowBg.rectTransform, new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(-770, -135 - row * 76), new Vector2(720, 66));
                TextMeshProUGUI num = UIKit.Label(rowBg.transform, "N", (row + 1).ToString(), 44, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Place(num.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(60, 60));
                Image chip = UIKit.Image(rowBg.transform, "Chip", m_Skin.circle, m_Race.RacerCars[r.Index].paint);
                UIKit.Place(chip.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(84, 0), new Vector2(44, 44));
                TextMeshProUGUI nm = UIKit.Label(rowBg.transform, "Name", you ? "YOU" : m_Race.RacerCars[r.Index].displayName, 42, you ? m_Skin.accent : Color.white, TextAlignmentOptions.Left, true);
                UIKit.Place(nm.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(300, 60));
                TextMeshProUGUI tm = UIKit.Label(rowBg.transform, "Time", r.Finished ? UIKit.FormatTime(r.FinishTime) : "racing", 40, Color.white, TextAlignmentOptions.Right, true);
                UIKit.Place(tm.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(260, 60));
            }

            Button again = UIKit.Button(pt, "RaceAgain", "Race Again", Icon.Restart, m_Skin.secondary, new Vector2(560, 140), () => m_Race.Restart(), 56);
            UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-30, 40), new Vector2(560, 140));
            Button cont = UIKit.Button(pt, "Continue", "Continue", Icon.Play, m_Skin.good, new Vector2(560, 140), () => m_Race.QuitToMenu(GameState.TrackSelect), 56);
            UIKit.Place((RectTransform)cont.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(30, 40), new Vector2(560, 140));
        }
    }
}
