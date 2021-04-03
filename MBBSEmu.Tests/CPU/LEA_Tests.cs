using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class LEA_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1, 4)]
        [InlineData(ushort.MaxValue, 6)]
        [InlineData(ushort.MinValue, 8)]
        public void LEA_R16_M16_MEM(ushort memValue, ushort memLocation)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetWord(2, memLocation, memValue);

            var instructions = new Assembler(16);
            instructions.lea(ax, __word_ptr[memLocation]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(memLocation, mbbsEmuCpuRegisters.AX);
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
        public void LEA_R32_M32_MEM(uint memValue, ushort memLocation)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetDWord(2, memLocation, memValue);

            var instructions = new Assembler(16);
            instructions.lea(eax, __dword_ptr[memLocation]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(memLocation, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(memValue, mbbsEmuMemoryCore.GetDWord(2, memLocation));
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(1000, 456, 1456)]
        [InlineData(ushort.MaxValue, 0, ushort.MaxValue)]
        [InlineData(ushort.MinValue, 8, 8)]
        public void LEA_R16_M16_REG(ushort regValue, ushort mathValue, ushort expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = regValue;

            var instructions = new Assembler(16);
            instructions.lea(ax, bx + mathValue);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(1000, 456, 1456)]
        [InlineData(uint.MaxValue, 0, uint.MaxValue)]
        [InlineData(uint.MinValue, 8, 8)]
        public void LEA_R32_M32_REG(uint regValue, ushort mathValue, uint expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.EBX = regValue;

            var instructions = new Assembler(16);
            instructions.lea(eax, ebx + mathValue);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
