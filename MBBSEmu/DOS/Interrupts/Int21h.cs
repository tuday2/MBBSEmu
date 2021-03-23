﻿using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.DOS.Structs;
using MBBSEmu.Extensions;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace MBBSEmu.DOS.Interrupts
{
    /// <summary>
    ///     Interrupt Vector 21h which handles the main DOS APIs
    ///
    ///     This is implemented within the DOS Kernel
    /// </summary>
    public class Int21h : IInterruptHandler
    {
        public const MethodImplOptions SubroutineCompilerOptimizations = MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining;

        const int DEFAULT_BLOCK_DEVICE = 2; //C:

        private ILogger _logger { get; init; }
        private CpuRegisters _registers { get; init; }
        private IMemoryCore _memory { get; init; }
        private IClock _clock { get; init; }
        private TextReader _stdin { get; init; }
        private TextWriter _stdout { get; init; }
        private TextWriter _stderr { get; init; }

        /// <summary>
        ///     Path of the current Execution Context
        /// </summary>
        private string _path { get; init; }

        /// <summary>
        ///     INT 21h defined Disk Transfer Area
        ///
        ///     Buffer used to hold information on the current Disk / IO operation
        /// </summary>
        private FarPtr DiskTransferArea = new(0xF000, 0x1000);

        public byte Vector => 0x21;

        private readonly Dictionary<byte, FarPtr> _interruptVectors;

        private readonly IFileUtility _fileUtility;

        private readonly Dictionary<int, FileStream> _fileHandles = new();

        public enum AllocationStrategy
        {
            FIRST_FIT = 0,
            BEST_FIT = 1,
            LAST_FIT = 2,
        }

        public enum FileHandle
        {
            STDIN = 0,
            STDOUT = 1,
            STDERR = 2,
            STDAUX = 3,
            STDPRN = 4,
        }

        private AllocationStrategy _allocationStrategy = AllocationStrategy.BEST_FIT;

        public Int21h(CpuRegisters registers, IMemoryCore memory, IClock clock, ILogger logger, IFileUtility fileUtility, TextReader stdin, TextWriter stdout, TextWriter stderr, string path = "")
        {
            _registers = registers;
            _memory = memory;
            _clock = clock;
            _logger = logger;
            _fileUtility = fileUtility;
            _stdin = stdin;
            _stdout = stdout;
            _stderr = stderr;
            _interruptVectors = new Dictionary<byte, FarPtr>();
            _path = path;
        }

        public void Handle()
        {
            //_logger.Error($"Interrupt AX {_registers.AX:X4} H:{_registers.AH:X2}");

            switch (_registers.AH)
            {
                case 0x3F:
                    ReadFromFileHandle_0x3F();
                    break;
                case 0x42:
                    MoveFilePointer_0x42();
                    break;
                case 0x3D:
                    OpenFile_0x3D();
                    break;
                case 0x3E:
                    CloseFile_0x3E();
                    break;
                case 0x43:
                    GetOrPutFileAttributes_0x43();
                    break;
                case 0x01:
                    KeyboardInputWithEcho_0x01();
                    break;
                case 0x67:
                    SetHandleCount_0x67();
                    break;
                case 0x48:
                    AllocateMemory_0x48();
                    break;
                case 0x49:
                    FreeMemory_0x49();
                    break;
                case 0x58:
                    GetOrSetMemoryAllocationStrategy_0x58();
                    break;
                case 0x09:
                    PrintString_0x09();
                    break;
                case 0x19:
                    GetDefaultDiskNumber_0x19();
                    break;
                case 0x1A:
                    SetDiskTransferArea_0x1A();
                    break;
                case 0x25:
                    SetInterruptVector_0x25();
                    break;
                case 0x2A:
                    GetCurrentDate_0x2A();
                    break;
                case 0x2C:
                    GetCurrentTime_0x2C();
                    break;
                case 0x2F:
                    GetDiskTransferAreaAddress_0x2F();
                    break;
                case 0x30:
                    GetDOSVersion_0x30();
                    break;
                case 0x35:
                    GetInterruptVector_0x35();
                    break;
                case 0x40:
                    WriteToFileWithHandle_0x40();
                    break;
                case 0x44 when _registers.AL == 0x00:
                    GetDeviceInformation();
                    return;
                case 0x47:
                    GetCurrentDirectory_0x47();
                    break;
                case 0x4A:
                    AdjustMemoryBlockSize_0x4A();
                    break;
                case 0x4C:
                    QuitWithExitCode_0x4C();
                    break;
                case 0x4E:
                    FindFirstAsciz_0x4E();
                    break;
                case 0x62:
                    GetPSPAddress_0x62();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported INT 21h Function: 0x{_registers.AH:X2}");
            }
        }

        [MethodImpl(SubroutineCompilerOptimizations)]
        private void ClearCarryFlag() => _registers.F = _registers.F.ClearFlag((ushort)EnumFlags.CF);

        [MethodImpl(SubroutineCompilerOptimizations)]
        private void SetCarryFlagErrorCodeInAX(DOSErrorCode code)
        {
            _registers.F = _registers.F.SetFlag((ushort)EnumFlags.CF);
            _registers.AX = (ushort)code;
        }

        private void ReadFromFileHandle_0x3F()
        {
            /*
            INT 21 - AH = 3Fh DOS 2+ - READ FROM FILE WITH HANDLE
            BX = file handle
            CX = number of bytes to read
            DS:DX = address of buffer
            Return: CF set on error
              AX = error code
            CF clear if successful
              AX = number of bytes read
            */
            var fileHandle = _registers.BX;
            var bytesToRead = _registers.CX;
            var destPtr = new FarPtr(_registers.DS, _registers.DX);

            if (!_fileHandles.TryGetValue(fileHandle, out var fileStream))
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INVALID_HANDLE);
                return;
            }

            try
            {
                var buf = new byte[bytesToRead];
                var actualBytesRead = fileStream.Read(buf, 0, bytesToRead);

                if (actualBytesRead > 0)
                {
                    _memory.SetArray(destPtr, buf.AsSpan().Slice(0, actualBytesRead));
                }

                ClearCarryFlag();
                _registers.AX = (ushort)actualBytesRead;
            }
            catch (Exception)
            {
                _logger.Warn($"Unable to read {bytesToRead} bytes from FD:{fileHandle}");
                SetCarryFlagErrorCodeInAX(DOSErrorCode.READ_FAULT);
            }
        }

        private void MoveFilePointer_0x42()
        {
            /*
            INT 21 - AH = 42h DOS 2+ - MOVE FILE READ/WRITE POINTER (LSEEK)
                    AL = method value
                        0 = offset from beginning of file
                        1 = offset from present location
                        2 = offset from end of file
                    BX = file handle
                    CX:DX = offset in bytes
                    Return: CF set on error
                        AX = error code
                        CF clear if successful
                        DX:AX = new offset
            */
            SeekOrigin seekOrigin;
            var fileHandle = _registers.BX;
            var offset = (uint)(_registers.CX << 16 | _registers.DX);

            switch (_registers.AL)
            {
                case 0: // begin
                    seekOrigin = SeekOrigin.Begin;
                    break;
                case 1: // current
                    seekOrigin = SeekOrigin.Current;
                    break;
                case 2: // end
                    seekOrigin = SeekOrigin.End;
                    break;
                default:
                    SetCarryFlagErrorCodeInAX(DOSErrorCode.UNKNOWN_COMMAND);
                    return;
            }

            if (!_fileHandles.TryGetValue(fileHandle, out var fileStream))
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INVALID_HANDLE);
                return;
            }

            try
            {
                var position = fileStream.Seek(offset, seekOrigin);

                ClearCarryFlag();
                _registers.DX = (ushort)(position >> 16);
                _registers.AX = (ushort)position;
            }
            catch (Exception)
            {
                _logger.Warn($"Unable to seek to {offset} from FD:{fileHandle}");
                SetCarryFlagErrorCodeInAX(DOSErrorCode.SEEK_ERROR);
            }
        }

        private void KeyboardInputWithEcho_0x01()
        {
            // DOS - KEYBOARD INPUT (with echo)
            // Return: AL = character read
            // TODO (check ^C/^BREAK) and if so EXECUTE int 23h
            var c = (byte)_stdin.Read();
            _stdout.Write((char)c);
            _registers.AL = c;
        }

        private void SetHandleCount_0x67()
        {
            // DOS - SET HANDLE COUNT
            // BX : Number of handles
            // Return: carry set if error (and error code in AX)
            ClearCarryFlag();
        }

        private void AllocateMemory_0x48()
        {
            // DOS - Allocate memory
            // BX = number of 16-byte paragraphs desired
            // Return: CF set on error
            //             AX = error code
            //             BX = maximum available
            //         CF clear if successful
            //             AX = segment of allocated memory block
            var ptr = _memory.Malloc((uint)(_registers.BX * 16));
            if (!ptr.IsNull() && ptr.Offset != 0)
                throw new DataMisalignedException("RealMode allocator returned memory not on segment boundary");

            if (ptr.IsNull())
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INSUFFICIENT_MEMORY);
                _registers.BX = 0; // TODO get maximum available here
            }
            else
            {
                ClearCarryFlag();
                _registers.AX = ptr.Segment;
            }
        }

        private void FreeMemory_0x49()
        {
            // DOS - Free Memory
            // ES = Segment address of area to be freed
            // Return: CF set on error
            //             AX = error code
            //         CF clear if successful
            _memory.Free(new FarPtr(_registers.ES, 0));
            // no status, so always say we're good
            ClearCarryFlag();
        }

        private void GetOrSetMemoryAllocationStrategy_0x58()
        {
            // INT 21 - AH = 58h DOS 3.x - GET/SET MEMORY ALLOCATION STRATEGY
            // AL = function code
            //     0 = get allocation strategy
            //     1 = set allocation strategy
            // BL = strategy code
            //     0 first fit (use first memory block large enough)
            //     1 best fit (use smallest memory block large enough)
            //     2 last fit (use high part of last usable memory block)
            // Return:
            //   CF set on error
            //     AX = error code
            //   CF clear if successful
            //     AX = strategy code
            // Note: the Set subfunction accepts any value in BL; 2 or greater means last fit.
            // the Get subfunction returns the last value set, so programs should check
            // whether the value is >= 2, not just equal to 2.

            if (_registers.AL == 0)
            {
                ClearCarryFlag();
                _registers.AX = (ushort)_allocationStrategy;
            }
            else if (_registers.AL == 1)
            {
                if (_registers.BL > 2)
                    _allocationStrategy = AllocationStrategy.LAST_FIT;
                else
                    _allocationStrategy = (AllocationStrategy)_registers.BL;

                ClearCarryFlag();
                _registers.AX = (ushort)_allocationStrategy;
            }
            else
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.UNKNOWN_COMMAND);
            }
            return;
        }

        private void PrintString_0x09()
        {
            /*
                DS:DX = address of string terminated by "$"
                Note: ^C/^Break checked, and INT 23h called if pressed
            */

            var src = new FarPtr(_registers.DS, _registers.DX);
            var memoryStream = new MemoryStream();
            byte b;
            while ((b = _memory.GetByte(src++)) != '$')
                memoryStream.WriteByte(b);

            _stdout.Write(Encoding.ASCII.GetString(memoryStream.ToArray()));
        }

        private void GetDefaultDiskNumber_0x19()
        {
            //DOS - GET DEFAULT DISK NUMBER
            //Return: AL = Drive Number
            _registers.AL = DEFAULT_BLOCK_DEVICE;
        }

        private void SetDiskTransferArea_0x1A()
        {
            //Specifies the memory area to be used for subsequent FCB operations.
            //DS:DX = Segment:offset of DTA
            DiskTransferArea = new FarPtr(_registers.DS, _registers.DX);
            return;
        }

        /// <summary>
        ///     TODO: Should write to 0000:0000 instead of internally
        /// </summary>
        private void SetInterruptVector_0x25()
        {
            /*
                INT 21 - AH = 25h DOS - SET INTERRUPT VECTOR
                AL = interrupt number
                DS:DX = new vector to be used for specified interrupt
            */

            var interruptVector = _registers.AL;
            var newVectorPointer = new FarPtr(_registers.DS, _registers.DX);

            _interruptVectors[interruptVector] = newVectorPointer;
        }

        private void GetCurrentDate_0x2A()
        {
            //DOS - GET CURRENT DATE
            //Return: DL = day, DH = month, CX = year
            //AL = day of the week(0 = Sunday, 1 = Monday, etc.)
            var now = _clock.Now;
            _registers.DL = (byte)now.Day;
            _registers.DH = (byte)now.Month;
            _registers.CX = (ushort)now.Year;
            _registers.AL = (byte)now.DayOfWeek;
        }

        private void GetCurrentTime_0x2C()
        {
            //DOS - GET CURRENT TIME
            //Return: CH = hour, CL = minute, DH = second, DL = 1/100 seconds
            var now = _clock.Now;
            _registers.CH = (byte) now.Hour;
            _registers.CL = (byte) now.Minute;
            _registers.DH = (byte) now.Second;
            _registers.DL = (byte) (now.Millisecond / 10);
        }

        private void GetDiskTransferAreaAddress_0x2F()
        {
            /*
                *  Action:	Returns the segment:offset of the current DTA for read/write operations.
                On entry:	AH = 2Fh
                Returns:	ES:BX = Segment.offset of current DTA
            */
            _registers.ES = DiskTransferArea.Segment;
            _registers.BX = DiskTransferArea.Offset;
        }

        private void GetDOSVersion_0x30()
        {
            /*  DOS 2+ - GET DOS VERSION
                AH = 30h
                Return: AL = Major Version number (0 for DOS 1.x)
                AH = Minor Version number
                BH = OEM number
                    00h IBM
                    16h DEC
                BL:CX = 24-bit user number
            */
            _registers.AL = 6;
            _registers.AH = 22;
            _registers.BH = 0;
            _registers.CX = 0x1234;
        }

        /// <summary>
        ///     TODO: These interrupt vectors live at memory address 0000:0000 and should be read
        ///           from there.
        /// </summary>
        private void GetInterruptVector_0x35()
        {
            /*
                INT 21 - AH = 35h DOS 2+ - GET INTERRUPT VECTOR
                AL = interrupt number
                Return: ES:BX = value of interrupt vector
            */

            if (!_interruptVectors.TryGetValue(_registers.AL, out var resultVector))
            {
                _registers.ES = 0xFFFF;
                _registers.BX = _registers.AL;
            }
            else
            {
                _registers.ES = resultVector.Segment;
                _registers.BX = resultVector.Offset;
            }
        }

        private void WriteToFileWithHandle_0x40()
        {
            /*
                INT 21 - AH = 40h DOS 2+ - WRITE TO FILE WITH HANDLE
                BX = file handle
                CX = number of bytes to write
                DS:DX -> buffer

                Return: CF set on error
                    AX = error code

                CF clear if successful
                    AX = number of bytes written

                Note: if CX is zero, no data is written, and the file is truncated or extended
                    to the current position
            */
            var fileHandle = _registers.BX;
            var numberOfBytes = _registers.CX;
            var bufferPointer = new FarPtr(_registers.DS, _registers.DX);

            var dataToWrite = _memory.GetArray(bufferPointer, numberOfBytes);

            switch (fileHandle)
            {
                case (ushort)FileHandle.STDIN:
                    SetCarryFlagErrorCodeInAX(DOSErrorCode.WRITE_FAULT);
                    break;
                case (ushort)FileHandle.STDOUT:
                    _stdout.Write(Encoding.ASCII.GetString(dataToWrite));
                    ClearCarryFlag();
                    _registers.AX = numberOfBytes;
                    return;
                case (ushort)FileHandle.STDERR:
                    _stderr.Write(Encoding.ASCII.GetString(dataToWrite));
                    ClearCarryFlag();
                    _registers.AX = numberOfBytes;
                    return;
                default:
                    break;
            }

            if (!_fileHandles.TryGetValue(fileHandle, out var fileStream))
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INVALID_HANDLE);
                return;
            }

            try
            {
                var initial = fileStream.Position;
                fileStream.Write(dataToWrite);
                var final = fileStream.Position;

                ClearCarryFlag();
                _registers.AX = (ushort)(final - initial);
            }
            catch (Exception)
            {
                _logger.Warn($"Unable to write {numberOfBytes} to FD:{fileHandle}");
                SetCarryFlagErrorCodeInAX(DOSErrorCode.WRITE_FAULT);
            }
        }

        private void GetCurrentDirectory_0x47()
        {
            /*
                DOS 2+ - GET CURRENT DIRECTORY
                DL = drive (0=default, 1=A, etc.)
                DS:SI points to 64-byte buffer area
                Return: CF set on error
                AX = error code
                Note: the returned path does not include the initial backslash
            */
            _memory.SetArray(_registers.DS, _registers.SI, Encoding.ASCII.GetBytes("BBSV6\0"));
            _registers.DL = DEFAULT_BLOCK_DEVICE;
            ClearCarryFlag();
        }

        private void AdjustMemoryBlockSize_0x4A()
        {
            /*
                INT 21 - AH = 4Ah DOS 2+ - ADJUST MEMORY BLOCK SIZE (SETBLOCK)
                ES = Segment address of block to change
                BX = New size in paragraphs
                Return: CF set on error
                AX = error code
                BX = maximum size possible for the block

                Because MBBSEmu allocates blocks as 0xFFFF in length, we ignore this and proceed
            */

            var segmentToAdjust = _registers.ES;
            var newSize = _registers.BX;

            if (_memory is ProtectedModeMemoryCore protectedMemory)
            {
                if (!protectedMemory.HasSegment(segmentToAdjust))
                    protectedMemory.AddSegment(segmentToAdjust);

                _registers.BX = 0xFFFF;
                ClearCarryFlag();
                return;
            }

            // real mode memory
            var ptr = new FarPtr(_registers.ES, 0);
            var currentBlockSize = _memory.GetAllocatedMemorySize(ptr);
            _logger.Warn($"int21 0x4A: AdjustMemoryBlockSize called, from {ptr}:{currentBlockSize} to {_registers.BX}. We don't really support it");
            if (currentBlockSize < 0)
            {
                _registers.BX = 0;
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INVALID_MEMORY_BLOCK_ADDRESS);
            }
            else
            {
                _registers.BX = (ushort)currentBlockSize;
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INSUFFICIENT_MEMORY);
            }
        }

        private void QuitWithExitCode_0x4C()
        {
            /*
                INT 21 - AH = 4Ch DOS 2+ - QUIT WITH EXIT CODE (EXIT)
                AL = exit code
                Return: never returns
            */
            _stdout.Flush();
            _stderr.Flush();

            //_stdout.WriteLine($"Exiting With Exit Code: {_registers.AL}");
            _registers.Halt = true;
        }

        private void FindFirstAsciz_0x4E()
        {
            /*
            INT 21 - AH = 4Eh DOS 2+ - FIND FIRST ASCIZ (FIND FIRST)
            CX = search attributes
            DS:DX -> ASCIZ filename
            Return: CF set on error
                AX = error code
                [DTA] = data block
                undocumented fields
                    PC-DOS 3.10
                            byte 00h: drive letter
                            bytes 01h-0Bh: search template
                            byte 0Ch: search attributes
                    DOS 2.x (and DOS 3.x except 3.1???)
                            byte 00h: search attributes
                            byte 01h: drive letter
                            bytes 02h-0Ch: search template
                            bytes 0Dh-0Eh: entry count within directory
                            bytes 0Fh-12h: reserved
                            bytes 13h-14h: cluster number of parent directory
                            byte 15h: attribute of file found
                            bytes 16h-17h: file time
                            bytes 18h-19h: file date
                            bytes 1Ah-1Dh: file size
                            bytes 1Eh-3Ah: ASCIZ filename+extension
            */
            var fileName = Encoding.ASCII.GetString(_memory.GetString(_registers.DS, _registers.DX, stripNull: true));
            var foundFile = _fileUtility.FindFile(_path, fileName);

            if(!File.Exists($"{_path}{foundFile}"))
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.FILE_NOT_FOUND);
                return;
            }

            ClearCarryFlag();
            throw new NotImplementedException();
        }

        private void GetPSPAddress_0x62()
        {
            /*
                INT 21 - AH = 62h DOS 3.x - GET PSP ADDRESS
                Return: BX = segment address of PSP

                This is only set when an EXE is running, thus should only be called from
                an EXE.
            */
            if (!_memory.TryGetVariablePointer("Int21h-PSP", out var pspPointer))
                throw new Exception("No PSP has been defined");

            _registers.BX = _memory.GetWord(pspPointer);
        }

        private void GetDeviceInformation()
        {
            /*
                INT 21 - AX = 4400h DOS 2+ - IOCTL - GET DEVICE INFORMATION
                BX = file or device handle
                Return: CF set on error
                    AX = error code
                CF clear if successful
                    DX = device info
            */

            ClearCarryFlag();

            switch (_registers.BX)
            {
                case (ushort)FileHandle.STDIN:
                    _registers.DX = 0x80 | 0x40 | 0x1;
                    break;
                case (ushort)FileHandle.STDERR:
                case (ushort)FileHandle.STDOUT:
                    _registers.DX = 0x80 | 0x40 | 0x2;
                    break;
                default:
                    if (!_fileHandles.TryGetValue(_registers.BX, out var fileStream))
                    {
                        SetCarryFlagErrorCodeInAX(DOSErrorCode.INVALID_HANDLE);
                        return;
                    }

                    _registers.DX = DEFAULT_BLOCK_DEVICE;
                    break;
            }
        }

        private void GetOrPutFileAttributes_0x43()
        {
            /*
            INT 21 - AH = 43h DOS 2+ - GET/PUT FILE ATTRIBUTES (CHMOD)
            AL =
                0 get file attributes
                1 put file attributes
            CX = file attribute bits
                0 = read only
                1 = hidden file
                2 = system file
                3 = volume label
                4 = subdirectory
                5 = written since backup
                8 = shareable (Novell NetWare)
            DS:DX -> ASCIZ file name
            Return: CF set on error
                AX = error code
                CX = file attributes on get
            */
            var file = Encoding.ASCII.GetString(_memory.GetString(_registers.DS, _registers.DX, stripNull: true));
            if (_registers.AL != 0)
                throw new NotImplementedException();

            var fileInfo = new FileInfo(file);
            if (!fileInfo.Exists)
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.FILE_NOT_FOUND);
                return;
            }

            _registers.CX = 0;
            if (fileInfo.IsReadOnly)
                _registers.CX |= (ushort)EnumDirectoryAttributeFlags.ReadOnly;
            if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                _registers.CX |= (ushort)EnumDirectoryAttributeFlags.Hidden;
            if (fileInfo.Attributes.HasFlag(FileAttributes.System))
                _registers.CX |= (ushort)EnumDirectoryAttributeFlags.System;
            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                _registers.CX |= (ushort)EnumDirectoryAttributeFlags.Directory;
            if (fileInfo.Attributes.HasFlag(FileAttributes.Archive))
                _registers.CX |= (ushort)EnumDirectoryAttributeFlags.Archive;

            ClearCarryFlag();
        }

        private void CloseFile_0x3E()
        {
            /*
            INT 21 - AH = 3Eh DOS 2+ - CLOSE A FILE WITH HANDLE
            BX = file handle
            Return: CF set on error
                AX = error code
            */

            ClearCarryFlag();

            switch (_registers.BX)
            {
                case (ushort)FileHandle.STDIN:
                    _stdin.Close();
                    return;
                case (ushort)FileHandle.STDOUT:
                    _stdout.Close();
                    return;
                case (ushort)FileHandle.STDERR:
                    _stderr.Close();
                    return;
                case (ushort)FileHandle.STDAUX:
                case (ushort)FileHandle.STDPRN:
                    return;
                default:
                    // in file handle table
                    break;
            }

            var fileHandle = _registers.BX;

            _logger.Debug($"Closing file {fileHandle}");

            if (!_fileHandles.TryGetValue(fileHandle, out var fileStream))
            {
                SetCarryFlagErrorCodeInAX(DOSErrorCode.INVALID_HANDLE);
                return;
            }

            _fileHandles.Remove(fileHandle);

            fileStream.Close();
            fileStream.Dispose();
        }

        private void OpenFile_0x3D()
        {
            /*
            INT 21 - AH = 3Dh DOS 2+ - OPEN DISK FILE WITH HANDLE
            AL = access code
                0 = Read Only
                1 = Write Only
                2 = Read/Write
            AL bits 7-3 = file-sharing modes (DOS 3.x)
                bit 7 = inheritance flag, set for no inheritance
                bits 4-6 = sharing mode
                000 compatibility mode
                001 exclusive (deny all)
                010 write access denied (deny write)
                011 read access denied (deny read)
                100 full access permitted (deny none)
                bit 3 = reserved, should be zero
            DS:DX = address of ASCIZ filename
            Return:
                CF set on error
                    AX = error code
                CF clear if successful
                    AX = file handle
            */

            var fullPath = Encoding.ASCII.GetString(_memory.GetString(_registers.DS, _registers.DX, stripNull: true));
            FileMode fileMode = FileMode.Open;
            FileAccess fileAccess;
            switch (_registers.AL)
            {
                case 0:
                    fileAccess = FileAccess.Read;
                    break;
                case 1:
                    fileAccess = FileAccess.Write;
                    break;
                case 2:
                default:
                    fileAccess = FileAccess.ReadWrite;
                    break;
            }

            //Setup the File Stream
            try
            {
                var fileStream = File.Open(fullPath, fileMode, fileAccess);
                var handle = GetNextHandle();

                _logger.Debug($"Opening file {fullPath} as FD:{handle}");

                _fileHandles[handle] = fileStream;

                ClearCarryFlag();
                _registers.AX = (ushort)handle;
            }
            catch (Exception ex)
            {
                SetCarryFlagErrorCodeInAX(ExceptionToErrorCode(ex));
            }
        }

        private int GetNextHandle()
        {
            if (_fileHandles.Count == 0)
                return 6;

            int handle = _fileHandles.Max(kvp => kvp.Key) + 1;
            if (handle <= 5)
                handle = 6;

            return handle;
        }

        private DOSErrorCode ExceptionToErrorCode(Exception ex)
        {
            if (ex is PathTooLongException)
                return DOSErrorCode.PATH_NOT_FOUND;
            if (ex is DirectoryNotFoundException)
                return DOSErrorCode.PATH_NOT_FOUND;
            if (ex is IOException)
                return DOSErrorCode.WRITE_FAULT;
            if (ex is UnauthorizedAccessException)
                return DOSErrorCode.ACCESS_DENIED;
            if (ex is FileNotFoundException)
                return DOSErrorCode.FILE_NOT_FOUND;

            return DOSErrorCode.GENERAL_FAILURE;
        }
    }
}
