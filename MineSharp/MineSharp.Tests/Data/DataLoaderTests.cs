using MineSharp.Data;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MineSharp.Tests.Data;

public class DataLoaderTests : IDisposable
{
    private readonly string _testDataDir;

    public DataLoaderTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }

    [Fact]
    public void LoadJson_ValidJsonFile_ReturnsDeserializedObject()
    {
        // Arrange
        var testData = new { Name = "Test", Value = 42 };
        var json = System.Text.Json.JsonSerializer.Serialize(testData);
        var filePath = Path.Combine(_testDataDir, "test.json");
        File.WriteAllText(filePath, json);

        // Act
        var result = DataLoader.LoadJson<Dictionary<string, object>>(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("Name"));
        Assert.True(result.ContainsKey("Value"));
    }

    [Fact]
    public void LoadJson_ArrayJsonFile_ReturnsList()
    {
        // Arrange
        var testData = new[] { "item1", "item2", "item3" };
        var json = System.Text.Json.JsonSerializer.Serialize(testData);
        var filePath = Path.Combine(_testDataDir, "array.json");
        File.WriteAllText(filePath, json);

        // Act
        var result = DataLoader.LoadJson<List<string>>(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("item1", result[0]);
        Assert.Equal("item2", result[1]);
        Assert.Equal("item3", result[2]);
    }

    [Fact]
    public void LoadJson_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataDir, "nonexistent.json");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => DataLoader.LoadJson<object>(filePath));
    }

    [Fact]
    public void SaveJson_ValidObject_WritesJsonFile()
    {
        // Arrange
        var testData = new { Name = "Test", Value = 42 };
        var filePath = Path.Combine(_testDataDir, "output.json");

        // Act
        DataLoader.SaveJson(filePath, testData);

        // Assert
        Assert.True(File.Exists(filePath));
        var content = File.ReadAllText(filePath);
        Assert.Contains("Test", content);
        Assert.Contains("42", content);
    }

    [Fact]
    public void LoadJson_WithComments_SkipsComments()
    {
        // Arrange
        var json = @"{
            // This is a comment
            ""Name"": ""Test"",
            ""Value"": 42
        }";
        var filePath = Path.Combine(_testDataDir, "with_comments.json");
        File.WriteAllText(filePath, json);

        // Act
        var result = DataLoader.LoadJson<Dictionary<string, object>>(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("Name"));
    }
}

