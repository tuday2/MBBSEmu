﻿using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class JBE_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void JBE_DoesJump(bool carryFlagValue, bool zeroFlagValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.jbe(3);
            CreateCodeSegment(instructions);

            mbbsEmuCpuRegisters.CarryFlag = carryFlagValue;
            mbbsEmuCpuRegisters.ZeroFlag = zeroFlagValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(3, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (carryFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            }

            if (zeroFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            }

            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
