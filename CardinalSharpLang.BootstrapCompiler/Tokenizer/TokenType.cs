using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharpLang.BootstrapCompiler.Tokenizer
{
    internal enum TokenType
    {
        Unknown = 0,
        VisibilityKeyword,
        TypeKeyword,
        OperatorKeyword,
        Keyword,

        ComparisonOperator,
        BitwiseOperator,
        UnaryOperator,
        Operator,

        AssignmentBitwiseOperator,
        AssignmentOperator,
        
        HexLiteral,
        BinaryLiteral,
        DoubleLiteral,
        FloatLiteral,
        IntegerLiteral,
        BooleanLiteral,
        CharLiteral,
        StringLiteral,

        Comment,
        Comma,
        Semicolon,
        OpenParen,
        CloseParen,
        OpenBrace,
        CloseBrace,
        OpenBracket,
        CloseBracket,

        Identifier,
    }
}
