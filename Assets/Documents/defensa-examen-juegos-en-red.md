# Defensa del Proyecto — Nitro Rumble (Juegos en Red)

Guía para responder "¿dónde está X y cómo lo resolviste?" en la mesa. Organizado en el mismo orden que las PPTs de la materia. Cada punto trae: **qué es** (repaso rápido de la teoría), **dónde está** (archivo:línea) y **cómo lo resolvimos** (la decisión concreta que tomamos).

---

## 1. Protocolos (TCP/UDP) y Topología de Red

**Qué es:** TCP garantiza orden y entrega (handshake, más pesado); UDP es liviano pero no garantiza nada. Las arquitecturas de red son Cliente-Servidor, P2P, o Híbrida.

**Cómo lo resolvimos:** no implementamos sockets a mano — usamos **Photon PUN**, que abstrae el transporte. Photon usa una arquitectura **híbrida**: hay un servidor (autoridad final, guarda el estado de la sala) pero los clientes también "hablan entre sí" a través de él. Dentro de Photon podemos elegir el modo de entrega por mensaje (confiable = TCP-like, no confiable = UDP-like) con `SendOptions.SendReliable` / `SendUnreliable`.

**Dónde se ve la elección de reliability:**
- `RaceManager.cs:133` — el arranque de carrera (`EVENT_RACE_START`) usa `SendOptions.SendReliable`: **no podemos permitirnos perder ese mensaje**, si un cliente no lo recibe nunca arranca.
- `CarNetworkSync.OnPhotonSerializeView` (línea 117) — la posición/velocidad del auto viaja por el canal por defecto de la `PhotonView` (no confiable, alta frecuencia) porque no importa perder un paquete de posición: el siguiente lo corrige.

**Si preguntan "¿por qué Photon y no sockets a mano?"** → porque el trabajo es de diseño de gameplay en red, no de infraestructura de transporte; Photon nos da matchmaking, rooms, RPC y serialización ya resueltos y homologados, dejándonos enfocar en la sincronización del *juego*.

---

## 2. Estado de Juego — PhotonView, Instantiate, IsMine, MasterClient

**Qué es:** el "Estado de Juego" es todo dato que dos o más clientes necesitan compartir para que el juego funcione igual en todas las pantallas. `PhotonView` es el componente que administra qué se sincroniza de un GameObject; `PhotonNetwork.Instantiate` crea objetos de forma que **todos** los clientes los vean (a diferencia de un `Instantiate` normal, que es solo local).

**Dónde está:**
- **Instantiate en red:** `PlayerSpawner.cs:33` — cada cliente instancia su propio auto vía `PhotonNetwork.Instantiate("Car", ...)`. El prefab está en `Assets/Resources/Car.prefab` (requisito de Photon: el prefab **debe** vivir en una carpeta `Resources`).
- **Fallback offline:** `PlayerSpawner.cs:39-44` — si no hay conexión (testing en editor), usamos `Instantiate` común. Buena defensa de por qué distinguimos los dos casos.
- **IsMine / autoridad:** no usamos `photonView.IsMine` a pelo en todos lados — centralizamos el criterio en `Assets/Scripts/Network/PhotonViewAuthority.cs:11`, que envuelve `view.IsMine`. Por qué: en el minijuego de choque y en otros sistemas necesitábamos el mismo criterio de "¿este objeto me pertenece?" en múltiples clases, así que lo pusimos en un solo lugar en vez de repetir la condición.
- **MasterClient:** usado para centralizar decisiones que solo deben ocurrir una vez (tal cual la PPT). Ejemplos: `RaceManager.cs:124` (solo el Master dispara el countdown), `RaceGridManager.AssignGridFromCurrentRoom` en `PlayerSpawner.cs:98` (solo el Master arma la grilla de largada). `OnMasterClientSwitched` está implementado en 4 clases (`RaceManager`, `ChampionshipManager`, `InRoomHandler`, `RoomConfigPanel`) para reaccionar si el Master se desconecta.

**Cómo lo resolvimos (patrón general):** cada Car tiene una `PhotonView` en su raíz (ver jerarquía del prefab) y un componente `CarNetworkSync` que implementa `IPunObservable` para definir manualmente qué se serializa — no usamos el `Transform View` genérico de Photon porque necesitábamos **interpolar/extrapolar** manualmente (ver sección 6) y sincronizar variables propias del gameplay (drift, boost, squash) además de la posición.

---

## 3. Rooms — creación, opciones, propiedades custom, sync de escenas

**Dónde está:** `Assets/Scripts/Network/MatchmakingManager.cs`

- **Crear/unirse:** `RequestCreateRoom` (línea 53) usa `PhotonNetwork.CreateRoom`; `RequestJoinRoom` (línea 35) distingue `JoinRoom(nombre)` de `JoinRandomRoom()` si el nombre viene vacío.
- **Room Options (línea 55-69):**
  - `PlayerTtl = 10000` → si alguien se cae, su lugar queda reservado 10s para reconexión (no lo pisa otro jugador).
  - `EmptyRoomTtl = 15000` → si TODOS se caen, Photon mantiene la sala 15s viva por si alguien reconecta.
  - `CustomRoomProperties` (`LAPS_KEY`, `RACE_COUNT_KEY`, `MAP_POOL_KEY`) — configuración de la carrera (vueltas, cantidad de carreras del campeonato, mapas habilitados) vive **en la sala**, visible para todos los actores, tal cual la PPT de Rooms.
  - `CustomRoomPropertiesForLobby` — expone `LAPS_KEY`/`RACE_COUNT_KEY` a la lista de salas del Lobby (así `RoomObject` puede mostrar "3 vueltas / 5 carreras" sin tener que entrar a la sala).
- **Grid de largada como propiedad custom:** `PlayerSpawner.cs:109` — el Master calcula el orden de largada y lo guarda con `room.SetCustomProperties(new Hashtable { GRID_ACTORS_KEY, ... })`; todos los clientes lo leen para decidir su propio `spawnPoint` (línea 145).
- **Sync de escenas:** `NetworkManager.cs:41` — `PhotonNetwork.AutomaticallySyncScene = true`, así cuando el Master carga una escena, todos los clientes la cargan también (usado para pasar de Lobby a la pista).

**Si preguntan "¿cómo decide cada auto en qué posición largar sin RPCs?"** → justamente por eso usamos una Custom Property de sala en vez de un RPC: cualquier actor que entre (incluso tarde) puede leerla, no depende de haber "escuchado" un mensaje puntual.

---

## 4. RPCs y RaiseEvent

**Qué es:** los RPC llaman un método marcado `[PunRPC]` en todas las `PhotonView` de un objeto específico. `RaiseEvent` es más genérico: no depende de una View, se identifica por un código numérico y puede tener distintos `Receivers`.

**RPCs — dónde están (8 usos reales en el proyecto):**
- `CarClashMinigame.cs` — el corazón del minijuego de choque: `RPC_StartClash` (línea 67, dispara el duelo en ambos autos), `RPC_SyncProgress` (línea 101, cada tecleo de Espacio se replica), `RPC_EndClash` (línea 107).
- `CarArcadeCollision.cs:239`, `CarRamDestroy.cs:58` — destrucción/impacto de autos.
- `Racer.cs:60` — `RPC_SetFinished`, marcado en todos los clientes cuando un auto termina.
- `PowerUpEffects.cs` — 6 RPCs distintos (líneas 157-234) para aplicar EMP/Shield/Boost de forma sincronizada.
- `CarSkinLoader.cs:117` — sincroniza qué skin de auto ve cada cliente.

**Ejemplo completo para explicar en la mesa — `CarClashMinigame.TriggerClash` (línea 40-62):**
```csharp
photonView.RPC(nameof(RPC_StartClash), RpcTarget.All, oppViewId, sineAxis, true);
opponent.photonView.RPC(nameof(RPC_StartClash), RpcTarget.All, myViewId, sineAxis, false);
```
Se llama **una vez por auto involucrado**, targets `RpcTarget.All` porque **todos** los clientes deben ver el duelo (no solo los dos jugadores en cuestión). Los parámetros son tipos simples (`int`, `Vector3`, `bool`) — tal como advierte la PPT, Photon no serializa RPCs con estructuras complejas sin registro previo.

**RaiseEvent — dónde está:** `RaceManager.cs`, que implementa `IOnEventCallback` (línea 9) y centraliza 4 eventos custom (`EVENT_RACE_START`, `EVENT_REPORT_FINISH`, `EVENT_PODIUM`, `EVENT_COUNTDOWN`), todos manejados en un único `OnEvent()` (línea 143) con switch por código — el patrón exacto de la PPT.
- `Receivers = ReceiverGroup.All` (línea 132, 347, 395) para eventos que todos deben saber (arranque, countdown, podio).
- `Receivers = ReceiverGroup.MasterClient` (línea 291) para el reporte de finish: **cada auto le informa su tiempo únicamente al Master**, que es quien decide el podio — evita que todos los clientes calculen el podio en paralelo y puedan desincronizarse.

**Por qué RPC en un caso y RaiseEvent en otro:** RPC cuando la acción pertenece a un objeto de red concreto (un auto específico explota, un power-up específico se activa). RaiseEvent cuando el evento es "global" del sistema de carrera y no de un GameObject puntual (arranque, podio).

---

## 5. Características de Red — Lag, Interpolación, Extrapolación, Autoridad

**Dónde está todo junto:** `Assets/Scripts/Car/CarNetworkSync.cs` — es el archivo más denso en conceptos de la materia.

- **Interpolación (posición angular):** línea 96-97, `Quaternion.Lerp` de la rotación actual a la última recibida por red, multiplicado por `Time.deltaTime * _rotationLerpSpeed`. Suaviza saltos de rotación.
- **Extrapolación (posición lineal):** líneas 83-88.
  ```csharp
  float lag = Mathf.Clamp((float)(PhotonNetwork.Time - _lastReceiveTime), 0f, _maxExtrapolation);
  Vector3 extrapolated = _netSpherePos + _netSphereVel * lag;
  Vector3 targetPos = Vector3.Lerp(_sphere.position, extrapolated, Time.fixedDeltaTime * _positionLerpSpeed);
  ```
  Tal cual la PPT: `posición_nueva = última_posición + velocidad × tiempo_transcurrido`. El `Clamp` a `_maxExtrapolation` (0.5s) evita "adivinar" demasiado lejos si el paquete tarda mucho — ahí preferimos que se note un poco el lag a que el auto atraviese una pared por sobre-predicción.
- **Autoridad (Client-Side vs Master-Side):** el dueño del auto (`IsLocal`, línea 58, delega en `PhotonViewAuthority`) controla su propia física normalmente (Client-Side, como un juego offline) — así el manejo se siente responsive. Pero decisiones "canónicas" (quién gana el clash, el podio, el countdown) las resuelve el **Master** (`CarClashMinigame.cs:151`, `RaceManager` en general) — es el patrón "Master-Side" que menciona la PPT como alternativa intermedia cuando no hay servidor dedicado.
- **Delta Sync / optimización de ancho de banda:** líneas 43-52 y 138-159. En vez de mandar 6 variables discretas (drifting, boosting, driftLevel, boostLevel, driftCharge, squash) en cada paquete, armamos un **bitmask de 1 byte** (`dirty`) y solo mandamos el valor de las que cambiaron desde el último envío:
  ```csharp
  byte dirty = 0;
  if (isDrifting != _prevIsDrifting) dirty |= DIRTY_DRIFTING;
  ...
  stream.SendNext(dirty);
  if ((dirty & DIRTY_DRIFTING) != 0) stream.SendNext(isDrifting);
  ```
  Esto es exactamente "Delta Compression" (PPT Sync2, slide 30) — mandamos solo lo que cambió, no el estado completo cada vez.
- **Cuantización:** líneas 189-198 — el squash (un `float`) se comprime a **1 solo byte** (`QuantizeSquash`/`DequantizeSquash`, mapeo `[-1,1] → [0,255]`) en vez de mandar 4 bytes crudos. Ahorro de ancho de banda explícito, mencionado en la PPT de Serialización-API sobre binario vs otros formatos.
- **Ping / RTT:** no está expuesto en UI todavía, pero `PhotonNetwork.GetPing()` está disponible si preguntan por RTT — es la llamada que usa Photon internamente, la mencionan en la PPT Red-Lag.

**Si preguntan "¿por qué interpolás rotación pero extrapolás posición?"** → la rotación cambia de forma más "predecible/lenta" visualmente y un `Lerp` alcanza; la posición durante un boost o un choque puede tener saltos de velocidad grandes, así que ahí conviene proyectar con la última velocidad conocida (extrapolación) en vez de solo interpolar entre dos puntos viejos.

---

## 6. Sync avanzado — Countdown sincronizado, PhotonNetwork.Time, Custom Types

**Countdown sincronizado (el ejemplo más fuerte para defender — es LITERAL el ejercicio "Sync2" de la PPT, slide 8):** `RaceManager.cs:117-199`.

1. El Master calcula `startTime = PhotonNetwork.Time + _countdownDuration` (línea 126) — el reloj de red compartido, no `Time.time` local.
2. Lo manda por `RaiseEvent(EVENT_RACE_START, startTime, ...)` a todos (línea 129).
3. Cada cliente, al recibirlo, corre `SyncedCountdown(startTime)` (línea 170): un `while (PhotonNetwork.Time < startTime)` que dispara los números 3-2-1 comparando el reloj compartido contra ese `startTime` — **todos los clientes llegan a "GO" en el mismo instante real**, sin depender de si su FPS local es distinto.
4. Fallback offline: `LocalCountdown()` (línea 189) con `WaitForSeconds` normal si no hay red.

Esto es exactamente el patrón "la carrera empieza a las 19:02:050" que se explicó en la PPT de Sync — mandar CUÁNDO en vez de mandar AHORA.

**Custom Package / RegisterType:** `Assets/Scripts/Network/CustomTypes/PhotonCustomTypes.cs` + `RaceResultPackage.cs`.
- Registramos un tipo propio (`RaceResultPackage`: viewId + nombre + tiempo + timestamp) con `PhotonPeer.RegisterType` (línea 33), código de tipo `'R'` (82) — Photon ya usa 'P'/'Q'/'V'/'W' para sus tipos internos, elegimos uno libre.
- Serialización manual campo a campo con `BitConverter` (líneas 47-64) y deserialización en el mismo orden exacto (líneas 67-80) — tal cual la advertencia de la PPT de "es crítico mantener el orden en ambos métodos".
- Se usa directamente en un `RaiseEvent` (`RaceManager.cs:288`) como si fuera un tipo nativo de Photon, gracias al registro previo en `NetworkManager.Awake()`.

**PhotonNetwork.Time para más que el countdown:** también se usa para el tiempo final de carrera (`RaceManager.cs:278`, resta contra `ExactStartTime` — reloj universal, no depende de FPS) y para la rotación sincronizada de las cajas de power-up (`PowerUpBox.cs:68`, todos calculan el mismo ángulo absoluto en el mismo instante sin RPC).

---

## 7. Serialización, Encriptación y APIs REST

**JSON:** usamos **Newtonsoft** (no `JsonUtility`) porque necesitábamos listas/anidados que `JsonUtility` no soporta bien — se ve en `LeaderboardService.cs` (`JsonConvert.SerializeObject`, línea 83) y `LiveOpsConfig.cs` (`JArray.Parse`, línea 177).

**Encriptación XOR:** `Assets/Scripts/Leaderboard/XorCipher.cs` — implementación textual del ejercicio de la PPT (Serialización-API, slides 28-39): `text[i] ^ key[i % key.Length]`, e insiste en el comentario que **XOR es involutivo** (Encrypt y Decrypt son la misma operación, línea 36). Usado en dos lugares:
- `LeaderboardService.cs:85` — ofusca el body del POST del leaderboard si `_encryptPayload` está activo.
- El Auth con Google Sheets (`Assets/Documents/auth-google-sheet-setup.md`) — la contraseña viaja ofuscada con la misma técnica antes de llegar al Apps Script.

**Por qué XOR y no algo más fuerte:** el propio código lo aclara (comentario en `XorCipher.cs:13`) — es ofuscación, no criptografía real, alcanza para frenar a alguien que edita el request a mano casualmente, no a un atacante serio. Es una respuesta honesta y defendible: "sabemos que no es seguro de verdad, elegimos el nivel de esfuerzo acorde al riesgo real del proyecto".

**APIs REST con UnityWebRequest:** `Assets/Scripts/Leaderboard/LeaderboardService.cs`.
- `SubmitScore` (línea 59) → `PostScoreRoutine` (línea 81): corrutina + `UnityWebRequest` POST, `using` para liberar el `WebRequest` al terminar (tal cual la PPT lo remarca).
- `GetTopScores`/`GetTopScoresByTrack` (líneas 115-145) → GET, parseo con Newtonsoft soportando **tanto array JSON como objeto/mapa** (línea 184-195) porque el backend puede ser un REST propio o Firebase Realtime Database — decisión defendible: "diseñamos el parser para no atarnos a un solo formato de backend".
- Manejo de error explícito en ambos casos (`ConnectionError`/`ProtocolError`, líneas 97-101 y 158-161) — no se rompe el juego si el backend no responde.

---

## 8. Live-Ops / Remote Config

**Dónde está:** `Assets/Scripts/Network/Live Ops/LiveOpsConfig.cs`.

- Fetchea 3 claves JSON de Unity Remote Config al iniciar (`disabledTrackIds`, `disabledSkinIds`, `disabledPowerUps`) — línea 22-24 documenta el modelo de datos.
- **Diseño "fail-open" (línea 26-31, explícito en el propio código):** lo que NO está en la lista de deshabilitados está disponible. Si falla el fetch, si no hay red, o si el JSON viene vacío/mal formado → todo queda habilitado. Decisión de diseño defendible: preferimos que el juego sea siempre jugable a que un fallo de LiveOps bloquee contenido.
- Polling periódico (línea 117, cada 60s por defecto) porque el SDK de Remote Config **no** empuja cambios en tiempo real — hay que volver a pedir la config para detectar un cambio hecho en el dashboard mientras el juego corre.
- Consumido por gameplay/UI vía 3 queries públicas: `IsTrackAvailable`, `IsSkinAvailable`, `IsPowerUpAvailable` (líneas 159-166).

---

## 9. Bots (IA local, sin ser "Player" de Photon)

**Qué es:** un caso interesante que no está en la PPT directamente, pero usa exactamente los mismos conceptos de State de Juego, MasterClient y sincronización sin RPC — solo que aplicados a un objeto que **no tiene un `Player` de Photon detrás**.

**Dónde está:**
- **Spawn:** `PlayerSpawner.SpawnBotsIfMaster()` (línea 60) — **solo el Master Client** instancia los bots (una vez, `PhotonNetwork.IsMasterClient` en la línea 62), igual que la grilla de largada de la sección 3. Cada bot se crea con `PhotonNetwork.Instantiate(_carPrefabName, sp.position, sp.rotation, 0, new object[] { true, i })` (línea 88) — el `object[]` es **Instantiation Data**: viaja pegado a la creación en red y llega **idéntico a todos los clientes** en el mismo frame en que el objeto aparece, sin depender de un RPC posterior. `true` = "es bot", `i` = su índice (0, 1 o 2).
- **Lectura del Instantiation Data:** `Racer.Awake()` (línea 46-55) lee `photonView.InstantiationData` y setea `IsBot`/`BotIndex`. Como es el mismo array para todos los clientes, **todos calculan el mismo nombre localmente** sin sincronizar nada más: `BotNames[BotIndex % BotNames.Length]` (línea 23, 31) — mismo patrón que la rotación de las cajas de power-up (sección 6): compartimos el dato de entrada (acá el índice, ahí `PhotonNetwork.Time`) y cada cliente deriva el mismo resultado por su cuenta.
- **Ownership y MasterClient migration:** un bot no tiene dueño "natural" — su `PhotonView` (`OwnershipTransfer = Takeover` en el prefab) pertenece a quien lo instanció, o sea el Master Client de ese momento. Si el Master se cae, `RaceManager.OnMasterClientSwitched` (línea 572) hace `racer.photonView.TransferOwnership(newMasterClient)` para **todos** los bots — el nuevo Master pasa a controlarlos sin que ningún otro sistema (`CarController`, `CarBotDriver`, etc.) necesite enterarse: en cuanto cambia la ownership, `HasLocalInputAuthority` empieza a dar `true` en esa máquina y el auto arranca solo.
- **Por qué esto rompía la autoridad existente:** antes de los bots, "¿esta `PhotonView` es mía?" (`photonView.IsMine`) era sinónimo de "es mi propio auto". Con bots, el Master Client puede ser dueño de **su auto real Y de un bot** al mismo tiempo — dos objetos con `IsMine = true` pero solo uno es "yo, el humano". Se resolvió agregando `PhotonViewAuthority.IsLocalHumanRacer()` (línea 23): igual que `HasLocalInputAuthority` pero excluye explícitamente los `Racer.IsBot`. Se usa en 8 lugares (HUD de posición, vuelta, item, minimapa, nickname, `RaceResultReporter`) — todo lo que antes preguntaba "¿es mi auto?" para decidir qué mostrar/reportar ahora pregunta "¿soy YO, el humano?".
- **Los bots no cuentan como jugadores reales, en ningún lado:**
  - `RaceManager.RegisterRacer` (línea 91-97) separa `humanRacers` del total de `_racers` — la carrera arranca cuando los **humanos** están listos, los bots no destraban ni bloquean el inicio.
  - `RaceManager.RealPlayerCountAtStart` (línea 27, seteado en línea 220) guarda cuántos humanos había al arrancar. `RaceResultReporter` (línea 50) usa ese número para **no mandar el tiempo al leaderboard** si `RealPlayerCountAtStart <= 1` — evita que una carrera de "1 humano contra bots" ensucie el ranking online como si fuera competitivo real.
  - `InRoomHandler.ReloadStartButton` cuenta humanos + bots contra el `MinPlayers` de la sala — los bots sí sirven para habilitar el botón "Iniciar" (esa es la gracia de tenerlos), pero el conteo de la sala Photon (`Room.PlayerCount`, matchmaking) nunca los ve, porque no son `Player`, son solo GameObjects con `PhotonView`.

**Si preguntan "¿por qué el bot no se sincroniza por RPC?"** → porque no hace falta: su única decisión de red es **quién lo posee** (ownership, resuelto por Photon nativamente) y **su identidad** (instantiation data, viaja gratis con el `Instantiate`). El resto — hacia dónde dobla, cuánto acelera — lo calcula `CarBotDriver` de forma **puramente local** en la máquina que tiene la ownership en ese momento, exactamente igual que el auto de un humano: autoridad Client-Side (sección 5), solo que el "cliente" es el Master Client actuando en nombre del bot.

---

## 10. Extras del proyecto (más allá del temario base)

**Seamless Reconnection:** `Assets/Scripts/Network/ReconnectionManager.cs`.
- Escucha `NetworkManager.OnDisconnect` (línea 35) y filtra qué causas ameritan reintentar (`IsUnexpectedDisconnect`, línea 127 — descarta desconexiones intencionales del usuario).
- Reintenta con `PhotonNetwork.ReconnectAndRejoin()` (línea 65) hasta `_maxRetries` veces, con delay entre intentos — usa el `PlayerTtl` de la Room Option (sección 3) para que el slot siga reservado.
- Expone estado (`Reconnecting`/`Success`/`Failed`) por evento para que la UI (`ReconnectionPanel`) le dé feedback al jugador — no es una reconexión "silenciosa", el jugador ve qué está pasando.

**Chat de voz por proximidad:** `Assets/Scripts/Network/Voice/ProximityVoice.cs` (sobre Photon Voice 2).
- No reinventa el envío de audio — usa el `Speaker` de Photon Voice, y este componente solo **ajusta el volumen** del `AudioSource` según distancia (línea 60-73), con una `AnimationCurve` de falloff configurable en inspector.
- Corre solo en autos remotos (`_photonView.IsMine` descarta el auto local, línea 63) — el auto propio no se escucha a sí mismo.
- El "oyente" es la cámara principal, no el auto — porque la cámara sigue al jugador con offset, es la posición real desde donde "escucha" (línea 79).

**Auth con Google Sheets:** ver `Assets/Documents/auth-google-sheet-setup.md` — Apps Script como backend liviano (`doPost` con acciones `register`/`login`), password ofuscada con XOR (misma clave en ambos lados), columna `salt` prevista para migrar a hash real si hiciera falta subir el nivel de seguridad.

---

## Resumen para memorizar en 30 segundos

> "Usamos Photon PUN (arquitectura híbrida) para no reinventar transporte/matchmaking. Cada auto tiene una PhotonView con `IPunObservable` manual porque necesitábamos interpolación + extrapolación + delta-sync propios en vez del Transform View genérico. El Master Client centraliza decisiones únicas (countdown, podio, grid de largada) vía RaiseEvent con un tipo custom registrado a mano; los eventos puntuales de un auto (choque, power-up) van por RPC. Encima de la materia agregamos reconexión automática, voz por proximidad, auth contra Google Sheets, LiveOps con Remote Config, y bots de IA local que se instancian con Instantiation Data (sin RPC), migran de dueño solos cuando cambia el Master Client, y nunca cuentan como jugador real para el matchmaking ni el leaderboard — todos con manejo explícito de fallas (fail-open, reintentos, no-op si falta config)."
