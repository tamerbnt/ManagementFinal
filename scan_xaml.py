import os
import re

patterns = {
    'Content': re.compile(r'Content="([^\{][^"]*)"'),
    'Header': re.compile(r'Header="([^\{][^"]*)"'),
    'Text': re.compile(r'Text="([^\{][^"]*)"'),
    'ToolTip': re.compile(r'ToolTip="([^\{][^"]*)"'),
    'Placeholder': re.compile(r'Placeholder="([^\{][^"]*)"')
}

root_dir = r'c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\Views'
output_file = r'c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\xaml_hardcoded_report.txt'

with open(output_file, 'w', encoding='utf-8') as f:
    for root, dirs, files in os.walk(root_dir):
        for file in files:
            if file.endswith('.xaml'):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8') as xaml:
                        for i, line in enumerate(xaml, 1):
                            for attr, pattern in patterns.items():
                                for match in pattern.finditer(line):
                                    val = match.group(1)
                                    if val.strip():
                                        f.write(f'{file}, {i}, {val}, Hardcoded XAML\n')
                except Exception as e:
                    f.write(f'Error reading {file}: {e}\n')

print(f"Report generated at {output_file}")
