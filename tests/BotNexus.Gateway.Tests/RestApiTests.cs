namespace BotNexus.Gateway.Tests;

public class RestApiTests
{
    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void GetHealth_ReturnsOkWithoutAgentContext() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void PostAgent_WithValidPayload_CreatesAgent() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void PostAgent_WithInvalidPayload_ReturnsProblemDetails() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void GetSession_WithKnownSessionId_ReturnsSessionState() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void DeleteSession_WithKnownSessionId_ClosesAndRemovesSession() { }

    [Fact(Skip = "Pending Gateway interfaces and implementation.")]
    public void RestAndWebSocketState_ReadsReturnConsistentAgentView() { }
}
