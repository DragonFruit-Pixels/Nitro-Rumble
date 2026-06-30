# Setup de Auth — Google Sheet + Apps Script (req. #22)

Pasos para conectar el login usuario/contraseña del juego con una planilla de Google.

## 1. Crear la planilla

1. Google Drive → nueva **Hoja de cálculo**. Nombre: `NitroRumble-Users`.
2. En la primera fila (headers), poné: `username` | `passwordXor` | `salt` | `createdAt`.

## 2. Crear el Apps Script

1. En la planilla: **Extensiones → Apps Script**.
2. Borrá el código de ejemplo y pegá esto (cambiá `XOR_KEY` por la misma clave que pongas en el inspector de `AuthService`):

```javascript
// Debe coincidir con _xorKey del AuthService en Unity.
const XOR_KEY = 'ChangeMeSecretKey';

function xor(text, key) {
  let out = '';
  for (let i = 0; i < text.length; i++) {
    out += String.fromCharCode(text.charCodeAt(i) ^ key.charCodeAt(i % key.length));
  }
  return out;
}

function json(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}

function doPost(e) {
  const sheet = SpreadsheetApp.getActiveSpreadsheet().getSheets()[0];
  const req = JSON.parse(e.postData.contents);
  const username = (req.username || '').trim();
  const password = xor(req.password || '', XOR_KEY); // des-ofuscar la password recibida

  if (!username || !password) return json({ success: false, error: 'Datos incompletos.' });

  const rows = sheet.getDataRange().getValues(); // incluye header en rows[0]
  let foundRow = -1;
  for (let i = 1; i < rows.length; i++) {
    if (String(rows[i][0]) === username) { foundRow = i; break; }
  }

  if (req.action === 'register') {
    if (foundRow !== -1) return json({ success: false, error: 'El usuario ya existe.' });
    // Para un proyecto real: guardar un HASH (ej. Utilities.computeDigest SHA-256 + salt).
    sheet.appendRow([username, password, '', new Date().toISOString()]);
    return json({ success: true });
  }

  if (req.action === 'login') {
    if (foundRow === -1) return json({ success: false, error: 'Usuario no encontrado.' });
    const stored = String(rows[foundRow][1]);
    if (stored !== password) return json({ success: false, error: 'Contraseña incorrecta.' });
    return json({ success: true });
  }

  return json({ success: false, error: 'Acción desconocida.' });
}
```

## 3. Publicar como Web App

1. Apps Script → **Implementar → Nueva implementación**.
2. Tipo: **Aplicación web**.
3. *Ejecutar como:* **Yo**. *Quién tiene acceso:* **Cualquier persona**.
4. **Implementar** → copiá la **URL** (termina en `/exec`).

## 4. Conectar en Unity

1. En la escena de menú, el GameObject con `AuthService`:
   - `_baseUrl` = la URL `/exec` del paso 3.
   - `_xorKey` = la MISMA clave que `XOR_KEY` del Apps Script.
2. Wirear el `AuthPanel` (inputs usuario/contraseña, botones Login/Register, status text) y
   enganchar `OnAuthenticated` a lo que siga (mostrar lobby / conectar a Photon).

## Notas

- La contraseña viaja **ofuscada con XOR** (no es cripto fuerte; frena al casual, como dice la PPT).
- Para subir la nota: guardar un **hash** (SHA-256 + salt) en vez de la password ofuscada — la
  columna `salt` ya está prevista. El cambio es solo del lado del Apps Script.
- Al loguear, `AuthSession` setea `PhotonNetwork.NickName = username` → el nombre sobre el auto (req. 14).
```
