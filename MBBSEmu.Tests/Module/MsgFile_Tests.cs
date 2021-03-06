using FluentAssertions;
using MBBSEmu.IO;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.Module
{
  public class MsgFile_Tests : TestBase, IDisposable
  {
    private readonly string _modulePath;

    private MemoryStream Load(string resourceFile)
    {
        var resource = ResourceManager.GetTestResourceManager().GetResource($"MBBSEmu.Tests.Assets.{resourceFile}");
        return new MemoryStream(resource.ToArray());
    }

    public MsgFile_Tests() 
    {
        _modulePath = GetModulePath();
    }

    public void Dispose() 
    {
        if (Directory.Exists(_modulePath))
        {
            Directory.Delete(_modulePath, recursive: true);
        }
    }

    [Fact]
    public void ReplaceWithEmptyDictionary()
    {
        var sourceMessage = Load("MBBSEMU.MSG");
        var outputRawStream = new MemoryStream();
        using var sourceStream = new StreamStream(sourceMessage);
        using var outputStream = new StreamStream(outputRawStream);

        MsgFile.UpdateValues(sourceStream, outputStream, new Dictionary<string, string>());

        outputRawStream.Flush();
        outputRawStream.Seek(0, SeekOrigin.Begin);
        var result = outputRawStream.ToArray();

        sourceMessage.Seek(0, SeekOrigin.Begin);
        var expected = sourceMessage.ToArray();

        result.Should().BeEquivalentTo(expected);
    }
  
    [Fact]
    public void ReplaceWithActualValues()
    {
        var sourceMessage = Load("MBBSEMU.MSG");
        var outputRawStream = new MemoryStream();
        using var sourceStream = new StreamStream(sourceMessage);
        using var outputStream = new StreamStream(outputRawStream);

        MsgFile.UpdateValues(sourceStream, outputStream, new Dictionary<string, string>() {{"SOCCCR", "128"}, {"SLOWTICS", "Whatever"}, {"MAXITEM", "45"}});
    
        outputRawStream.Flush();
        outputRawStream.Seek(0, SeekOrigin.Begin);
        var result = Encoding.ASCII.GetString(outputRawStream.ToArray());

        // expected should have the mods applied
        var expected = Encoding.ASCII.GetString(Load("MBBSEMU.MSG").ToArray());
        expected = expected.Replace("SOCCCR {SoC credit consumption rate adjustment, per min: 0}", "SOCCCR {SoC credit consumption rate adjustment, per min: 128}");
        expected = expected.Replace("SLOWTICS {Slow system factor: 10000}", "SLOWTICS {Slow system factor: Whatever}");
        expected = expected.Replace("MAXITEM {Maximum number of items: 954}", "MAXITEM {Maximum number of items: 45}");

        result.Should().Be(expected);
    }

    [Fact]
    public void ReplaceFileEmptyDictionary()
    {
        var fileName = Path.Combine(_modulePath, "MBBSEMU.MSG");

        Directory.CreateDirectory(_modulePath);
        File.WriteAllBytes(fileName, Load("MBBSEMU.MSG").ToArray());

        MsgFile.UpdateValues(fileName, new Dictionary<string, string>());

        File.ReadAllBytes(fileName).Should().BeEquivalentTo(Load("MBBSEMU.MSG").ToArray());
    }

    [Fact]
    public void ReplaceFileWithActualValues()
    {
        var fileName = Path.Combine(_modulePath, "MBBSEMU.MSG");

        Directory.CreateDirectory(_modulePath);
        File.WriteAllBytes(fileName, Load("MBBSEMU.MSG").ToArray());

        MsgFile.UpdateValues(fileName, new Dictionary<string, string>() {{"SOCCCR", "128"}, {"SLOWTICS", "Whatever"}, {"MAXITEM", "45"}});

        // expected should have the mods applied
        var expected = Encoding.ASCII.GetString(Load("MBBSEMU.MSG").ToArray());
        expected = expected.Replace("SOCCCR {SoC credit consumption rate adjustment, per min: 0}", "SOCCCR {SoC credit consumption rate adjustment, per min: 128}");
        expected = expected.Replace("SLOWTICS {Slow system factor: 10000}", "SLOWTICS {Slow system factor: Whatever}");
        expected = expected.Replace("MAXITEM {Maximum number of items: 954}", "MAXITEM {Maximum number of items: 45}");

        File.ReadAllBytes(fileName).Should().BeEquivalentTo(Encoding.ASCII.GetBytes(expected));
    }
  }
}
