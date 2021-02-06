using System.Collections.Generic;
using System.IO;
using System;

namespace MBBSEmu.Util
{
    public class AnsiParser
    {
        public enum AnsiColor
        {
            BLACK,
            RED,
            GREEN,
            YELLOW,
            BLUE,
            MAGENTA,
            CYAN,
            WHITE,
        }

        public enum AnsiAttribute
        {
            NONE,
            BOLD,
            FAINT,
            ITALIC,
            UNDERLINE,
            SLOW_BLINK,
            RAPID_BLINK,
            REVERSE_VIDEO,
            CONCEAL,
            CROSSED_OUT,
        }

        private enum AnsiParseState
        {
            NORMAL,
            ESCAPE,
            BRACKET,
            VALUE_ACCUM,
            WAIT_FOR_ANSI_END,
        };

        private const char ASCII_ESCAPE = (char)0x1B;
        private static readonly HashSet<char> ANSI_ENDS =
            new HashSet<char>
            {
          'H', 'h', 'f', 'A', 'B', 'C', 'D', 's', 'u', 'J', 'K', 'm', 'l', 'p',
            };

        private AnsiParseState _state = AnsiParseState.NORMAL;

        public ReadOnlySpan<byte> ParseAnsiString(ReadOnlySpan<byte> str)
        {
            var memoryStream = new MemoryStream(str.Length);
            foreach (var b in str)
            {
                var c = ParseAnsiCharacter((char)b);
                if (c != default)
                    memoryStream.WriteByte((byte)c);
            }
            return memoryStream.ToArray();
        }

        public char ParseAnsiCharacter(char c)
        {
            switch (_state)
            {
                case AnsiParseState.NORMAL when c == ASCII_ESCAPE:
                    _state = AnsiParseState.ESCAPE;
                    break;
                case AnsiParseState.NORMAL:
                    return c;
                case AnsiParseState.ESCAPE when c == '[':
                    _state = AnsiParseState.BRACKET;
                    break;
                case AnsiParseState.ESCAPE:
                    // just consume the prior escape
                    _state = AnsiParseState.NORMAL;
                    return c;
                case AnsiParseState.BRACKET when Char.IsDigit(c):
                    _state = AnsiParseState.VALUE_ACCUM;
                    break;
                case AnsiParseState.BRACKET:
                    // something else? how about waiting until an ending frame
                    _state = AnsiParseState.WAIT_FOR_ANSI_END;
                    break;
                case AnsiParseState.VALUE_ACCUM when IsAnsiSequenceFinished(c):
                    _state = AnsiParseState.NORMAL;
                    break;
                case AnsiParseState.WAIT_FOR_ANSI_END when c == ASCII_ESCAPE:
                    _state = AnsiParseState.ESCAPE;
                    break;
                case AnsiParseState.WAIT_FOR_ANSI_END when IsAnsiSequenceFinished(c):
                    _state = AnsiParseState.NORMAL;
                    break;
            }
            return default;
        }

        private static bool IsAnsiSequenceFinished(char c) => ANSI_ENDS.Contains(c);
    }
}
