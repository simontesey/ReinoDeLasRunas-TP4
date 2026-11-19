# ⚔️ REINO DE LAS RUNAS — Prototipo de RPG por turnos con barra ATB (TP4)

## 🎮 Jugar sin Unity (Windows)

1. Descargá el juego compilado: **[ReinoDeLasRunas-Windows.zip](https://github.com/simontesey/ReinoDeLasRunas-TP4/raw/main/Build~/ReinoDeLasRunas-Windows.zip)** (carpeta `Build~` del repositorio).
2. Descomprimí el .zip (clic derecho → *Extraer todo*).
3. Abrí **`ReinoDeLasRunas.exe`**. Si Windows muestra "Windows protegió su PC", elegí *Más información → Ejecutar de todas formas* (pasa con cualquier .exe sin firma digital).

Para cerrar el juego: **Alt + F4**.


> **Cómo usar este repositorio:** contiene la carpeta del juego para Unity. Creá un proyecto **Unity 6 (6000.3) con la plantilla Universal 3D (URP)**, cloná este repositorio dentro de `Assets/ReinoDeLasRunas` (`git clone <url> Assets/ReinoDeLasRunas`) y abrí la escena `Assets/ReinoDeLasRunas/Scenes/CombateCastillo.unity`. La escena, los ScriptableObjects y los materiales ya vienen generados; si hiciera falta regenerarlos: menú **Reino de las Runas → Construir escena de combate**.


Los monstruos asaltan el patio del castillo al atardecer. Tres héroes los enfrentan:

| Héroe | Rol | Estilo |
|---|---|---|
| **Sir Aldric** | Caballero · **Tanque cuerpo a cuerpo** | Mucha vida y defensa. *Provocar* obliga a los monstruos a atacarlo, *Golpe de Escudo* paraliza, *Tajo Devastador* pega muy fuerte pero tarda en recuperarse. |
| **Elara** | **Maga** arcana | Hechizos de fuego, rayo y veneno. Su habilidad especial **Lluvia de Meteoros** es una cinemática que oculta la interfaz. |
| **Isolde** | Juglaresa · **Buffs y debuffs** | Sus canciones suben ATQ/MAG del grupo, dan *Prisa*, curan, bajan DEF/ATQ enemigas y aplican *Lentitud*. |

Enemigos: **Goblin Saqueador** (rápido, envenena), **Orco Bruto** (lento, golpes devastadores, se potencia) y **Chamán Goblin** (cura a sus aliados, quema y maldice).

---

## 1. Cómo abrirlo

1. Proyecto Unity 6 o 2022.3 LTS (Built-in o URP). Copiar `Assets/ReinoDeLasRunas` dentro de `Assets`.
2. Menú **Reino de las Runas → Construir escena de combate**. Se crean los ScriptableObjects (`Assets/ReinoDeLasRunas/Data`), los materiales y la escena `Scenes/CombateCastillo.unity`.
3. **Play** (recomendado: activar *Maximize On Play* en la vista Game).

Todo se genera por código: modelos 3D hechos con primitivas y animaciones procedurales, sonidos sintetizados, interfaz uGUI y efectos de partículas. No hace falta importar nada.

---

## 2. Controles (teclado **o** control)

| Acción | Teclado | Control |
|---|---|---|
| Navegar menús / cambiar objetivo | Flechas o WASD | Cruceta o stick izquierdo |
| Confirmar | Enter / Espacio / Z | **A** (Xbox) / **Cruz** (PlayStation) |
| Volver | Esc / Retroceso / X | **B** / **Círculo** |
| Acelerar las barras ATB (mantener) | Shift / Tab | Gatillo derecho (RT / R2) |
| Reintentar al terminar | Enter | A |

La ayuda de abajo cambia sola según el último dispositivo usado ("[Enter]" ↔ "(A)").

---

## 3. Sistema de combate

- **Barra ATB:** cada unidad llena su barra según su VELOCIDAD. Al llegar a 100 actúa. **Modo espera**: mientras un héroe elige su acción, el tiempo se detiene.
- **Recuperación de las acciones:** después de actuar, la barra queda en el valor `atbAfterAction` de la acción:
  - Acciones fuertes la dejan en **negativo** (se ve en rojo como "RECUPERANDO"): *Tajo Devastador* −55, *Lluvia de Meteoros* −60, *Golpe Aplastante* −40.
  - Acciones rápidas la dejan **adelantada**: *Defender* +30, objetos +25, *Golpe de Escudo* +25, *Toxina Arcana* +20.
  - **Acelerar o atrasar a otros:** *Canción Ligera* (+50 ATB y Prisa a un aliado), *Tónico Veloz* (+40), *Nana Pesada* (−40 ATB y Lentitud a un enemigo).
- **Daño:** `poder × (ATQ o MAG) × 0.25 × 50/(50+DEF)` con variación y críticos. La magia ignora el 40% de la defensa. *Defender* reduce el daño a la mitad.
- **Estados alterados:** Veneno (7% de la vida por turno), Quemadura (5% por turno y ATQ −20%), Parálisis (40% de perder el turno y barra más lenta), Provocar, Prisa, Lentitud y Defensa.
- **Buffs y debuffs:** modificadores temporales de ATQ / DEF / MAG / VEL por turnos (↑ / ↓ en el panel).
- **IA enemiga (`EnemyBrain`):** cura a un aliado herido si puede, se potencia si no lo está, usa habilidades con cierta probabilidad y elige objetivo (respeta *Provocar*; si no, prioriza al más débil o elige al azar).

---

## 4. Cumplimiento de requisitos

### Nota 4
| Requisito | Dónde |
|---|---|
| Turnos secuenciales para héroes y enemigos | `BattleManager` (cola de turnos, `TurnRoutine`) |
| Personajes con modelos 3D y animaciones | Modelos en `BattleSceneBuilder` (caballero con espada, escudo y capa; maga con sombrero y bastón con cristal; juglaresa con laúd; goblin, orco con garrote, chamán con calavera). Animaciones en `UnitView`: respirar, avanzar, golpear, lanzar hechizos, cantar, usar objeto, recibir golpe, defenderse, festejar y caer. |
| Enemigos que eligen objetivo y atacan | `EnemyBrain` |
| Ataque básico + habilidades que consumen maná | `UnitData.basicAttack` y `skills` (`SkillData.mpCost`) |
| Vida y maná | `BattleUnit` (PV / PM) |
| Consumibles de vida y maná | Poción de Vida, Éter Arcano (`ItemData`) |
| UI con vida, maná, participantes y acciones | `BattleUI`: panel del grupo, panel de enemigos y menú de comandos con descripción |
| Escena de combate funcional | `CombateCastillo.unity` (3 héroes contra 3 monstruos) |
| Victoria y derrota | `BattleManager.EndBattle` + pantalla final con reintento |

### Nota 7
| Requisito | Dónde |
|---|---|
| Barra ATB según velocidad (aliados y enemigos) | `BattleManager.Update`, `BattleUnit.Speed` |
| Costos de recuperación / acelerar a otros | `atbAfterAction` y `targetAtbChange` en `SkillData` / `ItemData` |
| Habilidades diferenciadas | 14 habilidades de héroes + 8 de enemigos, con distintos costos, objetivos y recuperación |
| Objetos de buff y debuff | Elixir del Toro (ATQ↑ MAG↑), Polvo de Debilidad (DEF↓ VEL↓ al enemigo), Tónico Veloz, Hierbas Purificadoras |
| ScriptableObjects | `UnitData`, `SkillData`, `ItemData` (carpeta `Data/Unidades`, `Data/Habilidades`, `Data/Objetos`) |
| Feedback visual y sonoro | Números de daño/curación, críticos, destellos, temblor de cámara, partículas por elemento, proyectiles con estela y luz, rayos, anillos, flechas de buff/debuff y ~33 sonidos sintetizados |

### Nota 10
| Requisito | Dónde |
|---|---|
| Estados alterados | Veneno, Quemadura, Parálisis (+ Prisa, Lentitud, Provocar), con partículas sobre el personaje y siglas en la UI |
| Habilidad especial que oculta la UI | **Lluvia de Meteoros** (`BattleManager.UltimateRoutine`): se oculta la interfaz, primer plano de Elara levitando sobre un círculo mágico, el cielo se oscurece, plano general y meteoros con explosiones, luces, temblor y sonido; luego todo vuelve a la normalidad |
| Soporte completo de teclado y control | `RInput` (Input System + respaldo del Input Manager clásico): navegación, selección de objetivos, confirmar, volver, acelerar y reintentar |
| Scripts por responsabilidad | Datos / Combate / IA / Visual / UI / FX / Entrada separados |

---

## 5. Estructura

```
Assets/ReinoDeLasRunas/
├── Editor/BattleSceneBuilder.cs     Genera datos, materiales, escenario y personajes
└── Scripts/
    ├── Core/RInput.cs                Teclado + control
    ├── Data/                         UnitData, SkillData, ItemData, StatModifierData, BattleEnums
    ├── Battle/                       BattleManager (ATB, ejecución, efectos), BattleUnit, BattleCommand, EnemyBrain
    ├── Visual/                       UnitView (animaciones), BattleCamera
    ├── UI/BattleUI.cs                Interfaz navegable
    └── FX/                           Sfx, Vfx, FloatingText, TorchFlicker, etc.
```
