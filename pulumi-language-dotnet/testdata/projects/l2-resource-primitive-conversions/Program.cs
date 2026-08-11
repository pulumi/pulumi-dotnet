using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var plainBool = config.RequireBoolean("plainBool");
    var plainNumber = config.RequireDouble("plainNumber");
    var plainInteger = config.RequireInt32("plainInteger");
    var plainString = config.Require("plainString");
    var plainNumericString = config.Require("plainNumericString");
    var secretNumber = config.RequireSecretDouble("secretNumber");
    var secretInteger = config.RequireSecretInt32("secretInteger");
    var secretString = config.RequireSecret("secretString");
    var secretNumericString = config.RequireSecret("secretNumericString");
    var plainValues = new Primitive.Resource("plainValues", new()
    {
        Boolean = plainString == "true",
        Float = (double)plainInteger,
        Integer = int.Parse(plainNumericString, System.Globalization.CultureInfo.InvariantCulture),
        String = plainNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NumberArray = new[]
        {
            (double)plainInteger,
            double.Parse(plainNumericString, System.Globalization.CultureInfo.InvariantCulture),
            plainNumber,
        },
        BooleanMap = 
        {
            { "fromBool", plainBool },
            { "fromString", plainString == "true" },
        },
    });

    var secretValues = new Primitive.Resource("secretValues", new()
    {
        Boolean = secretString.Apply(x => x == "true"),
        Float = secretInteger.Apply(x => (double)x),
        Integer = secretNumericString.Apply(x => int.Parse(x, System.Globalization.CultureInfo.InvariantCulture)),
        String = secretNumber.Apply(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        NumberArray = new[]
        {
            (double)plainInteger,
            double.Parse(plainNumericString, System.Globalization.CultureInfo.InvariantCulture),
            plainNumber,
        },
        BooleanMap = 
        {
            { "fromBool", plainBool },
            { "fromString", plainString == "true" },
        },
    });

    var invokeResult = Primitive.InvokeFunction.Invoke(new()
    {
        Boolean = plainString == "true",
        Float = (double)plainInteger,
        Integer = int.Parse(plainNumericString, System.Globalization.CultureInfo.InvariantCulture),
        String = plainBool ? "true" : "false",
        NumberArray = new[]
        {
            (double)plainInteger,
            double.Parse(plainNumericString, System.Globalization.CultureInfo.InvariantCulture),
            plainNumber,
        },
        BooleanMap = 
        {
            { "fromBool", plainBool },
            { "fromString", plainString == "true" },
        },
    });

    var invokeValues = new Primitive.Resource("invokeValues", new()
    {
        Boolean = invokeResult.Apply(invokeResult => invokeResult.Boolean),
        Float = invokeResult.Apply(invokeResult => invokeResult.Float),
        Integer = invokeResult.Apply(invokeResult => invokeResult.Integer),
        String = invokeResult.Apply(invokeResult => invokeResult.String),
        NumberArray = invokeResult.Apply(invokeResult => invokeResult.NumberArray),
        BooleanMap = invokeResult.Apply(invokeResult => invokeResult.BooleanMap),
    });

});

