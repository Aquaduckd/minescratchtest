using MineSharp.World.Generation;
using System.IO;

namespace MineSharp.Server;

/// <summary>
/// Server configuration settings.
/// </summary>
public class ServerConfiguration
{
    public int Port { get; set; } = 25565;
    public int ViewDistance { get; set; } = 10;
    
    /// <summary>
    /// Terrain generator ID (e.g., "flat", "noise", "void").
    /// </summary>
    public string TerrainGeneratorId { get; set; } = "noise";
    
    /// <summary>
    /// Generator-specific configuration.
    /// </summary>
    public GeneratorConfig? TerrainGeneratorConfig { get; set; }
    
    // Legacy property - kept for backward compatibility
    public bool UseTerrainGeneration { get; set; } = false;
    
    public string DataPath { get; set; } = GetDefaultDataPath();

    private static string GetDefaultDataPath()
    {
        // Try to find extracted_data relative to the executable
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", ".."));
        var dataPath = Path.Combine(projectRoot, "extracted_data");
        
        // If that doesn't exist, try relative to current directory
        if (!Directory.Exists(dataPath))
        {
            dataPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "extracted_data"));
        }
        
        // If still doesn't exist, try absolute path from project root
        if (!Directory.Exists(dataPath))
        {
            var currentDir = Directory.GetCurrentDirectory();
            if (currentDir.Contains("MineSharp"))
            {
                var rootDir = currentDir.Substring(0, currentDir.IndexOf("MineSharp"));
                dataPath = Path.Combine(rootDir, "extracted_data");
            }
        }
        
        return dataPath;
    }
    public int MaxPlayers { get; set; } = 20;
    public string Motd { get; set; } = "MineSharp Server";
}

