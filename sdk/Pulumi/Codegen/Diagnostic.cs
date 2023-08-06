namespace Pulumi.Codegen
{
    public enum DiagnosticSeverity
    {
        Invalid,
        Warning,
        Error
    }

    public sealed class Position
    {
        public long Line { get; set; }
        public long Column { get; set; }
        public long Byte { get; set; }
    }

    public sealed class Range
    {
        public string? Filename { get; set; }
        public Position? Start { get; set; } = null;
        public Position? End { get; set; } = null;
    }

    public sealed class Diagnostic
    {
        public string? Summary { get; set; }
        public string? Detail { get; set; }
        public Range? Subject { get; set; }
        public Range? Context { get; set; }
        public DiagnosticSeverity Severity { get; set; }
    }
}
