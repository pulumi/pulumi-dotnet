// Copyright 2016-2024, Pulumi Corporation

using System;
using System.Reflection;
using Google.Protobuf;

namespace Pulumirpc
{
    internal static class ParseContextHelper
    {
        private delegate void ParseContextAction(ref ParseContext parseContext, CodedInputStream input);

        private static readonly ParseContextAction _copyStateTo = CreateParseContextDelegate("CopyStateTo");
        private static readonly ParseContextAction _loadStateFrom = CreateParseContextDelegate("LoadStateFrom");
        private static readonly FieldInfo _stateField = GetCodedInputStreamStateField();
        private static readonly FieldInfo _stateRecursionLimitField = GetParserInternalStateRecursionLimitField();

        private static ParseContextAction CreateParseContextDelegate(string name)
        {
            var method = typeof(ParseContext).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method is null)
            {
                throw new InvalidOperationException(
                    $"Unable to find the internal method '{name}' on 'Google.Protobuf.ParseContext'.");
            }
            return method.CreateDelegate<ParseContextAction>();
        }

        private static FieldInfo GetCodedInputStreamStateField()
        {
            var codedInputStreamType = typeof(CodedInputStream);
            var stateField = codedInputStreamType.GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField is null)
            {
                throw new InvalidOperationException(
                    "Unable to find the internal field 'state' on 'Google.Protobuf.CodedInputStream'.");
            }
            return stateField;
        }

        private static FieldInfo GetParserInternalStateRecursionLimitField()
        {
            var parseContextType = typeof(ParseContext);
            // Retrieve the internal type 'ParserInternalState' via reflection
            var parserInternalStateType = parseContextType.Assembly.GetType("Google.Protobuf.ParserInternalState");
            if (parserInternalStateType is null)
            {
                throw new InvalidOperationException(
                    "Unable to find the internal type 'Google.Protobuf.ParserInternalState'.");
            }

            var recursionLimitField = parserInternalStateType.GetField("recursionLimit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (recursionLimitField is null)
            {
                throw new InvalidOperationException(
                    "Unable to find the internal field 'recursionLimit' on 'Google.Protobuf.ParserInternalState'.");
            }
            return recursionLimitField;
        }

        /// <summary>
        /// Sets the recursion limit on the given <see cref="ParseContext"/>.
        /// </summary>
        /// <param name="parseContext">The <see cref="ParseContext"/> to modify.</param>
        /// <param name="value">The new recursion limit</param>
        internal static void SetRecursionLimit(this ref ParseContext parseContext, int value)
        {
            // There doesn't appear to be a way to get the value of `ParseContext`'s internal
            // `state` field via reflection because it is a ref struct, which doesn't appear
            // to work with `FieldInfo.GetValue`.
            //
            // But, `ParseContext` does provide internal `CopyStateTo` and `LoadStateFrom`
            // methods to copy/load the state to/from a `CodedInputStream`.
            //
            // We can use those methods to copy the state, modify it, and load it back in
            // to the `ParseContext`.

            // Copy the state from the `ParseContext` to a temp `CodedInputStream` instance.
            var stream = new CodedInputStream(Array.Empty<byte>());
            _copyStateTo(ref parseContext, stream);

            // Modify the recursion limit on the state.
            var stateValue = _stateField.GetValue(stream);
            _stateRecursionLimitField.SetValue(stateValue, value);
            _stateField.SetValue(stream, stateValue);

            // Copy the state back to the `ParseContext`.
            _loadStateFrom(ref parseContext, stream);
        }
    }
}
