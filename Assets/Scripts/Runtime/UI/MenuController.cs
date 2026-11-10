using System.Collections.Generic;
using TMPro;
using TurboRush.Core;
using TurboRush.Save;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace TurboRush.UI
{
    /// <summary>
    /// Main menu (Play, Garage, Tracks, Settings), vehicle select, track select and settings. A slowly turning
    /// showroom car sits in the 3D background. States: MainMenu / VehicleSelect / TrackSelect.
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        [SerializeField] private Transform m_Turntable;
        [SerializeField] private Light m_Sun;
        [SerializeField] private Camera m_Camera;

        private Services m_S;
        private GameConfig m_Cfg;
        private UISkin m_Skin;
        private RectTransform m_Safe;
        private GameObject m_Main, m_Garage, m_Tracks, m_Settings;
        private TextMeshProUGUI m_StarCount;
        private GameObject m_ShowCar;
        private int m_PreviewCar;
        private TextMeshProUGUI m_CarName, m_CarLock;
        private Button m_SelectButton;
        private TextMeshProUGUI m_SelectLabel;
        private readonly List<Image> m_CarChips = new List<Image>();
        private readonly List<GameObject> m_TrackCards = new List<GameObject>();
        private readonly Image[] m_DiffButtons = new Image[3];
        private static readonly Color[] DiffColors = { new Color(0.25f, 0.75f, 0.35f), new Color(1f, 0.62f, 0.1f), new Color(0.9f, 0.27f, 0.25f) };
        private readonly Dictionary<string, TextMeshProUGUI> m_SettingValues = new Dictionary<string, TextMeshProUGUI>();

        public GameObject MainRoot => m_Main;
        public GameObject GarageRoot => m_Garage;
        public GameObject TracksRoot => m_Tracks;
        public GameObject SettingsRoot => m_Settings;

        private void Awake()
        {
            m_S = Services.Ensure();
            m_Cfg = m_S.Config;
            m_Skin = m_Cfg.ui;
            UIKit.Skin = m_Skin;
            Progression.EnsureDefaults(m_S.Save, m_Cfg);
            TrackDefinition skin = m_Cfg.tracks.Length > 0 ? m_Cfg.tracks[0] : null;
            if (skin != null) RaceEnvironment.Apply(skin, m_Cfg.materials, m_Sun);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 400f;
            BuildBackdrop();

            m_Safe = UIKit.Canvas("MenuCanvas", 10, out _);
            BuildMain();
            BuildGarage();
            BuildTracks();
            BuildSettings();
            m_PreviewCar = Mathf.Max(0, System.Array.IndexOf(m_Cfg.cars, m_S.SelectedCar));
            ShowCar(m_PreviewCar);
        }

        private void Start()
        {
            m_S.Audio.PlayMusic(false);
            GameState entry = m_S.MenuEntryState;
            m_S.MenuEntryState = GameState.MainMenu;
            if (m_S.States.State != GameState.MainMenu) m_S.States.Force(GameState.MainMenu);
            Show(entry);
        }

        private void Update()
        {
            if (m_Turntable != null) m_Turntable.Rotate(0f, 22f * Time.deltaTime, 0f, Space.World);
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && m_S.States.State != GameState.MainMenu) Show(GameState.MainMenu);
        }

        public void Show(GameState screen)
        {
            if (screen != m_S.States.State && !m_S.States.TryTransition(screen)) m_S.States.Force(screen);
            m_Main.SetActive(screen == GameState.MainMenu);
            m_Garage.SetActive(screen == GameState.VehicleSelect);
            m_Tracks.SetActive(screen == GameState.TrackSelect);
            m_Settings.SetActive(false);
            if (screen == GameState.VehicleSelect) { m_PreviewCar = Mathf.Max(0, System.Array.IndexOf(m_Cfg.cars, m_S.SelectedCar)); RefreshGarage(); }
            if (screen == GameState.TrackSelect) RefreshTracks();
            if (screen != GameState.VehicleSelect) ShowCar(System.Array.IndexOf(m_Cfg.cars, m_S.SelectedCar));
            m_StarCount.SetText("{0}", m_S.Save.TotalStars());
            if (m_Camera != null)
            {
                bool garage = screen == GameState.VehicleSelect;
                m_Camera.transform.position = garage ? new Vector3(2.4f, 2.2f, -7.4f) : new Vector3(-1f, 2.2f, -8.6f);
                m_Camera.transform.LookAt(garage ? new Vector3(0f, 0.2f, 0f) : new Vector3(-2.6f, 1.2f, 0f));
            }
        }

        // ---------- Backdrop ----------
        private void BuildBackdrop()
        {
            var k = new MeshKit();
            TrackPalette p = m_Cfg.tracks.Length > 0 ? m_Cfg.tracks[0].palette : new TrackPalette();
            k.Prism(new Vector3(0, -0.6f, 0), Vector3.up, 4.2f, 4.2f, 0.6f, 24, new Color(0.95f, 0.95f, 1f), 0f, new Color(0.85f, 0.88f, 0.95f));
            k.Prism(new Vector3(0, -1.2f, 0), Vector3.up, 4.8f, 4.6f, 0.6f, 24, p.curbA, 0f, p.curbA);
            k.Quad(new Vector3(-400, -1.2f, -400), new Vector3(-400, -1.2f, 400), new Vector3(400, -1.2f, 400), new Vector3(400, -1.2f, -400), p.ground);
            var rng = new System.Random(7);
            for (int i = 0; i < 30; i++)
            {
                float a = i / 30f * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a) * 380f, -1.2f, Mathf.Sin(a) * 380f);
                float h = 45f + (float)rng.NextDouble() * 55f;
                k.Prism(c, Vector3.up, 70f + (float)rng.NextDouble() * 40f, 22f, h * 0.7f, 6, p.mountain, (float)rng.NextDouble());
                k.Prism(c + Vector3.up * h * 0.7f, Vector3.up, 22f, 0f, h * 0.3f, 6, Color.white, (float)rng.NextDouble());
            }
            for (int i = 0; i < 40; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 30f + (float)rng.NextDouble() * 120f;
                Vector3 c = new Vector3(Mathf.Cos(a) * r, -1.2f, Mathf.Sin(a) * r);
                if (c.z < 10f && Mathf.Abs(c.x) < 30f) continue;
                k.Prism(c, Vector3.up, 0.3f, 0.2f, 2f, 5, new Color(0.45f, 0.3f, 0.2f));
                k.Prism(c + Vector3.up * 1.5f, Vector3.up, 2.2f, 0f, 4.5f, 6, p.scenery, (float)rng.NextDouble());
            }
            for (int i = 0; i < 10; i++)
            {
                Vector3 c = new Vector3(((float)rng.NextDouble() - 0.5f) * 300f, 40f + (float)rng.NextDouble() * 40f, 80f + (float)rng.NextDouble() * 150f);
                k.Blob(c, new Vector3(22f, 8f, 14f), Color.white, new Color(0.9f, 0.94f, 1f), rng.Next(), 0.1f);
            }
            TrackBuilder.AddRenderer(transform, "Showroom", k.ToMesh("Showroom"), m_Cfg.materials.vertexLit);
        }

        private void ShowCar(int index)
        {
            if (index < 0 || index >= m_Cfg.cars.Length || m_Turntable == null) return;
            if (m_ShowCar != null) Destroy(m_ShowCar);
            m_ShowCar = Instantiate(m_Cfg.cars[index].prefab, m_Turntable);
            m_ShowCar.transform.localPosition = Vector3.zero;
            m_ShowCar.transform.localRotation = Quaternion.identity;
            var view = m_ShowCar.GetComponent<RacerView>();
            if (view != null) view.enabled = false;
        }

        // ---------- Main ----------
        private void BuildMain()
        {
            m_Main = UIKit.Rect(m_Safe, "Main").gameObject;
            var root = UIKit.Stretch((RectTransform)m_Main.transform);
            TextMeshProUGUI logo = UIKit.Label(root, "Logo", "TURBO RUSH", 150, m_Skin.accent, TextAlignmentOptions.Left, true);
            UIKit.Place(logo.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -10), new Vector2(1100, 190));
            logo.fontStyle = FontStyles.Italic;
            Image badge = UIKit.Panel(root, "Badge3D", m_Skin.primary);
            UIKit.Place(badge.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1010, -50), new Vector2(170, 120));
            badge.raycastTarget = false;
            TextMeshProUGUI b3 = UIKit.Label(badge.transform, "T", "3D", 90, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(b3.rectTransform);

            Button play = UIKit.Button(root, "Play", "PLAY", Icon.Play, m_Skin.good, new Vector2(620, 190), () => m_S.StartRace(m_S.SelectedTrack), 96);
            UIKit.Place((RectTransform)play.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(620, 190));
            Button garage = UIKit.Button(root, "Garage", "Garage", Icon.Car, m_Skin.secondary, new Vector2(620, 130), () => Show(GameState.VehicleSelect), 60);
            UIKit.Place((RectTransform)garage.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -150), new Vector2(620, 130));
            Button tracks = UIKit.Button(root, "Tracks", "Tracks", Icon.Road, m_Skin.primary, new Vector2(620, 130), () => Show(GameState.TrackSelect), 60);
            UIKit.Place((RectTransform)tracks.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -300), new Vector2(620, 130));
            Button settings = UIKit.Button(root, "Settings", null, Icon.Settings, new Color(0.4f, 0.45f, 0.6f), new Vector2(140, 140), OpenSettings);
            UIKit.Place((RectTransform)settings.transform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(140, 140));

            Image starBg = UIKit.Panel(root, "Stars", m_Skin.panel);
            starBg.raycastTarget = false;
            UIKit.Place(starBg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -15), new Vector2(240, 110));
            Image star = UIKit.Image(starBg.transform, "Icon", m_Skin.Get(Icon.Star), m_Skin.accent);
            UIKit.Place(star.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60, 2), new Vector2(80, 80));
            m_StarCount = UIKit.Label(starBg.transform, "Count", "0", 70, Color.white, TextAlignmentOptions.Left, true);
            UIKit.Place(m_StarCount.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(115, 4), new Vector2(120, 90));
        }

        private Button BackButton(Transform parent)
        {
            Button back = UIKit.Button(parent, "Back", null, Icon.Back, new Color(0.4f, 0.45f, 0.6f), new Vector2(140, 140), () => Show(GameState.MainMenu));
            UIKit.Place((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(140, 140));
            return back;
        }

        private TextMeshProUGUI Title(Transform parent, string text)
        {
            TextMeshProUGUI t = UIKit.Label(parent, "Title", text, 96, Color.white, TextAlignmentOptions.Left, true);
            UIKit.Place(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -10), new Vector2(900, 130));
            return t;
        }

        // ---------- Garage ----------
        private void BuildGarage()
        {
            m_Garage = UIKit.Rect(m_Safe, "Garage").gameObject;
            var root = UIKit.Stretch((RectTransform)m_Garage.transform);
            BackButton(root);
            Title(root, "Garage");
            Button left = UIKit.Button(root, "Prev", null, Icon.ArrowLeft, m_Skin.secondary, new Vector2(160, 160), () => { m_PreviewCar = (m_PreviewCar + m_Cfg.cars.Length - 1) % m_Cfg.cars.Length; RefreshGarage(); });
            UIKit.Place((RectTransform)left.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 40), new Vector2(160, 160));
            Button right = UIKit.Button(root, "Next", null, Icon.ArrowRight, m_Skin.secondary, new Vector2(160, 160), () => { m_PreviewCar = (m_PreviewCar + 1) % m_Cfg.cars.Length; RefreshGarage(); });
            UIKit.Place((RectTransform)right.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 40), new Vector2(160, 160));

            Image info = UIKit.Panel(root, "Info", m_Skin.panel);
            info.raycastTarget = false;
            UIKit.Place(info.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(980, 150));
            m_CarName = UIKit.Label(info.transform, "Name", "", 80, Color.white, TextAlignmentOptions.Left, true);
            UIKit.Place(m_CarName.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(480, 120));
            m_CarLock = UIKit.Label(info.transform, "Lock", "", 48, m_Skin.accent, TextAlignmentOptions.Right, true);
            UIKit.Place(m_CarLock.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-420, 0), new Vector2(360, 100));
            m_SelectButton = UIKit.Button(info.transform, "Select", "Select", Icon.Check, m_Skin.good, new Vector2(360, 120), SelectPreviewCar, 56);
            UIKit.Place((RectTransform)m_SelectButton.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(360, 120));
            m_SelectLabel = m_SelectButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            RectTransform strip = UIKit.Place(UIKit.Rect(root, "Chips"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1100, 110));
            int n = m_Cfg.cars.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                Button chip = UIKit.Button(strip, "Chip" + i, null, null, m_Cfg.cars[i].paint, new Vector2(110, 100), () => { m_PreviewCar = idx; RefreshGarage(); });
                UIKit.Place((RectTransform)chip.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) * 0.5f) * 130f, 0), new Vector2(110, 100));
                m_CarChips.Add(chip.GetComponent<Image>());
                Image lockIcon = UIKit.Image(chip.transform, "Lock", m_Skin.Get(Icon.Lock), new Color(1, 1, 1, 0.9f));
                UIKit.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(56, 56));
            }
            m_Garage.SetActive(false);
        }

        private void RefreshGarage()
        {
            CarDefinition c = m_Cfg.cars[m_PreviewCar];
            ShowCar(m_PreviewCar);
            bool unlocked = Progression.IsCarUnlocked(m_S.Save, c);
            bool selected = m_S.Save.selectedCar == c.id;
            m_CarName.SetText(c.displayName);
            m_CarName.color = c.paint == Color.white ? Color.white : Color.Lerp(c.paint, Color.white, 0.35f);
            if (unlocked) m_CarLock.SetText(string.Empty);
            else m_CarLock.SetText("Need {0} stars", c.unlockStars);
            m_SelectButton.gameObject.SetActive(unlocked);
            m_SelectButton.interactable = !selected;
            m_SelectLabel.SetText(selected ? "Ready!" : "Select");
            for (int i = 0; i < m_CarChips.Count; i++)
            {
                CarDefinition ci = m_Cfg.cars[i];
                bool un = Progression.IsCarUnlocked(m_S.Save, ci);
                m_CarChips[i].transform.Find("Lock").gameObject.SetActive(!un);
                m_CarChips[i].transform.localScale = i == m_PreviewCar ? Vector3.one * 1.18f : Vector3.one;
                m_CarChips[i].color = un ? ci.paint : Color.Lerp(ci.paint, m_Skin.locked, 0.6f);
            }
        }

        private void SelectPreviewCar()
        {
            CarDefinition c = m_Cfg.cars[m_PreviewCar];
            if (!Progression.IsCarUnlocked(m_S.Save, c)) return;
            m_S.Save.selectedCar = c.id;
            m_S.Persist();
            RefreshGarage();
        }

        // ---------- Tracks ----------
        private void BuildTracks()
        {
            m_Tracks = UIKit.Rect(m_Safe, "Tracks").gameObject;
            var root = UIKit.Stretch((RectTransform)m_Tracks.transform);
            Image dim = UIKit.Image(root, "Dim", m_Skin.rounded, new Color(0f, 0.05f, 0.2f, 0.35f));
            UIKit.Stretch(dim.rectTransform, -40);
            dim.rectTransform.SetAsFirstSibling();
            BackButton(root);
            Title(root, "Tracks");
            // Difficulty (top-right): Easy is the original field; Normal and Hard race harder and grab more power-ups.
            string[] names = { "Easy", "Normal", "Hard" };
            for (int d = 0; d < 3; d++)
            {
                int level = d;
                Button b = UIKit.Button(root, "Difficulty_" + names[d], names[d], null, DiffColors[d], new Vector2(250, 110), () => SetDifficulty((AI.Difficulty)level), 48);
                UIKit.Place((RectTransform)b.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10 - (2 - d) * 265, -12), new Vector2(250, 110));
                m_DiffButtons[d] = b.GetComponent<Image>();
            }
            int n = m_Cfg.tracks.Length;
            // Up to three cards at full size; more cards shrink to fit the 1920-wide reference width.
            float gap = n > 3 ? 36f : 50f;
            float w = Mathf.Min(540f, (1780f - (n - 1) * gap) / Mathf.Max(1, n)), h = 640;
            float f = w / 540f;
            for (int i = 0; i < n; i++)
            {
                TrackDefinition t = m_Cfg.tracks[i];
                Button card = UIKit.Button(root, "Card_" + t.id, null, null, Color.white, new Vector2(w, h), () => TryStart(t));
                UIKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) * 0.5f) * (w + gap), -60), new Vector2(w, h));
                Image prev = UIKit.Image(card.transform, "Preview", t.preview, Color.white);
                UIKit.Place(prev.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(w - 40, (w - 40) * 0.625f));
                TextMeshProUGUI name = UIKit.Label(card.transform, "Name", t.displayName, Mathf.Round(58 * Mathf.Lerp(1f, f, 0.6f)), m_Skin.textDark, TextAlignmentOptions.Center);
                UIKit.Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -375), new Vector2(w - 20, 80));
                TextMeshProUGUI info = UIKit.Label(card.transform, "Info", "", 40, m_Skin.textDark, TextAlignmentOptions.Center);
                UIKit.Place(info.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -455), new Vector2(w - 20, 60));
                for (int s = 0; s < 3; s++)
                {
                    Image st = UIKit.Image(card.transform, "Star" + s, m_Skin.Get(Icon.Star), m_Skin.accent);
                    UIKit.Place(st.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((s - 1) * 90 * Mathf.Min(1f, f + 0.1f), 30), new Vector2(80, 80));
                }
                Image lockPanel = UIKit.Image(card.transform, "Locked", m_Skin.rounded, new Color(0.1f, 0.12f, 0.25f, 0.82f));
                UIKit.Stretch(lockPanel.rectTransform);
                Image lockIcon = UIKit.Image(lockPanel.transform, "Icon", m_Skin.Get(Icon.Lock), Color.white);
                UIKit.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(150, 150));
                Image needStar = UIKit.Image(lockPanel.transform, "NeedStar", m_Skin.Get(Icon.Star), m_Skin.accent);
                UIKit.Place(needStar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150 * f, 10), new Vector2(80, 80));
                TextMeshProUGUI need = UIKit.Label(lockPanel.transform, "Need", "", 56, Color.white, TextAlignmentOptions.Left, true);
                UIKit.Place(need.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-150 * f + 50, 10), new Vector2(w * 0.5f + 150 * f - 60, 80));
                TextMeshProUGUI lockedName = UIKit.Label(lockPanel.transform, "Name", t.displayName, Mathf.Round(54 * Mathf.Lerp(1f, f, 0.6f)), Color.white, TextAlignmentOptions.Center, true);
                UIKit.Place(lockedName.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -375), new Vector2(w - 20, 80));
                m_TrackCards.Add(card.gameObject);
            }
            m_Tracks.SetActive(false);
        }

        private void RefreshTracks()
        {
            for (int i = 0; i < m_TrackCards.Count; i++)
            {
                TrackDefinition t = m_Cfg.tracks[i];
                Transform card = m_TrackCards[i].transform;
                bool unlocked = Progression.IsTrackUnlocked(m_S.Save, t);
                TrackRecord rec = m_S.Save.Record(t.id, false);
                int stars = rec != null ? rec.bestStars : 0;
                card.Find("Locked").gameObject.SetActive(!unlocked);
                card.Find("Locked/Need").GetComponent<TextMeshProUGUI>().SetText("x {0}", t.unlockStars);
                card.Find("Info").GetComponent<TextMeshProUGUI>().SetText(rec != null && rec.bestTime > 0 ? "Best " + UIKit.FormatTime(rec.bestTime) : t.laps + " laps");
                for (int s = 0; s < 3; s++)
                {
                    Image st = card.Find("Star" + s).GetComponent<Image>();
                    st.sprite = m_Skin.Get(s < stars ? Icon.Star : Icon.StarEmpty);
                    st.color = s < stars ? m_Skin.accent : new Color(0.3f, 0.35f, 0.5f, 0.45f);
                }
                card.localScale = m_S.Save.selectedTrack == t.id ? Vector3.one * 1.04f : Vector3.one;
            }
            RefreshDifficulty();
        }

        private void SetDifficulty(AI.Difficulty d)
        {
            m_S.Settings.difficulty = d;
            m_S.Persist();
            RefreshDifficulty();
        }

        private void RefreshDifficulty()
        {
            int sel = (int)m_S.Settings.difficulty;
            for (int d = 0; d < m_DiffButtons.Length; d++)
            {
                bool on = d == sel;
                m_DiffButtons[d].color = on ? DiffColors[d] : Color.Lerp(DiffColors[d], m_Skin.locked, 0.7f);
                m_DiffButtons[d].transform.localScale = on ? Vector3.one * 1.06f : Vector3.one * 0.92f;
            }
        }

        private void TryStart(TrackDefinition t)
        {
            if (!Progression.IsTrackUnlocked(m_S.Save, t)) { m_S.Audio.Play(Audio.Sfx.Back, 1f, true); return; }
            m_S.StartRace(t);
        }

        // ---------- Settings ----------
        public void OpenSettings()
        {
            m_Settings.SetActive(true);
            m_Settings.transform.SetAsLastSibling();
            RefreshSettings();
        }

        private void BuildSettings()
        {
            m_Settings = UIKit.Overlay(m_Safe, "Settings", 0.65f);
            RectTransform safe = UIKit.Rect(m_Settings.transform, "Safe");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Image panel = UIKit.Panel(safe, "Panel", m_Skin.panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 940));
            Transform p = panel.transform;
            TextMeshProUGUI title = UIKit.Label(p, "Title", "Settings", 90, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(800, 120));
            Button close = UIKit.Button(p, "Close", null, Icon.Check, m_Skin.good, new Vector2(130, 130), () => { m_S.Persist(); m_Settings.SetActive(false); });
            UIKit.Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(130, 130));

            SettingsData s = m_S.Settings;
            int row = 0;
            void Row(string key, Icon icon, string label, System.Action onTap, bool stepper = false, System.Action onMinus = null)
            {
                float y = -170 - row * 125;
                float x = row % 2 == 0 ? -360 : 360;
                y = -170 - (row / 2) * 150;
                Image ic = UIKit.Image(p, key + "Icon", m_Skin.Get(icon), Color.white);
                UIKit.Place(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x - 300, y - 60), new Vector2(80, 80));
                TextMeshProUGUI l = UIKit.Label(p, key + "Label", label, 46, Color.white, TextAlignmentOptions.Left, true);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(x - 245, y - 60), new Vector2(260, 80));
                if (stepper)
                {
                    Button minus = UIKit.Button(p, key + "Minus", "-", null, m_Skin.secondary, new Vector2(100, 100), onMinus, 70);
                    UIKit.Place((RectTransform)minus.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x + 85, y - 60), new Vector2(100, 100));
                    TextMeshProUGUI v = UIKit.Label(p, key + "Value", "", 46, m_Skin.accent, TextAlignmentOptions.Center, true);
                    UIKit.Place(v.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x + 200, y - 60), new Vector2(130, 80));
                    Button plus = UIKit.Button(p, key + "Plus", "+", null, m_Skin.secondary, new Vector2(100, 100), onTap, 70);
                    UIKit.Place((RectTransform)plus.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x + 315, y - 60), new Vector2(100, 100));
                    m_SettingValues[key] = v;
                }
                else
                {
                    Button b = UIKit.Button(p, key + "Toggle", "", null, m_Skin.good, new Vector2(300, 110), onTap, 46);
                    UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x + 200, y - 60), new Vector2(300, 110));
                    m_SettingValues[key] = b.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                }
                row++;
            }
            Row("music", Icon.Music, "Music", () => { s.music = !s.music; Changed(); });
            Row("sfx", Icon.Sound, "Sound", () => { s.sfx = !s.sfx; Changed(); });
            Row("vibe", Icon.Vibrate, "Vibration", () => { s.vibration = !s.vibration; Changed(); if (s.vibration) Platform.Haptics.Pulse(40); });
            Row("units", Icon.Speed, "Units", () => { s.units = s.units == SpeedUnit.Kmh ? SpeedUnit.Mph : SpeedUnit.Kmh; Changed(); });
            Row("steer", Icon.Steer, "Steering", () => { s.steeringSensitivity = Mathf.Min(1.4f, s.steeringSensitivity + 0.1f); Changed(); }, true,
                () => { s.steeringSensitivity = Mathf.Max(0.6f, s.steeringSensitivity - 0.1f); Changed(); });
            Row("quality", Icon.Quality, "Graphics", () => { s.quality = (QualityChoice)(((int)s.quality + 1) % 4); Changed(); });
            m_Settings.SetActive(false);
        }

        private void Changed()
        {
            m_S.ApplySettings();
            m_S.Persist();
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            SettingsData s = m_S.Settings;
            void Set(string key, bool on, string text)
            {
                TextMeshProUGUI t = m_SettingValues[key];
                t.SetText(text);
                Image bg = t.transform.parent.GetComponent<Image>();
                if (bg != null && key != "steer") bg.color = on ? m_Skin.good : m_Skin.locked;
            }
            Set("music", s.music, s.music ? "ON" : "OFF");
            Set("sfx", s.sfx, s.sfx ? "ON" : "OFF");
            Set("vibe", s.vibration, s.vibration ? "ON" : "OFF");
            Set("units", true, s.units == SpeedUnit.Kmh ? "km/h" : "mph");
            Set("quality", true, s.quality.ToString());
            m_SettingValues["steer"].SetText("{0:1}", s.steeringSensitivity);
        }
    }
}
