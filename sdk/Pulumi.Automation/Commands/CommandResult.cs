// Copyright 2016-2021, Pulumi Corporation

using System.Globalization;
using System.Text;

namespace Pulumi.Automation.Commands
{
    public sealed class CommandResult
    {
        public int Code { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public CommandResult(
            int code,
            string standardOutput,
            string standardError)
        {
            this.Code = code;
            this.StandardOutput = standardOutput;
            this.StandardError = standardError;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"code: {Code}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"stdout: {StandardOutput}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"stderr: {StandardError}");

            return sb.ToString();
        }
    }
}
