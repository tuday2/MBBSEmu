using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strtok_Tests : ExportedModuleTestBase
    {
        private const int STRTOK_ORDINAL = 585;

        [Fact]
        public void strtok()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("STR", 0xFF);
            mbbsEmuMemoryCore.SetArray("STR", Encoding.ASCII.GetBytes("This is a cool:Test of the system:More padding?Sure Why not"));

            var delimPointer = mbbsEmuMemoryCore.AllocateVariable("DELIM", 0xF);
            mbbsEmuMemoryCore.SetArray("DELIM", Encoding.ASCII.GetBytes(":?"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOK_ORDINAL, new List<FarPtr> { stringPointer, delimPointer });
            Assert.Equal("This is a cool", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOK_ORDINAL, new List<FarPtr> { FarPtr.Empty, delimPointer });
            Assert.Equal("Test of the system", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOK_ORDINAL, new List<FarPtr> { FarPtr.Empty, delimPointer });
            Assert.Equal("More padding", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOK_ORDINAL, new List<FarPtr> { FarPtr.Empty, delimPointer });
            Assert.Equal("Sure Why not", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOK_ORDINAL, new List<FarPtr> { FarPtr.Empty, delimPointer });
            Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOK_ORDINAL, new List<FarPtr> { FarPtr.Empty, delimPointer });
            Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}
