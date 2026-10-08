namespace FaraTaraz.BuildingBlocks.Tests;

using FaraTaraz.BuildingBlocks.Errors;
using Xunit;

/// <summary>
/// Evidence for the foundational error-code contract: a valid descriptor retains its
/// metadata, and malformed codes are rejected at construction.
/// </summary>
public class ErrorContractTests
{
    [Fact]
    public void Valid_descriptor_retains_its_metadata()
    {
        var code = new ErrorCode("FT-ING-SYNC-003");
        var descriptor = new ErrorDescriptor(
            code,
            ErrorCategory.ProviderTransient,
            ErrorSeverity.Error,
            isRetryable: true,
            safeMessageKey: "sync.source_record.unreadable");

        Assert.Equal("FT-ING-SYNC-003", descriptor.Code.Value);
        Assert.Equal(code, descriptor.Code);
        Assert.Equal(ErrorCategory.ProviderTransient, descriptor.Category);
        Assert.Equal(ErrorSeverity.Error, descriptor.Severity);
        Assert.True(descriptor.IsRetryable);
        Assert.Equal("sync.source_record.unreadable", descriptor.SafeMessageKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FT-ING-SYNC-3")]
    [InlineData("ft-ing-sync-003")]
    public void Malformed_error_codes_are_rejected(string? value)
        => Assert.ThrowsAny<ArgumentException>(() => new ErrorCode(value!));
}
