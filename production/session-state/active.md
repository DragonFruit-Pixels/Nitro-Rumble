# Session State

- **Project:** Unnamed Multiplayer Racing Game
- **Division:** Game Dev
- **Stage:** D) Development (active)
- **User role:** Mix (design + dev)
- **Session focus:** Explorar el código / entender sistemas
- **Date:** 2026-06-12

## Stack detected
- Unity 2021.3.16f1
- Photon PUN (multiplayer realtime) + Photon Chat
- ParrelSync (multi-instance local testing)
- Arcade car physics: sphere-rigidbody + visual container pattern

## Code structure (Assets/Scripts/)
- **Car/** — CarController (sphere physics), CarDriftBoost, CarNetworkSync, collision (arcade, side, tuning, ram-destroy, wall-impact), CarVisuals, CarAudio, CarCamera
- **PowerUps/** — Database, Type, Inventory(+Hud), Effects, Box(+Placer), EmpProjectile, PickupManager, Visuals
- **Race/** — RaceManager (Singleton + Photon event sync), Racer, Checkpoint
- **Network/** — GameBootstrap (test-only), NetworkManager, MatchmakingManager, PlayerSpawner, ReconnectionManager
- **UI/** — HUD panels (item/lap/position), Minimap, ReviveUIPanel
- **Scenes/** — GameSceneManager, TransitionManager
- **Utility/** — Singleton, Timer, FollowTransform, WeightedNode

## Key architecture notes
- RaceManager: Master Client raises EVENT_RACE_START with PhotonNetwork.Time → synced countdown across clients. Checkpoint order enforced; positions = lap*total + lastCheckpoint, tiebreak by RaceTime.
- CarController: separates physics (sphere Rigidbody) from visuals (container). Local authority via photonView.IsMine; remote state injected via SetRemoteState.
- GameBootstrap is test-only (disable in final build).

## Recommended flow
- `/context-prime` — load full project context
- `/audit-game` — GDD vs code consistency (note: no GDD/design docs found yet)
- `/code-review` — review a specific system

## Gaps noted
- No design/ or docs/ GDD found — design is implicit in code.

## Work done this session — Firebase leaderboard (time-ranked)
- **Racer.cs:** + HasFinished, MarkFinished(), TopSpeed tracking, IsLocal→IsLocalPlayer (público).
- **RaceManager.cs:** + OnRacerFinished event; per-racer finish decoupled from global race-end (winner = first finisher, others keep racing to record their own time); NotifyCheckpoint gate relaxed.
- **Leaderboard/LeaderboardService.cs:** Firebase RTDB client (Singleton). Real impl behind `FIREBASE_ENABLED` define; no-op stub otherwise so project compiles without SDK.
- **Leaderboard/ScoreEntry.cs, RaceResultReporter.cs:** reporter sube tiempo del jugador local en OnRacerFinished.
- **UI/LeaderboardPanel.cs, LeaderboardRow.cs:** tabla top-N por tiempo ascendente.
- **MainMenuHandler.cs:** input de nombre → PlayerPrefs("PlayerName") + PhotonNetwork.NickName.

## Pending (manual, in Unity Editor / Firebase Console)
1. Crear proyecto Firebase + Realtime Database; importar Firebase Unity SDK; google-services/databaseURL.
2. Player Settings → Scripting Define Symbols → agregar `FIREBASE_ENABLED`.
3. Wiring: TMP_InputField de nombre en menú; RaceResultReporter en escena de juego; LeaderboardService en escena bootstrap; prefab de fila (LeaderboardRow) + panel.
4. Validar con ParrelSync 2 instancias: ganador Y perdedor suben su tiempo.
