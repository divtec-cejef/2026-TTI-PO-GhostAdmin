# GhostAdmin — Documentation technique

> Documentation de l'archive `GhostAdmin.zip` (snapshot du projet Unity au 16.09.2026).
> Dépôt : `divtec-cejef/2026-TTI-PO-GhostAdmin` — branche `develop`.

---

## 1. Ce que contient l'archive

`GhostAdmin.zip` — **1.1 Go**, **65 064 entrées**. C'est une copie brute du dossier de
travail Unity, caches inclus.

| Dossier | Entrées | Utile ? | Rôle |
|---|---:|---|---|
| `GhostAdmin/Library/` | 60 843 | ❌ | Cache d'import Unity — régénéré automatiquement |
| `GhostAdmin/Temp/` | 1 297 | ❌ | Fichiers temporaires d'une session Editor ouverte |
| `GhostAdmin/Build/` | 226 | ❌ | Build Windows compilé (`GhostAdmin.exe`) |
| `GhostAdmin/Assets/` | **2 637** | ✅ | **Le projet réel** : scripts, scènes, prefabs, sprites |
| `GhostAdmin/ProjectSettings/` | 29 | ✅ | Configuration du projet (layers, physique, URP…) |
| `GhostAdmin/Packages/` | 3 | ✅ | `manifest.json` — dépendances du projet |
| `GhostAdmin/UserSettings/`, `Logs/` | 9 | ❌ | Préférences locales et logs |
| `*.csproj`, `*.sln`, `UpgradeLog.htm` | 15 | ❌ | Générés par Unity / Visual Studio |

> ⚠️ **~94 % de l'archive est régénérable.** `Library/`, `Temp/`, `Build/`, `Logs/`,
> `UserSettings/`, `*.csproj` et `*.sln` sont d'ailleurs déjà listés dans le
> `.gitignore` du dépôt. Une archive limitée à `Assets/` + `ProjectSettings/` +
> `Packages/` ferait quelques dizaines de Mo au lieu de 1.1 Go, et Unity
> reconstruirait le reste au premier lancement.

---

## 2. Le projet en bref

**GhostAdmin** est un jeu **2D multijoueur en réseau** développé sous Unity, dans
lequel plusieurs joueurs incarnent des personnages qui se déplacent dans un décor
de bureau (chaises, bureaux, sol en tilemap).

| Élément | Valeur |
|---|---|
| Moteur | Unity **6000.5.9f1** (Unity 6) |
| Rendu | Universal Render Pipeline **17.6.0**, pipeline 2D (`Renderer2D`) |
| Réseau | **Mirror** (inclus dans `Assets/Mirror/`) |
| Transport | **kcp2k** (UDP) — port **7777** |
| Caméra | **Cinemachine 3.1.7** |
| Entrées | **Input System 1.20.0** (nouveau système) |
| Nom produit | `GhostAdmin` — version `1.0` |
| Plateforme de build | Windows (`Assets/Settings/Build Profiles/Windows.asset`) |

---

## 3. Structure de `Assets/`

```
Assets/
├── Scripts/
│   ├── Player.cs          ← logique joueur + synchronisation réseau
│   └── CameraFollow.cs    ← suivi de caméra manuel (NON UTILISÉ, voir §8)
├── Scenes/
│   ├── Main.unity         ← scène de jeu principale
│   └── SampleScene.unity  ← scène par défaut d'Unity
├── Prefabs/
│   ├── player1.prefab     ← prefab joueur utilisé par le NetworkManager
│   └── PlayerTest.prefab  ← prefab de test (NON RÉFÉRENCÉ)
├── Animations/
│   ├── player1.controller ← Animator, paramètre float "Speed"
│   ├── AnimationPerso.anim (idle, vitesse 0.17)
│   ├── MarchePerso.anim
│   └── Course.anim         (vitesse 0.35)
├── Sprite/                ← Bureau.png, Chaise.png, character_20x20_green.png
├── Pieces/                ← pieceTest.png découpé en 6 tuiles
├── Palette/               ← Palette.prefab + SolBleu.png (palette de Tilemap)
├── Settings/              ← URP, Renderer2D, InputSystem_Actions, Build Profiles
├── Resources/             ← (vide)
└── Mirror/                ← framework Mirror complet (Core, Components, Transports…)
```

---

## 4. Architecture réseau

Le modèle est celui de Mirror : un joueur héberge la partie (*Host*), ou un serveur
dédié tourne, et les autres se connectent en *Client*.

### 4.1 `NetworkManager` (dans `Main.unity`)

| Paramètre | Valeur | Commentaire |
|---|---|---|
| `networkAddress` | `localhost` | À changer pour jouer en LAN / en ligne |
| `transport` | `kcp2k.KcpTransport` | UDP fiable, port **7777**, `DualMode` (IPv4+IPv6) |
| `maxConnections` | 100 | |
| `sendRate` | 60 Hz | Fréquence d'envoi des snapshots |
| `playerPrefab` | `player1.prefab` | |
| `autoCreatePlayer` | ✅ | Le joueur apparaît dès la connexion |
| `playerSpawnMethod` | `RoundRobin` | Alterne entre les points de spawn |
| `dontDestroyOnLoad` | ✅ | |
| `exceptionsDisconnect` | ✅ | Une exception réseau déconnecte le client fautif |

Un **`NetworkManagerHUD`** est présent sur le même GameObject : c'est lui qui
affiche en jeu les boutons *Host / Client / Server*.

### 4.2 Points d'apparition

Deux enfants du `NetworkManager` portent un `NetworkStartPosition` :

- `SpawnPointA` → position locale `(-0.71, 1.39)`
- `SpawnPointB` → position locale `(2.38, -1.08)`

Combinés au mode `RoundRobin`, ils font apparaître les joueurs alternativement
à l'un puis à l'autre.

### 4.3 Répartition de la synchronisation

| Donnée | Mécanisme | Direction |
|---|---|---|
| Position | `NetworkTransformReliable` (`syncPosition` on, `syncRotation` off) | Client → Serveur (`syncDirection: ClientToServer`) |
| Orientation du sprite (`facingLeft`) | `[SyncVar]` alimentée par un `[Command]` | Serveur → tous |
| Vitesse d'animation (`animSpeed`) | `[SyncVar]` alimentée par un `[Command]` | Serveur → tous |

---

## 5. `Scripts/Player.cs`

Composant `NetworkBehaviour` attaché à `player1.prefab`. C'est le cœur du jeu.

### Champs

| Champ | Type | Rôle |
|---|---|---|
| `rb` | `Rigidbody2D` | Assigné dans l'inspecteur |
| `speed` | `float` = 3 | Vitesse de déplacement (unités/s) |
| `grondLayer` | `LayerMask` | **Déclaré mais jamais utilisé** (voir §8) |
| `moveInput` | `Vector2` | Dernière direction lue depuis l'Input System |
| `facingLeft` | `bool` `[SyncVar]` | Hook → `OnFacingLeftChanged` |
| `animSpeed` | `float` `[SyncVar]` | Hook → `OnAnimSpeedChanged` |

### Cycle de vie

```
Awake()
  └─ récupère SpriteRenderer et Animator

OnStartLocalPlayer()            ← uniquement sur MON joueur
  ├─ active le composant PlayerInput (désactivé par défaut dans le prefab)
  └─ accroche la CinemachineCamera de la scène sur ce transform

OnStartClient()                 ← sur tous les clients
  └─ si ce n'est pas mon joueur → Rigidbody2D passe en Kinematic
       (la physique locale ne perturbe pas la position reçue du réseau)

Move(InputAction.CallbackContext)   ← événement Input System
  ├─ ignore si !isLocalPlayer
  ├─ lit la direction, en déduit `left` (flip) et `spd` (magnitude)
  ├─ ApplyVisuals() → applique immédiatement en local (pas de latence perçue)
  └─ CmdSetVisuals() → demande au serveur de propager aux autres

FixedUpdate()
  └─ si isLocalPlayer : rb.linearVelocity = moveInput * speed
```

Le point notable est le **double chemin visuel** : `ApplyVisuals` s'applique tout de
suite chez le joueur local, pendant que `CmdSetVisuals` fait l'aller-retour serveur
pour les autres clients via les hooks de `SyncVar`.

### Entrées

- Asset : `Assets/Settings/InputSystem_Actions.inputactions`
- Action map `Player` (actions disponibles : `Move`, `Look`, `Attack`, `Interact`,
  `Crouch`, `Jump`, `Previous`, `Next`, `Sprint`) — **seule `Move` est câblée**.
- `PlayerInput` est en mode **Invoke Unity Events** ; l'événement de `Move` pointe
  sur `Player.Move`.
- Le composant `PlayerInput` est **désactivé dans le prefab** et activé par code
  dans `OnStartLocalPlayer()` : c'est ce qui empêche un client de piloter les
  personnages des autres.

---

## 6. Caméra

- La scène contient une `CinemachineCamera1` (`CinemachineCamera` +
  `CinemachinePositionComposer`, `CameraDistance: 10`) et la `Main Camera` porte
  un `CinemachineBrain`.
- `Player.OnStartLocalPlayer()` fait `vcam.Target.TrackingTarget = transform`,
  donc **chaque joueur suit son propre personnage**.
- Si aucune `CinemachineCamera` n'est trouvée, un `Debug.LogWarning` est émis.

---

## 7. Physique et layers

- Layers définis : `Default`, `TransparentFX`, `Ignore Raycast`, **`Player`**,
  `Water`, `UI`.
- La **matrice de collision 2D désactive `Player` ↔ `Player`** : les joueurs se
  traversent, ils ne se poussent pas.
- `Rigidbody2D` du joueur : `GravityScale = 0`, `Mass = 1`, type *Dynamic* — jeu en
  vue de dessus, pas de plateforme.
- Collider : `CircleCollider2D`.

---

## 8. Points à corriger / dette technique

Constats relevés à la lecture du code et des assets :

1. **`CameraFollow.cs` est du code mort.** Son GUID
   (`d5763cb89aaa46844b08a7ac706f34a9`) n'apparaît dans aucune scène ni aucun
   prefab. Le suivi de caméra est assuré par Cinemachine. À supprimer, ou à
   documenter comme solution de repli.
2. **`PlayerTest.prefab` n'est référencé nulle part.** Le `NetworkManager` utilise
   `player1.prefab`.
3. **`grondLayer`** dans `Player.cs` : champ public non utilisé, et le nom contient
   une faute de frappe (`ground`). À supprimer ou à implémenter.
4. **`FindObjectOfType<CinemachineCamera>()` est obsolète sous Unity 6.** Remplacer
   par `FindFirstObjectByType<CinemachineCamera>()` — sinon un avertissement de
   compilation à chaque build, et l'API finira par disparaître.
5. **`Player.cs` est encodé en ISO-8859-1**, pas en UTF-8 : l'accent de
   `"Pas de CinemachineCamera dans la scène !"` est cassé. Réenregistrer le fichier
   en UTF-8.
6. **`networkAddress` est figé à `localhost`** dans la scène. Pour jouer en réseau il
   faut soit l'éditer, soit saisir l'IP dans le champ du `NetworkManagerHUD` avant de
   cliquer sur *Client*.
7. **`SampleScene.unity`** est la scène vide par défaut d'Unity — à supprimer si elle
   n'a pas d'usage.
8. **L'archive embarque `Library/`, `Temp/` et `Build/`** alors que le `.gitignore`
   les exclut : d'où les 1.1 Go. Voir §1.

---

## 9. Ouvrir et lancer le projet

### Depuis l'archive

1. Décompresser `GhostAdmin.zip`.
2. Ouvrir **Unity Hub** → *Add project from disk* → sélectionner le dossier
   `GhostAdmin/` (celui qui contient `Assets/`, `Packages/`, `ProjectSettings/`).
3. Utiliser **exactement Unity 6000.5.9f1** ; une autre version déclenchera une
   migration des assets.
4. Ouvrir `Assets/Scenes/Main.unity`.

> Optionnel : supprimer `Library/` et `Temp/` avant d'ouvrir. Le premier lancement
> sera plus long (réimport complet) mais évite les caches incohérents.

### Tester le multijoueur

**En local, deux instances sur la même machine :**

1. Lancer la scène `Main` dans l'Editor → cliquer **Host** dans le HUD.
2. Lancer `Build/GhostAdmin.exe` → cliquer **Client** (adresse `localhost`).

**Sur le réseau :**

1. Sur la machine hôte : **Host** (ou **Server** seul pour un serveur dédié).
2. Sur les autres : saisir l'IP de l'hôte dans le champ du HUD → **Client**.
3. Ouvrir le port **UDP 7777** sur le pare-feu de l'hôte.

### Recompiler un build

`File > Build Profiles` → profil **Windows** → *Build*.

---

## 10. Historique et organisation du dépôt

Le dépôt suit un modèle **Git Flow** : `main` ← `develop` ← branches de
fonctionnalité, fusionnées par pull request.

Branches distantes : `main`, `develop`, `Sprint1`, `CreationJoueur`,
`CreationPieceTest`, `GestionCamera`, `AnimationPerso`, `MultiJoueurADeux`,
`fix/animations`.

Derniers commits (branche `develop`) :

```
c5637b38  Modification animation
2b723fa1  Merge branch 'develop' into AnimationPerso
262942ef  Merge branch 'CreationPieceTest' into develop
8778d2fa  Update Main.unity
0dcd4031  Merge pull request #38 from divtec-cejef/CreationPieceTest
1af033b6  Modification scène
b1e349b6  Ajout nouveau sol
27cad8b4  Ajout chaise + bureau dans la scène
19ced13e  background
40eb4343  tout fonctionne
d01c68b4  La caméra follow les joueurs
a748c6a1  Les collisions entre les joueurs ne sont plus prises en compte
e0f6f0ac  Ajout du jeux en ligne sur un serveur
72b2b1ce  Ajout animation de marche du perso
504d993d  Camera suit le perso + amélioration qualité graphique
```

L'ordre des fonctionnalités livrées se lit clairement : création du joueur → caméra
qui suit → animation de marche → mise en réseau → désactivation des collisions entre
joueurs → décor (sol, bureaux, chaises).

---

*Document généré le 16.09.2026 à partir du contenu de `GhostAdmin.zip`.*
