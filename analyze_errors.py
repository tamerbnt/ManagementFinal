import re
import os

log_path = r"C:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\build_errors.log"

with open(log_path, 'r', encoding='utf-8') as f:
    text = f.read()

# Match standard msbuild error lines: File(Line,Col): error CSxxxx: Message
pattern = re.compile(r"^(.*?\.cs)\((\d+),\d+\):\s+error\s+CS(\d+):", re.MULTILINE)

errors = pattern.findall(text)
files_to_patch = set()

for file_path, line, code in errors:
    # We only care about name not found (CS0103) or missing arguments/invalid terms caused by regex botch
    if file_path.endswith(".cs"):
        files_to_patch.add(file_path)

print(f"Found {len(files_to_patch)} unique C# files with errors.")

for file_path in files_to_patch:
    print(file_path)

