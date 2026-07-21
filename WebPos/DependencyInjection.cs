using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Abstractions;
using WebPos.Core.Services;
using WebPos.Core.Validation;

namespace WebPos;

/// <summary>
/// Domain service registration. All domain services are Scoped so
/// <see cref="WebPos.Core.Interfaces.ITenantService"/> stays aligned for the request.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWebPosDomainServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IPartyLedgerService, PartyLedgerService>();
        services.AddScoped<IPartyService, PartyService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ISalesReturnService, SalesReturnService>();
        services.AddScoped<IProcurementService, ProcurementService>();
        services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<ISyncService, SyncService>();

        services.AddValidatorsFromAssemblyContaining<CreatePartyRequestValidator>();

        return services;
    }
}
