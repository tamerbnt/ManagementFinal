using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        var asm = Assembly.LoadFrom(@"C:\Users\techbox\.nuget\packages\velopack\3.0.170\lib\net8.0-windows7.0\Velopack.dll");
        var appType = asm.GetTypes().FirstOrDefault(t => t.Name == "VelopackApp");
        var meths = appType.GetMethods().Select(m => m.Name).Distinct();
        foreach (var m in meths) Console.WriteLine(m);
    }
}
