using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class JAE_JB_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 3)]
        public void JAE_Test(bool carryFlagValue, ushort ipValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.jae(3);
            CreateCodeSegment(instructions);

            mbbsEmuCpuRegisters.CarryFlag = carryFlagValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(ipValue, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (carryFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            }

            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(true, 3)]
        [InlineData(false, 2)]
        public void JB_Test(bool carryFlagValue, ushort ipValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.jb(3);
            CreateCodeSegment(instructions);

            mbbsEmuCpuRegisters.CarryFlag = carryFlagValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(ipValue, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (carryFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            }

            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
