# Guía de Implementación — Unnamed Multiplayer Racing Game

> Guía de referencia de TODO lo implementado y CÓMO se hizo. Pensada para responder
> preguntas del tipo "¿cómo se implementó X?". Última actualización: 2026-06-17.

## Stack
- **Unity 2021.3.16f1**
- **Photon PUN 2** (multiplayer realtime) + Photon Chat
- **ParrelSync** (testing multi-instancia local)
- **Newtonsoft.Json** (leaderboard REST)
- Patrón de física arcade: **sphere-rigidbody + visual container**
- Persistencia local: **BinaryFormatter + XOR** en `playerdata.bin`

## Mapa de carpetas (`Assets/Scripts/`)
- **Car/** — física, drift/boost, colisiones, visuales, audio, cámara, clash, skins
- **PowerUps/** — database, inventario, efectos, cajas, EMP, pickup manager
- **Race/** — RaceManager, Racer, Checkpoint
- **Network/** — NetworkManager, MatchmakingManager, PlayerSpawner, ReconnectionManager, GameBootstrap
- **Lobby/** — LobbyHandler + subcategorías (Create/Join/InRoom/Leaderboard), Tracks
- **Main Menu/** — MainMenuHandler, CarChooseButton
- **Leaderboard/** — LeaderboardService (REST), ScoreDto/Entry, XorCipher, RaceResultReporter
- **Save/** — LocalSaveManager, PlayerProfile
- **Scenes/** — GameSceneManager, TransitionManager
- **UI/** — HUD panels, minimap, banners, overlays
- **Audio/** — GameSFX (ScriptableObject)
- **Utility/** — Singleton, Timer, FollowTransform, etc.
- **Debugging/, Environment/, Extensions/, Keys/**

---

## 1. Sistema de Auto (`Car/`)

### Arquitectura central: patrón sphere-container
El auto separa **física** de **visual**:
- `_sphere` — un `Rigidbody` esférico que recibe todas las fuerzas y colisiones reales.
- `_container` — Transform que contiene la malla, ruedas y sensores. Se mantiene
  siempre `_containerOffset = 0.65f` debajo de la sphere (`LateUpdate`).

**Autoridad local vs remota** (patrón usado en todo el proyecto):
```csharp
IsLocalAuthority => photonView == null || photonView.ViewID == 0 || photonView.IsMine;
```
Esto permite correr **offline** (sin Photon) y online con el mismo código.

### CarController.cs — motor de física
- `FixedUpdate`: `CheckGround()` (raycast `_groundRayDistance=0.7`) → `HandleInput()`
  (`AddForce` forward con `_accelerationForce=35`, ForceMode.Acceleration) →
  `ApplySteering()` (rota el container) → `AlignToGround()` (lerp a la normal del
  suelo) → `SyncPhysicsBodyRotation()` → `ApplyIdleDamping()` (`_idleVelocityDamping=8`)
  → `UpdateSpeed()`.
- Exponen estado: `Speed` (dot con forward), `LinearSpeed` (throttle normalizado),
  `Acceleration`, `SteerInput`, `IsDrifting` (lateral > 4 m/s y grounded).
- `CanMove` controla `_sphere.isKinematic`.
- **Inyección remota**: `SetRemoteState(linearSpeed, steerInput)` para autos de otros.
- Drift hooks: `SetDriftHandling(steerMult, accelMult)`, `ApplyDriftVelocityDrag()`,
  `ResetDriftHandling()`.

### CarNetworkSync.cs — replicación (IPunObservable)
- Serializa: posición/velocidad de la sphere, rotación del container, `LinearSpeed`,
  `SteerInput`, y estado de drift/boost (`IsDrifting`, `DriftCharge01`, `DriftLevel`,
  `IsBoosting`, `BoostLevel`).
- Remoto: extrapola posición (`_netSpherePos + _netSphereVel * lag`, lag clampeado a
  `_maxExtrapolation=0.5s`), lerp a `_positionLerpSpeed=15`, inyecta vía
  `SetRemoteState`. En `Awake`, si no es local: `CanMove=false` + `isKinematic=true`.

### CarDriftBoost.cs — drift y boost de 3 tiers
- Tecla `LeftShift`. Carga si `velocidad ≥ 4`, `steer ≥ 0.15` y grounded.
- `_chargeTimeForMaxBoost=2.2s`. Umbrales de tier: 0.32 / 0.66 / 0.95.
- Al soltar Shift: `TryStartBoost()`. Duración boost por tier: 0.45/0.8/1.15s.
  Aceleración por tier: 25/38/52.
- VFX de humo cambia color por tier: gris → azul → naranja → rojo.

### Colisiones
- **CarCollisionSide.cs**: 4 BoxCollider trigger hijos (Front/Back/Left/Right). Infiere
  el lado por el nombre del GameObject; delega a `CarArcadeCollision.HandleSideTrigger`.
- **CarArcadeCollision.cs**: calcula impulso por *closing speed* (`_impulseStrength=7`,
  cap `_maxImpulse=12`). Aplica `AddForce(VelocityChange)` + spark + camera shake.
  Contiene la lógica de los dos "ataques":
  - **Nitro Ram**: sensor Front con turbo activo golpea el Back del rival →
    `RPC_RequestRamResolve` al **MasterClient**, que valida escudo y decide
    `RPC_RamDestroyed` o `RPC_ConsumeShield`.
  - **Drift Boost Clash**: sensor lateral con boost activo golpea lateral del rival →
    `CarClashMinigame.TriggerClash`.
- **CarWallImpact.cs**: `OnCollisionEnter` con paredes → camera shake + sparks
  (ignora colisiones contra otro auto).
- **CarCollisionTuning.cs**: ScriptableObject con los parámetros de colisión.

### CarRamDestroy.cs — destrucción y revive
Secuencia (coroutine `ReviveSequence`): explosión VFX → desactiva física/visual/colliders
→ muestra `ReviveUIPanel` (solo local) → countdown `_reviveTime=3` → teleport al último
checkpoint (`GetRespawnPosition/Rotation`, proyecta forward al plano XZ) → grace period
`_graceTime=3` con flickering e invencibilidad (`IsInvincible`).

### CarClashMinigame.cs — minijuego de choque
Disparado por boost lateral vs lateral. `RPC_StartClash` en ambos autos: `IsInClash=true`,
`CanMove=false`, muestra `ClashHUD`. Cada uno spamea **SPACE** (`+0.10` por tap). El
MasterClient resuelve por mayor `Progress` o por timeout `_duration=3s`; el perdedor recibe
`RPC_RamDestroyed` (o consume escudo). Durante el clash los autos oscilan con un seno lateral.

### Otros
- **CarVisuals.cs**: rotación de ruedas por aceleración, steering de las delanteras
  (`_maxSteerAngle=30`), body lean y pitch.
- **CarAudio.cs**: pitch/volumen del motor por velocidad, screech por intensidad de drift,
  impacto en colisión. Lee parámetros de `GameSFX`.
- **CarCamera.cs**: follow detrás-arriba con distancia dinámica por velocidad
  (`8→16`), FOV dinámico (`60→75`), obstacle avoidance por SphereCast, tilt por steering y
  `Shake()`/`TurboFovPunch()`.
- **CarNicknameLabel.cs**: label TMP billboard con el `NickName` del owner (oculto en local).

### Skins (`Car/Skin/`)
- **CarSkinSO**: `skinID`, `SkinName`, `SkinMesh`, `SkinPreview`.
- **CarSkinCatalogueSO**: lista + diccionario por ID (`GetSkin`, `HasSkin`).
- **CarSkinLoader**: lee `LocalSaveManager.Profile.selectedSkin`, publica el ID en
  Photon player properties (`SKIN_PROP`) y manda **RPC buffered** `RPC_LoadSkin` (para
  late-joiners). `ChangeCurrentSkin()` cambia mesh en vivo y persiste.

---

## 2. Sistema de PowerUps (`PowerUps/`)

### Tipos y database
- **PowerUpType**: `None, EMP, Shield, Turbo`.
- **PowerUpDatabase** (ScriptableObject): mapea tipo → icono + nombre (`GetIcon`,
  `GetDisplayName`).

### Inventario
- **PowerUpInventory** (`MonoBehaviourPun`): guarda **un** powerup (`HasPowerUp` es
  exclusivo). Tecla `E` → `TryUseCurrentPowerUp()` → delega a `PowerUpEffects`. Solo
  procesa input si `IsLocalAuthority`. Recibe powerups vía `ReceivePowerUp()`.
- **PowerUpInventoryHud / HUDItemPanel**: muestran el powerup actual (HUD encuentra el
  inventario local por `PhotonView.IsMine`).

### Efectos — PowerUpEffects.cs (corazón del sistema)
Cada efecto se aplica vía RPC `RpcTarget.All` (visuales en todos, física solo en autoridad):
- **EMP**: `RPC_SpawnEmpProjectile` instancia el proyectil en todos. El proyectil
  (`PowerUpEmpProjectile`) se mueve forward; **solo el MasterClient detecta el impacto**
  (anticheat). Al pegar: `ResolveEmpHit` → si target tiene escudo `RPC_ConsumeShield`,
  si no `RPC_ApplyEmp` (stun `_empStunDuration=1.25`, `CanMove=false`, daña velocidad,
  camera shake).
- **Shield**: `RPC_SetShield(true, 6s)` activa visual rotatorio + timestamp; coroutine lo
  apaga a los `_shieldDuration=6`.
- **Turbo**: `RPC_ActivateTurbo(1.1s, 45)` → partículas en todos; en la autoridad local
  agrega fuerza forward cada FixedUpdate, `IsTurboActive=true`, FOV punch + speed flash.

### Cajas y pickup
- **PowerUpBox**: collider trigger con `_boxId` único, rota/flota/cicla color HSV. En
  `OnTriggerEnter` marca `_available=false` y llama `PowerUpPickupManager.RequestPickup`.
  Se registra/desregistra en `OnEnable/OnDisable`.
- **PowerUpBoxPlacer**: herramienta de editor (ContextMenu) que genera grillas de cajas
  con IDs únicos.
- **PowerUpPickupManager** (Singleton, `IOnEventCallback`): arbitra pickups vía **Photon
  Events** (no RPC):
  - `RequestPickupEvent (41)` cliente → MasterClient.
  - El MC bufferea 1 frame las requests, elige ganador por **menor serverTimestamp**
    (maneja wrap-around con `unchecked`), elige tipo random del `_powerUpPool`.
  - `ResolvePickupEvent (42)` MC → todos: aplica `ReceivePowerUp` al ganador.
  - `SetBoxAvailableEvent (43)`: respawn tras `_respawnDelay=6s`.
  - Modo offline: `GrantPickupLocal()`.

---

## 3. Carrera y Red (`Race/`, `Network/`, `Scenes/`)

### RaceManager.cs (Singleton, IOnEventCallback) — orquestador
- Config: `_totalLaps=3`, `_countdownDuration=3`, `_podiumTimeout=120`, array
  `_checkpoints[]` (el último es la meta). Estados: `Waiting/Countdown/Racing/Finished`.
- **Reloj universal** = `PhotonNetwork.Time`. `ExactStartTime` = momento del "GO!".
- **Eventos Photon**: `EVENT_RACE_START(1)`, `EVENT_REPORT_FINISH(2)`,
  `EVENT_PODIUM(3)`, `EVENT_COUNTDOWN(4)`.

**Arranque sincronizado**: cuando todos los racers se registraron, el MC calcula
`startTime = PhotonNetwork.Time + 3` y lo manda con `EVENT_RACE_START`. Todos corren
`SyncedCountdown(startTime)` mirando el mismo reloj → salen exactamente juntos → `FireGo()`.

**Checkpoints y posiciones**:
- `NotifyCheckpoint` valida que sea el checkpoint **siguiente** en orden (anti-shortcut).
- Posición = ordenar por `CurrentLap * nCheckpoints + LastCheckpoint` (desc), tiebreak
  `RaceTime` (asc).

**Modelo de finish (por-corredor, validado globalmente)** — ver `[[racemanager-finish-model]]`:
- Cada cliente reporta cuando **su** auto completa la última vuelta:
  `RPC_SetFinished` (congela el auto en todos) + `EVENT_REPORT_FINISH` al MC con su
  raceTime.
- El MC acumula finishes deduplicando por viewId. El primero dispara un timeout dinámico
  (`max(30, avgLap*1.5)`) y `EVENT_COUNTDOWN` para mostrar HUD. Cuando todos terminan o
  expira el timeout → `BroadcastPodium()` ordenando por `ServerTimestamp`.
- `EVENT_PODIUM` asigna posiciones finales (DNF a los que no llegaron), guarda en
  `LocalSaveManager` y dispara `OnRaceFinished(winner)`.

**Eventos C# que consume el HUD**: `OnCountdown`, `OnRaceStart`, `OnLapCompleted`,
`OnPositionsUpdated`, `OnLocalRacerFinished`, `OnCountdownStarted`, `OnRaceFinished`.

### Racer.cs (MonoBehaviourPun)
Estado por auto: `CurrentLap`, `LastCheckpoint`, `RaceTime` (acumula `Time.deltaTime` si
`_racing`), `Position`, `FinishTime`, `IsFinished`, `PlayerName`. Se registra en `Start`.
`RPC_SetFinished()` congela el rigidbody, desactiva colliders y sensores laterales.

### Checkpoint.cs
Trigger box: en `OnTriggerEnter` busca el `Racer` en el parent y llama
`RaceManager.NotifyCheckpoint`. Gizmos para visualizar respawn/orientación.

### Network/
- **NetworkManager** (PunSingleton): conexión. Estados `Offline/Connecting/Connected`.
  `RequestConnection()` con `AutomaticallySyncScene=true`. Evento `OnStatusChanged`.
- **MatchmakingManager** (PunSingleton): rooms. `MaxPlayers=4`, `MinPlayers=2`.
  `RequestCreateRoom(name, trackID)` guarda el track en `CustomProperties[Keys.MAP_PROP_KEY]`.
  `RequestJoinRoom` (vacío = random). Estados `InLobby/JoiningRoom/CreatingRoom/InRoom`.
- **PlayerSpawner**: en `Start` hace `PhotonNetwork.Instantiate("Car", spawnPoint)`
  (offline = `Resources.Load`). Spawn point por `ActorNumber`. Vincula la `CarCamera`.
- **ReconnectionManager** (Singleton): reintenta `ReconnectAndRejoin()` hasta 3 veces
  ante desconexiones inesperadas; usa `SuppressOfflineOnDisconnect` para no saltar al menú.
  Si falla, vuelve al menú.
- **GameBootstrap**: SOLO testing — conecta y crea "TestRoom" para entrar directo a la
  escena de juego. Desactivar en build final.

### Scenes/
- **GameSceneManager** (Singleton, DontDestroyOnLoad): observa `OnStatusChanged`;
  Connected → escena "Lobby", Offline → escena "Menu".
- **TransitionManager** (Singleton): cross-fade de 3s (fade-in → `LoadSceneAsync` →
  fade-out) controlando `CanvasGroup.alpha`.

---

## 4. Lobby y Menú (`Lobby/`, `Main Menu/`)

- **MainMenuHandler**: input de nombre (límite 16; persiste en `LocalSaveManager` y
  `PhotonNetwork.NickName`), grilla de skins (`CarChooseButton` → `CarSkinLoader`),
  botón Play → `NetworkManager.RequestConnection()`. Si el nombre está vacío genera
  `Player_####`.
- **LobbyHandler** (`ILobbyHandlerCommands`): orquesta pantallas (`_joinOrCreateScreen`
  vs `_inRoomScreen`) y subcategorías. `RequestStartGame()` (solo MasterClient) lee el
  trackID de las room properties y hace `PhotonNetwork.LoadLevel(TrackSceneName)`.
- **CreateRoomHandler**: input de nombre + grilla de `TrackChooseButton`; crea el room
  con el track elegido.
- **JoinRoomHandler**: se une al lobby, escucha `OnRoomListUpdate`, cachea rooms y
  recrea `RoomObject` por cada sala.
- **RoomObject**: muestra nombre, `[X/Y]`, mapa; botón JOIN/FULL según capacidad.
- **InRoomHandler**: muestra info del room (nombre, mapa, mejor tiempo), lista de
  jugadores y habilita "Start" si es MasterClient y hay `≥ MinPlayers`.
- **LobbyLeaderboardPanel**: selección de track + top-N global (vía `LeaderboardService`)
  + récord personal (vía `LocalSaveManager`).
- **TrackSO**: `TrackID`, `TrackName`, `TrackSceneName`, `TrackImage`.
  **TrackCatalogueSO**: lista + diccionario por ID.
- **Keys.MAP_PROP_KEY = "map"**: clave de la room property que guarda el trackID.

---

## 5. Leaderboard y Persistencia (`Leaderboard/`, `Save/`)

### LeaderboardService.cs — cliente REST (Singleton, DontDestroyOnLoad)
- Config inspector: `_baseUrl` (**si está vacía, todo es no-op silencioso**),
  `_encryptPayload`, `_xorKey`. `IsReady => !_baseUrl.IsEmpty()`.
- `SubmitScore(name, time, position)`: arma un `ScoreDto` (con escena actual como track
  + timestamp ISO) y hace **POST** (cuerpo JSON via `JsonConvert`, opcionalmente ofuscado
  con `XorCipher`).
- `GetTopScores(n, cb)` / `GetTopScoresByTrack(track, n, cb)`: **GET**, parsea con
  `JToken` (acepta array JSON propio **o** objeto estilo Firebase RTDB), filtra por track,
  **ordena ascendente por tiempo** (menor = mejor) y recorta a top-N.
- **ScoreDto** (JsonProperty: name/time/position/track/timestamp) ↔ **ScoreEntry** (struct
  para UI). **XorCipher**: `Encrypt`/`Decrypt` (XOR involutivo), anti-cheat básico.
- **RaceResultReporter**: se suscribe a `OnRaceFinished`, encuentra el racer local, toma
  su `FinishTime` y llama `SubmitScore` (flag `_reported` evita duplicados).

### LocalSaveManager.cs — guardado local (Singleton)
- Archivo: `Application.persistentDataPath/playerdata.bin`. Serializa con
  `BinaryFormatter` y cifra byte-a-byte con XOR (`EncryptionKey` = "RacingKey").
- `OnRaceCompleted(trackId, time, position, totalPlayers)`: actualiza stats globales
  (`totalRaces/totalWins/totalPodiums`) y el `TrackRecord` (mejor tiempo/posición).
- `SaveNickname`, `SaveSkin`, `GetBestTime`, `GetBestPosition`.
- **PlayerProfile** [Serializable]: nickname, selectedSkin, stats globales y
  `Dictionary<string, TrackRecord>` (bestRaceTime, bestPosition, racesCompleted).

Ver también la nota de memoria `[[rest-leaderboard]]`.

---

## 6. UI / HUD (`UI/`)
Todos los paneles del HUD encuentran al jugador local por `PhotonView.IsMine` y se
suscriben a eventos del `RaceManager`:
- **HUDItemPanel** (powerup actual), **HUDLapPanel** (`vuelta/total`),
  **HUDPositionPanel** (posición con sufijo ordinal), **HUDCountdownDisplay**
  (3/2/1/GO! con sonido de `GameSFX`), **HUDFinishBanner** (banner al cruzar meta),
  **HUDLeaderboard** (tabla final por posición), **HUDRaceCountdown** (timer cuando otro
  termina).
- **MinimapController**: cámara ortográfica cenital auto-ajustada a los bounds del track;
  excluye la layer "MinimapIcon" del resto de cámaras. **MinimapIcon**: blanco si local,
  color por `ActorNumber` si remoto.
- **ReviveUIPanel** (countdown de revive), **ClashHUD** (barras del minijuego de choque),
  **SpeedEffectsOverlay** (speed lines + vignette + turbo flash por velocidad).
- **LeaderboardPanel / LeaderboardRow**: tabla global vía REST con auto-refresh.

---

## 7. Soporte (`Audio/`, `Utility/`, `Debugging/`, `Environment/`, `Extensions/`)
- **GameSFX** (ScriptableObject, Resources): base centralizada de SFX y parámetros de
  audio (countdown, combate, powerups, race, motor/screech/impacto).
- **Singleton<T> / PunSingleton<T>**: patrón singleton genérico.
- **Timer**: `CountdownTimer` y `StopwatchTimer` con eventos.
- **FollowTransform / LookAtTransform**: follow y look-at suaves.
- **WeightedNode<T>**: nodo con peso para comparaciones.
- **Logger** (estático, condicional editor/dev) y **LoggerSO** (logger con color/prefijo).
- **ProceduralTerrain**: mesh procedural con centro plano (pista) y montañas Perlin en los
  bordes; `[ExecuteInEditMode]`.
- **Extensions**: `CanvasGroup.ToggleVisibility`, `SpriteRenderer.ChangeSprite`,
  `string.IsEmpty`, `Vector3.NoY`.

---

## Patrones transversales (claves para entender el código)
1. **`IsLocalAuthority`** en todos lados → mismo código corre offline y online.
2. **MasterClient como árbitro** de eventos críticos: ram destroy, clash, EMP hit,
   pickups y orden del podio. Los clientes no manipulan resultados.
3. **`PhotonNetwork.Time`** como reloj común para countdown y tiempos de carrera.
4. **RPC `RpcTarget.All`** para sincronizar visuales; **Photon Events** (codes 41-43, 1-4)
   para arbitraje request→resolve.
5. **`unchecked` en timestamps** para manejar wrap-around de `ServerTimestamp` (int).
6. **Sphere-container** separa física de visual; remotos van kinematic + interpolación.

## Pendientes / notas de wiring (Unity Editor)
- `LeaderboardService._baseUrl` debe apuntar al backend REST (si vacío, no sube tiempos).
- `GameBootstrap` es solo para testing — desactivar en build final.
- Prefab del auto en `Resources/` (nombre por defecto "Car").
- Validar siempre con **ParrelSync** (2 instancias): ganador y perdedor deben registrar
  su tiempo independientemente.
