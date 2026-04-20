using Shouldly;

namespace Shouldly;

internal static class NullableBoolShouldlyExtensions
{
    public static void ShouldBeTrue(this bool? actual, string? customMessage = null)
    {
        actual.ShouldNotBeNull(customMessage);
        actual!.Value.ShouldBeTrue(customMessage);
    }

    public static void ShouldBeFalse(this bool? actual, string? customMessage = null)
    {
        actual.ShouldNotBeNull(customMessage);
        actual!.Value.ShouldBeFalse(customMessage);
    }
}
