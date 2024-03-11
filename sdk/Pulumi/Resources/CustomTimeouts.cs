// Copyright 2016-2019, Pulumi Corporation

using System;

namespace Pulumi
{
    /// <summary>
    /// Optional timeouts to supply in <see cref="ResourceOptions.CustomTimeouts"/>.
    /// </summary>
    public sealed class CustomTimeouts
    {
        /// <summary>
        /// The optional create timeout.
        /// </summary>
        public TimeSpan? Create { get; set; }

        /// <summary>
        /// The optional update timeout.
        /// </summary>
        public TimeSpan? Update { get; set; }

        /// <summary>
        /// The optional delete timeout.
        /// </summary>
        public TimeSpan? Delete { get; set; }

        internal static CustomTimeouts? Clone(CustomTimeouts? timeouts)
            => timeouts == null ? null : new CustomTimeouts
            {
                Create = timeouts.Create,
                Delete = timeouts.Delete,
                Update = timeouts.Update,
            };


        private static string TimeoutString(TimeSpan? timeSpan)
        {
            if (timeSpan == null)
                return "";

            // This will eventually be parsed by go's ParseDuration function here:
            // https://github.com/pulumi/pulumi/blob/06d4dde8898b2a0de2c3c7ff8e45f97495b89d82/pkg/resource/deploy/source_eval.go#L967
            //
            // So we generate a legal duration as allowed by
            // https://golang.org/pkg/time/#ParseDuration.
            //
            // Simply put, we simply convert our ticks to the integral number of nanoseconds
            // corresponding to it.  Since each tick is 100ns, this can trivially be done just by
            // appending "00" to it.
            return timeSpan.Value.Ticks + "00ns";
        }

        internal Pulumirpc.RegisterResourceRequest.Types.CustomTimeouts Serialize()
        {
            return new Pulumirpc.RegisterResourceRequest.Types.CustomTimeouts
            {
                Create = TimeoutString(Create),
                Update = TimeoutString(Update),
                Delete = TimeoutString(Delete),
            };
        }

        internal static CustomTimeouts Deserialize(Pulumirpc.RegisterResourceRequest.Types.CustomTimeouts customTimeouts)
        {
            static TimeSpan? parse(string s)
            {
                if (s == null || s == "")
                {
                    return null;
                }

                // A duration string is a possibly signed sequence of decimal numbers, each with optional
                // fraction and a unit suffix, such as "300ms", "-1.5h" or "2h45m". Valid time units are "ns",
                // "us" (or "µs"), "ms", "s", "m", "h".

                var span = s.AsSpan();

                var neg = false;
                if (span[0] == '-' || span[0] == '+')
                {
                    neg = span[0] == '-';
                    span = span[1..];
                }
                if (span == "0")
                {
                    return TimeSpan.Zero;
                }
                if (span.IsEmpty)
                {
                    throw new ArgumentException("invalid duration " + s);
                }
                var duration = TimeSpan.Zero;
                while (!span.IsEmpty)
                {
                    // find the next timeunit
                    var i = 0;
                    while (i < span.Length && (('0' <= span[i] && span[i] <= '9') || span[i] == '.'))
                    {
                        i++;
                    }
                    // parse the number
                    var v = double.Parse(span[0..i]);
                    // parse the unit
                    span = span[i..];
                    if (span.IsEmpty)
                    {
                        throw new ArgumentException("missing unit in duration " + s);
                    }
                    if (span.StartsWith("µs") || span.StartsWith("us"))
                    {
                        duration += TimeSpan.FromTicks((long)(v / 100));
                        span = span[2..];
                    }
                    else if (span.StartsWith("ms"))
                    {
                        duration += TimeSpan.FromMilliseconds(v);
                        span = span[2..];
                    }
                    else if (span.StartsWith("s"))
                    {
                        duration += TimeSpan.FromSeconds(v);
                        span = span[1..];
                    }
                    else if (span.StartsWith("m"))
                    {
                        duration += TimeSpan.FromMinutes(v);
                        span = span[1..];
                    }
                    else if (span.StartsWith("h"))
                    {
                        duration += TimeSpan.FromHours(v);
                        span = span[1..];
                    }
                    else if (span.StartsWith("d"))
                    {
                        duration += TimeSpan.FromDays(v);
                        span = span[1..];
                    }
                    else
                    {
                        throw new ArgumentException("invalid unit in duration " + s);
                    }
                }
                if (neg)
                {
                    duration = -duration;
                }
                return duration;
            };

            return new CustomTimeouts
            {
                Create = parse(customTimeouts.Create),
                Update = parse(customTimeouts.Update),
                Delete = parse(customTimeouts.Delete),
            };
        }
    }
}
