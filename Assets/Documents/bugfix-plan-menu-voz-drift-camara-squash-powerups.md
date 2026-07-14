# Fix 6 bugs: menú, voz, conducción, cámara, squash sync, power-up boxes

## Contexto

El usuario reportó varios problemas jugando builds recientes: no se puede volver al menú desde la sala, la voz no funciona y no hay indicador, el auto derrapa como en hielo, la cámara rota de forma muy exagerada siguiendo al auto, el stretch/squash no se ve, y los power-up boxes no tienen una animación de rotación sincronizada entre clientes. Se investigó cada uno leyendo el código (sin tocar nada todavía) y se encontró causa raíz concreta para 5 de los 6; el de voz requiere además una verificación en vivo porque el wiring estático se ve correcto.

## 1. No se puede volver al menú desde la sala

`RequestLeaveRoom` (LobbyHandler.cs → MatchmakingManager) solo hace `PhotonNetwork.LeaveRoom()`, lo que vuelve a la pantalla "join or create" **dentro de Lobby.unity**, pero el jugador sigue conectado a Photon. `GameSceneManager.cs` solo carga `Menu.unity` cuando `NetworkManager` pasa a `Offline`, y el ÚNICO lugar del código que llama `PhotonNetwork.Disconnect()` es `ReconnectionManager.cs:116` (caso de reconexión fallida). No existe ningún botón/comando para volver al menú principal — falta por completo, no está roto.

**Fix:**
- `NetworkManager.cs`: agregar `RequestDisconnect()` que llama `PhotonNetwork.Disconnect()`.
- `LobbyHandler.cs`: agregar `RequestReturnToMainMenu()` a `ILobbyHandlerCommands`, implementación llama `NetworkManager.Instance.RequestDisconnect()`.
- Escena `Lobby.unity`: agregar botón "Volver al Menú" en la pantalla `_joinOrCreateScreen` (mismo estilo Kenney que el botón Leave), wireado directo en `LobbyHandler` (nuevo `[SerializeField] Button _backToMenuButton` + listener en Awake).

## 2. Voz: sin indicador + reporte de "no funciona"

Revisado `Car.prefab`: `AppIdVoice` está configurado en `PhotonServerSettings.asset`, el `Recorder` tiene `transmitEnabled/recordingEnabled/recordWhenJoined` todos en 1, `PunVoiceClient` está en la escena. Wiring estático correcto. Pero **no existe ningún indicador visual de voz en todo el código** (confirmado por grep) — el usuario tiene razón en que no hay nada que muestre si está funcionando.

**Fix:**
- Nuevo componente `VoiceActivityIndicator.cs` en `Assets/Scripts/Network/Voice/`: en el auto local, lee `Recorder.IsCurrentlyTransmitting` cada frame; en autos remotos, lee actividad del `Speaker`. Prende/apaga un ícono world-space (billboard simple) sobre el auto.
- Agregar el ícono como hijo del `Car.prefab` (mismo patrón que otros hijos visuales), asignar el componente vía Unity MCP.
- Como el wiring estático ya se ve bien, el paso de "no funciona" se valida en vivo con 2 clientes ParrelSync durante la verificación (abajo) — si aparece una falla real (dispositivo de micrófono, permisos, ruteo del Speaker) se corrige ahí mismo con logs de consola.

## 3. Conducción: auto derrapa como en hielo

`CarController.cs` no tiene ningún agarre lateral durante manejo normal. `CarDriftBoost.FixedUpdate()` solo llama `_controller.ApplyDriftVelocityDrag(...)` cuando `_isDriftButtonHeld` es true — fuera de eso, nada frena la velocidad lateral del Rigidbody-esfera, por eso el auto resbala en cualquier curva como si no hubiera fricción.

**Fix (reutiliza el método existente, no crea uno nuevo):**
En `CarDriftBoost.cs` FixedUpdate, agregar el `else`:
```csharp
if (_isDriftButtonHeld)
    _controller.ApplyDriftVelocityDrag(_driftForwardDrag, _driftLateralDrag);
else
    _controller.ApplyDriftVelocityDrag(0f, _normalGripLateralDrag);
```
Nuevo campo `[SerializeField] private float _normalGripLateralDrag = 8f;` (fuerte, mata el deslizamiento lateral sin frenar hacia adelante). Cambio mínimo, mismo método `ApplyDriftVelocityDrag` ya probado, solo cambia cuándo y con qué coeficientes se llama.

## 4. Cámara: seguimiento y rotación muy exagerados

`CarCamera.UpdatePosition()` (Assets/Scripts/Car/CarCamera.cs:91-107) hace `transform.LookAt(...)` **instantáneo, sin ningún suavizado** — cada frame la rotación de la cámara salta directo a mirar exactamente hacia donde apunta el auto. La posición sí tiene un `Lerp` con `_followSpeed`, pero la rotación no tiene ninguno. Como el auto puede girar rápido (`CarController` permite hasta ~4 rad/s de giro angular con el steering al máximo, más el multiplicador de drift 1.45x), la cámara gira junto con el auto de forma instantánea y brusca en vez de ir un poco atrás suavizada, que es la sensación típica de cámara de persecución en juegos de carreras.

**Fix:**
- Reemplazar el `transform.LookAt(...)` directo por una rotación suavizada:
```csharp
Quaternion targetRotation = Quaternion.LookRotation(
    (car.position + car.forward * _lookAheadOffset + Vector3.up) - transform.position);
transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
```
- Nuevo campo `[SerializeField] private float _rotationSpeed = 5f;` (tunable, probablemente un poco más lento que `_followSpeed` para que la cámara "se quede atrás" un poco al girar, dando sensación de peso/inercia).
- `UpdateTilt()` sigue aplicando el tilt en `.z` después, sin cambios — se combina bien porque solo pisa el eje Z del euler ya calculado.
- Esto también ayuda a que el drift/derrape del punto 3 se sienta mejor (la cámara no exagera cada corrección de grip).

## 5. Stretch & squash sincronizado no se ve

`CarStretchSquash._squashTarget` en `Car.prefab` apunta a la transform `vehicle-truck-yellow` — que es EXACTAMENTE la misma transform que `CarController._container` / `CarNetworkSync._container` (el "container raíz" que el propio tooltip del script dice explícitamente no usar). Retargetear directo al hijo `body` no sirve: `body` tiene una rotación local de -90° en X, así que su eje Z local no es el "adelante" del auto — escalarlo estiraría en el eje equivocado (vertical en vez de adelante/atrás).

**Fix (prefab, Prefab Stage):**
- Crear un GameObject vacío `SquashPivot` hijo de `vehicle-truck-yellow`, con transform local identidad (posición/rotación 0, escala 1) — así su eje Z local sigue siendo "adelante del auto".
- Reparentar `body` (único hijo con mesh propio, sin hijos) dentro de `SquashPivot`, preservando su local transform actual (como el pivot es identidad, los valores no cambian).
- `CarStretchSquash._squashTarget` → `SquashPivot`.
- Subir un poco la magnitud para que se note: `_stretchAmount` 0.15 → 0.3, `_boostStretch` 0.5 → 0.8.
- No hace falta tocar `CarNetworkSync` — el bitmask `DIRTY_SQUASH` ya replica `CurrentSquash` a los remotos correctamente, el problema era solo qué transform se deformaba.

## 6. Cuadrados CON animación de rotación sincronizada

Foco del punto: el cuadrado tiene que quedar **CON** una animación realmente sincronizada entre todos los clientes (hoy no la tiene). `PowerUpBox.Update()` rota y hace bob usando `Time.deltaTime`/`Time.time` locales — sin PhotonView ni referencia de red. Cada cliente acumula su propia rotación desde que cargó la escena, así que todos ven ángulos distintos en el mismo instante (drift de fase entre clientes) — no hay ninguna garantía de sync hoy.

**Fix (cero costo de red, no necesita PhotonView ni RPC — la sync sale gratis del reloj compartido):**
```csharp
private void Update()
{
    if (!_available || _visualRoot == null) return;

    float t = (float)PhotonNetwork.Time;
    _visualRoot.localRotation = Quaternion.Euler(0f, (t * _rotationSpeed) % 360f, 0f);
    _visualRoot.localPosition = _startLocalPosition + Vector3.up * (Mathf.Sin(t * _bobSpeed) * _bobAmplitude);
    UpdateColor(t);
}
```
`UpdateColor` también pasa a usar `t` en vez de `Time.time` para que el ciclo de color coincida entre clientes. `PhotonNetwork.Time` es el reloj de red compartido — todos calculan el mismo ángulo absoluto en el mismo momento, sin RPC ni Observable.

## Archivos a tocar

- `Assets/Scripts/Network/NetworkManager.cs` (nuevo `RequestDisconnect`)
- `Assets/Scripts/Lobby/LobbyHandler.cs` (nuevo comando + botón)
- `Assets/Scenes/Lobby.unity` (botón "Volver al Menú")
- `Assets/Scripts/Network/Voice/VoiceActivityIndicator.cs` (nuevo)
- `Assets/Resources/Car.prefab` (indicador de voz, SquashPivot, tuning squash)
- `Assets/Scripts/Car/CarDriftBoost.cs` (grip normal)
- `Assets/Scripts/Car/CarCamera.cs` (rotación suavizada del seguimiento)
- `Assets/Scripts/Car/CarStretchSquash.cs` (tuning, opcional)
- `Assets/Scripts/PowerUps/PowerUpBox.cs` (rotación por PhotonNetwork.Time)

## Verificación

1. `read_console` tras cada compilación (0 errores).
2. Play Mode con 2 instancias ParrelSync:
   - Crear sala → botón "Volver al Menú" visible y funcional (vuelve a Menu.unity, desconecta Photon).
   - Manejar: confirmar que el auto ya no resbala en curvas normales, y que Shift sigue dando el drift/boost intencional.
   - Girar bruscamente / driftear: confirmar que la cámara sigue con un poco de inercia en vez de rotar instantáneo con el auto.
   - Acelerar/boostear: confirmar que el cuerpo del auto se estira/aplasta visiblemente, tanto en el auto local como visto desde el otro cliente.
   - Power-up boxes: confirmar mismo ángulo de rotación en ambas ventanas en el mismo instante.
   - Voz: hablar en una instancia, confirmar audio + ícono indicador en la otra; si falla algo puntual (dispositivo de mic, permisos), diagnosticar con consola en ese momento.

## Estado

Implementado (2026-07-04):
- #1 Botón "Volver al Menú" agregado en `Lobby.unity` (pantalla join/create, esquina sup. derecha, simétrico al logo) + `NetworkManager.RequestDisconnect()` + `LobbyHandler.RequestReturnToMainMenu()`.
- #2 `VoiceActivityIndicator.cs` creado y wireado en `Car.prefab` (ícono mic sobre el auto, sprite `gizmo-microphone.png` de la demo de Photon Voice).
- #3 Grip lateral normal agregado en `CarDriftBoost` (reusa `ApplyDriftVelocityDrag`).
- #4 Rotación de cámara suavizada con `Quaternion.Slerp` en `CarCamera`.
- #5 `SquashPivot` creado en `Car.prefab` (hijo de `vehicle-truck-yellow`, reparenta `body`), `CarStretchSquash._squashTarget` retargeteado, magnitud subida.
- #6 `PowerUpBox.Update()` ahora usa `PhotonNetwork.Time` para rotación/bob/color.

Compilación verificada sin errores (0 errores, solo warnings preexistentes no relacionados). Smoke test en Play Mode (una instancia) sin excepciones nuevas.

**Pendiente de verificación manual del usuario** (requiere 2 clientes ParrelSync, no disponible en esta sesión de MCP):
- Ida y vuelta real Sala → "Volver al Menú".
- Sensación de manejo (grip) y cámara en una sesión de red real.
- Voz entre dos clientes + que el ícono prenda/apague correctamente.
- Que el stretch/squash se vea bien (ángulo correcto, no exagerado) y sincronizado en el auto remoto.
- Que los power-up boxes roten exactamente igual en ambas ventanas.
