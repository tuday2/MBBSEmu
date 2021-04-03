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
        public void LEA_R16_M16(ushort memValue, ushort memLocation)
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
        }

        [Theory]
        [InlineData(1, 4)]
        [InlineData(uint.MaxValue, 6)]
        [InlineData(uint.MinValue, 8)]
        public void LEA_R16_M32(uint memValue, ushort memLocation)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetDWord(2, memLocation, memValue);

            var instructions = new Assembler(16);
            instructions.lea(ax, __dword_ptr[memLocation]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(memLocation, mbbsEmuCpuRegisters.AX);
            Assert.Equal(memValue, mbbsEmuMemoryCore.GetDWord(2, memLocation));
        }

        [Theory]
        [InlineData(1, 4)]
        [InlineData(ushort.MaxValue, 6)]
        [InlineData(ushort.MinValue, 8)]
        public void LEA_R32_M16(ushort memValue, ushort memLocation)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetDWord(2, memLocation, memValue);

            var instructions = new Assembler(16);
            instructions.lea(eax, __word_ptr[memLocation]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(memLocation, mbbsEmuCpuRegisters.AX);
            Assert.Equal(memValue, mbbsEmuMemoryCore.GetWord(2, memLocation));
        }

        [Theory]
        [InlineData(1, 4)]
        [InlineData(uint.MaxValue, 6)]
        [InlineData(uint.MinValue, 8)]
        public void LEA_R32_M32(uint memValue, ushort memLocation)
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
        }
    }
}
