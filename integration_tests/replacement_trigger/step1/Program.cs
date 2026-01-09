using System;
using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    Console.Error.WriteLine($"[PROGRAM_STEP1] Creating Component with ReplacementTrigger='test'");
    
    var options = new ComponentResourceOptions {
      ReplacementTrigger = "test"
    };
    Console.Error.WriteLine($"[PROGRAM_STEP1] Options created, ReplacementTrigger={options.ReplacementTrigger}");
    
    var trigger = new Component("trigger", new ComponentArgs {
      Echo = 42
    }, options);
    
    Console.Error.WriteLine($"[PROGRAM_STEP1] Component created");
});
