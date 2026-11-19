using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReinoRunas.EditorTools
{
    /// <summary>
    /// Menú "Reino de las Runas → Construir escena de combate".
    /// Genera ScriptableObjects (personajes, enemigos, habilidades, objetos), materiales y la escena:
    /// patio de un castillo al atardecer, antorchas, los 3 héroes y 3 monstruos modelados con primitivas,
    /// cámara de combate, BattleManager y BattleUI.
    /// </summary>
    public static class BattleSceneBuilder
    {
        const string Root = "Assets/ReinoDeLasRunas";
        const string DataDir = Root + "/Data";
        const string SkillDir = DataDir + "/Habilidades";
        const string ItemDir = DataDir + "/Objetos";
        const string UnitDir = DataDir + "/Unidades";
        const string MatDir = Root + "/Materials";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/CombateCastillo.unity";

        static Dictionary<string, Material> mats;
        static Transform env;

        // =====================================================================================

        [MenuItem("Reino de las Runas/Construir escena de combate", priority = 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var d in new[] { Root, DataDir, SkillDir, ItemDir, UnitDir, MatDir, SceneDir }) EnsureFolder(d);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMaterials();

            // ---------- Datos ----------
            var S = CreateSkills();
            var I = CreateItems();
            var aldricData = Unit("Heroe_Aldric", "Sir Aldric", "Caballero · Tanque", 340, 50, 24, 22, 8, 8, S["Espadazo"], new[] { S["Golpe de Escudo"], S["Provocar"], S["Tajo Devastador"] }, 0f, 0f,
                "Caballero de la Guardia Real. Mucha vida y defensa; protege al grupo atrayendo los golpes.");
            var elaraData = Unit("Heroe_Elara", "Elara", "Maga arcana", 180, 130, 9, 8, 28, 12, S["Golpe de Bastón"], new[] { S["Bola de Fuego"], S["Cadena de Rayos"], S["Toxina Arcana"], S["Lluvia de Meteoros"] }, 0f, 0f,
                "Hechicera de la torre. Daño mágico enorme y estados alterados, pero es frágil.");
            var isoldeData = Unit("Heroe_Isolde", "Isolde", "Juglaresa · Apoyo", 220, 100, 14, 11, 18, 15, S["Puñalada"], new[] { S["Himno de Valor"], S["Canción Ligera"], S["Canto Sanador"], S["Réquiem Quebrantador"], S["Nana Pesada"] }, 0f, 0f,
                "Barda errante. Sus canciones potencian al grupo, debilitan enemigos y manipulan la barra ATB.");
            var goblinData = Unit("Enemigo_Goblin", "Goblin Saqueador", "Monstruo", 200, 30, 17, 9, 6, 16, S["Cuchillada"], new[] { S["Daga Envenenada"] }, 0.35f, 0.6f, "Rápido y traicionero.");
            var orcData = Unit("Enemigo_Orco", "Orco Bruto", "Monstruo", 480, 40, 30, 18, 4, 7, S["Garrotazo"], new[] { S["Golpe Aplastante"], S["Grito de Guerra"] }, 0.4f, 0.3f, "Lento pero devastador.");
            var shamanData = Unit("Enemigo_Chaman", "Chamán Goblin", "Monstruo", 240, 90, 8, 10, 22, 11, S["Bastonazo"], new[] { S["Llamarada"], S["Maldición"], S["Curación Oscura"] }, 0.6f, 0.4f, "Cura a sus aliados y lanza maldiciones.");

            // ---------- Escena ----------
            var sun = SetupLighting();
            env = new GameObject("Escenario").transform;
            BuildEnvironment();

            var unitsRoot = new GameObject("Combatientes").transform;
            var aldric = BuildAldric(unitsRoot, aldricData, new Vector3(-3.6f, 0f, -1.4f));
            var elara = BuildElara(unitsRoot, elaraData, new Vector3(-5.4f, 0f, 0.6f));
            var isolde = BuildIsolde(unitsRoot, isoldeData, new Vector3(-4.3f, 0f, 2.6f));
            var goblin = BuildGoblin(unitsRoot, goblinData, new Vector3(3.6f, 0f, -1.4f));
            var orc = BuildOrc(unitsRoot, orcData, new Vector3(5.4f, 0f, 0.6f));
            var shaman = BuildShaman(unitsRoot, shamanData, new Vector3(4.2f, 0f, 2.8f));

            // Cámara
            var camGo = new GameObject("Cámara de combate");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 3.4f, -8.6f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.2f, 1.2f) - camGo.transform.position);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            if (RenderSettings.skybox != null) cam.clearFlags = CameraClearFlags.Skybox;
            else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.95f, 0.6f, 0.45f); }
            camGo.AddComponent<AudioListener>();
            var bcam = camGo.AddComponent<BattleCamera>();
            bcam.lookAtPoint = new Vector3(0f, -0.2f, 1.2f);

            // Sistemas
            var sys = new GameObject("Combate");
            var bm = sys.AddComponent<BattleManager>();
            bm.party = new List<BattleUnit> { aldric, elara, isolde };
            bm.enemies = new List<BattleUnit> { goblin, orc, shaman };
            bm.items = I;
            bm.battleCamera = bcam;
            bm.sun = sun;
            new GameObject("Interfaz").AddComponent<BattleUI>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Reino de las Runas", "¡Escena de combate construida!\n\n" + ScenePath +
                "\nDatos editables: " + DataDir + "\n\nApretá Play.", "¡A las armas!");
        }

        [MenuItem("Reino de las Runas/Restablecer datos por defecto (borra ScriptableObjects)", priority = 20)]
        public static void ResetData()
        {
            if (!EditorUtility.DisplayDialog("Reino de las Runas", "Se borran los ScriptableObjects de " + DataDir + " y se recrean con los valores por defecto al reconstruir. ¿Seguir?", "Sí", "Cancelar")) return;
            AssetDatabase.DeleteAsset(DataDir);
            AssetDatabase.Refresh();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }

        static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a; // respeta los cambios hechos en el Inspector
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // =====================================================================================
        // Habilidades

        static StatusApplication St(StatusType t, float chance, int turns) => new StatusApplication { status = t, chance = chance, turns = turns };
        static StatModifierData Mod(Stat s, float m, int turns) => new StatModifierData { stat = s, multiplier = m, turns = turns };

        static SkillData Skill(string name, string desc, int mp, TargetType target, SkillKind kind, float power,
            SkillAnimation anim, SkillVisual visual, Color color, float atbAfter = 0f, float targetAtb = 0f,
            StatusApplication[] statuses = null, StatModifierData[] mods = null, bool ultimate = false, float crit = 0.1f)
        {
            string file = name.Replace(" ", "").Replace("ñ", "n").Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
            return LoadOrCreate<SkillData>(SkillDir + "/" + file + ".asset", s =>
            {
                s.displayName = name;
                s.description = desc;
                s.mpCost = mp;
                s.target = target;
                s.kind = kind;
                s.power = power;
                s.animation = anim;
                s.visual = visual;
                s.color = color;
                s.atbAfterAction = atbAfter;
                s.targetAtbChange = targetAtb;
                s.statuses = statuses != null ? statuses.ToList() : new List<StatusApplication>();
                s.statModifiers = mods != null ? mods.ToList() : new List<StatModifierData>();
                s.isUltimate = ultimate;
                s.critChance = crit;
            });
        }

        static Dictionary<string, SkillData> CreateSkills()
        {
            var fire = new Color(1f, 0.5f, 0.15f);
            var light = new Color(1f, 0.95f, 0.45f);
            var poison = new Color(0.5f, 1f, 0.25f);
            var arcane = new Color(0.75f, 0.4f, 1f);
            var steel = new Color(0.85f, 0.9f, 1f);
            var song = new Color(0.4f, 0.95f, 0.85f);
            var holy = new Color(0.5f, 1f, 0.55f);
            var dark = new Color(0.6f, 0.25f, 0.8f);

            var d = new Dictionary<string, SkillData>();
            void Add(SkillData s) => d[s.displayName] = s;

            // --- Sir Aldric (tanque) ---
            Add(Skill("Espadazo", "Un tajo firme con la espada larga.", 0, TargetType.SingleEnemy, SkillKind.Physical, 10, SkillAnimation.Melee, SkillVisual.Slash, steel));
            Add(Skill("Golpe de Escudo", "Golpea con el escudo. Daño moderado y 45% de PARALIZAR. Acción rápida.", 6, TargetType.SingleEnemy, SkillKind.Physical, 8, SkillAnimation.Melee, SkillVisual.Shield, steel,
                atbAfter: 25f, statuses: new[] { St(StatusType.Paralysis, 0.45f, 2) }));
            Add(Skill("Provocar", "Desafía a los monstruos: durante 3 turnos están OBLIGADOS a atacar a Aldric, que además gana DEF↑.", 8, TargetType.Self, SkillKind.Support, 0, SkillAnimation.Cast, SkillVisual.Shield, new Color(1f, 0.35f, 0.3f),
                atbAfter: 20f, statuses: new[] { St(StatusType.Taunt, 1f, 3) }, mods: new[] { Mod(Stat.Defense, 1.5f, 3) }));
            Add(Skill("Tajo Devastador", "Golpe brutal con todo el peso del cuerpo. Daño muy alto pero deja a Aldric expuesto (recuperación MUY LENTA).", 14, TargetType.SingleEnemy, SkillKind.Physical, 22, SkillAnimation.Melee, SkillVisual.Slash, new Color(1f, 0.85f, 0.5f),
                atbAfter: -55f, crit: 0.2f));

            // --- Elara (maga) ---
            Add(Skill("Golpe de Bastón", "Un bastonazo. Poco daño, no gasta PM.", 0, TargetType.SingleEnemy, SkillKind.Physical, 7, SkillAnimation.Melee, SkillVisual.Slash, arcane, crit: 0.05f));
            Add(Skill("Bola de Fuego", "Proyectil ígneo. Daño mágico alto y 50% de QUEMAR (daño por turno y ATQ↓).", 10, TargetType.SingleEnemy, SkillKind.Magical, 14, SkillAnimation.Projectile, SkillVisual.Fire, fire,
                statuses: new[] { St(StatusType.Burn, 0.5f, 3) }));
            Add(Skill("Cadena de Rayos", "Un relámpago que salta entre TODOS los enemigos. 30% de PARALIZAR. Recuperación lenta.", 18, TargetType.AllEnemies, SkillKind.Magical, 9, SkillAnimation.Projectile, SkillVisual.Lightning, light,
                atbAfter: -15f, statuses: new[] { St(StatusType.Paralysis, 0.3f, 2) }));
            Add(Skill("Toxina Arcana", "Maldición venenosa: ENVENENA seguro durante 4 turnos (7% de la vida por turno). Acción rápida.", 8, TargetType.SingleEnemy, SkillKind.Magical, 4, SkillAnimation.Cast, SkillVisual.Poison, poison,
                atbAfter: 20f, statuses: new[] { St(StatusType.Poison, 1f, 4) }));
            Add(Skill("Lluvia de Meteoros", "HABILIDAD ESPECIAL. Elara invoca meteoros sobre TODOS los enemigos (60% de QUEMAR). Cinemática; recuperación MUY LENTA.", 40, TargetType.AllEnemies, SkillKind.Magical, 18, SkillAnimation.Ultimate, SkillVisual.Meteor, fire,
                atbAfter: -60f, statuses: new[] { St(StatusType.Burn, 0.6f, 3) }, ultimate: true, crit: 0.15f));

            // --- Isolde (buffs / debuffs) ---
            Add(Skill("Puñalada", "Un rápido golpe de daga.", 0, TargetType.SingleEnemy, SkillKind.Physical, 9, SkillAnimation.Melee, SkillVisual.Slash, steel, atbAfter: 10f, crit: 0.2f));
            Add(Skill("Himno de Valor", "Canción épica: ATQ↑ y MAG↑ (+30%) a TODO el grupo por 3 turnos.", 12, TargetType.AllAllies, SkillKind.Support, 0, SkillAnimation.Song, SkillVisual.Song, new Color(1f, 0.8f, 0.3f),
                mods: new[] { Mod(Stat.Attack, 1.3f, 3), Mod(Stat.Magic, 1.3f, 3) }));
            Add(Skill("Canción Ligera", "Un aliado recibe PRISA (barra ATB +50% más rápida) y su barra avanza +50 al instante.", 10, TargetType.SingleAlly, SkillKind.Support, 0, SkillAnimation.Song, SkillVisual.Song, new Color(0.4f, 0.9f, 1f),
                atbAfter: 15f, targetAtb: 50f, statuses: new[] { St(StatusType.Haste, 1f, 3) }));
            Add(Skill("Canto Sanador", "Melodía que cura a TODO el grupo.", 14, TargetType.AllAllies, SkillKind.Heal, 6, SkillAnimation.Song, SkillVisual.Holy, holy));
            Add(Skill("Réquiem Quebrantador", "Canción lúgubre: DEF↓ (-40%) y ATQ↓ (-25%) a un enemigo por 3 turnos.", 12, TargetType.SingleEnemy, SkillKind.Support, 0, SkillAnimation.Song, SkillVisual.Dark, dark,
                mods: new[] { Mod(Stat.Defense, 0.6f, 3), Mod(Stat.Attack, 0.75f, 3) }));
            Add(Skill("Nana Pesada", "Un enemigo sufre LENTITUD (barra a la mitad de velocidad) y su barra retrocede -40.", 10, TargetType.SingleEnemy, SkillKind.Support, 0, SkillAnimation.Song, SkillVisual.Song, new Color(0.7f, 0.5f, 1f),
                atbAfter: 15f, targetAtb: -40f, statuses: new[] { St(StatusType.Slow, 1f, 3) }));

            // --- Enemigos ---
            Add(Skill("Cuchillada", "Ataque del goblin.", 0, TargetType.SingleEnemy, SkillKind.Physical, 9, SkillAnimation.Melee, SkillVisual.Slash, steel));
            Add(Skill("Daga Envenenada", "70% de ENVENENAR.", 6, TargetType.SingleEnemy, SkillKind.Physical, 7, SkillAnimation.Melee, SkillVisual.Poison, poison,
                statuses: new[] { St(StatusType.Poison, 0.7f, 3) }));
            Add(Skill("Garrotazo", "Ataque del orco.", 0, TargetType.SingleEnemy, SkillKind.Physical, 11, SkillAnimation.Melee, SkillVisual.Slash, steel));
            Add(Skill("Golpe Aplastante", "Golpe tremendo, 30% de PARALIZAR. Recuperación lenta.", 10, TargetType.SingleEnemy, SkillKind.Physical, 18, SkillAnimation.Melee, SkillVisual.Shield, steel,
                atbAfter: -40f, statuses: new[] { St(StatusType.Paralysis, 0.3f, 2) }));
            Add(Skill("Grito de Guerra", "ATQ↑ +40% propio por 3 turnos.", 8, TargetType.Self, SkillKind.Support, 0, SkillAnimation.Cast, SkillVisual.Dark, new Color(1f, 0.3f, 0.2f),
                atbAfter: 10f, mods: new[] { Mod(Stat.Attack, 1.4f, 3) }));
            Add(Skill("Bastonazo", "Ataque del chamán.", 0, TargetType.SingleEnemy, SkillKind.Physical, 6, SkillAnimation.Melee, SkillVisual.Slash, steel));
            Add(Skill("Llamarada", "Fuego oscuro, 50% de QUEMAR.", 10, TargetType.SingleEnemy, SkillKind.Magical, 12, SkillAnimation.Projectile, SkillVisual.Fire, new Color(0.9f, 0.3f, 1f),
                statuses: new[] { St(StatusType.Burn, 0.5f, 3) }));
            Add(Skill("Maldición", "ATQ↓ y DEF↓ (-25%) a un héroe por 3 turnos.", 10, TargetType.SingleEnemy, SkillKind.Support, 0, SkillAnimation.Cast, SkillVisual.Dark, dark,
                mods: new[] { Mod(Stat.Attack, 0.75f, 3), Mod(Stat.Defense, 0.75f, 3) }));
            Add(Skill("Curación Oscura", "Cura a un aliado monstruo.", 14, TargetType.SingleAlly, SkillKind.Heal, 9, SkillAnimation.Cast, SkillVisual.Holy, new Color(0.6f, 1f, 0.4f)));
            return d;
        }

        // =====================================================================================
        // Objetos

        static List<ItemData> CreateItems()
        {
            ItemData Item(string file, string name, string desc, TargetType t, int qty, Color c, System.Action<ItemData> extra)
            {
                return LoadOrCreate<ItemData>(ItemDir + "/" + file + ".asset", it =>
                {
                    it.displayName = name;
                    it.description = desc;
                    it.target = t;
                    it.startingQuantity = qty;
                    it.color = c;
                    it.atbAfterAction = 25f;
                    extra(it);
                });
            }

            return new List<ItemData>
            {
                Item("Pocion", "Poción de Vida", "Recupera 120 PV a un aliado.", TargetType.SingleAlly, 4, new Color(1f, 0.3f, 0.35f), it => it.healHP = 120f),
                Item("Eter", "Éter Arcano", "Recupera 40 PM a un aliado.", TargetType.SingleAlly, 3, new Color(0.3f, 0.55f, 1f), it => it.healMP = 40f),
                Item("Antidoto", "Hierbas Purificadoras", "Cura veneno, quemadura, parálisis y lentitud.", TargetType.SingleAlly, 2, new Color(0.5f, 1f, 0.6f), it => it.curesStatuses = true),
                Item("ElixirFuerza", "Elixir del Toro", "BUFF: ATQ↑ y MAG↑ +40% a un aliado por 3 turnos.", TargetType.SingleAlly, 2, new Color(1f, 0.6f, 0.2f), it =>
                    it.statModifiers = new List<StatModifierData> { Mod(Stat.Attack, 1.4f, 3), Mod(Stat.Magic, 1.4f, 3) }),
                Item("PolvoDebilidad", "Polvo de Debilidad", "DEBUFF: DEF↓ -40% y VEL↓ -30% a un enemigo por 3 turnos.", TargetType.SingleEnemy, 2, new Color(0.7f, 0.4f, 0.9f), it =>
                    it.statModifiers = new List<StatModifierData> { Mod(Stat.Defense, 0.6f, 3), Mod(Stat.Speed, 0.7f, 3) }),
                Item("TonicoVeloz", "Tónico Veloz", "PRISA por 3 turnos y la barra ATB del aliado avanza +40.", TargetType.SingleAlly, 1, new Color(0.4f, 0.9f, 1f), it =>
                {
                    it.statuses = new List<StatusApplication> { St(StatusType.Haste, 1f, 3) };
                    it.targetAtbChange = 40f;
                }),
            };
        }

        static UnitData Unit(string file, string name, string role, float hp, float mp, float atk, float def, float mag, float spd,
            SkillData basic, SkillData[] skills, float skillChance, float focusWeak, string desc)
        {
            return LoadOrCreate<UnitData>(UnitDir + "/" + file + ".asset", u =>
            {
                u.displayName = name;
                u.role = role;
                u.description = desc;
                u.maxHP = hp;
                u.maxMP = mp;
                u.attack = atk;
                u.defense = def;
                u.magic = mag;
                u.speed = spd;
                u.basicAttack = basic;
                u.skills = skills.ToList();
                u.skillUseChance = skillChance;
                u.focusWeakestChance = focusWeak;
            });
        }

        // =====================================================================================
        // Materiales y luz

        static void CreateMaterials()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(tmp);
            mats = new Dictionary<string, Material>();

            void M(string n, Color c, float smooth = 0.15f, float metal = 0f)
            {
                string path = MatDir + "/" + n + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, path); }
                else if (m.shader != baseMat.shader) m.shader = baseMat.shader;
                m.color = c;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
                EditorUtility.SetDirty(m);
                mats[n] = m;
            }

            M("Pasto", new Color(0.28f, 0.42f, 0.2f));
            M("PiedraArena", new Color(0.55f, 0.52f, 0.48f));
            M("PiedraClara", new Color(0.7f, 0.66f, 0.6f));
            M("Muralla", new Color(0.6f, 0.56f, 0.5f));
            M("MurallaOscura", new Color(0.42f, 0.4f, 0.38f));
            M("Tejado", new Color(0.35f, 0.18f, 0.45f));
            M("TejadoRojo", new Color(0.6f, 0.15f, 0.12f));
            M("Madera", new Color(0.4f, 0.26f, 0.14f));
            M("MaderaOscura", new Color(0.25f, 0.16f, 0.08f));
            M("Estandarte", new Color(0.65f, 0.1f, 0.12f));
            M("Oro", new Color(1f, 0.78f, 0.25f), 0.7f, 0.8f);
            M("Pino", new Color(0.12f, 0.3f, 0.16f));
            M("Roca", new Color(0.45f, 0.44f, 0.42f));
            M("Llama", new Color(1f, 0.6f, 0.15f));
            M("Acero", new Color(0.72f, 0.75f, 0.8f), 0.7f, 0.8f);
            M("AceroOscuro", new Color(0.35f, 0.37f, 0.42f), 0.6f, 0.7f);
            M("AzulReal", new Color(0.15f, 0.25f, 0.6f));
            M("Capa", new Color(0.7f, 0.12f, 0.12f));
            M("Piel", new Color(0.95f, 0.78f, 0.65f));
            M("Negro", new Color(0.05f, 0.05f, 0.06f));
            M("Blanco", new Color(0.95f, 0.95f, 0.92f));
            M("TunicaMaga", new Color(0.4f, 0.18f, 0.6f));
            M("TunicaMagaClara", new Color(0.55f, 0.3f, 0.75f));
            M("Pelo", new Color(0.9f, 0.45f, 0.15f));
            M("Cristal", new Color(0.4f, 0.9f, 1f), 0.9f);
            M("TunicaBarda", new Color(0.2f, 0.55f, 0.35f));
            M("Cuero", new Color(0.45f, 0.3f, 0.18f));
            M("PeloRubio", new Color(0.95f, 0.85f, 0.45f));
            M("Pluma", new Color(0.9f, 0.2f, 0.2f));
            M("PielGoblin", new Color(0.45f, 0.65f, 0.25f));
            M("PielOrco", new Color(0.35f, 0.45f, 0.3f));
            M("Harapos", new Color(0.45f, 0.35f, 0.25f));
            M("CueroOscuro", new Color(0.25f, 0.18f, 0.12f));
            M("Colmillo", new Color(0.95f, 0.92f, 0.8f));
            M("OjoRojo", new Color(1f, 0.15f, 0.1f));
            M("CapuchaChaman", new Color(0.35f, 0.12f, 0.4f));
            M("Hueso", new Color(0.9f, 0.88f, 0.78f));
        }

        static Light SetupLighting()
        {
            var sunGo = new GameObject("Sol del atardecer");
            var l = sunGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.3f;
            l.color = new Color(1f, 0.78f, 0.55f);
            l.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(28f, -55f, 0f);
            RenderSettings.sun = l;

            var sky = Shader.Find("Skybox/Procedural");
            if (sky != null)
            {
                string p = MatDir + "/CieloAtardecer.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(sky); AssetDatabase.CreateAsset(m, p); }
                if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.75f, 0.45f, 0.6f));
                if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", new Color(0.25f, 0.2f, 0.22f));
                if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1.8f);
                if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.2f);
                EditorUtility.SetDirty(m);
                RenderSettings.skybox = m;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.45f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.42f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.18f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.55f, 0.55f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 90f;
            return l;
        }

        // =====================================================================================
        // Helpers

        static GameObject P(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, string mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mats[mat];
            Object.DestroyImmediate(go.GetComponent<Collider>()); // el combate no usa física
            return go;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        // =====================================================================================
        // Escenario: patio del castillo al atardecer

        static void BuildEnvironment()
        {
            P(PrimitiveType.Cube, "Suelo", env, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), "Pasto");
            P(PrimitiveType.Cylinder, "Arena de piedra", env, new Vector3(0f, 0.01f, 0.5f), new Vector3(20f, 0.02f, 16f), "PiedraArena");
            P(PrimitiveType.Cylinder, "Círculo central", env, new Vector3(0f, 0.025f, 0.5f), new Vector3(6f, 0.02f, 6f), "PiedraClara");
            P(PrimitiveType.Cylinder, "Emblema", env, new Vector3(0f, 0.035f, 0.5f), new Vector3(2.2f, 0.02f, 2.2f), "Oro");

            // Muralla del castillo
            P(PrimitiveType.Cube, "Muralla", env, new Vector3(0f, 3.5f, 14f), new Vector3(44f, 7f, 2f), "Muralla");
            for (float x = -21f; x <= 21.1f; x += 2.4f)
                P(PrimitiveType.Cube, "Almena", env, new Vector3(x, 7.5f, 14f), new Vector3(1.2f, 1f, 2f), "Muralla");
            P(PrimitiveType.Cube, "Portón", env, new Vector3(0f, 2.4f, 12.95f), new Vector3(4f, 4.8f, 0.2f), "MaderaOscura");
            P(PrimitiveType.Cube, "Arco", env, new Vector3(0f, 5.1f, 12.9f), new Vector3(5f, 0.6f, 0.3f), "MurallaOscura");
            for (int i = -2; i <= 2; i++)
                P(PrimitiveType.Cube, "Herraje", env, new Vector3(i * 0.8f, 2.4f, 12.83f), new Vector3(0.08f, 4.6f, 0.05f), "AceroOscuro");

            foreach (float x in new[] { -13f, 13f })
            {
                P(PrimitiveType.Cylinder, "Torre", env, new Vector3(x, 6f, 14f), new Vector3(4.5f, 6f, 4.5f), "Muralla");
                P(PrimitiveType.Cylinder, "Corona torre", env, new Vector3(x, 12.3f, 14f), new Vector3(5.2f, 0.4f, 5.2f), "MurallaOscura");
                for (int k = 0; k < 5; k++)
                {
                    float s = 5f - k * 1f;
                    P(PrimitiveType.Cylinder, "Tejado", env, new Vector3(x, 12.9f + k * 0.8f, 14f), new Vector3(s, 0.4f, s), k % 2 == 0 ? "Tejado" : "TejadoRojo");
                }
                P(PrimitiveType.Cube, "Bandera", env, new Vector3(x + 0.6f, 17.6f, 14f), new Vector3(1.2f, 0.7f, 0.05f), "Estandarte");
                P(PrimitiveType.Cylinder, "Mástil", env, new Vector3(x, 17.3f, 14f), new Vector3(0.08f, 1.2f, 0.08f), "Madera");
            }

            foreach (float x in new[] { -6f, 6f, -18f, 18f })
            {
                P(PrimitiveType.Cube, "Estandarte", env, new Vector3(x, 4.3f, 12.92f), new Vector3(1.6f, 3.4f, 0.06f), "Estandarte");
                P(PrimitiveType.Cube, "Escudo heráldico", env, new Vector3(x, 4.8f, 12.86f), new Vector3(0.8f, 0.9f, 0.04f), "Oro");
                P(PrimitiveType.Cube, "Barra estandarte", env, new Vector3(x, 6.05f, 12.9f), new Vector3(1.9f, 0.1f, 0.1f), "Madera");
            }

            // Antorchas alrededor de la arena
            foreach (var p in new[] { new Vector3(-8.5f, 0f, -4f), new Vector3(8.5f, 0f, -4f), new Vector3(-8.5f, 0f, 6f), new Vector3(8.5f, 0f, 6f) })
            {
                P(PrimitiveType.Cylinder, "Poste antorcha", env, p + Vector3.up * 1.1f, new Vector3(0.15f, 1.1f, 0.15f), "Madera");
                P(PrimitiveType.Cylinder, "Brasero", env, p + Vector3.up * 2.25f, new Vector3(0.5f, 0.12f, 0.5f), "AceroOscuro");
                var flame = P(PrimitiveType.Sphere, "Llama", env, p + Vector3.up * 2.5f, new Vector3(0.35f, 0.5f, 0.35f), "Llama");
                var lgo = new GameObject("Luz antorcha");
                lgo.transform.SetParent(flame.transform, false);
                lgo.transform.localPosition = Vector3.up * 0.5f;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.6f, 0.25f);
                l.range = 9f;
                l.intensity = 2.2f;
                var fl = flame.AddComponent<TorchFlicker>();
                fl.torchLight = l;
                fl.baseIntensity = 2.2f;
            }

            // Pinos y rocas
            foreach (var p in new[] { new Vector3(-17f, 0f, 5f), new Vector3(-20f, 0f, -3f), new Vector3(-15f, 0f, -11f), new Vector3(18f, 0f, 6f), new Vector3(20f, 0f, -4f), new Vector3(16f, 0f, -12f), new Vector3(-24f, 0f, 9f), new Vector3(25f, 0f, 9f) })
            {
                P(PrimitiveType.Cylinder, "Tronco", env, p + Vector3.up * 1f, new Vector3(0.5f, 1f, 0.5f), "Madera");
                for (int k = 0; k < 4; k++)
                {
                    float s = 3.4f - k * 0.75f;
                    P(PrimitiveType.Cylinder, "Copa pino", env, p + Vector3.up * (2.2f + k * 1.1f), new Vector3(s, 0.6f, s), "Pino");
                }
            }
            foreach (var p in new[] { new Vector3(-10f, 0f, -7f), new Vector3(11f, 0f, -8f), new Vector3(-11f, 0f, 9f), new Vector3(10f, 0f, 10f), new Vector3(-6f, 0f, -8.5f) })
                P(PrimitiveType.Sphere, "Roca", env, p + Vector3.up * 0.3f, new Vector3(1.4f, 0.8f, 1.1f), "Roca", new Vector3(0f, p.x * 13f, 0f));

            // Barriles y cajas decorativas
            P(PrimitiveType.Cylinder, "Barril", env, new Vector3(-9.5f, 0.6f, 2f), new Vector3(0.9f, 0.6f, 0.9f), "Madera");
            P(PrimitiveType.Cylinder, "Barril", env, new Vector3(-10.4f, 0.6f, 2.8f), new Vector3(0.9f, 0.6f, 0.9f), "Madera");
            P(PrimitiveType.Cube, "Caja", env, new Vector3(9.8f, 0.5f, 1.5f), Vector3.one, "Madera", new Vector3(0f, 20f, 0f));
            P(PrimitiveType.Cube, "Caja", env, new Vector3(10.2f, 1.4f, 1.6f), Vector3.one * 0.8f, "Madera", new Vector3(0f, -10f, 0f));
        }

        // =====================================================================================
        // Personajes (modelos 3D hechos con primitivas)

        class Body
        {
            public Transform root, model, rightArm, leftArm, castPoint;
            public UnitView view;
        }

        static Body Humanoid(Transform parent, string name, Vector3 pos, bool facingRight, float scale,
            string skin, string torso, string legs, string arms)
        {
            var b = new Body();
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0f, facingRight ? 90f : -90f, 0f);
            b.root = root;
            b.model = Pivot("Modelo", root, Vector3.zero);
            b.model.localScale = Vector3.one * scale;
            var m = b.model;

            P(PrimitiveType.Capsule, "Pierna izq", m, new Vector3(-0.17f, 0.45f, 0f), new Vector3(0.22f, 0.45f, 0.22f), legs);
            P(PrimitiveType.Capsule, "Pierna der", m, new Vector3(0.17f, 0.45f, 0f), new Vector3(0.22f, 0.45f, 0.22f), legs);
            P(PrimitiveType.Capsule, "Torso", m, new Vector3(0f, 1.2f, 0f), new Vector3(0.64f, 0.5f, 0.44f), torso);
            P(PrimitiveType.Sphere, "Cabeza", m, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.42f, skin);
            P(PrimitiveType.Sphere, "Ojo", m, new Vector3(0.08f, 1.88f, 0.18f), Vector3.one * 0.06f, "Negro");
            P(PrimitiveType.Sphere, "Ojo", m, new Vector3(-0.08f, 1.88f, 0.18f), Vector3.one * 0.06f, "Negro");

            b.rightArm = Pivot("Brazo derecho", m, new Vector3(0.42f, 1.52f, 0f));
            P(PrimitiveType.Capsule, "Brazo", b.rightArm, new Vector3(0f, -0.32f, 0f), new Vector3(0.17f, 0.32f, 0.17f), arms);
            P(PrimitiveType.Sphere, "Mano", b.rightArm, new Vector3(0f, -0.66f, 0f), Vector3.one * 0.15f, skin);
            b.leftArm = Pivot("Brazo izquierdo", m, new Vector3(-0.42f, 1.52f, 0f));
            P(PrimitiveType.Capsule, "Brazo", b.leftArm, new Vector3(0f, -0.32f, 0f), new Vector3(0.17f, 0.32f, 0.17f), arms);
            P(PrimitiveType.Sphere, "Mano", b.leftArm, new Vector3(0f, -0.66f, 0f), Vector3.one * 0.15f, skin);
            return b;
        }

        static BattleUnit Finish(Body b, UnitData data, Team team, float scale)
        {
            var unit = b.root.gameObject.AddComponent<BattleUnit>();
            unit.data = data;
            unit.team = team;
            var view = b.root.gameObject.AddComponent<UnitView>();
            view.model = b.model;
            view.rightArm = b.rightArm;
            view.leftArm = b.leftArm;
            view.castPoint = b.castPoint;
            view.height = 2.1f * scale;
            return unit;
        }

        static BattleUnit BuildAldric(Transform parent, UnitData data, Vector3 pos)
        {
            var b = Humanoid(parent, "Sir Aldric (Tanque)", pos, true, 1.05f, "Piel", "Acero", "AceroOscuro", "Acero");
            var m = b.model;
            P(PrimitiveType.Cylinder, "Yelmo", m, new Vector3(0f, 1.95f, 0f), new Vector3(0.47f, 0.2f, 0.47f), "Acero");
            P(PrimitiveType.Cube, "Visera", m, new Vector3(0f, 1.87f, 0.2f), new Vector3(0.36f, 0.05f, 0.06f), "AceroOscuro");
            P(PrimitiveType.Capsule, "Penacho", m, new Vector3(0f, 2.22f, -0.05f), new Vector3(0.08f, 0.18f, 0.25f), "Capa", new Vector3(-20f, 0f, 0f));
            P(PrimitiveType.Sphere, "Hombrera", m, new Vector3(0.38f, 1.55f, 0f), Vector3.one * 0.3f, "AceroOscuro");
            P(PrimitiveType.Sphere, "Hombrera", m, new Vector3(-0.38f, 1.55f, 0f), Vector3.one * 0.3f, "AceroOscuro");
            P(PrimitiveType.Cube, "Capa", m, new Vector3(0f, 1.05f, -0.27f), new Vector3(0.62f, 1.15f, 0.05f), "Capa", new Vector3(-6f, 0f, 0f));
            P(PrimitiveType.Cylinder, "Cinturón", m, new Vector3(0f, 0.92f, 0f), new Vector3(0.66f, 0.04f, 0.47f), "Cuero");
            P(PrimitiveType.Cube, "Emblema", m, new Vector3(0f, 1.3f, 0.23f), new Vector3(0.18f, 0.22f, 0.02f), "Oro");
            // Espada
            P(PrimitiveType.Cube, "Hoja", b.rightArm, new Vector3(0f, -0.66f, 0.55f), new Vector3(0.06f, 0.1f, 0.95f), "Acero");
            P(PrimitiveType.Cube, "Guarda", b.rightArm, new Vector3(0f, -0.66f, 0.07f), new Vector3(0.3f, 0.06f, 0.06f), "Oro");
            // Escudo
            P(PrimitiveType.Cube, "Escudo", b.leftArm, new Vector3(-0.08f, -0.5f, 0.28f), new Vector3(0.62f, 0.78f, 0.08f), "AzulReal");
            P(PrimitiveType.Cube, "Borde escudo", b.leftArm, new Vector3(-0.08f, -0.5f, 0.31f), new Vector3(0.12f, 0.7f, 0.03f), "Oro");
            P(PrimitiveType.Sphere, "Umbo", b.leftArm, new Vector3(-0.08f, -0.45f, 0.34f), Vector3.one * 0.14f, "Oro");
            b.castPoint = Pivot("PuntoMagia", b.leftArm, new Vector3(-0.08f, -0.5f, 0.4f));
            return Finish(b, data, Team.Player, 1.05f);
        }

        static BattleUnit BuildElara(Transform parent, UnitData data, Vector3 pos)
        {
            var b = Humanoid(parent, "Elara (Maga)", pos, true, 0.95f, "Piel", "TunicaMaga", "TunicaMaga", "TunicaMagaClara");
            var m = b.model;
            P(PrimitiveType.Cylinder, "Falda túnica", m, new Vector3(0f, 0.5f, 0f), new Vector3(0.72f, 0.5f, 0.62f), "TunicaMaga");
            P(PrimitiveType.Cylinder, "Faja", m, new Vector3(0f, 0.98f, 0f), new Vector3(0.62f, 0.05f, 0.47f), "Oro");
            P(PrimitiveType.Sphere, "Pelo", m, new Vector3(0f, 1.82f, -0.1f), new Vector3(0.46f, 0.5f, 0.4f), "Pelo");
            P(PrimitiveType.Capsule, "Trenza", m, new Vector3(0f, 1.5f, -0.22f), new Vector3(0.12f, 0.3f, 0.12f), "Pelo");
            P(PrimitiveType.Cylinder, "Ala sombrero", m, new Vector3(0f, 2.02f, 0f), new Vector3(0.85f, 0.025f, 0.85f), "TunicaMaga");
            P(PrimitiveType.Cylinder, "Sombrero 1", m, new Vector3(0f, 2.15f, 0f), new Vector3(0.44f, 0.12f, 0.44f), "TunicaMaga");
            P(PrimitiveType.Cylinder, "Sombrero 2", m, new Vector3(0f, 2.38f, -0.03f), new Vector3(0.3f, 0.12f, 0.3f), "TunicaMaga", new Vector3(-8f, 0f, 0f));
            P(PrimitiveType.Cylinder, "Sombrero 3", m, new Vector3(0f, 2.58f, -0.1f), new Vector3(0.16f, 0.12f, 0.16f), "TunicaMaga", new Vector3(-22f, 0f, 0f));
            P(PrimitiveType.Cylinder, "Cinta sombrero", m, new Vector3(0f, 2.08f, 0f), new Vector3(0.46f, 0.03f, 0.46f), "Oro");
            // Bastón con cristal
            P(PrimitiveType.Cylinder, "Bastón", b.rightArm, new Vector3(0f, -0.51f, 0.26f), new Vector3(0.07f, 0.7f, 0.07f), "Madera", new Vector3(60f, 0f, 0f));
            P(PrimitiveType.Sphere, "Cristal", b.rightArm, new Vector3(0f, -0.135f, 0.91f), Vector3.one * 0.22f, "Cristal");
            P(PrimitiveType.Cylinder, "Garra", b.rightArm, new Vector3(0f, -0.19f, 0.82f), new Vector3(0.16f, 0.05f, 0.16f), "Oro", new Vector3(60f, 0f, 0f));
            b.castPoint = Pivot("PuntoMagia", b.rightArm, new Vector3(0f, -0.135f, 0.91f));
            var lgo = new GameObject("Brillo cristal");
            lgo.transform.SetParent(b.castPoint, false);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.4f, 0.9f, 1f);
            l.range = 2.5f;
            l.intensity = 1.5f;
            return Finish(b, data, Team.Player, 0.95f);
        }

        static BattleUnit BuildIsolde(Transform parent, UnitData data, Vector3 pos)
        {
            var b = Humanoid(parent, "Isolde (Juglaresa)", pos, true, 0.95f, "Piel", "TunicaBarda", "Cuero", "TunicaBarda");
            var m = b.model;
            P(PrimitiveType.Cylinder, "Cinturón", m, new Vector3(0f, 0.92f, 0f), new Vector3(0.66f, 0.05f, 0.47f), "Cuero");
            P(PrimitiveType.Sphere, "Pelo", m, new Vector3(0f, 1.8f, -0.08f), new Vector3(0.46f, 0.48f, 0.42f), "PeloRubio");
            P(PrimitiveType.Sphere, "Gorro", m, new Vector3(0f, 2.02f, -0.02f), new Vector3(0.48f, 0.2f, 0.5f), "TunicaBarda");
            P(PrimitiveType.Capsule, "Pluma", m, new Vector3(0.12f, 2.25f, -0.15f), new Vector3(0.06f, 0.25f, 0.12f), "Pluma", new Vector3(-35f, 0f, -15f));
            P(PrimitiveType.Cube, "Capa corta", m, new Vector3(0f, 1.3f, -0.25f), new Vector3(0.6f, 0.6f, 0.05f), "Pluma", new Vector3(-8f, 0f, 0f));
            // Laúd en la espalda
            P(PrimitiveType.Sphere, "Laúd", m, new Vector3(0.05f, 1.15f, -0.38f), new Vector3(0.45f, 0.55f, 0.16f), "Madera", new Vector3(0f, 0f, 25f));
            P(PrimitiveType.Cube, "Mástil laúd", m, new Vector3(-0.15f, 1.6f, -0.38f), new Vector3(0.07f, 0.55f, 0.05f), "MaderaOscura", new Vector3(0f, 0f, 25f));
            // Daga y flauta
            P(PrimitiveType.Cube, "Daga", b.rightArm, new Vector3(0f, -0.66f, 0.22f), new Vector3(0.04f, 0.05f, 0.38f), "Acero");
            P(PrimitiveType.Cylinder, "Flauta", b.leftArm, new Vector3(0f, -0.66f, 0.15f), new Vector3(0.05f, 0.22f, 0.05f), "Oro", new Vector3(90f, 0f, 0f));
            b.castPoint = Pivot("PuntoMagia", b.rightArm, new Vector3(0f, -0.7f, 0.1f));
            return Finish(b, data, Team.Player, 0.95f);
        }

        static BattleUnit BuildGoblin(Transform parent, UnitData data, Vector3 pos)
        {
            var b = Humanoid(parent, "Goblin Saqueador", pos, false, 0.8f, "PielGoblin", "Harapos", "PielGoblin", "PielGoblin");
            var m = b.model;
            P(PrimitiveType.Capsule, "Oreja", m, new Vector3(0.28f, 1.92f, 0f), new Vector3(0.1f, 0.24f, 0.06f), "PielGoblin", new Vector3(0f, 0f, -70f));
            P(PrimitiveType.Capsule, "Oreja", m, new Vector3(-0.28f, 1.92f, 0f), new Vector3(0.1f, 0.24f, 0.06f), "PielGoblin", new Vector3(0f, 0f, 70f));
            P(PrimitiveType.Sphere, "Nariz", m, new Vector3(0f, 1.82f, 0.22f), new Vector3(0.1f, 0.1f, 0.16f), "PielGoblin");
            P(PrimitiveType.Sphere, "Ojo rojo", m, new Vector3(0.08f, 1.9f, 0.19f), Vector3.one * 0.07f, "OjoRojo");
            P(PrimitiveType.Sphere, "Ojo rojo", m, new Vector3(-0.08f, 1.9f, 0.19f), Vector3.one * 0.07f, "OjoRojo");
            P(PrimitiveType.Cylinder, "Cinto", m, new Vector3(0f, 0.92f, 0f), new Vector3(0.66f, 0.06f, 0.47f), "CueroOscuro");
            P(PrimitiveType.Cube, "Daga oxidada", b.rightArm, new Vector3(0f, -0.66f, 0.25f), new Vector3(0.05f, 0.06f, 0.45f), "AceroOscuro");
            b.castPoint = Pivot("PuntoMagia", b.rightArm, new Vector3(0f, -0.66f, 0.4f));
            return Finish(b, data, Team.Enemy, 0.8f);
        }

        static BattleUnit BuildOrc(Transform parent, UnitData data, Vector3 pos)
        {
            var b = Humanoid(parent, "Orco Bruto", pos, false, 1.35f, "PielOrco", "CueroOscuro", "Cuero", "PielOrco");
            var m = b.model;
            P(PrimitiveType.Capsule, "Colmillo", m, new Vector3(0.09f, 1.75f, 0.2f), new Vector3(0.05f, 0.08f, 0.05f), "Colmillo");
            P(PrimitiveType.Capsule, "Colmillo", m, new Vector3(-0.09f, 1.75f, 0.2f), new Vector3(0.05f, 0.08f, 0.05f), "Colmillo");
            P(PrimitiveType.Sphere, "Ojo rojo", m, new Vector3(0.08f, 1.9f, 0.19f), Vector3.one * 0.06f, "OjoRojo");
            P(PrimitiveType.Sphere, "Ojo rojo", m, new Vector3(-0.08f, 1.9f, 0.19f), Vector3.one * 0.06f, "OjoRojo");
            P(PrimitiveType.Sphere, "Hombrera", m, new Vector3(0.4f, 1.58f, 0f), new Vector3(0.35f, 0.25f, 0.35f), "AceroOscuro");
            P(PrimitiveType.Sphere, "Hombrera", m, new Vector3(-0.4f, 1.58f, 0f), new Vector3(0.35f, 0.25f, 0.35f), "AceroOscuro");
            P(PrimitiveType.Cylinder, "Cinto", m, new Vector3(0f, 0.92f, 0f), new Vector3(0.68f, 0.07f, 0.49f), "Hueso");
            P(PrimitiveType.Cube, "Taparrabos", m, new Vector3(0f, 0.7f, 0.22f), new Vector3(0.3f, 0.4f, 0.04f), "Harapos");
            // Garrote con pinchos
            P(PrimitiveType.Cylinder, "Garrote", b.rightArm, new Vector3(0f, -0.66f, 0.45f), new Vector3(0.18f, 0.45f, 0.18f), "Madera", new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                P(PrimitiveType.Sphere, "Pincho", b.rightArm, new Vector3(Mathf.Cos(i * 1.57f) * 0.1f, -0.66f + Mathf.Sin(i * 1.57f) * 0.1f, 0.75f), Vector3.one * 0.08f, "AceroOscuro");
            b.castPoint = Pivot("PuntoMagia", b.rightArm, new Vector3(0f, -0.66f, 0.8f));
            return Finish(b, data, Team.Enemy, 1.35f);
        }

        static BattleUnit BuildShaman(Transform parent, UnitData data, Vector3 pos)
        {
            var b = Humanoid(parent, "Chamán Goblin", pos, false, 0.85f, "PielGoblin", "CapuchaChaman", "CapuchaChaman", "PielGoblin");
            var m = b.model;
            P(PrimitiveType.Sphere, "Capucha", m, new Vector3(0f, 1.92f, -0.05f), new Vector3(0.5f, 0.52f, 0.5f), "CapuchaChaman");
            P(PrimitiveType.Cylinder, "Túnica", m, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 0.5f, 0.6f), "CapuchaChaman");
            P(PrimitiveType.Sphere, "Ojo rojo", m, new Vector3(0.08f, 1.88f, 0.2f), Vector3.one * 0.08f, "OjoRojo");
            P(PrimitiveType.Sphere, "Ojo rojo", m, new Vector3(-0.08f, 1.88f, 0.2f), Vector3.one * 0.08f, "OjoRojo");
            P(PrimitiveType.Sphere, "Collar", m, new Vector3(0f, 1.55f, 0.2f), new Vector3(0.12f, 0.12f, 0.06f), "Hueso");
            // Bastón con calavera
            P(PrimitiveType.Cylinder, "Bastón", b.rightArm, new Vector3(0f, -0.51f, 0.26f), new Vector3(0.07f, 0.7f, 0.07f), "MaderaOscura", new Vector3(60f, 0f, 0f));
            P(PrimitiveType.Sphere, "Calavera", b.rightArm, new Vector3(0f, -0.12f, 0.93f), new Vector3(0.24f, 0.22f, 0.24f), "Hueso");
            P(PrimitiveType.Sphere, "Gema", b.rightArm, new Vector3(0f, -0.12f, 1.05f), Vector3.one * 0.08f, "OjoRojo");
            b.castPoint = Pivot("PuntoMagia", b.rightArm, new Vector3(0f, -0.12f, 0.93f));
            return Finish(b, data, Team.Enemy, 0.85f);
        }
    }
}
