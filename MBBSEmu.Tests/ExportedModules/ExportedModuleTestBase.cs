using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.TextVariables;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.Tests.ExportedModules
{
    public abstract class ExportedModuleTestBase : TestBase
    {
        // list of ordinals that use the __stdcall convention, which means the callee cleans up the
        // stack.
        // __cdecl convention has the caller cleaning up the stack.
        private static readonly HashSet<ushort> STDCALL_ORDINALS = new HashSet<ushort> {
            654, // f_ldiv
            656, // f_ludiv
            665, // f_scopy
            655, // f_lmod
            657, // f_lumod
        };

        protected const ushort STACK_SEGMENT = 0;
        protected const ushort CODE_SEGMENT = 1;

        protected readonly FakeClock fakeClock = new FakeClock();
        protected CpuCore mbbsEmuCpuCore;
        protected MemoryCore mbbsEmuMemoryCore;
        protected CpuRegisters mbbsEmuCpuRegisters;
        protected MbbsModule mbbsModule;
        protected HostProcess.ExportedModules.Majorbbs majorbbs;
        protected HostProcess.ExportedModules.Galgsbl galgsbl;
        protected PointerDictionary<SessionBase> testSessions;
        protected readonly ServiceResolver _serviceResolver;

        protected ExportedModuleTestBase() : this(Path.GetTempPath()) {}

        protected ExportedModuleTestBase(string modulePath)
        {
            _serviceResolver = new ServiceResolver(fakeClock, SessionBuilder.ForTest($"MBBSDb_{RANDOM.Next()}"));
            var textVariableService = _serviceResolver.GetService<ITextVariableService>();

            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsModule = new MbbsModule(FileUtility.CreateForTest(), fakeClock, _serviceResolver.GetService<ILogger>(), null, modulePath, mbbsEmuMemoryCore);

            testSessions = new PointerDictionary<SessionBase>();
            testSessions.Allocate(new TestSession(null, textVariableService));
            testSessions.Allocate(new TestSession(null, textVariableService));

            majorbbs = new HostProcess.ExportedModules.Majorbbs(
                _serviceResolver.GetService<IClock>(),
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions,
                _serviceResolver.GetService<IAccountKeyRepository>(),
                _serviceResolver.GetService<IAccountRepository>());

            galgsbl = new HostProcess.ExportedModules.Galgsbl(
                _serviceResolver.GetService<IClock>(),
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions);

            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, ExportedFunctionDelegate, null);
        }

        private ReadOnlySpan<byte> ExportedFunctionDelegate(ushort ordinal, ushort functionOrdinal)
        {
            switch (ordinal)
            {
                case HostProcess.ExportedModules.Majorbbs.Segment:
                    {
                        majorbbs.SetRegisters(mbbsEmuCpuRegisters);
                        return majorbbs.Invoke(functionOrdinal, offsetsOnly: false);
                    }
                case HostProcess.ExportedModules.Galgsbl.Segment:
                    {
                        galgsbl.SetRegisters(mbbsEmuCpuRegisters);
                        return galgsbl.Invoke(functionOrdinal, offsetsOnly: false);
                    }
                default:
                    throw new Exception($"Unsupported Exported Module Segment: {ordinal}");
            }
        }

        protected virtual void Reset()
        {
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuRegisters.CS = CODE_SEGMENT;
            mbbsEmuCpuRegisters.IP = 0;

            testSessions = new PointerDictionary<SessionBase>();
            var textVariableService = _serviceResolver.GetService<ITextVariableService>();
            testSessions.Allocate(new TestSession(null, textVariableService));
            testSessions.Allocate(new TestSession(null, textVariableService));

            //Redeclare to re-allocate memory values that have been cleared
            majorbbs = new HostProcess.ExportedModules.Majorbbs(
                _serviceResolver.GetService<IClock>(),
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions,
                _serviceResolver.GetService<IAccountKeyRepository>(),
                _serviceResolver.GetService<IAccountRepository>());

            galgsbl = new HostProcess.ExportedModules.Galgsbl(
                _serviceResolver.GetService<IClock>(),
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions);

        }

        /// <summary>
        ///     Executes an x86 Instruction to call the specified Library/API Ordinal with the specified arguments
        /// </summary>
        /// <param name="exportedModuleSegment"></param>
        /// <param name="apiOrdinal"></param>
        /// <param name="apiArguments"></param>
        protected void ExecuteApiTest(ushort exportedModuleSegment, ushort apiOrdinal, IEnumerable<ushort> apiArguments)
        {
            if (!mbbsEmuMemoryCore.HasSegment(STACK_SEGMENT))
            {
                mbbsEmuMemoryCore.AddSegment(STACK_SEGMENT);
            }

            if (mbbsEmuMemoryCore.HasSegment(CODE_SEGMENT))
            {
                mbbsEmuMemoryCore.RemoveSegment(CODE_SEGMENT);
            }

            var apiTestCodeSegment = new Segment
            {
                Ordinal = CODE_SEGMENT,
                //Create a new CODE Segment with a
                //simple ASM call for CALL FAR librarySegment:apiOrdinal
                Data = new byte[] { 0x9A, (byte)(apiOrdinal & 0xFF), (byte)(apiOrdinal >> 8), (byte)(exportedModuleSegment & 0xFF), (byte)(exportedModuleSegment >> 8), },
                Flag = (ushort)EnumSegmentFlags.Code
            };
            mbbsEmuMemoryCore.AddSegment(apiTestCodeSegment);

            mbbsEmuCpuRegisters.CS = CODE_SEGMENT;
            mbbsEmuCpuRegisters.IP = 0;

            //Push Arguments to Stack
            foreach (var a in apiArguments.Reverse())
                mbbsEmuCpuCore.Push(a);

            //Process Instruction, e.g. call the method
            mbbsEmuCpuCore.Tick();

            if (isCdeclOrdinal(apiOrdinal))
                foreach (var a in apiArguments)
                    mbbsEmuCpuCore.Pop();
        }

        private static bool isCdeclOrdinal(ushort ordinal) => !STDCALL_ORDINALS.Contains(ordinal);

        /// <summary>
        ///     Executes an x86 Instruction to call the specified Library/API Ordinal with the specified arguments
        /// </summary>
        /// <param name="exportedModuleSegment"></param>
        /// <param name="apiOrdinal"></param>
        /// <param name="apiArguments"></param>
        protected void ExecuteApiTest(ushort exportedModuleSegment, ushort apiOrdinal, IEnumerable<FarPtr> apiArguments)
        {
            var argumentsList = new List<ushort>(apiArguments.Count() * 2);

            foreach (var a in apiArguments)
            {
                argumentsList.Add(a.Offset);
                argumentsList.Add(a.Segment);
            }

            ExecuteApiTest(exportedModuleSegment, apiOrdinal, argumentsList);
        }

        /// <summary>
        ///     Executes a test directly against the MajorBBS Exported Module to evaluate the return value of a given property
        ///
        ///     We invoke these directly as properties are handled at decompile time by applying the relocation information to the memory
        ///     address for the property. Because Unit Tests aren't going through the same relocation process, we simulate it by getting the
        ///     SEG:OFF of the Property as it would be returned during relocation. This allows us to evaluate the given value of the returned
        ///     address.
        /// </summary>
        /// <param name="apiOrdinal"></param>
        protected ReadOnlySpan<byte> ExecutePropertyTest(ushort apiOrdinal) => majorbbs.Invoke(apiOrdinal);

        /// <summary>
        ///     Generates Parameters that can be passed into a method
        ///
        ///     Memory must be Reset() between runs or else string will remain allocated in the heap
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        protected List<ushort> GenerateParameters(object[] values)
        {
            var parameters = new List<ushort>();
            foreach (var v in values)
            {
                switch (v)
                {
                    case string @parameterString:
                    {
                        var stringParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(@parameterString.Length + 1));
                        mbbsEmuMemoryCore.SetArray(stringParameterPointer, Encoding.ASCII.GetBytes(@parameterString));
                        parameters.Add(stringParameterPointer.Offset);
                        parameters.Add(stringParameterPointer.Segment);
                        break;
                    }
                    case uint @parameterULong:
                    {
                        var longBytes = BitConverter.GetBytes(@parameterULong);
                        parameters.Add(BitConverter.ToUInt16(longBytes, 0));
                        parameters.Add(BitConverter.ToUInt16(longBytes, 2));
                        break;
                    }
                    case int @parameterLong:
                    {
                        var longBytes = BitConverter.GetBytes(@parameterLong);
                        parameters.Add(BitConverter.ToUInt16(longBytes, 0));
                        parameters.Add(BitConverter.ToUInt16(longBytes, 2));
                        break;
                    }
                    case ushort @parameterUInt:
                        parameters.Add(@parameterUInt);
                        break;

                    case short @parameterInt:
                        parameters.Add((ushort)@parameterInt);
                        break;
                }
            }

            return parameters;
        }

        /// <summary>
        ///     Sets the current btrieve file (BB value) based on btrieveFile
        /// </summary>
        protected void AllocateBB(BtrieveFile btrieveFile, ushort maxRecordLength)
        {
            var btrieve = new BtrieveFileProcessor() { FullPath = Path.Combine(mbbsModule.ModulePath, btrieveFile.FileName) };
            var connectionString = "Data Source=acs.db;Mode=Memory";

            btrieve.CreateSqliteDBWithConnectionString(connectionString, btrieveFile);
            majorbbs.AllocateBB(btrieve, maxRecordLength, Path.GetFileName(btrieve.FullPath));
        }
    }
}
