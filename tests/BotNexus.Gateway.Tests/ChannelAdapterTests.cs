namespace BotNexus.Gateway.Tests;

public class ChannelAdapterTests
{
    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void RegisterAdapters_ExposesEnabledTuiTelegramAndWebUiAdapters() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void NormalizeInboundMessage_MapsAdapterPayloadToCanonicalRequest() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void FormatOutboundMessage_UsesAdapterSpecificContract() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void AdapterFailure_DoesNotImpactOtherRegisteredAdapters() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void AdapterReconnect_RecoversFromTransientTransportFailure() { }
}
