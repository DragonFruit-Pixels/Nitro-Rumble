using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RaceManager : Singleton<RaceManager>, IOnEventCallback, IInRoomCallbacks
{
    [Header("Race Settings")]
    [SerializeField] private int   _totalLaps          = 3;
    [SerializeField] private float _countdownDuration  = 3f;
    [SerializeField] private float _raceHardTimeout    = 300f; // s máx de carrera antes de forzar el podio pase lo que pase
    [SerializeField] private float _racerWaitTimeout   = 30f; // s máx esperando autos antes de arrancar igual
    [SerializeField] private float _aloneGracePeriod   = 12f; // s esperando reconexión antes de volver al lobby

    [Tooltip("Cuántos checkpoints seguidos se toleran salteados antes de descartar el paso. " +
             "Evita que perder un gate congele el progreso del auto para el resto de la carrera.")]
    [SerializeField] private int   _maxSkippedCheckpoints = 2;

    [Tooltip("Cada cuánto se recalculan las posiciones mientras se corre (s). Sin esto la " +
             "posición solo cambiaría al cruzar un checkpoint, no al pasar a un rival.")]
    [SerializeField] private float _positionUpdateInterval = 0.2f;

    [Header("Track")]
    [Tooltip("Arrastrar los checkpoints en orden. El último es la meta.")]
    [SerializeField] private Checkpoint[] _checkpoints;

    public int       TotalLaps      => _totalLaps;
    public RaceState State          { get; private set; } = RaceState.Waiting;
    public double    ExactStartTime { get; private set; }
    // Cuantos jugadores humanos reales habia al arrancar — usado para no contaminar
    // el registro de resultados con partidas solo-humano-vs-bots.
    public int       RealPlayerCountAtStart { get; private set; }

    public int FinishLineIndex => _checkpoints.Length - 1;

    private const byte EVENT_RACE_START    = 1;
    private const byte EVENT_REPORT_FINISH = 2; // cliente → MC: crucé la meta
    private const byte EVENT_PODIUM        = 3; // MC → todos: podio oficial
    private const byte EVENT_COUNTDOWN     = 4; // MC → todos: alguien terminó, arranca el countdown

    public event Action<int>           OnCountdown;
    public event Action                OnRaceStart;
    public event Action<Racer>         OnLapCompleted;
    public event Action<Racer>         OnRaceFinished;
    public event Action                OnPositionsUpdated;
    public event Action                OnLocalRacerFinished;  // solo se dispara en el cliente del auto que termina
    public event Action<string, float> OnCountdownStarted;    // nombre del primero + segundos restantes
    public event Action<float>         OnAloneGracePeriodStarted;
    public event Action                OnAloneGracePeriodCancelled;

    private readonly List<Racer> _racers = new();
    private Coroutine _racerWaitTimeoutRoutine;
    private Coroutine _aloneRoutine;
    private Coroutine _positionRoutine;
    private Coroutine _hardTimeoutRoutine;

    // Progreso calculado una vez por UpdatePositions y reusado por el comparador, en lugar de
    // recalcularlo en cada comparación del Sort.
    private readonly Dictionary<Racer, float> _progressCache = new();

    // El MC acumula los reportes de meta y los ordena por ServerTimestamp.
    private struct FinishRecord
    {
        public int    racerViewId;
        public int    serverTimestamp;
        public double raceTime;
    }
    private readonly List<FinishRecord> _finishRecords = new();
    private Coroutine _podiumTimeoutRoutine;

    #region Unity

    private void Start()
    {
        // Recalculate ambient probe from the scene's skybox.
        // Needed because the previous scene (Lobby) has no skybox, leaving a stale dark probe.
        DynamicGI.UpdateEnvironment();

        // Si hay un campeonato activo, usar las vueltas configuradas por el host.
        if (ChampionshipManager.Instance != null && ChampionshipManager.Instance.IsActive)
            _totalLaps = ChampionshipManager.Instance.TotalLaps;
    }

    private void OnEnable()  => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    #endregion

    #region Registration

public void RegisterRacer(Racer racer)
    {
        if (!_racers.Contains(racer))
            _racers.Add(racer);

        Logger.Log($"[RaceManager] Racer registrado: {racer.name} — total: {_racers.Count}");

        racer.SetCanMove(false);

        // Los bots no cuentan para "cuantos humanos hacen falta para arrancar" — se registran
        // igual (deben existir antes del arranque) pero no inflan ni tapan el conteo real.
        int humanRacers = 0;
        foreach (var r in _racers)
            if (!r.IsBot) humanRacers++;

        bool allReady = PhotonNetwork.IsConnected && PhotonNetwork.InRoom
            ? humanRacers >= GetActivePlayerCount()
            : humanRacers >= 1;

        if (allReady)
        {
            if (_racerWaitTimeoutRoutine != null) { StopCoroutine(_racerWaitTimeoutRoutine); _racerWaitTimeoutRoutine = null; }
            StartCountdown();
            return;
        }

        // Primer racer registrado: arrancar el timeout por si algún jugador se desconectó durante la carga.
        if (_racers.Count == 1 && PhotonNetwork.IsMasterClient && _racerWaitTimeoutRoutine == null)
            _racerWaitTimeoutRoutine = StartCoroutine(RacerWaitTimeoutRoutine());
    }

    private IEnumerator RacerWaitTimeoutRoutine()
    {
        yield return new WaitForSeconds(_racerWaitTimeout);
        if (State == RaceState.Waiting && _racers.Count > 0)
        {
            Logger.LogWarning($"[RaceManager] Timeout esperando autos ({_racers.Count}/{GetActivePlayerCount()}). Arrancando igual.");
            StartCountdown();
        }
        _racerWaitTimeoutRoutine = null;
    }

    #endregion

    #region Countdown

    private void StartCountdown()
    {
        if (State != RaceState.Waiting) return;
        State = RaceState.Countdown;

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                double startTime = PhotonNetwork.Time + _countdownDuration;
                Logger.Log($"[RaceManager] Master envía startTime: {startTime:F3}");

                PhotonNetwork.RaiseEvent(
                    EVENT_RACE_START,
                    startTime,
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    SendOptions.SendReliable
                );
            }
        }
        else
        {
            StartCoroutine(LocalCountdown());
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case EVENT_RACE_START:
            {
                double startTime = (double)photonEvent.CustomData;
                Logger.Log($"[RaceManager] startTime recibido: {startTime:F3} | ahora: {PhotonNetwork.Time:F3}");
                StartCoroutine(SyncedCountdown(startTime));
                break;
            }
            case EVENT_REPORT_FINISH:
                HandleFinishReport((RaceResultPackage)photonEvent.CustomData);
                break;
            case EVENT_PODIUM:
                HandlePodiumEvent((object[])photonEvent.CustomData);
                break;
            case EVENT_COUNTDOWN:
            {
                var cd = (object[])photonEvent.CustomData;
                OnCountdownStarted?.Invoke((string)cd[0], (float)cd[1]);
                break;
            }
        }
    }

    // Todos los clientes corren este coroutine con el mismo startTime → se sincronizan solos.
    private IEnumerator SyncedCountdown(double startTime)
    {
        ExactStartTime = startTime;
        bool emitted3 = false, emitted2 = false, emitted1 = false;

        while (PhotonNetwork.Time < startTime)
        {
            double remaining = startTime - PhotonNetwork.Time;

            if (!emitted3 && remaining <= 3.0) { OnCountdown?.Invoke(3); Logger.Log("[RaceManager] Countdown: 3"); emitted3 = true; }
            if (!emitted2 && remaining <= 2.0) { OnCountdown?.Invoke(2); Logger.Log("[RaceManager] Countdown: 2"); emitted2 = true; }
            if (!emitted1 && remaining <= 1.0) { OnCountdown?.Invoke(1); Logger.Log("[RaceManager] Countdown: 1"); emitted1 = true; }

            yield return null;
        }

        FireGo();
    }

    private IEnumerator LocalCountdown()
    {
        float step = _countdownDuration / 3f;
        OnCountdown?.Invoke(3); Logger.Log("[RaceManager] Countdown: 3");
        yield return new WaitForSeconds(step);
        OnCountdown?.Invoke(2); Logger.Log("[RaceManager] Countdown: 2");
        yield return new WaitForSeconds(step);
        OnCountdown?.Invoke(1); Logger.Log("[RaceManager] Countdown: 1");
        yield return new WaitForSeconds(step);
        FireGo();
    }

private void FireGo()
    {
        Logger.Log("[RaceManager] GO!");

        // Offline: ExactStartTime no se fijó en SyncedCountdown; usar el momento actual.
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            ExactStartTime = PhotonNetwork.Time;

        // Snapshot de jugadores humanos reales al arrancar — usado después para no subir
        // al registro de resultados una partida que fue solo-humano-vs-bots.
        RealPlayerCountAtStart = GetActivePlayerCount();

        OnCountdown?.Invoke(0);
        State = RaceState.Racing;
        foreach (var racer in _racers)
            racer.SetCanMove(true);
        UpdatePositions();
        OnRaceStart?.Invoke();

        if (_positionRoutine != null) StopCoroutine(_positionRoutine);
        _positionRoutine = StartCoroutine(PositionUpdateRoutine());

        // Red de seguridad: si nadie llega a cruzar la meta (auto trabado, checkpoint perdido,
        // jugador AFK), la partida igual cierra en vez de quedar colgada para siempre.
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient
            && _hardTimeoutRoutine == null)
            _hardTimeoutRoutine = StartCoroutine(RaceHardTimeoutRoutine());
    }

    private IEnumerator RaceHardTimeoutRoutine()
    {
        yield return new WaitForSeconds(_raceHardTimeout);
        _hardTimeoutRoutine = null;

        if (State == RaceState.Finished) yield break;

        Logger.LogWarning($"[RaceManager MC] Timeout duro de carrera ({_raceHardTimeout:F0}s) — cerrando la partida con los resultados que haya.");
        BroadcastPodium();
    }

    #endregion

    #region Checkpoint Logic

    public void NotifyCheckpoint(Racer racer, Checkpoint checkpoint)
    {
        if (State != RaceState.Racing) return;

        int index = Array.IndexOf(_checkpoints, checkpoint);
        if (index < 0)
        {
            Logger.LogWarning($"[RaceManager] Checkpoint no registrado en el array: {checkpoint.name}");
            return;
        }

        if (index == FinishLineIndex)
        {
            HandleFinishLine(racer);
        }
        else
        {
            // Se tolera perder algún gate (línea rara, respawn, empujón). Antes cualquier salto
            // se descartaba en silencio y el racer quedaba congelado en su último checkpoint
            // para el resto de la carrera: ni avanzaba de posición ni podía completar vuelta.
            int expected = racer.LastCheckpoint + 1;
            int skipped  = index - expected;

            if (skipped < 0 || skipped > _maxSkippedCheckpoints)
            {
                Logger.Log($"[RaceManager] {racer.name} pisó checkpoint {index} fuera de orden (esperado: {expected}) — ignorado");
                return;
            }

            racer.AdvanceCheckpoint(index);

            if (skipped > 0)
                Logger.LogWarning($"[RaceManager] {racer.name} salteó {skipped} checkpoint(s) — se cuenta el {index} igual (esperado: {expected})");
            else
                Logger.Log($"[RaceManager] {racer.name} ✓ checkpoint {index}/{FinishLineIndex - 1}");

            UpdatePositions();
        }
    }

    private void HandleFinishLine(Racer racer)
    {
        int minCheckpoint = FinishLineIndex - 1 - _maxSkippedCheckpoints;
        if (racer.LastCheckpoint < minCheckpoint)
        {
            Logger.Log($"[RaceManager] {racer.name} cruzó la meta sin completar los checkpoints (último: {racer.LastCheckpoint}, mínimo: {minCheckpoint}) — ignorado");
            return;
        }

        racer.CompleteLap();
        Logger.Log($"[RaceManager] {racer.name} completó vuelta {racer.CurrentLap}/{_totalLaps}");
        OnLapCompleted?.Invoke(racer);
        UpdatePositions();

        if (racer.CurrentLap < _totalLaps) return;

        // ── Última vuelta: el dueño del auto reporta al MC en lugar de decidir localmente ──
        bool isLocalRacer = PhotonViewAuthority.HasLocalInputAuthority(racer.photonView);

        if (!isLocalRacer) return; // solo el cliente propietario reporta su propio auto

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Notificar a todos los clientes que este auto terminó (deshabilita sus colisiones).
            racer.photonView.RPC(nameof(Racer.RPC_SetFinished), RpcTarget.All);

            // Mostrar banner FINISH solo en la pantalla del jugador local — nunca para un bot
            // (el Master Client es "dueno" del bot tambien, pero no es SU auto).
            if (!racer.IsBot)
                OnLocalRacerFinished?.Invoke();

            // TiempoFinal = PhotonNetwork.Time - exactStartTime (reloj universal, sin depender de FPS)
            double raceTime = PhotonNetwork.Time - ExactStartTime;

            // Tipo custom registrado (PhotonCustomTypes): viaja serializado por nuestros propios
            // métodos en lugar de un object[] sin tipo.
            var result = new RaceResultPackage(
                racer.photonView.ViewID,
                racer.PlayerName,
                (float)raceTime,
                PhotonNetwork.ServerTimestamp
            );
            PhotonNetwork.RaiseEvent(
                EVENT_REPORT_FINISH,
                result,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable
            );
            Logger.Log($"[RaceManager] {racer.name} reportó finish al MC — raceTime: {raceTime:F2}s");
        }
        else
        {
            racer.RPC_SetFinished(); // offline: llamada directa, no hay red
            if (!racer.IsBot)
                OnLocalRacerFinished?.Invoke();
            State = RaceState.Finished;
            racer.SetCanMove(false);
            Logger.Log($"[RaceManager] {racer.name} GANÓ (offline) en {racer.RaceTime:F2}s");
            OnRaceFinished?.Invoke(racer);
        }
    }

    #endregion

    #region Podio — Master Client

    // Solo el MC procesa este evento. Acumula finishes y emite el podio cuando todos terminaron.
    private void HandleFinishReport(RaceResultPackage result)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int    racerViewId     = result.RacerViewId;
        int    serverTimestamp = result.ServerTimestamp;
        double raceTime        = result.RaceTime;

        // Deduplicar (Photon SendReliable puede reintentar en caso de pérdida de paquete)
        foreach (var record in _finishRecords)
            if (record.racerViewId == racerViewId) return;

        _finishRecords.Add(new FinishRecord
        {
            racerViewId     = racerViewId,
            serverTimestamp = serverTimestamp,
            raceTime        = raceTime
        });

        Logger.Log($"[RaceManager MC] Finish registrado: viewId={racerViewId}, time={raceTime:F2}s ({_finishRecords.Count}/{_racers.Count})");

        // Si es el primer finisher, calcular timeout dinámico basado en su tiempo promedio
        // por vuelta y arrancar la cuenta regresiva para los que aún no terminaron.
        if (_finishRecords.Count == 1 && _podiumTimeoutRoutine == null)
        {
            float avgLapTime     = (float)(raceTime / _totalLaps);
            float dynamicTimeout = Mathf.Max(30f, avgLapTime * 1.5f);
            Logger.Log($"[RaceManager MC] Timeout dinámico: {dynamicTimeout:F1}s (avgLap={avgLapTime:F1}s)");

            // Notificar a todos para que muestren el countdown en el HUD.
            string finisherName = PhotonView.Find(racerViewId)?.GetComponent<Racer>()?.PlayerName ?? LocalizationManager.Get("race.unknownPlayer");
            object[] cdPayload = { finisherName, dynamicTimeout };
            PhotonNetwork.RaiseEvent(
                EVENT_COUNTDOWN,
                cdPayload,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable
            );

            _podiumTimeoutRoutine = StartCoroutine(PodiumTimeoutRoutine(dynamicTimeout));
        }

        if (_finishRecords.Count >= _racers.Count)
            BroadcastPodium();
    }

    private IEnumerator PodiumTimeoutRoutine(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        if (State != RaceState.Finished)
        {
            Logger.Log("[RaceManager MC] Timeout de podio — emitiendo con los que terminaron");
            BroadcastPodium();
        }
    }

    private void BroadcastPodium()
    {
        if (State == RaceState.Finished) return;
        State = RaceState.Finished;

        if (_podiumTimeoutRoutine != null)
        {
            StopCoroutine(_podiumTimeoutRoutine);
            _podiumTimeoutRoutine = null;
        }

        if (_hardTimeoutRoutine != null)
        {
            StopCoroutine(_hardTimeoutRoutine);
            _hardTimeoutRoutine = null;
        }

        // Ordenar por ServerTimestamp: resta unchecked maneja el wrap-around del int (~24 días).
        _finishRecords.Sort((a, b) =>
            unchecked(a.serverTimestamp - b.serverTimestamp));

        int[]    orderedViewIds = new int[_finishRecords.Count];
        double[] raceTimes      = new double[_finishRecords.Count];
        for (int i = 0; i < _finishRecords.Count; i++)
        {
            orderedViewIds[i] = _finishRecords[i].racerViewId;
            raceTimes[i]      = _finishRecords[i].raceTime;
        }

        object[] podiumPayload = { orderedViewIds, raceTimes };
        PhotonNetwork.RaiseEvent(
            EVENT_PODIUM,
            podiumPayload,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable
        );

        Logger.Log("[RaceManager MC] Podio oficial emitido.");
    }

    // Todos los clientes (incluido el MC) reciben el podio canónico.
    private void HandlePodiumEvent(object[] payload)
    {
        int[]    orderedViewIds = (int[])payload[0];
        double[] raceTimes      = (double[])payload[1];

        State = RaceState.Finished;

        // Asignar posiciones a los que terminaron (orden del podio).
        var finishedSet = new HashSet<Racer>();
        Racer winner = null;
        for (int i = 0; i < orderedViewIds.Length; i++)
        {
            PhotonView pv = PhotonView.Find(orderedViewIds[i]);
            if (pv == null) continue;

            Racer racer = pv.GetComponent<Racer>();
            if (racer == null) continue;

            racer.Position   = i + 1;
            racer.FinishTime = raceTimes[i];
            racer.SetCanMove(false);
            finishedSet.Add(racer);

            if (i == 0) winner = racer;
        }

        // Asignar posiciones a los DNF por el progreso real que tenían al final.
        var dnfRacers = new List<Racer>();
        foreach (var racer in _racers)
            if (!finishedSet.Contains(racer)) dnfRacers.Add(racer);

        dnfRacers.Sort((a, b) => GetProgress(b).CompareTo(GetProgress(a)));

        int dnfPosition = orderedViewIds.Length + 1;
        foreach (var racer in dnfRacers)
        {
            racer.Position   = dnfPosition++;
            racer.FinishTime = 0;
            racer.SetCanMove(false);
        }

        // Ordenar _racers por posición final para que GetRacers() devuelva el podio en orden.
        _racers.Sort((a, b) => a.Position.CompareTo(b.Position));

        // Podio vacío (timeout duro sin ningún finisher): el "ganador" es el que más lejos
        // llegó. Sin esto el evento no se emitía y la partida quedaba sin cerrar en la UI.
        if (winner == null && _racers.Count > 0)
            winner = _racers[0];

        // Guardar récord local del jugador en este cliente.
        // LocalSaveManager.Instance puede ser null si el GameObject no está en escena todavía.
        SaveLocalRecord();

        Logger.Log($"[RaceManager] Podio recibido — ganador: {winner?.name ?? "?"}");
        if (winner != null)
            OnRaceFinished?.Invoke(winner);
    }

    #endregion

    #region Local Save

    private void SaveLocalRecord()
    {
        if (LocalSaveManager.Instance == null) return;

        // Buscar al racer local en la lista ya ordenada por posición.
        Racer localRacer = null;
        foreach (var r in _racers)
        {
            bool isLocal = PhotonViewAuthority.IsLocalHumanRacer(r.photonView);
            if (isLocal) { localRacer = r; break; }
        }

        if (localRacer == null) return;

        string trackId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        LocalSaveManager.Instance.OnRaceCompleted(trackId, localRacer.FinishTime, localRacer.Position, _racers.Count);

        // Sincronizar nickname por si cambió en esta sesión.
        if (!string.IsNullOrEmpty(Photon.Pun.PhotonNetwork.NickName))
            LocalSaveManager.Instance.SaveNickname(Photon.Pun.PhotonNetwork.NickName);
    }

    #endregion

    #region Positions

    // Recalcula posiciones a intervalo fijo mientras se corre. Sin esto la posición solo
    // cambiaba al pisar un checkpoint: pasabas al rival en la recta y el HUD no se enteraba
    // hasta el próximo gate.
    private IEnumerator PositionUpdateRoutine()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, _positionUpdateInterval));
        while (State == RaceState.Racing)
        {
            UpdatePositions();
            yield return wait;
        }
        _positionRoutine = null;
    }

    // Progreso continuo: vuelta + último checkpoint + fracción recorrida hacia el siguiente.
    // La parte fraccionaria es lo que permite ordenar dos autos que están entre los mismos
    // gates; con la clave entera vieja empataban y el orden lo decidía el azar del Sort.
    private float GetProgress(Racer racer)
    {
        int   total    = _checkpoints.Length;
        float discrete = racer.CurrentLap * total + racer.LastCheckpoint;

        // Con LastCheckpoint == -1 (recién largó o recién cruzó la meta) el tramo actual es
        // el que va de la meta al primer checkpoint.
        Transform from = GetCheckpointTransform(racer.LastCheckpoint >= 0 ? racer.LastCheckpoint : FinishLineIndex);
        Transform to   = GetCheckpointTransform(racer.LastCheckpoint + 1);
        if (from == null || to == null) return discrete;

        Vector3 segment   = to.position - from.position;
        float   sqrLength = segment.sqrMagnitude;
        if (sqrLength < 0.01f) return discrete;

        float t = Mathf.Clamp01(Vector3.Dot(racer.WorldPosition - from.position, segment) / sqrLength);
        return discrete + t;
    }

    private void UpdatePositions()
    {
        if (_checkpoints == null || _checkpoints.Length == 0) return;

        // Un jugador que se va deja su auto destruido. Esto ahora corre 5 veces por segundo,
        // así que una referencia muerta sería spam de excepciones en vez de un error aislado.
        _racers.RemoveAll(racer => racer == null);

        _progressCache.Clear();
        foreach (var racer in _racers)
            _progressCache[racer] = GetProgress(racer);

        _racers.Sort((a, b) =>
        {
            int byProgress = _progressCache[b].CompareTo(_progressCache[a]);
            if (byProgress != 0) return byProgress;
            // Desempate determinístico: List.Sort es inestable, así que sin esto dos autos
            // empatados se intercambian de posición al azar y el HUD parpadea.
            return GetRacerId(a).CompareTo(GetRacerId(b));
        });

        bool changed = false;
        for (int i = 0; i < _racers.Count; i++)
        {
            if (_racers[i].Position != i + 1) changed = true;
            _racers[i].Position = i + 1;
        }

        // Solo logueamos cuando el orden cambió: esto corre 5 veces por segundo.
        if (changed)
            foreach (var racer in _racers)
                Logger.Log($"[RaceManager] P{racer.Position}: {racer.name} (vuelta {racer.CurrentLap}, checkpoint {racer.LastCheckpoint})");

        OnPositionsUpdated?.Invoke();
    }

    private static int GetRacerId(Racer racer) =>
        racer.photonView != null ? racer.photonView.ViewID : racer.GetInstanceID();

    public IReadOnlyList<Racer> GetRacers() => _racers;

    public Transform GetCheckpointTransform(int index)
    {
        if (_checkpoints == null || index < 0 || index >= _checkpoints.Length) return null;
        return _checkpoints[index].transform;
    }

    #endregion

    #region IInRoomCallbacks — solo player detection

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Si la carrera ya terminó o no estamos en sala, no hacer nada.
        if (State == RaceState.Finished || !PhotonNetwork.InRoom) return;

        // Solo activar si nos quedamos solos.
        if (GetActivePlayerCount() > 1) return;

        Logger.LogWarning("[RaceManager] Quedaste solo en la carrera. Esperando reconexión...");
        OnAloneGracePeriodStarted?.Invoke(_aloneGracePeriod);
        if (_aloneRoutine != null) StopCoroutine(_aloneRoutine);
        _aloneRoutine = StartCoroutine(AloneGracePeriodRoutine());
    }

    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Alguien reconectó — cancelar el retorno al lobby.
        if (_aloneRoutine == null) return;
        StopCoroutine(_aloneRoutine);
        _aloneRoutine = null;
        OnAloneGracePeriodCancelled?.Invoke();
        Logger.Log($"[RaceManager] {newPlayer.NickName} reconectó. Continuando la carrera.");
    }

    private IEnumerator AloneGracePeriodRoutine()
    {
        yield return new WaitForSeconds(_aloneGracePeriod);

        // Si después del grace period seguimos solos, volver al lobby.
        if (!PhotonNetwork.InRoom || GetActivePlayerCount() > 1) yield break;

        Logger.LogWarning("[RaceManager] Nadie reconectó. Volviendo al lobby...");
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel("Lobby");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    // Métodos requeridos por IInRoomCallbacks que no necesitamos usar.
    public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) { }
    public void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) { }
    public void OnMasterClientSwitched(Player newMasterClient)
    {
        // Si soy el nuevo MC, tomo el manejo de los bots (su PhotonView usa OwnershipTransfer
        // = Takeover). El resto (CarController/CarNetworkSync/CarDriftBoost/CarStretchSquash)
        // no necesita saber nada de esto: en cuanto la ownership pasa, HasLocalInputAuthority
        // empieza a dar true aca y todo arranca solo en esta maquina.
        if (!PhotonNetwork.LocalPlayer.Equals(newMasterClient)) return;

        foreach (var racer in _racers)
        {
            if (racer != null && racer.IsBot && racer.photonView != null)
                racer.photonView.TransferOwnership(newMasterClient);
        }
    }

    private static int GetActivePlayerCount()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return 0;

        int count = 0;
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (!player.IsInactive)
                count++;
        }

        return count;
    }

    #endregion
}

public enum RaceState { Waiting, Countdown, Racing, Finished }
