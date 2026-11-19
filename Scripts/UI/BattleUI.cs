using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ReinoRunas
{
    /// <summary>
    /// Interfaz de combate construida por código, 100% navegable con teclado o control:
    /// - Panel del grupo (PV, PM, barra ATB y estados de cada héroe) y panel de enemigos.
    /// - Menú de comandos → Habilidades / Objetos → selección de objetivo con marcador 3D.
    /// - Descripción de cada acción con costo, objetivo y recuperación ATB.
    /// - Cartel de acciones, ayuda de controles según el dispositivo, pantallas de victoria/derrota.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        public static BattleUI Instance { get; private set; }

        class Bar
        {
            public RectTransform fill;
            public Image fillImg;
            public Text text;
        }

        class UnitRow
        {
            public BattleUnit unit;
            public Image bg;
            public Text name, status;
            public Bar hp, mp, atb;
        }

        struct Entry
        {
            public string label, right, desc;
            public bool enabled;
        }

        enum MenuState { Closed, Main, Skills, Items, Target }

        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color Panel = new Color(0.08f, 0.06f, 0.1f, 0.82f);
        static readonly Color PanelEdge = new Color(0.55f, 0.42f, 0.2f, 0.9f);
        const int VisibleEntries = 6;

        Canvas canvas;
        readonly List<UnitRow> rows = new List<UnitRow>();
        GameObject menuPanel, descPanel, endPanel;
        Text menuTitle, descText, bannerText, hintText, endTitle, endSub;
        Image bannerBg;
        readonly List<Text> entryTexts = new List<Text>();
        readonly List<Text> entryRight = new List<Text>();
        readonly List<GameObject> markers = new List<GameObject>();
        float bannerTimer;

        // estado del menú
        MenuState state = MenuState.Closed;
        BattleUnit actor;
        Action<BattleCommand> onChosen;
        List<Entry> entries = new List<Entry>();
        int index, scroll, mainIndex;
        MenuState targetReturn;
        CommandType pendingType;
        SkillData pendingSkill;
        ItemData pendingItem;
        TargetType pendingTarget;
        List<BattleUnit> targetCandidates = new List<BattleUnit>();
        int targetIndex;
        readonly List<ItemData> itemList = new List<ItemData>();

        // ------------------------------------------------------------------ API estática

        public static void SetHidden(bool hidden)
        {
            if (Instance == null) return;
            Instance.canvas.enabled = !hidden;
            if (hidden) Instance.HideMarkers();
        }

        public static void Banner(string text, float duration = 1.5f)
        {
            if (Instance == null) return;
            Instance.bannerText.text = text;
            Instance.bannerTimer = duration;
        }

        // ------------------------------------------------------------------ construcción

        void Awake()
        {
            Instance = this;
            Build();
        }

        void Start()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;
            for (int i = 0; i < bm.party.Count; i++) rows.Add(CreateRow(bm.party[i], true, i));
            for (int i = 0; i < bm.enemies.Count; i++) rows.Add(CreateRow(bm.enemies[i], false, i));
            for (int i = 0; i < 4; i++) markers.Add(CreateMarker());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Transform root;

        void Build()
        {
            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root = cgo.transform;

            // Cartel superior
            bannerBg = Img("Cartel", root, new Color(0f, 0f, 0f, 0.6f), V(.5f, 1), V(.5f, 1), V(.5f, 1), V(0, -24), V(1100, 64));
            bannerText = Label("CartelTexto", bannerBg.transform, "", 30, V(0, 0), V(1, 1), V(.5f, .5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, Gold);

            // Menú de comandos
            var mp = Framed("Menu", V(0, 0), V(0, 0), V(0, 0), V(30, 60), V(560, 330));
            menuPanel = mp.gameObject;
            menuTitle = Label("Titulo", mp.transform, "", 26, V(0, 1), V(1, 1), V(0, 1), V(20, -12), V(-40, 34), TextAnchor.UpperLeft, Gold);
            for (int i = 0; i < VisibleEntries; i++)
            {
                entryTexts.Add(Label("Opcion" + i, mp.transform, "", 28, V(0, 1), V(1, 1), V(0, 1), V(24, -58 - i * 42), V(-48, 40), TextAnchor.MiddleLeft, Color.white));
                entryRight.Add(Label("Costo" + i, mp.transform, "", 24, V(0, 1), V(1, 1), V(1, 1), V(-24, -58 - i * 42), V(160, 40), TextAnchor.MiddleRight, new Color(0.55f, 0.8f, 1f)));
            }
            menuPanel.SetActive(false);

            var dp = Framed("Descripcion", V(0, 0), V(0, 0), V(0, 0), V(30, 400), V(700, 130));
            descPanel = dp.gameObject;
            descText = Label("Texto", dp.transform, "", 22, V(0, 0), V(1, 1), V(.5f, .5f), Vector2.zero, V(-36, -24), TextAnchor.UpperLeft, Color.white);
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descPanel.SetActive(false);

            // Ayuda de controles
            hintText = Label("Controles", root, "", 20, V(.5f, 0), V(.5f, 0), V(.5f, 0), V(0, 14), V(1800, 30), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));

            // Pantalla final
            var end = Img("Final", root, new Color(0f, 0f, 0f, 0.7f), V(0, 0), V(1, 1), V(.5f, .5f), Vector2.zero, Vector2.zero);
            endPanel = end.gameObject;
            endTitle = Label("Titulo", end.transform, "", 120, V(.5f, .5f), V(.5f, .5f), V(.5f, .5f), V(0, 80), V(1600, 160), TextAnchor.MiddleCenter, Gold);
            endSub = Label("Sub", end.transform, "", 34, V(.5f, .5f), V(.5f, .5f), V(.5f, .5f), V(0, -60), V(1400, 120), TextAnchor.MiddleCenter, Color.white);
            endPanel.SetActive(false);
        }

        UnitRow CreateRow(BattleUnit u, bool isParty, int i)
        {
            var r = new UnitRow { unit = u };
            Image bg;
            if (isParty) bg = Framed("Heroe_" + u.Name, V(1, 0), V(1, 0), V(1, 0), V(-30, 60 + (2 - i) * 92), V(1000, 84));
            else bg = Framed("Enemigo_" + u.Name, V(1, 1), V(1, 1), V(1, 1), V(-30, -110 - i * 76), V(620, 68));
            r.bg = bg;
            var t = bg.transform;
            if (isParty)
            {
                r.name = Label("Nombre", t, u.Name, 28, V(0, 1), V(0, 1), V(0, 1), V(18, -8), V(230, 34), TextAnchor.UpperLeft, Color.white);
                r.status = Label("Estados", t, "", 18, V(0, 0), V(0, 0), V(0, 0), V(18, 8), V(300, 28), TextAnchor.LowerLeft, Color.white);
                r.hp = MakeBar(t, "PV", 250, 250, new Color(0.35f, 0.85f, 0.35f));
                r.mp = MakeBar(t, "PM", 520, 190, new Color(0.3f, 0.55f, 1f));
                r.atb = MakeBar(t, "ATB", 730, 240, Gold);
            }
            else
            {
                r.name = Label("Nombre", t, u.Name, 24, V(0, 1), V(0, 1), V(0, 1), V(16, -6), V(230, 30), TextAnchor.UpperLeft, new Color(1f, 0.75f, 0.7f));
                r.status = Label("Estados", t, "", 16, V(0, 0), V(0, 0), V(0, 0), V(16, 6), V(260, 24), TextAnchor.LowerLeft, Color.white);
                r.hp = MakeBar(t, "PV", 250, 200, new Color(0.85f, 0.3f, 0.3f));
                r.atb = MakeBar(t, "ATB", 470, 130, Gold);
            }
            return r;
        }

        Bar MakeBar(Transform parent, string label, float x, float width, Color color)
        {
            Label(label + "_Etiqueta", parent, label, 16, V(0, .5f), V(0, .5f), V(0, 0), V(x, 14), V(width, 22), TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.7f));
            var bg = Img(label + "_Fondo", parent, new Color(0f, 0f, 0f, 0.6f), V(0, .5f), V(0, .5f), V(0, 1), V(x, 10), V(width, 24));
            var fill = Img(label + "_Relleno", bg.transform, color, V(0, 0), V(1, 1), V(0, .5f), Vector2.zero, Vector2.zero);
            fill.rectTransform.offsetMin = V(3, 3);
            fill.rectTransform.offsetMax = V(-3, -3);
            var txt = Label(label + "_Texto", bg.transform, "", 18, V(0, 0), V(1, 1), V(.5f, .5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, Color.white);
            return new Bar { fill = fill.rectTransform, fillImg = fill, text = txt };
        }

        Image Framed(string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var edge = Img(name, root, PanelEdge, aMin, aMax, pivot, pos, size);
            var inner = Img("Fondo", edge.transform, Panel, V(0, 0), V(1, 1), V(.5f, .5f), Vector2.zero, Vector2.zero);
            inner.rectTransform.offsetMin = V(3, 3);
            inner.rectTransform.offsetMax = V(-3, -3);
            return edge;
        }

        GameObject CreateMarker()
        {
            var m = new GameObject("MarcadorObjetivo");
            var gem = Vfx.Primitive(PrimitiveType.Cube, new Color(1f, 0.85f, 0.2f), new Vector3(0.28f, 0.28f, 0.28f), m.transform, Vector3.zero, new Vector3(45f, 0f, 45f), true);
            gem.name = "Gema";
            m.AddComponent<Spinner>().speed = new Vector3(0f, 200f, 0f);
            m.SetActive(false);
            return m;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static Image Img(string name, Transform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Label(string name, Transform parent, string content, int size, Vector2 aMin, Vector2 aMax, Vector2 pivot,
            Vector2 pos, Vector2 sizeDelta, TextAnchor align, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = Vfx.UIFont;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        // ------------------------------------------------------------------ menú

        public void OpenCommandMenu(BattleUnit unit, Action<BattleCommand> callback)
        {
            actor = unit;
            onChosen = callback;
            Sfx.Play(SfxId.UiConfirm, 0.4f, 0f);
            ShowMain(0);
        }

        void ShowMain(int selected)
        {
            state = MenuState.Main;
            HideMarkers();
            bool hasItems = BattleManager.Instance.items.Any(it => BattleManager.Instance.ItemCount(it) > 0);
            bool hasSkills = actor.data.skills.Count > 0;
            entries = new List<Entry>
            {
                new Entry { label = "Atacar", enabled = true, desc = actor.data.basicAttack != null ? Describe(actor.data.basicAttack) : "Ataque básico." },
                new Entry { label = "Habilidades", enabled = hasSkills, desc = "Técnicas y hechizos especiales. Consumen PM." },
                new Entry { label = "Objetos", enabled = hasItems, desc = hasItems ? "Pociones, éteres y otros objetos del grupo." : "No quedan objetos." },
                new Entry { label = "Defender", enabled = true, desc = "Reduce a la mitad el daño recibido hasta tu próximo turno.\nRecuperación: <color=#7FFF7F>Rápida</color>" },
            };
            index = Mathf.Clamp(selected, 0, entries.Count - 1);
            scroll = 0;
            menuTitle.text = $"{actor.Name.ToUpper()} <size=20><color=#BBBBBB>· {actor.data.role}</color></size>";
            RefreshList();
        }

        void ShowSkills(int selected)
        {
            state = MenuState.Skills;
            HideMarkers();
            entries = actor.data.skills.Select(s => new Entry
            {
                label = s.displayName + (s.isUltimate ? " <size=18><color=#FF9F40>ESPECIAL</color></size>" : ""),
                right = $"{s.mpCost} PM",
                enabled = actor.MP >= s.mpCost,
                desc = Describe(s)
            }).ToList();
            index = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
            scroll = 0;
            menuTitle.text = "HABILIDADES";
            RefreshList();
        }

        void ShowItems(int selected)
        {
            state = MenuState.Items;
            HideMarkers();
            var bm = BattleManager.Instance;
            itemList.Clear();
            itemList.AddRange(bm.items.Where(it => bm.ItemCount(it) > 0));
            entries = itemList.Select(it => new Entry
            {
                label = it.displayName,
                right = "x" + bm.ItemCount(it),
                enabled = true,
                desc = it.description + "\n<color=#AAAAAA>Objetivo: " + TargetName(it.target) + " · Recuperación: " + RecoveryLabel(it.atbAfterAction) + "</color>"
            }).ToList();
            index = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
            scroll = 0;
            menuTitle.text = "OBJETOS";
            RefreshList();
        }

        void BeginTarget(TargetType t, MenuState returnTo)
        {
            targetReturn = returnTo;
            pendingTarget = t;
            targetCandidates = BattleManager.Instance.ValidTargets(actor, t);
            if (targetCandidates.Count == 0)
            {
                Sfx.Play(SfxId.UiError);
                return;
            }
            state = MenuState.Target;
            // Por defecto: aliado más herido para curaciones, si no el primero.
            targetIndex = 0;
            if (t == TargetType.SingleAlly)
                targetIndex = targetCandidates.IndexOf(targetCandidates.OrderBy(u => u.HP / u.MaxHP).First());
            Sfx.Play(SfxId.UiConfirm, 0.6f, 0f);
        }

        static string Describe(SkillData s)
        {
            string extra = $"\n<color=#AAAAAA>Objetivo: {TargetName(s.target)} · Recuperación: {RecoveryColored(s.atbAfterAction)}";
            if (s.mpCost > 0) extra += $" · Costo: {s.mpCost} PM";
            extra += "</color>";
            return s.description + extra;
        }

        static string RecoveryLabel(float atbAfter)
        {
            if (atbAfter <= -40f) return "Muy lenta";
            if (atbAfter < 0f) return "Lenta";
            if (atbAfter >= 25f) return "Rápida";
            return "Normal";
        }

        static string RecoveryColored(float atbAfter)
        {
            string l = RecoveryLabel(atbAfter);
            string c = atbAfter < 0f ? "#FF8A80" : atbAfter >= 25f ? "#7FFF7F" : "#FFFFFF";
            return $"<color={c}>{l}</color>";
        }

        static string TargetName(TargetType t)
        {
            switch (t)
            {
                case TargetType.SingleEnemy: return "1 enemigo";
                case TargetType.AllEnemies: return "Todos los enemigos";
                case TargetType.SingleAlly: return "1 aliado";
                case TargetType.AllAllies: return "Todo el grupo";
                default: return "Uno mismo";
            }
        }

        void RefreshList()
        {
            if (index < scroll) scroll = index;
            if (index >= scroll + VisibleEntries) scroll = index - VisibleEntries + 1;
            for (int i = 0; i < VisibleEntries; i++)
            {
                int e = scroll + i;
                if (e >= entries.Count)
                {
                    entryTexts[i].text = "";
                    entryRight[i].text = "";
                    continue;
                }
                bool sel = e == index;
                var en = entries[e];
                Color c = !en.enabled ? new Color(0.5f, 0.5f, 0.5f) : sel ? Gold : Color.white;
                entryTexts[i].text = (sel ? "► " : "   ") + en.label;
                entryTexts[i].color = c;
                entryRight[i].text = en.right ?? "";
                entryRight[i].color = !en.enabled ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.55f, 0.8f, 1f);
            }
            if (entries.Count == 0)
            {
                entryTexts[0].text = "   (vacío)";
                entryTexts[0].color = Color.gray;
            }
            descText.text = entries.Count > 0 ? entries[index].desc : "";
        }

        void Submit(List<BattleUnit> targets)
        {
            var cmd = new BattleCommand { actor = actor, type = pendingType, skill = pendingSkill, item = pendingItem, targets = targets };
            state = MenuState.Closed;
            HideMarkers();
            Sfx.Play(SfxId.UiConfirm, 0.8f, 0f);
            var cb = onChosen;
            onChosen = null;
            cb?.Invoke(cmd);
        }

        // ------------------------------------------------------------------ actualización

        void Update()
        {
            UpdateRows();
            UpdateBanner();
            UpdateHint();
            HandleMenuInput();

            bool menuOpen = state != MenuState.Closed && state != MenuState.Target;
            menuPanel.SetActive(state != MenuState.Closed);
            descPanel.SetActive(state != MenuState.Closed);
            if (state == MenuState.Target) UpdateTargetView();
            else if (menuOpen) { }
        }

        void HandleMenuInput()
        {
            if (state == MenuState.Closed || !canvas.enabled) return;
            var nav = RInput.Navigate;

            if (state == MenuState.Target)
            {
                bool single = pendingTarget == TargetType.SingleEnemy || pendingTarget == TargetType.SingleAlly;
                if (single && nav != Vector2Int.zero)
                {
                    int d = (nav.y < 0 || nav.x > 0) ? 1 : -1;
                    targetIndex = (targetIndex + d + targetCandidates.Count) % targetCandidates.Count;
                    Sfx.Play(SfxId.UiMove, 0.5f, 0f);
                }
                if (RInput.Confirm)
                {
                    var list = single ? new List<BattleUnit> { targetCandidates[targetIndex] } : new List<BattleUnit>(targetCandidates);
                    Submit(list);
                }
                else if (RInput.Cancel)
                {
                    Sfx.Play(SfxId.UiCancel, 0.6f, 0f);
                    if (targetReturn == MenuState.Skills) ShowSkills(index);
                    else if (targetReturn == MenuState.Items) ShowItems(index);
                    else ShowMain(mainIndex);
                }
                return;
            }

            if (nav.y != 0 && entries.Count > 0)
            {
                index = (index - nav.y + entries.Count) % entries.Count;
                Sfx.Play(SfxId.UiMove, 0.5f, 0f);
                RefreshList();
            }

            if (RInput.Cancel && state != MenuState.Main)
            {
                Sfx.Play(SfxId.UiCancel, 0.6f, 0f);
                ShowMain(mainIndex);
                return;
            }

            if (!RInput.Confirm || entries.Count == 0) return;
            if (!entries[index].enabled)
            {
                Sfx.Play(SfxId.UiError);
                if (state == MenuState.Skills) Banner("No tenés suficientes PM", 1f);
                return;
            }

            switch (state)
            {
                case MenuState.Main:
                    mainIndex = index;
                    switch (index)
                    {
                        case 0:
                            pendingType = CommandType.Attack;
                            pendingSkill = actor.data.basicAttack;
                            pendingItem = null;
                            BeginTarget(pendingSkill != null ? pendingSkill.target : TargetType.SingleEnemy, MenuState.Main);
                            break;
                        case 1:
                            Sfx.Play(SfxId.UiConfirm, 0.6f, 0f);
                            ShowSkills(0);
                            break;
                        case 2:
                            Sfx.Play(SfxId.UiConfirm, 0.6f, 0f);
                            ShowItems(0);
                            break;
                        default:
                            pendingType = CommandType.Defend;
                            pendingSkill = null;
                            pendingItem = null;
                            Submit(new List<BattleUnit> { actor });
                            break;
                    }
                    break;

                case MenuState.Skills:
                    pendingType = CommandType.Skill;
                    pendingSkill = actor.data.skills[index];
                    pendingItem = null;
                    BeginTarget(pendingSkill.target, MenuState.Skills);
                    break;

                case MenuState.Items:
                    pendingType = CommandType.Item;
                    pendingItem = itemList[index];
                    pendingSkill = null;
                    BeginTarget(pendingItem.target, MenuState.Items);
                    break;
            }
        }

        void UpdateTargetView()
        {
            bool single = pendingTarget == TargetType.SingleEnemy || pendingTarget == TargetType.SingleAlly;
            HideMarkers();
            for (int i = 0; i < targetCandidates.Count && i < markers.Count; i++)
            {
                if (single && i != targetIndex) continue;
                var u = targetCandidates[i];
                markers[i].SetActive(true);
                markers[i].transform.position = u.View.TopPoint + Vector3.up * (0.35f + Mathf.Sin(Time.time * 5f) * 0.1f);
            }
            if (single)
            {
                var u = targetCandidates[targetIndex];
                descText.text = $"Objetivo: <color=#FFD15A>{u.Name}</color>   PV {u.HP:0}/{u.MaxHP:0}   PM {u.MP:0}/{u.MaxMP:0}\n{u.StatusLine()}";
            }
            else descText.text = $"Objetivo: <color=#FFD15A>{TargetName(pendingTarget)}</color>";
        }

        void HideMarkers()
        {
            foreach (var m in markers)
                if (m != null) m.SetActive(false);
        }

        void UpdateRows()
        {
            var bm = BattleManager.Instance;
            foreach (var r in rows)
            {
                var u = r.unit;
                if (u == null) continue;
                bool active = bm != null && bm.ActiveUnit == u;
                r.bg.color = active ? Gold : PanelEdge;
                r.name.color = !u.IsAlive ? new Color(0.5f, 0.5f, 0.5f) : (u.team == Team.Player ? Color.white : new Color(1f, 0.75f, 0.7f));
                r.name.text = u.IsAlive ? (active ? "► " + u.Name : u.Name) : u.Name + " (K.O.)";
                SetBar(r.hp, u.HP / u.MaxHP, $"{u.HP:0}/{u.MaxHP:0}");
                r.hp.fillImg.color = u.team == Team.Player
                    ? Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.35f, 0.85f, 0.35f), u.HP / u.MaxHP)
                    : new Color(0.85f, 0.3f, 0.3f);
                if (r.mp != null) SetBar(r.mp, u.MaxMP > 0f ? u.MP / u.MaxMP : 0f, $"{u.MP:0}/{u.MaxMP:0}");

                if (!u.IsAlive) SetBar(r.atb, 0f, "");
                else if (u.ATB < 0f)
                {
                    SetBar(r.atb, -u.ATB / 100f, "RECUPERANDO");
                    r.atb.fillImg.color = new Color(0.65f, 0.2f, 0.25f);
                }
                else
                {
                    bool full = u.ATB >= 100f;
                    SetBar(r.atb, u.ATB / 100f, full ? "¡LISTO!" : "");
                    r.atb.fillImg.color = full ? Color.Lerp(Gold, Color.white, Mathf.PingPong(Time.time * 3f, 1f)) : Gold;
                }
                r.status.text = u.IsAlive ? u.StatusLine() : "";
            }
        }

        static void SetBar(Bar b, float pct, string text)
        {
            b.fill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
            b.text.text = text;
        }

        void UpdateBanner()
        {
            bannerTimer -= Time.deltaTime;
            float a = Mathf.Clamp01(bannerTimer * 3f);
            bannerBg.color = new Color(0f, 0f, 0f, 0.6f * a);
            bannerText.color = new Color(Gold.r, Gold.g, Gold.b, a);
        }

        void UpdateHint()
        {
            string nav = RInput.NavLabel, ok = RInput.ConfirmLabel, back = RInput.CancelLabel, fast = RInput.FastLabel;
            switch (state)
            {
                case MenuState.Target:
                    hintText.text = $"{nav} Elegir objetivo     {ok} Confirmar     {back} Volver";
                    break;
                case MenuState.Closed:
                    var bm = BattleManager.Instance;
                    hintText.text = bm != null && (bm.CurrentPhase == BattleManager.Phase.Victory || bm.CurrentPhase == BattleManager.Phase.Defeat)
                        ? "" : $"Mantené {fast} para acelerar las barras ATB";
                    break;
                default:
                    hintText.text = $"{nav} Navegar     {ok} Confirmar     {back} Volver     Mantené {fast} para acelerar";
                    break;
            }
        }

        public void ShowEnd(bool victory)
        {
            state = MenuState.Closed;
            HideMarkers();
            endPanel.SetActive(true);
            endTitle.text = victory ? "¡VICTORIA!" : "DERROTA";
            endTitle.color = victory ? Gold : new Color(0.9f, 0.3f, 0.3f);
            endSub.text = (victory ? "El castillo está a salvo. ¡Los monstruos huyen!" : "Los héroes cayeron... el castillo está en peligro.")
                          + $"\n\n<color=#FFD15A>{RInput.ConfirmLabel} Volver a jugar</color>";
        }
    }
}
