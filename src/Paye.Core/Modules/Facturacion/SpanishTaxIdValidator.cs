using System.Globalization;

namespace Paye.Core.Modules.Facturacion;

/// <summary>
/// Validador oficial de formato y dígito/letra de control para identificadores fiscales españoles:
/// - DNI / NIF (8 dígitos + letra de control, o prefijos especiales K, L, M)
/// - NIE (X, Y, Z + 7 dígitos + letra de control)
/// - CIF (Letra de entidad A-W + 7 dígitos + dígito o letra de control)
/// </summary>
public static class SpanishTaxIdValidator
{
    private const string DniControlLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string CifControlLetters = "JABCDEFGHI";
    private const string ValidCifPrefixes = "ABCDEFGHJNPQRSUVW";
    private const string CifMustBeLetterPrefixes = "PQRSWN";
    private const string CifMustBeDigitPrefixes = "ABEH";

    public static string Normalize(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[taxId.Length];
        var index = 0;

        foreach (var ch in taxId.Trim())
        {
            if (ch is ' ' or '-' or '.' or '/')
            {
                continue;
            }

            buffer[index++] = char.ToUpperInvariant(ch);
        }

        var normalized = new string(buffer[..index]);
        if (normalized.StartsWith("ES", StringComparison.Ordinal) && normalized.Length == 11)
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    public static bool IsValid(string? taxId, out string? errorMessage)
    {
        var normalized = Normalize(taxId);
        if (normalized.Length != 9)
        {
            errorMessage = "El NIF/CIF/NIE debe tener exactamente 9 caracteres alfanuméricos.";
            return false;
        }

        var first = normalized[0];
        var last = normalized[8];

        // 1. DNI estándar: 8 dígitos + letra
        if (char.IsAsciiDigit(first))
        {
            if (!long.TryParse(normalized[..8], CultureInfo.InvariantCulture, out var dniNumber) || !char.IsAsciiLetterUpper(last))
            {
                errorMessage = "El formato del DNI/NIF no es válido (8 dígitos seguidos de una letra).";
                return false;
            }

            var expectedLetter = DniControlLetters[(int)(dniNumber % 23)];
            if (last != expectedLetter)
            {
                errorMessage = "La letra de control del DNI/NIF no es válida.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        // 2. NIE (X, Y, Z) o NIF especial de personas físicas (K, L, M): letra inicial + 7 dígitos + letra de control
        if (first is 'X' or 'Y' or 'Z' or 'K' or 'L' or 'M')
        {
            var middleDigits = normalized[1..8];
            if (!middleDigits.All(char.IsAsciiDigit) || !char.IsAsciiLetterUpper(last))
            {
                errorMessage = "El formato del NIE/NIF especial no es válido.";
                return false;
            }

            var numericPrefix = first switch
            {
                'X' => "0",
                'Y' => "1",
                'Z' => "2",
                _ => "0"
            };

            var fullNumber = long.Parse(numericPrefix + middleDigits, CultureInfo.InvariantCulture);
            var expectedLetter = DniControlLetters[(int)(fullNumber % 23)];
            if (last != expectedLetter)
            {
                errorMessage = "La letra de control del NIE/NIF no es válida.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        // 3. CIF personas jurídicas y entidades: letra de sociedad + 7 dígitos + control
        if (ValidCifPrefixes.Contains(first, StringComparison.Ordinal))
        {
            var body = normalized[1..8];
            if (!body.All(char.IsAsciiDigit))
            {
                errorMessage = "El cuerpo del CIF debe contener 7 dígitos numéricos.";
                return false;
            }

            var sumEven = 0;
            var sumOdd = 0;

            for (var i = 0; i < 7; i++)
            {
                var digit = body[i] - '0';
                // Posiciones 1, 3, 5, 7 (índices 0, 2, 4, 6) son impares
                if (i % 2 == 0)
                {
                    var doubled = digit * 2;
                    sumOdd += (doubled / 10) + (doubled % 10);
                }
                else
                {
                    sumEven += digit;
                }
            }

            var total = sumEven + sumOdd;
            var unitDigit = (10 - (total % 10)) % 10;
            var expectedControlDigit = (char)('0' + unitDigit);
            var expectedControlLetter = CifControlLetters[unitDigit];

            bool isValidControl;
            if (CifMustBeLetterPrefixes.Contains(first, StringComparison.Ordinal))
            {
                isValidControl = last == expectedControlLetter;
            }
            else if (CifMustBeDigitPrefixes.Contains(first, StringComparison.Ordinal))
            {
                isValidControl = last == expectedControlDigit;
            }
            else
            {
                isValidControl = last == expectedControlDigit || last == expectedControlLetter;
            }

            if (!isValidControl)
            {
                errorMessage = "El dígito o letra de control del CIF no es válido.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        errorMessage = "El identificador fiscal no corresponde a un NIF, NIE o CIF español válido.";
        return false;
    }
}
