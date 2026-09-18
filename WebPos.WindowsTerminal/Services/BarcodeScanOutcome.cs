using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class BarcodeScanOutcome
{
    public bool IsMaster { get; private init; }

    public SalesProductDto? Product { get; private init; }

    public SaleMasterDto? Master { get; private init; }

    public static BarcodeScanOutcome ForProduct(SalesProductDto product) =>
        new() { IsMaster = false, Product = product };

    public static BarcodeScanOutcome ForMaster(SaleMasterDto master) =>
        new() { IsMaster = true, Master = master };
}
