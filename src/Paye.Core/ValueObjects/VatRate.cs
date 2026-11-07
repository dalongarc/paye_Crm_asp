using System.Globalization;

namespace Paye.Core.ValueObjects;

/// <summary>
/// Tipo de IVA como valor preparado para comercios en España (sin implementar facturación).
/// </summary>
public readonly record struct VatRate
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public static readonly VatRate General21 = new(21.00m, "General (21 %)");
    public static readonly VatRate Reducido10 = new(10.00m, "Reducido (10 %)");
    public static readonly VatRate Superreducido4 = new(4.00m, "Superreducido (4 %)");
    public static readonly VatRate Exento0 = new(0.00m, "Exento / 0 %");

    public decimal Percentage { get; }
    public string Name { get; }

    public VatRate(decimal percentage, string? name = null)
    {
        if (percentage is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "El porcentaje de IVA debe estar entre 0 y 100.");
        }

        Percentage = decimal.Round(percentage, 2, Money.DefaultRounding);
        Name = string.IsNullOrWhiteSpace(name)
            ? $"{Percentage.ToString("0.##", SpanishCulture)} %"
            : name.Trim();
    }

    public decimal Factor => Percentage / 100m;

    public Money CalculateTaxFromNet(Money netAmount) =>
        netAmount.Multiply(Factor);

    public Money CalculateGrossFromNet(Money netAmount) =>
        netAmount + CalculateTaxFromNet(netAmount);

    public (Money Net, Money Tax, Money Gross) BreakdownFromGross(Money grossAmount)
    {
        var netDecimal = grossAmount.Amount / (1m + Factor);
        var net = new Money(netDecimal, grossAmount.Currency);
        var tax = grossAmount - net;
        return (net, tax, grossAmount);
    }

    public override string ToString() => Name;
}
