using System;
using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    Console.Error.WriteLine($"[PROGRAM_STEP3] Creating Component with ReplacementTrigger='changed'");
    
    var options = new ComponentResourceOptions {
      ReplacementTrigger = "changed"
    };
    Console.Error.WriteLine($"[PROGRAM_STEP3] Options created, ReplacementTrigger={options.ReplacementTrigger}");
    
    var trigger = new Component("trigger", new ComponentArgs {
      Echo = 42
    }, options);
    
    Console.Error.WriteLine($"[PROGRAM_STEP3] Component created");
});

