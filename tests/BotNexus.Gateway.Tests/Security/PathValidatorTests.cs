using BotNexus.Gateway.Abstractions.Security;
using BotNexus.Gateway.Security;

namespace BotNexus.Gateway.Tests.Security;

public sealed class PathValidatorTests
{
    private const string Workspace = @"Q:\repos\botnexus";

    [Fact]
    public void CanRead_AllowedPath_ReturnsTrue()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus\src"]
        });

        sut.CanRead(@"Q:\repos\botnexus\src\gateway\file.cs").ShouldBeTrue();
    }

    [Fact]
    public void CanRead_DeniedPath_ReturnsFalse()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus"],
            DeniedPaths = [@"Q:\repos\botnexus\src\gateway\secrets"]
        });

        sut.CanRead(@"Q:\repos\botnexus\src\gateway\secrets\file.txt").ShouldBeFalse();
    }

    [Fact]
    public void CanRead_OutsideAllPaths_ReturnsFalse()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus\src"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\outside\file.txt").ShouldBeFalse();
    }

    [Fact]
    public void CanWrite_AllowedWritePath_ReturnsTrue()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedWritePaths = [@"Q:\repos\botnexus\artifacts"]
        }, workspace: @"Q:\workspace");

        sut.CanWrite(@"Q:\repos\botnexus\artifacts\output.json").ShouldBeTrue();
    }

    [Fact]
    public void CanWrite_ReadOnlyPath_ReturnsFalse()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus\docs"]
        }, workspace: @"Q:\workspace");

        sut.CanWrite(@"Q:\repos\botnexus\docs\spec.md").ShouldBeFalse();
    }

    [Fact]
    public void DenyOverridesAllow()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus\src"],
            AllowedWritePaths = [@"Q:\repos\botnexus\src"],
            DeniedPaths = [@"Q:\repos\botnexus\src\private"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\botnexus\src\private\secrets.txt").ShouldBeFalse();
        sut.CanWrite(@"Q:\repos\botnexus\src\private\secrets.txt").ShouldBeFalse();
    }

    [Fact]
    public void ValidateAndResolve_ResolvesRelativePath()
    {
        var sut = CreateValidator(policy: null);

        var resolved = sut.ValidateAndResolve(@"src\gateway\Program.cs", FileAccessMode.Read);

        resolved.ShouldBe(@"Q:\repos\botnexus\src\gateway\Program.cs");
    }

    [Fact]
    public void ValidateAndResolve_ReturnsNull_WhenDenied()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus\src"],
            DeniedPaths = [@"Q:\repos\botnexus\src\gateway"]
        }, workspace: @"Q:\workspace");

        var resolved = sut.ValidateAndResolve(@"Q:\repos\botnexus\src\gateway\Program.cs", FileAccessMode.Read);

        resolved.ShouldBeNull();
    }

    [Fact]
    public void ValidateAndResolve_NormalizesSlashes()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus"]
        }, workspace: @"Q:\workspace");

        var resolved = sut.ValidateAndResolve(@"Q:/repos/botnexus/src/gateway/Program.cs", FileAccessMode.Read);

        resolved.ShouldBe(@"Q:\repos\botnexus\src\gateway\Program.cs");
    }

    [Fact]
    public void DefaultPolicy_WorkspaceOnly()
    {
        var nullPolicyValidator = CreateValidator(policy: null);
        var emptyPolicyValidator = CreateValidator(new FileAccessPolicy());

        nullPolicyValidator.CanRead(@"Q:\repos\botnexus\README.md").ShouldBeTrue();
        nullPolicyValidator.CanRead(@"Q:\elsewhere\README.md").ShouldBeFalse();
        emptyPolicyValidator.CanWrite(@"Q:\repos\botnexus\artifacts\output.txt").ShouldBeTrue();
        emptyPolicyValidator.CanWrite(@"Q:\elsewhere\output.txt").ShouldBeFalse();
    }

    [Fact]
    public void CaseInsensitive_OnWindows()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\Repos\BotNexus"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"q:\repos\botnexus\src\gateway\Program.cs").ShouldBe(OperatingSystem.IsWindows());
    }

    [Fact]
    public void SubdirectoryMatch()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\botnexus\src").ShouldBeTrue();
    }

    [Fact]
    public void PartialNameNoMatch()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\botnexus-other\src").ShouldBeFalse();
    }

    [Fact]
    public void GlobStar_MatchesAllUnderDirectory()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\*"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\botnexus\file.cs").ShouldBeTrue();
    }

    [Fact]
    public void GlobDoubleStar_MatchesRecursive()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\**\*.cs"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\botnexus\src\gateway\Program.cs").ShouldBeTrue();
        sut.CanRead(@"Q:\repos\botnexus\src\gateway\file.txt").ShouldBeFalse();
    }

    [Fact]
    public void GlobInDeny_BlocksPattern()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\repos\botnexus"],
            DeniedPaths = [@"**\*.env"]
        });

        sut.CanRead(@"Q:\repos\botnexus\src\.env").ShouldBeFalse();
        sut.CanRead(@"Q:\repos\botnexus\config\production.env").ShouldBeFalse();
        sut.CanRead(@"Q:\repos\botnexus\src\Program.cs").ShouldBeTrue();
    }

    [Fact]
    public void GlobAndLiteral_BothWork()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths =
            [
                @"Q:\repos\botnexus\docs",
                @"Q:\repos\**\*.cs"
            ]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\botnexus\docs\spec.md").ShouldBeTrue();
        sut.CanRead(@"Q:\repos\botnexus\src\Program.cs").ShouldBeTrue();
        sut.CanRead(@"Q:\repos\botnexus\src\readme.md").ShouldBeFalse();
    }

    [Fact]
    public void GlobNoMatch_ReturnsFalse()
    {
        var sut = CreateValidator(new FileAccessPolicy
        {
            AllowedReadPaths = [@"Q:\other\*"]
        }, workspace: @"Q:\workspace");

        sut.CanRead(@"Q:\repos\file.cs").ShouldBeFalse();
    }

    private static DefaultPathValidator CreateValidator(FileAccessPolicy? policy, string workspace = Workspace)
        => new(policy, workspace);
}
