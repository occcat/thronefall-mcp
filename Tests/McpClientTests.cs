using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class McpClientTests
{
    static readonly string[] ExpectedTools =
    {
        "thronefall_health",
        "thronefall_state",
        "thronefall_next_wave",
        "thronefall_harvest",
        "thronefall_slot_upgrade",
        "thronefall_slot_choice_cancel",
        "thronefall_night_call",
        "thronefall_units_command",
        "thronefall_units_send_to_spawn",
        "thronefall_path_toggle",
        "thronefall_king_teleport",
        "thronefall_loadout_select",
        "thronefall_level_start"
    };

    [Fact]
    public void Stdio_starts_and_lists_tools_when_game_is_down()
    {
        using var mcp = McpSession.Start("http://127.0.0.1:1");
        var init = mcp.Rpc(RpcRequest(1, "initialize", new Dictionary<string, object?>
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new Dictionary<string, object?>(),
            ["clientInfo"] = new Dictionary<string, object?> { ["name"] = "tests", ["version"] = "0" }
        }));
        Assert.Equal("thronefall-mcp", init["result"]?["serverInfo"]?["name"]?.ToString());

        mcp.Notify("notifications/initialized");

        var listed = mcp.Rpc(RpcRequest(2, "tools/list"));
        var names = listed["result"]?["tools"]?
            .Select(t => t["name"]?.ToString())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();
        Assert.NotNull(names);
        foreach (var tool in ExpectedTools)
            Assert.Contains(tool, names!);

        var nextWaveTool = listed["result"]?["tools"]?
            .FirstOrDefault(t => t["name"]?.ToString() == "thronefall_next_wave");
        var nextWaveDesc = nextWaveTool?["description"]?.ToString() ?? "";
        Assert.Contains("mouths", nextWaveDesc, StringComparison.Ordinal);
        Assert.DoesNotContain("map-wide spawn catalog", nextWaveDesc, StringComparison.Ordinal);

        var health = mcp.Rpc(ToolCall(3, "thronefall_health"));
        var body = Json.Deserialize<ErrorResponse>(ToolText(health));
        Assert.NotNull(body);
        Assert.False(body!.Ok);
        Assert.Equal("game_not_running", body.Error);
    }

    [Fact]
    public void Stdio_health_and_passthrough_parse_mock_http()
    {
        using var restore = ConfigRestore.Capture();
        PluginConfig.AuthToken = "";
        PluginConfig.BindAddress = "127.0.0.1";
        using var server = new Server();
        Assert.True(ServerTests.TryStartOnFreePort(server));
        var url = $"http://127.0.0.1:{PluginConfig.HttpPort}";

        using var mcp = McpSession.Start(url);
        mcp.Rpc(RpcRequest(1, "initialize", new Dictionary<string, object?>
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new Dictionary<string, object?>(),
            ["clientInfo"] = new Dictionary<string, object?> { ["name"] = "tests", ["version"] = "0" }
        }));

        var health = mcp.Rpc(ToolCall(2, "thronefall_health"));
        var live = Json.Deserialize<HealthResponse>(ToolText(health));
        Assert.NotNull(live);
        Assert.True(live!.Ok);
        Assert.Equal("ThronefallControl", live.Plugin);
        Assert.False(live.Ready);

        var state = mcp.Rpc(ToolCall(3, "thronefall_state", new Dictionary<string, object?>
        {
            ["include"] = "slots,units"
        }));
        var snapshot = Json.Deserialize<StateDto>(ToolText(state));
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.Ok);
        Assert.False(string.IsNullOrEmpty(snapshot.Phase));

        var harvest = mcp.Rpc(ToolCall(4, "thronefall_harvest", new Dictionary<string, object?>
        {
            ["clientRequestId"] = "h-1"
        }));
        var missingHarvest = Json.Deserialize<ErrorResponse>(ToolText(harvest));
        Assert.Equal(ErrorCodes.UnsupportedInThisBuild, missingHarvest!.Error);

        var upgrade = mcp.Rpc(ToolCall(5, "thronefall_slot_upgrade", new Dictionary<string, object?>
        {
            ["id"] = 4412
        }));
        var missingUpgrade = Json.Deserialize<ErrorResponse>(ToolText(upgrade));
        Assert.Equal(ErrorCodes.UnsupportedInThisBuild, missingUpgrade!.Error);
    }

    [Fact]
    public void Next_wave_tool_surfaces_mouths_and_counts()
    {
        var world = new FakeNextWaveWorld
        {
            HintsValue = new WorldHints
            {
                SceneName = "Nordfels",
                SceneState = "InGame",
                Timestate = "Day",
                MatchState = "InMatch"
            },
            Template = new StateDto
            {
                NextWave = new NextWaveDto
                {
                    Available = true,
                    WaveNumber = 2,
                    Groups =
                    {
                        new NextWaveGroupDto
                        {
                            Spawn = new EntityId { InstanceId = 88, Generation = 1, Kind = "spawn", Name = "High Back Road" },
                            EnemyName = "E Melee",
                            Count = 8
                        },
                        new NextWaveGroupDto
                        {
                            Spawn = new EntityId { InstanceId = 88, Generation = 1, Kind = "spawn", Name = "High Back Road" },
                            EnemyName = "E Archer",
                            Count = 3
                        }
                    }
                }
            }
        };
        var previous = GameFacade.Current;
        GameFacade.Current = new GameFacade(world);
        using var restore = ConfigRestore.Capture();
        PluginConfig.AuthToken = "";
        PluginConfig.BindAddress = "127.0.0.1";
        using var server = new Server();
        Assert.True(ServerTests.TryStartOnFreePort(server));
        try
        {
            using var mcp = McpSession.Start($"http://127.0.0.1:{PluginConfig.HttpPort}");
            mcp.Rpc(RpcRequest(1, "initialize", new Dictionary<string, object?>
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new Dictionary<string, object?>(),
                ["clientInfo"] = new Dictionary<string, object?> { ["name"] = "tests", ["version"] = "0" }
            }));

            var call = mcp.Rpc(ToolCall(2, "thronefall_next_wave"));
            var text = ToolText(call);
            Assert.Contains("High Back Road", text, StringComparison.Ordinal);
            Assert.Contains("E Melee", text, StringComparison.Ordinal);
            Assert.Contains("E Archer", text, StringComparison.Ordinal);
            Assert.Contains("\"count\":8", text.Replace(" ", ""), StringComparison.Ordinal);
            Assert.Contains("\"count\":3", text.Replace(" ", ""), StringComparison.Ordinal);

            var parsed = JObject.Parse(text);
            Assert.True(parsed["available"]?.Value<bool>());
            Assert.NotNull(parsed["mouths"]);
            Assert.Equal("High Back Road", parsed["mouths"]?[0]?["spawn"]?["name"]?.ToString());
            Assert.Equal(2, parsed["mouths"]?[0]?["enemies"]?.Count());
            Assert.Equal(2, parsed["nextWave"]?["waveNumber"]?.Value<int>());
            Assert.NotNull(parsed["nextWave"]?["mouths"]);
        }
        finally
        {
            GameFacade.Current = previous;
        }
    }

    sealed class FakeNextWaveWorld : IWorld
    {
        public WorldHints HintsValue { get; set; } = new();
        public StateDto Template { get; set; } = new();

        public WorldHints Hints() => HintsValue;

        public void Capture(GameFacade facade, StateDto dto, StateInclude include)
        {
            dto.NextWave = Template.NextWave;
            _ = include;
            _ = facade;
        }
    }

    static Dictionary<string, object?> RpcRequest(int id, string method, Dictionary<string, object?>? @params = null) =>
        new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = @params ?? new Dictionary<string, object?>()
        };

    static Dictionary<string, object?> ToolCall(int id, string name, Dictionary<string, object?>? arguments = null) =>
        RpcRequest(id, "tools/call", new Dictionary<string, object?>
        {
            ["name"] = name,
            ["arguments"] = arguments ?? new Dictionary<string, object?>()
        });

    static string ToolText(JObject response)
    {
        var text = response["result"]?["content"]?[0]?["text"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(text), response.ToString(Formatting.None));
        return text!;
    }
}

sealed class McpSession : IDisposable
{
    readonly Process _process;

    McpSession(Process process) => _process = process;

    public static McpSession Start(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = FindPython(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false)
        };
        psi.ArgumentList.Add(FindServerPy());
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        psi.Environment["THRONEFALL_URL"] = url;
        psi.Environment["THRONEFALL_TIMEOUT"] = "1.5";
        psi.Environment["THRONEFALL_TOKEN"] = "";

        var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start python MCP server");
        _ = Drain(process.StandardError);
        return new McpSession(process);
    }

    public JObject Rpc(Dictionary<string, object?> request)
    {
        Write(request);
        var line = ReadLine(TimeSpan.FromSeconds(8));
        var parsed = JObject.Parse(line);
        Assert.Equal("2.0", parsed["jsonrpc"]?.ToString());
        return parsed;
    }

    public void Notify(string method)
    {
        Write(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        });
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process already gone.
        }

        try { _process.Dispose(); }
        catch { /* ignore */ }
    }

    void Write(Dictionary<string, object?> payload)
    {
        var json = JsonConvert.SerializeObject(payload, Formatting.None);
        _process.StandardInput.Write(json);
        _process.StandardInput.Write('\n');
        _process.StandardInput.Flush();
    }

    string ReadLine(TimeSpan timeout)
    {
        var read = _process.StandardOutput.ReadLineAsync();
        if (!read.Wait(timeout))
            throw new TimeoutException("timed out reading MCP stdout");
        return read.Result ?? throw new EndOfStreamException("MCP stdout closed");
    }

    static Task Drain(StreamReader reader) => Task.Run(() =>
    {
        try
        {
            while (reader.ReadLine() != null)
            {
            }
        }
        catch
        {
            // Closed with the process.
        }
    });

    static string FindServerPy()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Clients", "mcp", "server.py");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Clients/mcp/server.py not found from " + AppContext.BaseDirectory);
    }

    static string FindPython()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var candidate in new[]
                 {
                     "python3",
                     Path.Combine(home, ".pyenv/shims/python3"),
                     "/opt/homebrew/bin/python3",
                     "/usr/bin/python3"
                 })
        {
            if (candidate != "python3" && !File.Exists(candidate))
                continue;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("print(1)");
                using var probe = Process.Start(psi);
                if (probe == null)
                    continue;
                if (!probe.WaitForExit(3000))
                {
                    try { probe.Kill(); } catch { /* ignore */ }
                    continue;
                }

                if (probe.ExitCode == 0)
                    return candidate;
            }
            catch
            {
                // Try the next interpreter.
            }
        }

        throw new InvalidOperationException("python3 is required for MCP tests");
    }
}
