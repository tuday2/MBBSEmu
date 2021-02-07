using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strstr_Tests : ExportedModuleTestBase
    {
        private const int STRSTR_ORDINAL = 584;

        [Theory]
        [InlineData("test", "test", 0)]
        [InlineData("abctest", "test", 3)]
        [InlineData("abctest", "not_found", -1)]
        [InlineData("abctest", "Test", -1)]
        [InlineData("abctesttest", "test", 3)]
        public void STRSTR_Test(string string1, string string2, long expectedOffset)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort)(string1.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING1", Encoding.ASCII.GetBytes(string1));
            var string2Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING2", (ushort)(string2.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING2", Encoding.ASCII.GetBytes(string2));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRSTR_ORDINAL, new List<FarPtr> {string1Pointer, string2Pointer});

            //Verify Results
            if (expectedOffset < 0) {
                Assert.Equal(0, mbbsEmuCpuRegisters.AX);
                Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            } else {
                Assert.Equal(string1Pointer.Offset + expectedOffset, mbbsEmuCpuRegisters.AX);
                Assert.Equal(string1Pointer.Segment, mbbsEmuCpuRegisters.DX);
            }
        }
    }
}
