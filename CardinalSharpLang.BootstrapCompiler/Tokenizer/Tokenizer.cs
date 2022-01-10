using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharpLang.BootstrapCompiler.Tokenizer
{
    internal class Token
    {
        public TokenType TokenType { get; set; }
        public string Value { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
    }

    class TokenSourceStream
    {
        private readonly string _source;
        private int _offset;

        public bool EndOfStream { get { return _offset >= _source.Length; } }
        public char CurrentChar { get { return _source[_offset]; } }
        public int LineNumber { get; private set; }
        public int ColumnNumber { get; private set; }

        public TokenSourceStream(string s)
        {
            _source = s;
            _offset = 0;
            LineNumber = 1;
            ColumnNumber = 0;
        }

        public string ConsumeLine()
        {
            var eol = _source.IndexOf('\n', _offset);
            if (eol == -1) eol = _source.Length;
            string ln = _source.Substring(_offset, eol - _offset);
            _offset = eol;
            NextChar();
            return ln;
        }

        public void NextChar()
        {
            _offset++;
            ColumnNumber++;
            while (Match("\n"))
            {
                _offset++;
                LineNumber++;
                ColumnNumber = 0;
            }
        }

        public char PeekNextChar()
        {
            return _source[_offset + 1];
        }

        public bool Match(string s)
        {
            return _source.Substring(_offset, s.Length) == s;
        }

        public bool Match(string[] s, out string match)
        {
            foreach (string s2 in s)
                if (Match(s2))
                {
                    match = s2;
                    return true;
                }
            match = null;
            return false;
        }

        public bool ConsumeMatch(string s)
        {
            if (_source.Substring(_offset, s.Length) == s)
            {
                _offset += s.Length - 1;
                NextChar();
                return true;
            }
            return false;
        }

        public bool ConsumeMatch(string[] s, out string match)
        {
            foreach (string s2 in s)
                if (ConsumeMatch(s2))
                {
                    match = s2;
                    return true;
                }
            match = null;
            return false;
        }
    }

    internal class Tokenizer
    {
        public List<Token> Tokens = new();

        private void ConsumeWhitespace(TokenSourceStream ts)
        {
            while (char.IsWhiteSpace(ts.CurrentChar) && !ts.EndOfStream)
            {
                ts.NextChar();
            }
        }

        private bool ConsumeComment(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch("//"))
            {
                var cmt = ts.ConsumeLine();

                Tokens.Add(new Token()
                {
                    TokenType = TokenType.Comment,
                    Value = cmt,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeVisibilityKeyword(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "public", "private", "internal" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.VisibilityKeyword,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeTypeKeyword(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "void", "bool", "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "char", "string" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.TypeKeyword,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeOperatorKeyword(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "new", "sizeof", "loc_of", "val_at", "typeof", "as", "is" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.OperatorKeyword,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeKeyword(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "namespace", "struct", "class", "enum", "static", "operator", "this", "continue", "break", "null", "if", "else", "while", "do", "for", "foreach" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.Keyword,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeComparisonOperator(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { ">", "<", ">=", "<=", "!=", "==", }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.ComparisonOperator,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeBitwiseOperator(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "&", "^", "|" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.BitwiseOperator,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeUnaryOperator(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "!", "~", "++", "--", "+", "-" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.UnaryOperator,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeAssignmentBitwiseOperator(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "&=", "^=", "|=" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.AssignmentBitwiseOperator,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeAssignmentOperator(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "=", "+=", "-=", "*=", "/=", "%=" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.AssignmentOperator,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeOperator(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "+", "-", "*", "/", "%", "." }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.Operator,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeOtherSymbols(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            var symbols = new string[] { ",", ";", "(", ")", "{", "}", "[", "]" };
            if (ts.ConsumeMatch(symbols, out var matched))
            {
                var tkn_type = (int)TokenType.Comma;
                foreach (var m in symbols)
                {
                    if (m == matched)
                        break;
                    tkn_type++;
                }

                Tokens.Add(new Token()
                {
                    TokenType = (TokenType)tkn_type,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });

                return true;
            }
            return false;
        }

        private bool ConsumeDoubleOrFloatLiteral(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (char.IsDigit(ts.CurrentChar))
            {
                string intChars = "0123456789_.";
                string val = "";
                while (!ts.EndOfStream && intChars.Contains(ts.CurrentChar))
                {
                    if (ts.CurrentChar != '_')
                        val += ts.CurrentChar;
                    ts.NextChar();
                }
                if (val.Count(a => a == '.') > 1)
                    throw new InvalidOperationException("Unrecognized number format!");
                var tkn = new Token()
                {
                    TokenType = val.Contains('.') ? TokenType.DoubleLiteral : TokenType.IntegerLiteral,
                    Value = val,
                    LineNumber = line,
                    ColumnNumber = col,
                };
                if (ts.CurrentChar == 'f')
                {
                    tkn.TokenType = TokenType.FloatLiteral;
                    ts.NextChar();
                }
                else if (ts.CurrentChar == 'd')
                {
                    tkn.TokenType = TokenType.DoubleLiteral;
                    ts.NextChar();
                }

                Tokens.Add(tkn);
                return true;
            }
            return false;
        }

        private bool ConsumeHexLiteral(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch("0x"))
            {
                string hexChars = "0123456789ABCDEFabcdef_";
                string val = "0x";
                while (!ts.EndOfStream && hexChars.Contains(ts.CurrentChar))
                {
                    if (ts.CurrentChar != '_')
                        val += ts.CurrentChar;
                    ts.NextChar();
                }

                Tokens.Add(new Token()
                {
                    TokenType = TokenType.HexLiteral,
                    Value = val,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeBinaryLiteral(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch("0b"))
            {
                string binChars = "01_";
                string val = "0b";
                while (!ts.EndOfStream && binChars.Contains(ts.CurrentChar))
                {
                    if (ts.CurrentChar != '_')
                        val += ts.CurrentChar;
                    ts.NextChar();
                }

                Tokens.Add(new Token()
                {
                    TokenType = TokenType.BinaryLiteral,
                    Value = val,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeBooleanLiteral(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch(new string[] { "true", "false" }, out var matched))
            {
                Tokens.Add(new Token()
                {
                    TokenType = TokenType.BooleanLiteral,
                    Value = matched,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeCharLiteral(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch("'"))
            {
                string val = "'";
                bool escaped = false;
                while (!ts.EndOfStream)
                {
                    if (!escaped && ts.CurrentChar == '\'')
                        break;
                    escaped = false;
                    if (ts.CurrentChar == '\\')
                        escaped = true;
                    val += ts.CurrentChar;
                    ts.NextChar();
                }
                if (!ts.EndOfStream)
                {
                    val += ts.CurrentChar;
                    ts.NextChar();
                }

                Tokens.Add(new Token()
                {
                    TokenType = TokenType.CharLiteral,
                    Value = val,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        private bool ConsumeStringLiteral(TokenSourceStream ts)
        {
            var line = ts.LineNumber;
            var col = ts.ColumnNumber;
            if (ts.ConsumeMatch("\""))
            {
                string val = "\"";
                bool escaped = false;
                while (!ts.EndOfStream)
                {
                    if (!escaped && ts.CurrentChar == '"')
                        break;
                    escaped = false;
                    if (ts.CurrentChar == '\\')
                        escaped = true;
                    val += ts.CurrentChar;
                    ts.NextChar();
                }
                if (!ts.EndOfStream)
                {
                    val += ts.CurrentChar;
                    ts.NextChar();
                }

                Tokens.Add(new Token()
                {
                    TokenType = TokenType.StringLiteral,
                    Value = val,
                    LineNumber = line,
                    ColumnNumber = col,
                });
                return true;
            }
            return false;
        }

        public void Tokenize(string s)
        {
            var ts = new TokenSourceStream(s);
            while (!ts.EndOfStream)
            {
                ConsumeWhitespace(ts);

                if (ConsumeComment(ts))
                    continue;

                //Keywords
                if (ConsumeVisibilityKeyword(ts))
                    continue;
                if (ConsumeTypeKeyword(ts))
                    continue;
                if (ConsumeOperatorKeyword(ts))
                    continue;
                if (ConsumeKeyword(ts))
                    continue;

                //Operators
                if (ConsumeComparisonOperator(ts))
                    continue;
                if (ConsumeBitwiseOperator(ts))
                    continue;
                if (ConsumeUnaryOperator(ts))
                    continue;
                if (ConsumeAssignmentBitwiseOperator(ts))
                    continue;
                if (ConsumeAssignmentOperator(ts))
                    continue;
                if (ConsumeOperator(ts))
                    continue;

                //Literals
                if (ConsumeHexLiteral(ts))
                    continue;
                if (ConsumeBinaryLiteral(ts))
                    continue;
                if (ConsumeDoubleOrFloatLiteral(ts))
                    continue;
                if (ConsumeBooleanLiteral(ts))
                    continue;
                if (ConsumeCharLiteral(ts))
                    continue;
                if (ConsumeStringLiteral(ts))
                    continue;

                //Other symbols
                if (ConsumeOtherSymbols(ts))
                    continue;

                //Identifiers
                //At this point it could only be an identifier, barring errors
            }
        }
    }
}
