using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Pulumi;

	
string ComputeFileBase64Sha256(string path) 
{
    var fileData = Encoding.UTF8.GetBytes(File.ReadAllText(path));
    var hashData = SHA256.Create().ComputeHash(fileData);
    return Convert.ToBase64String(hashData);
}

	
string ReadFileBase64(string path) 
{
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
}

return await Deployment.RunAsync(() => 
{
    var fileContent = File.ReadAllText("testfile.txt");

    var fileB64 = ReadFileBase64("testfile.txt");

    var fileSha = ComputeFileBase64Sha256("testfile.txt");

    return new Dictionary<string, object?>
    {
        ["fileContent"] = fileContent,
        ["fileB64"] = fileB64,
        ["fileSha"] = fileSha,
    };
});

