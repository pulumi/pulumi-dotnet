// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Text;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// A minimal indentation-aware writer for emitting C# source text. Output
    /// always uses LF line endings, matching the repository's convention for
    /// .cs files.
    /// </summary>
    public sealed class CodeWriter
    {
        private readonly StringBuilder _builder = new();
        private int _indent;

        /// <summary>
        /// Writes a line at the current indentation. An empty call writes a
        /// blank line with no trailing whitespace.
        /// </summary>
        public void Line(string text = "")
        {
            if (text.Length > 0)
            {
                _builder.Append(' ', _indent * 4);
                _builder.Append(text);
            }

            _builder.Append('\n');
        }

        /// <summary>
        /// Writes an opening brace and increases the indentation.
        /// </summary>
        public void OpenBlock()
        {
            Line("{");
            _indent++;
        }

        /// <summary>
        /// Decreases the indentation and writes a closing brace.
        /// </summary>
        public void CloseBlock()
        {
            if (_indent == 0)
            {
                throw new InvalidOperationException("Cannot close a block at zero indentation.");
            }

            _indent--;
            Line("}");
        }

        public override string ToString()
            => _builder.ToString();
    }
}
