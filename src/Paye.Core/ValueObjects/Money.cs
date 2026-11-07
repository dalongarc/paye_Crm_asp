using System.Globalization;

namespace Paye.Core.ValueObjects;

/// <summary>
/// Representación inmutable de importes monetarios usando exclusivamente decimal con precisión explícita.
/// Prohibido el uso de float o double para dinero.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const MidpointRounding DefaultRounding = MidpointRounding.ToEven;

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "EUR", int decimals = 2)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("El código ISO de moneda debe tener 3 caracteres (ej. EUR).", nameof(currency));
        }

        if (decimals is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), "Los decimales monetarios deben estar entre 0 y 4.");
        }

        Amount = decimal.Round(amount, decimals, DefaultRounding);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency = "EUR") => new(0m, currency);

    public static Money Euros(decimal amount) => new(amount, "EUR");

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor, int decimals = 2) =>
        new(Amount * factor, Currency, decimals);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money left, decimal factor) => left.Multiply(factor);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public string FormatSpanish()
    {
        if (string.Equals(Currency, "EUR", StringComparison.Ordinal))
        {
            return Amount.ToString("C2", SpanishCulture);
        }

        return $"{Amount.ToString("N2", SpanishCulture)} {Currency}";
    }

    public override string ToString() => FormatSpanish();

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"No se pueden operar importes de distintas monedas ({Currency} vs {other.Currency}).");
        }
    }
}
