using MineSharp.Core;
using MineSharp.Core.Protocol;
using MineSharp.Core.Protocol.PacketTypes;
using MineSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MineSharp.Network.Handlers;

/// <summary>
/// Handles packets in the Configuration state.
/// </summary>
public class ConfigurationHandler
{
    private readonly RegistryManager _registryManager;

    public ConfigurationHandler(RegistryManager registryManager)
    {
        _registryManager = registryManager;
    }

    public async Task HandleClientInformationAsync(ClientConnection connection, ClientInformationPacket packet)
    {
        Console.WriteLine($"Client Information received:");
        Console.WriteLine($"  Locale: {packet.Locale}");
        Console.WriteLine($"  View Distance: {packet.ViewDistance}");
        Console.WriteLine($"  Chat Mode: {packet.ChatMode}");
        Console.WriteLine($"  Chat Colors: {packet.ChatColors}");
        Console.WriteLine($"  Main Hand: {packet.MainHand}");
        
        // Client information is acknowledged, but we don't need to send a response
        // The server will send registry data and finish configuration after this
    }

    public async Task SendAllRegistryDataAsync(ClientConnection connection)
    {
        var requiredRegistryIds = _registryManager.GetRequiredRegistryIds();
        
        Console.WriteLine($"  │  → Sending Registry Data for {requiredRegistryIds.Count} registries...");
        
        foreach (var registryId in requiredRegistryIds)
        {
            var entries = _registryManager.GetRegistryEntries(registryId);
            if (entries.Count == 0)
            {
                Console.WriteLine($"  │  ⚠ Warning: No entries found for {registryId}, skipping");
                continue;
            }
            
            try
            {
                await SendRegistryDataAsync(connection, registryId, entries);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │  ✗ Error sending {registryId}: {ex.Message}");
                // Continue with other registries even if one fails
            }
        }
        
        Console.WriteLine($"  └─");
    }

    public async Task SendRegistryDataAsync(ClientConnection connection, string registryId, List<(string EntryId, byte[]? NbtData)> entries)
    {
        Console.WriteLine($"  → Sending Registry Data for {registryId} ({entries.Count} entries)...");
        
        try
        {
            var registryData = PacketBuilder.BuildRegistryDataPacket(registryId, entries);
            await connection.SendPacketAsync(registryData);
            Console.WriteLine($"  ✓ {registryId}: {entries.Count} entry(ies) ({registryData.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Error sending {registryId}: {ex.Message}");
            throw;
        }
    }

    public async Task SendKnownPacksAsync(ClientConnection connection)
    {
        Console.WriteLine($"  → Sending Known Packs...");
        
        try
        {
            var knownPacks = PacketBuilder.BuildKnownPacksPacket();
            await connection.SendPacketAsync(knownPacks);
            Console.WriteLine($"  ✓ Known Packs sent ({knownPacks.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Error sending Known Packs: {ex.Message}");
            throw;
        }
    }

    public async Task SendFinishConfigurationAsync(ClientConnection connection)
    {
        Console.WriteLine($"  → Sending Finish Configuration...");
        
        try
        {
            var finishConfig = PacketBuilder.BuildFinishConfigurationPacket();
            await connection.SendPacketAsync(finishConfig);
            Console.WriteLine($"  ✓ Finish Configuration sent ({finishConfig.Length} bytes)");
            Console.WriteLine($"  → Waiting for Acknowledge Finish Configuration...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Error sending Finish Configuration: {ex.Message}");
            throw;
        }
    }
}

