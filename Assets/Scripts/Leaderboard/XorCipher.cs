using System.Text;

/// <summary>
/// Encriptación binaria simple por XOR (operador ^) para ofuscar datos sensibles
/// del leaderboard antes de enviarlos al servidor (anti-cheat client-side básico).
///
/// Recorre cada carácter del texto y le aplica XOR contra una clave secreta que
/// solo conoce el equipo. El resultado es texto ilegible para el usuario; el
/// backend lo desencripta con LA MISMA clave para recuperar el JSON original.
///
/// IMPORTANTE:
///  - El servidor DEBE compartir la misma clave para poder descifrar.
///  - XOR es ofuscación, no criptografía fuerte. Sirve para frenar al jugador
///    casual que edita el body de la request, no a un atacante serio.
///  - Encrypt y Decrypt son la misma operación (XOR es involutivo): aplicar
///    dos veces con la misma clave devuelve el texto original.
/// </summary>
public static class XorCipher
{
    /// <summary>Aplica XOR carácter a carácter contra <paramref name="key"/>.</summary>
    public static string Encrypt(string text, string key)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
            return text;

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char k = key[i % key.Length];
            sb.Append((char)(text[i] ^ k));
        }
        return sb.ToString();
    }

    /// <summary>XOR es involutivo: desencriptar es la misma operación que encriptar.</summary>
    public static string Decrypt(string text, string key) => Encrypt(text, key);
}
