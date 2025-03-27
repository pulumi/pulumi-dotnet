namespace Pulumi.Analyzer
{
    public record struct Urn(string Value)
    {
        public static implicit operator string(Urn value)
        {
            return value.Value;
        }
    }
}
