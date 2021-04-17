using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class LES_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1, 4)]
        [InlineData(ushort.MaxValue, 6)]
        [InlineData(ushort.MinValue, 8)]
        public void LES_R16_M16(ushort memValue, ushort memLocation)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, memLocation, memValue);

            var instructions = new Assembler(16);
            instructions.les(di, __word_ptr[memLocation]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(memValue, mbbsEmuMemoryCore.GetWord(2, memLocation));
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(1, 4)]
        [InlineData(uint.MaxValue, 6)]
        [InlineData(uint.MinValue, 8)]
        public void LES_R32_M16(uint memValue, ushort memLocation)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, memLocation, memValue);

            var instructions = new Assembler(16);
            instructions.les(di, __dword_ptr[memLocation]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(memValue, mbbsEmuMemoryCore.GetDWord(2, memLocation));
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
