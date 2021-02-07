using MBBSEmu.HostProcess;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextVariables;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace MBBSEmu.Session
{
    public class TestSession : SessionBase
    {
        private readonly BlockingCollection<byte> _data = new BlockingCollection<byte>();

        public TestSession(IMbbsHost host, ITextVariableService textVariableService) : base(host, "test", EnumSessionState.EnteringModule, textVariableService)
        {
            SendToClientMethod = Send;
            OutputEnabled = true;

            CurrentModule = host?.GetModule("MBBSEMU");

            SessionType = EnumSessionType.Test;

            Username = "Sysop";
            Email  = "sysop@grnet.com";
        }

        public override void Stop() {}


        /// <summary>
        ///     Reads data from the module until a new line is received, and returns the line with
        ///     the line endings removed.
        /// </summary>
        /// <param name="timeout">Maximum time to wait before throwing a TimeoutException</param>
        public string GetLine(TimeSpan timeout)
        {
            return GetLine('\n', timeout).Trim('\r', '\n');
        }

        /// <summary>
        ///     Reads data from the module until endingCharacter is received, and returns all data
        ///     accumulated including endingCharacter
        /// </summary>
        /// <param name="endingCharacter">Character which aborts reading</param>
        /// <param name="timeout">Maximum time to wait before throwing a TimeoutException</param>
        public string GetLine(char endingCharacter, TimeSpan timeout)
        {
            var line = new MemoryStream(80);
            while (true)
            {
                if (!_data.TryTake(out var b, timeout))
                {
                    throw new TimeoutException("Timeout, module likely didn't output expected text");
                }

                line.WriteByte(b);

                if (b == endingCharacter)
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(line.ToArray());
        }

        /// <summary>
        ///     Sends data originating from the module to the connected session, for consumption by
        ///     the test.
        /// </summary>
        /// <param name="dataToSend"></param>
        public virtual void Send(byte[] dataToSend)
        {
            foreach(var b in dataToSend)
            {
              _data.Add(b);
            }
        }

        /// <summary>
        ///     Sends client data to the module.
        /// </summary>
        /// <param name="dataToSend"></param>
        public void SendToModule(byte[] dataToSend)
        {
            foreach(var b in dataToSend)
            {
                DataFromClient.Add(b);
            }
        }
    }
}
