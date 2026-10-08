namespace FaraTaraz.Modules.AccountingSources;

using FaraTaraz.BuildingBlocks.Accounting;

/// <summary>
/// The accounting provider port.
///
/// A provider declares which capabilities it supports and exposes the matching
/// capability ports. It does NOT implement fake methods returning empty collections:
/// unsupported capabilities are handled explicitly via
/// <see cref="ProviderCapabilityExtensions.RequireCapability{TCapability}(IAccountingProvider, AccountingCapability)"/>.
/// </summary>
public interface IAccountingProvider
{
    AccountingCapability SupportedCapabilities { get; }
}

