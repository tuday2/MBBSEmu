using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stpans_Tests : ExportedModuleTestBase
    {
        private const int STPANS_ORDINAL = 712;

        [Theory]
        [InlineData("", "")]
        [InlineData("This is really cool.\r\nYep", "This is really cool.\r\nYep")]
        [InlineData("\u001B[K\u001B[2J\u001B[4;5H\u001B[=55h\u001B[=56l", "")]
        [InlineData("\u001B[1;40;30mThis is a \u001B[1;41;31mtest", "This is a test")]
        public void sptans_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STPANS_ORDINAL, new List<FarPtr> { stringPointer });

            //Verify Results
            Assert.Equal(stringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(stringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }
    }
}
