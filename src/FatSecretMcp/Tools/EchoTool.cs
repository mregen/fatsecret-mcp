using System.ComponentModel;
using ModelContextProtocol.Server;

namespace FatSecretMcp.Tools;

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the input back to the caller.")]
    public static string Echo(string message) => $"Echo: {message}";
}
