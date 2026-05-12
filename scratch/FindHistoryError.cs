using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

var logPath = @"c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\bin\Debug\net8.0-windows\logs\app-20260508.log";
if (!File.Exists(logPath)) {
    Console.WriteLine("Log file not found.");
    return;
}

var lines = File.ReadLines(logPath).Reverse().Take(5000).ToList();
var errorRegex = new Regex(@"\{""code"":""[^""]+"",""details"":.*\}", RegexOptions.Compiled);

foreach (var line in lines) {
    if (line.Contains("daily_history_summaries") || line.Contains("[SnapshotSync]")) {
        var match = errorRegex.Match(line);
        if (match.Success) {
            Console.WriteLine($"Line: {line}");
            Console.WriteLine($"Error JSON: {match.Value}");
            Console.WriteLine("-----------------------------------");
        }
    }
}
