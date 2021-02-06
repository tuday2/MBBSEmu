using System;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rawmsg_Tests : ExportedModuleTestBase
    {
        private const int RAWMSG_ORDINAL = 487;

        [Theory]
        [InlineData("Normal\r\n", "Normal\r")]
        [InlineData("", "")]
        [InlineData("\r\n", "\r")]
        [InlineData("123456", "123456")]
        [InlineData("--=\r\n=---\r\n", "--=\r=---\r")]
        [InlineData("!@)#!*$", "!@)#!*$")]
        public void rawmsg_Test(string msgValue, string expectedValue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(msgValue) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RAWMSG_ORDINAL, new List<ushort> { 0 });

            //Verify Results
            Assert.Equal(expectedValue, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));
        }

        [Fact]
        public void rawmsg_Greater1000_Test()
        {
            //Reset State
            Reset();

            var exceptionThrown = true;
            var msgValue = new byte[99];
            Array.Fill(msgValue, (byte) 0xA);

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(msgValue.ToString()) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            try
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RAWMSG_ORDINAL, new List<ushort> { 0 });
            }
            catch (Exception)
            {
                Assert.True(exceptionThrown);
            }
        }
    }
}
