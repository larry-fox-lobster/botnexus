using System.Text.Json;
using FluentAssertions;

namespace BotNexus.Extensions.Mcp.Tests;

public class McpExtensionConfigTests
{
    [Fact]
    public void Deserializes_FromJson()
    {
        var json = """
        {
            "servers": {
                "github": {
                    "command": "npx",
                    "args": ["-y", "@modelcontextprotocol/server-github"],
                    "env": {
                        "GITHUB_TOKEN": "${env:GITHUB_TOKEN}"
                    }
                },
                "filesystem": {
                    "command": "node",
                    "args": ["server.js"],
                    "workingDirectory": "/opt/mcp"
                }
            },
            "toolPrefix": true
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config.Should().NotBeNull();
        config!.Servers.Should().HaveCount(2);
        config.ToolPrefix.Should().BeTrue();

        config.Servers["github"].Command.Should().Be("npx");
        config.Servers["github"].Args.Should().Contain("-y");
        config.Servers["github"].Env.Should().ContainKey("GITHUB_TOKEN");

        config.Servers["filesystem"].Command.Should().Be("node");
        config.Servers["filesystem"].WorkingDirectory.Should().Be("/opt/mcp");
    }

    [Fact]
    public void Defaults_ToolPrefixToTrue()
    {
        var config = new McpExtensionConfig();
        config.ToolPrefix.Should().BeTrue();
    }

    [Fact]
    public void Defaults_Timeouts()
    {
        var serverConfig = new McpServerConfig();
        serverConfig.InitTimeoutMs.Should().Be(30_000);
        serverConfig.CallTimeoutMs.Should().Be(60_000);
    }

    [Fact]
    public void Deserializes_WithCustomTimeouts()
    {
        var json = """
        {
            "servers": {
                "slow-server": {
                    "command": "python",
                    "args": ["server.py"],
                    "initTimeoutMs": 60000,
                    "callTimeoutMs": 120000
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config!.Servers["slow-server"].InitTimeoutMs.Should().Be(60_000);
        config.Servers["slow-server"].CallTimeoutMs.Should().Be(120_000);
    }

    [Fact]
    public void Deserializes_HttpServerConfig()
    {
        var json = """
        {
            "servers": {
                "remote": {
                    "url": "http://localhost:3000/mcp",
                    "headers": {
                        "Authorization": "Bearer my-token"
                    }
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config.Should().NotBeNull();
        config!.Servers["remote"].Url.Should().Be("http://localhost:3000/mcp");
        config.Servers["remote"].Headers.Should().ContainKey("Authorization");
        config.Servers["remote"].Headers!["Authorization"].Should().Be("Bearer my-token");
        config.Servers["remote"].Command.Should().BeNull();
    }

    [Fact]
    public void Deserializes_MixedTransportConfig()
    {
        var json = """
        {
            "servers": {
                "local": {
                    "command": "npx",
                    "args": ["-y", "@modelcontextprotocol/server-github"]
                },
                "remote": {
                    "url": "http://remote-host:8080/mcp"
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config.Should().NotBeNull();
        config!.Servers.Should().HaveCount(2);
        config.Servers["local"].Command.Should().Be("npx");
        config.Servers["local"].Url.Should().BeNull();
        config.Servers["remote"].Url.Should().Be("http://remote-host:8080/mcp");
        config.Servers["remote"].Command.Should().BeNull();
    }

    [Fact]
    public void Deserializes_EmptyServers()
    {
        var json = """{ "servers": {} }""";

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config.Should().NotBeNull();
        config!.Servers.Should().BeEmpty();
    }

    [Fact]
    public void Deserializes_MinimalServerConfig()
    {
        var json = """
        {
            "servers": {
                "simple": {
                    "command": "echo"
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config!.Servers["simple"].Command.Should().Be("echo");
        config.Servers["simple"].Args.Should().BeNull();
        config.Servers["simple"].Env.Should().BeNull();
        config.Servers["simple"].WorkingDirectory.Should().BeNull();
        config.Servers["simple"].Url.Should().BeNull();
        config.Servers["simple"].Headers.Should().BeNull();
    }

    [Fact]
    public void Deserializes_WithBothCommandAndUrl()
    {
        var json = """
        {
            "servers": {
                "both": {
                    "command": "node",
                    "args": ["server.js"],
                    "url": "http://localhost:3000/mcp"
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        // Both fields are deserialized; runtime decides precedence (command wins in Phase 1-2)
        config!.Servers["both"].Command.Should().Be("node");
        config.Servers["both"].Url.Should().Be("http://localhost:3000/mcp");
    }

    [Fact]
    public void Defaults_ServersToEmptyDictionary()
    {
        var config = new McpExtensionConfig();
        config.Servers.Should().NotBeNull();
        config.Servers.Should().BeEmpty();
    }

    [Fact]
    public void ServerConfig_Defaults_AllOptionalFields()
    {
        var config = new McpServerConfig();

        config.Command.Should().BeNull();
        config.Args.Should().BeNull();
        config.Env.Should().BeNull();
        config.WorkingDirectory.Should().BeNull();
        config.Url.Should().BeNull();
        config.Headers.Should().BeNull();
        config.InitTimeoutMs.Should().Be(30_000);
        config.CallTimeoutMs.Should().Be(60_000);
    }

    [Fact]
    public void Deserializes_ToolPrefixFalse()
    {
        var json = """
        {
            "servers": {},
            "toolPrefix": false
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config!.ToolPrefix.Should().BeFalse();
    }

    [Fact]
    public void Deserializes_MultipleEnvVars()
    {
        var json = """
        {
            "servers": {
                "srv": {
                    "command": "node",
                    "env": {
                        "TOKEN": "${env:MY_TOKEN}",
                        "API_KEY": "${env:API_KEY:-default-key}",
                        "PLAIN": "literal-value"
                    }
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<McpExtensionConfig>(json);

        config!.Servers["srv"].Env.Should().HaveCount(3);
        config.Servers["srv"].Env!["TOKEN"].Should().Be("${env:MY_TOKEN}");
        config.Servers["srv"].Env!["API_KEY"].Should().Be("${env:API_KEY:-default-key}");
        config.Servers["srv"].Env!["PLAIN"].Should().Be("literal-value");
    }
}
