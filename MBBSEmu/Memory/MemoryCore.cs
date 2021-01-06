﻿using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Decoder = Iced.Intel.Decoder;


namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Handles Memory Operations for the Module
    ///
    ///     Information of x86 Memory Segmentation: https://en.wikipedia.org/wiki/X86_memory_segmentation
    /// </summary>
    public class MemoryCore : IMemoryCore
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        private readonly byte[][] _memorySegments = new byte[0x10000][];
        private readonly Segment[] _segments = new Segment[0x10000];
        private readonly Instruction[][] _decompiledSegments = new Instruction[0x10000][];

        private readonly Dictionary<string, FarPtr> _variablePointerDictionary;
        private FarPtr _currentVariablePointer;
        private const ushort VARIABLE_BASE_SEGMENT = 0x1000; //0x1000->0x1FFF == 256MB
        private FarPtr _currentRealModePointer;
        private const ushort REALMODE_BASE_SEGMENT = 0x2000; //0x2000->0x2FFF == 256MB
        private readonly PointerDictionary<Dictionary<ushort, FarPtr>> _bigMemoryBlocks;

        /// <summary>
        ///     Default Compiler Hints for use on methods within the MemoryCore
        ///
        ///     Works fastest with just AggressiveOptimization. Enabling AggressiveInlining slowed
        ///     down the code.
        /// </summary>
        private const MethodImplOptions CompilerOptimizations = MethodImplOptions.AggressiveOptimization;

        public MemoryCore()
        {
            _variablePointerDictionary = new Dictionary<string, FarPtr>();
            _currentVariablePointer = new FarPtr(VARIABLE_BASE_SEGMENT, 0);
            _currentRealModePointer = new FarPtr(REALMODE_BASE_SEGMENT, 0);
            _bigMemoryBlocks = new PointerDictionary<Dictionary<ushort, FarPtr>>();

            //Add Segment 0 by default, stack segment
            AddSegment(0);
        }

        /// <summary>
        ///     Deletes all defined Segments from Memory
        /// </summary>
        public void Clear()
        {
            Array.Clear(_memorySegments, 0, _memorySegments.Length);
            Array.Clear(_segments, 0, _segments.Length);
            Array.Clear(_decompiledSegments, 0, _decompiledSegments.Length);

            _variablePointerDictionary.Clear();
            _currentVariablePointer = new FarPtr(VARIABLE_BASE_SEGMENT, 0);
            _currentRealModePointer = new FarPtr(REALMODE_BASE_SEGMENT, 0);
            _bigMemoryBlocks.Clear();
        }

        /// <summary>
        ///     Allocates the specified variable name with the desired size
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <param name="declarePointer"></param>
        /// <returns></returns>
        public FarPtr AllocateVariable(string name, ushort size, bool declarePointer = false)
        {
            if (!string.IsNullOrEmpty(name) && _variablePointerDictionary.ContainsKey(name))
            {
                _logger.Warn($"Attempted to re-allocate variable: {name}");
                return _variablePointerDictionary[name];
            }

            //Do we have enough room in the current segment?
            //If not, declare a new segment and start there
            if (size + _currentVariablePointer.Offset >= ushort.MaxValue)
            {
                _currentVariablePointer.Segment++;
                _currentVariablePointer.Offset = 0;
                AddSegment(_currentVariablePointer.Segment);
            }

            if (!HasSegment(_currentVariablePointer.Segment))
                AddSegment(_currentVariablePointer.Segment);

#if DEBUG
            //_logger.Debug(
            //    $"Variable {name ?? "NULL"} allocated {size} bytes of memory in Host Memory Segment {_currentVariablePointer.Segment:X4}:{_currentVariablePointer.Offset:X4}");
#endif
            var currentOffset = _currentVariablePointer.Offset;
            _currentVariablePointer.Offset += (ushort)(size + 1);

            var newPointer = new FarPtr(_currentVariablePointer.Segment, currentOffset);

            if (declarePointer && string.IsNullOrEmpty(name))
                throw new Exception("Unsupported operation, declaring pointer type for NULL named variable");

            if (!string.IsNullOrEmpty(name))
            {
                _variablePointerDictionary[name] = newPointer;

                if (declarePointer)
                {
                    var variablePointer = AllocateVariable($"*{name}", 0x4, false);
                    SetArray(variablePointer, newPointer.Data);
                }
            }

            return newPointer;
        }

        /// <summary>
        ///     Returns the pointer to a defined variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public FarPtr GetVariablePointer(string name)
        {
            if (!TryGetVariablePointer(name, out var result))
                throw new ArgumentException($"Unknown Variable: {name}");

            return result;
        }

        /// <summary>
        ///     Safe retrieval of a pointer to a defined variable
        ///
        ///     Returns false if the variable isn't defined
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pointer"></param>
        /// <returns></returns>
        public bool TryGetVariablePointer(string name, out FarPtr pointer)
        {
            if (!_variablePointerDictionary.TryGetValue(name, out var result))
            {
                pointer = null;
                return false;
            }

            pointer = result;
            return true;
        }

        /// <summary>
        ///     Safely try to retrieve a variable, or allocate it if it's not present
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <param name="declarePointer">
        ///     Some variables are pointers to an underlying value. Setting this value to TRUE declares not only the
        ///     desired variable of NAME of SIZE, but also a 2 byte variable named "*NAME" which holds a pointer to NAME
        /// </param>
        /// <returns></returns>
        public FarPtr GetOrAllocateVariablePointer(string name, ushort size = 0x0, bool declarePointer = false)
        {
            if (_variablePointerDictionary.TryGetValue(name, out var result))
                return result;

            return AllocateVariable(name, size, declarePointer);
        }

        /// <summary>
        ///     Adds a new Memory Segment containing 65536 bytes
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="size"></param>
        public void AddSegment(ushort segmentNumber, int size = 0x10000)
        {
            if (_memorySegments[segmentNumber] != null)
                throw new Exception($"Segment with number {segmentNumber} already defined");

            _memorySegments[segmentNumber] = new byte[size];
        }

        /// <summary>
        ///     Removes the specified segment. Typically only used by a test.
        /// </summary>
        /// <param name="segment"></param>
        public void RemoveSegment(ushort segment)
        {
            _memorySegments[segment] = null;
            _segments[segment] = null;
            _decompiledSegments[segment] = null;
        }

        /// <summary>
        ///     Directly adds a raw segment from an NE file segment
        /// </summary>
        /// <param name="segment"></param>
        public void AddSegment(Segment segment)
        {
            //Get Address for this Segment
            var segmentMemory = new byte[0x10000];

            //Add the data to memory and record the segment offset in memory
            Array.Copy(segment.Data, 0, segmentMemory, 0, segment.Data.Length);
            _memorySegments[segment.Ordinal] = segmentMemory;

            if (segment.Flags.Contains(EnumSegmentFlags.Code))
            {
                //Decode the Segment
                var instructionList = new InstructionList();
                var codeReader = new ByteArrayCodeReader(segment.Data);
                var decoder = Decoder.Create(16, codeReader);
                decoder.IP = 0x0;

                while (decoder.IP < (ulong)segment.Data.Length)
                {
                    decoder.Decode(out instructionList.AllocUninitializedElement());
                }

                _decompiledSegments[segment.Ordinal] = new Instruction[0x10000];
                foreach (var i in instructionList)
                {
                    _decompiledSegments[segment.Ordinal][i.IP16] = i;
                }
            }

            _segments[segment.Ordinal] = segment;
        }

        /// <summary>
        ///     Adds a Decompiled code segment
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="segmentInstructionList"></param>
        public void AddSegment(ushort segmentNumber, InstructionList segmentInstructionList)
        {
            _decompiledSegments[segmentNumber] = new Instruction[0x10000];
            foreach (var i in segmentInstructionList)
            {
                _decompiledSegments[segmentNumber][i.IP16] = i;
            }
        }

        /// <summary>
        ///     Returns the Segment information for the desired Segment Number
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public Segment GetSegment(ushort segmentNumber) => _segments[segmentNumber];

        /// <summary>
        ///     Verifies the specified segment is defined
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public bool HasSegment(ushort segmentNumber) => _memorySegments[segmentNumber] != null;

        /// <summary>
        ///     Returns the decompiled instruction from the specified segment:pointer
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="instructionPointer"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public Instruction GetInstruction(ushort segment, ushort instructionPointer) =>
            _decompiledSegments[segment][instructionPointer];

        public Instruction Recompile(ushort segment, ushort instructionPointer)
        {
            //If it wasn't able to decompile linear through the data, there might have been
            //data in the path of the code that messed up decoding, in this case, we grab up to
            //6 bytes at the IP and decode the instruction manually. This works 9 times out of 10
            Span<byte> segmentData = _segments[segment].Data;
            var reader = new ByteArrayCodeReader(segmentData.Slice(instructionPointer, 6).ToArray());
            var decoder = Decoder.Create(16, reader);
            decoder.IP = instructionPointer;
            decoder.Decode(out var outputInstruction);

            _decompiledSegments[segment][instructionPointer] = outputInstruction;
            return outputInstruction;
        }

        /// <summary>
        ///     Returns a single byte from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public byte GetByte(FarPtr pointer) => GetByte(pointer.Segment, pointer.Offset);

        /// <summary>
        ///     Returns a single byte from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public byte GetByte(ushort segment, ushort offset) => _memorySegments[segment][offset];

        /// <summary>
        ///     Returns an unsigned byte from the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ushort GetWord(string variableName) => GetWord(GetVariablePointer(variableName));

        /// <summary>
        ///     Returns an unsigned word from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ushort GetWord(FarPtr pointer) => GetWord(pointer.Segment, pointer.Offset);

        /// <summary>
        ///     Returns an unsigned word from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public unsafe ushort GetWord(ushort segment, ushort offset) {
            fixed (byte *p = _memorySegments[segment]) {
                ushort* ptr = (ushort*)(p + offset);
                return *ptr;
            }
        }

        public uint GetDWord(string variableName) => GetDWord(GetVariablePointer(variableName));

        public uint GetDWord(FarPtr pointer) => GetWord(pointer.Segment, pointer.Offset);

        public unsafe uint GetDWord(ushort segment, ushort offset)
        {
            fixed (byte* p = _memorySegments[segment])
            {
                uint* ptr = (uint*)(p + offset);
                return *ptr;
            }
        }

        /// <summary>
        ///     Returns a pointer stored at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public FarPtr GetPointer(FarPtr pointer) => new FarPtr(GetArray(pointer, 4));

        /// <summary>
        ///     Returns a pointer stored at the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public FarPtr GetPointer(string variableName) => new FarPtr(GetArray(GetVariablePointer(variableName), 4));

        /// <summary>
        ///     Returns a pointer stored at the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public FarPtr GetPointer(ushort segment, ushort offset) => new FarPtr(GetArray(segment, offset, 4));

        /// <summary>
        ///     Returns an array with desired count from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetArray(FarPtr pointer, ushort count) =>
            GetArray(pointer.Segment, pointer.Offset, count);

        /// <summary>
        ///     Returns an array with the desired count from the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetArray(string variableName, ushort count) =>
            GetArray(GetVariablePointer(variableName), count);

        /// <summary>
        ///     Returns an array with the desired count from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count) =>
            _memorySegments[segment].AsSpan().Slice(offset, count);

        /// <summary>
        ///     Returns an array containing the cstring stored at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetString(FarPtr pointer, bool stripNull = false) =>
            GetString(pointer.Segment, pointer.Offset, stripNull);

        /// <summary>
        ///     Returns an array containing cstring stored at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetString(string variableName, bool stripNull = false) =>
            GetString(GetVariablePointer(variableName), stripNull);

        /// <summary>
        ///     Returns an array containing the cstring stored at the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false)
        {
            ReadOnlySpan<byte> segmentSpan = _memorySegments[segment];

            for (var i = offset; i < ushort.MaxValue; i++)
            {
                if (segmentSpan[i] == 0x0)
                    return segmentSpan.Slice(offset, (i - offset) + (stripNull ? 0 : 1));
            }

            throw new Exception($"Invalid String at {segment:X4}:{offset:X4}");
        }

        /// <summary>
        ///     Sets the specified byte at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetByte(string variableName, byte value) => SetByte(GetVariablePointer(variableName), value);

        /// <summary>
        ///     Sets the specified byte at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetByte(FarPtr pointer, byte value) => SetByte(pointer.Segment, pointer.Offset, value);

        /// <summary>
        ///     Sets the specified byte at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetByte(ushort segment, ushort offset, byte value) =>_memorySegments[segment][offset] = value;

        /// <summary>
        ///     Sets the specified word at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetWord(FarPtr pointer, ushort value) => SetWord(pointer.Segment, pointer.Offset, value);

        /// <summary>
        ///     Sets the specified word at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public unsafe void SetWord(ushort segment, ushort offset, ushort value)
        {
            fixed (byte *dst = _memorySegments[segment]) {
                ushort *ptr = (ushort*)(dst + offset);
                *ptr = value;
            }
        }

        /// <summary>
        ///     Sets the specified word at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetWord(string variableName, ushort value) => SetWord(GetVariablePointer(variableName), value);

        [MethodImpl(CompilerOptimizations)]
        public void SetDWord(FarPtr pointer, uint value) => SetDWord(pointer.Segment, pointer.Offset, value);

        [MethodImpl(CompilerOptimizations)]
        public unsafe void SetDWord(ushort segment, ushort offset, uint value)
        {
            fixed (byte* dst = _memorySegments[segment])
            {
                uint* ptr = (uint*)(dst + offset);
                *ptr = value;
            }
        }

        [MethodImpl(CompilerOptimizations)]
        public void SetDWord(string variableName, uint value) => SetDWord(GetVariablePointer(variableName), value);

        /// <summary>
        ///     Sets the specified array at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="array"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetArray(FarPtr pointer, ReadOnlySpan<byte> array) =>
            SetArray(pointer.Segment, pointer.Offset, array);

        [MethodImpl(CompilerOptimizations)]
        public void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array)
        {
            var destinationSpan = _memorySegments[segment].AsSpan(offset);
            array.CopyTo(destinationSpan);
        }

        /// <summary>
        ///     Sets the specified array at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="array"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetArray(string variableName, ReadOnlySpan<byte> value) =>
            SetArray(GetVariablePointer(variableName), value);

        /// <summary>
        ///     Writes the specified byte the specified number of times starting at the specified pointer
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        public void FillArray(ushort segment, ushort offset, ushort count, byte value)
        {
            Array.Fill(_memorySegments[segment],value, offset, count);
        }

        /// <summary>
        ///     Writes the specified byte the specified number of times starting at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        public void FillArray(FarPtr pointer, ushort count, byte value) =>
            FillArray(pointer.Segment, pointer.Offset, count, value);

        /// <summary>
        ///     Sets the specified pointer value at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetPointer(FarPtr pointer, FarPtr value) => SetArray(pointer, value.Data);

        /// <summary>
        ///     Sets the specified pointer value at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetPointer(string variableName, FarPtr value) =>
            SetArray(GetVariablePointer(variableName), value.Data);

        /// <summary>
        ///     Sets the specified pointer value at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetPointer(ushort segment, ushort offset, FarPtr value) => SetArray(segment, offset, value.Data);

        /// <summary>
        ///     Zeroes out the memory at the specified pointer for the desired number of bytes
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="length"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetZero(FarPtr pointer, int length)
        {
            var destinationSpan = new Span<byte>(_memorySegments[pointer.Segment], pointer.Offset, length);
            destinationSpan.Fill(0);
        }

        /// <summary>
        ///     Allocates the specific number of Big Memory Blocks with the desired size
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public FarPtr AllocateBigMemoryBlock(ushort quantity, ushort size)
        {
            var newBlockOffset = _bigMemoryBlocks.Allocate(new Dictionary<ushort, FarPtr>());

            //Fill the Region
            for (ushort i = 0; i < quantity; i++)
                _bigMemoryBlocks[newBlockOffset].Add(i, AllocateVariable($"ALCBLOK-{newBlockOffset}-{i}", size));

            return new FarPtr(0xFFFF, (ushort)newBlockOffset);
        }

        /// <summary>
        ///     Returns the specified block by index in the desired memory block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public FarPtr GetBigMemoryBlock(FarPtr block, ushort index) => _bigMemoryBlocks[block.Offset][index];

        /// <summary>
        ///     Returns a newly allocated Segment in "Real Mode" memory
        /// </summary>
        /// <returns></returns>
        public FarPtr AllocateRealModeSegment(ushort segmentSize = ushort.MaxValue)
        {
            _currentRealModePointer.Segment++;
            var realModeSegment = new FarPtr(_currentRealModePointer);
            AddSegment(realModeSegment.Segment, segmentSize);
            return realModeSegment;
        }
    }
}
