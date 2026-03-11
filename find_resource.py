
import os

def find_static_resource_on_line_24(directory):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".xaml"):
                filepath = os.path.join(root, file)
                try:
                    with open(filepath, "r", encoding="utf-8") as f:
                        lines = f.readlines()
                        if len(lines) >= 24:
                            line_24 = lines[23]
                            if "StaticResource" in line_24:
                                print(f"Found in {filepath}:")
                                print(f"Line 24: {line_24.strip()}")
                                print("-" * 40)
                except Exception as e:
                    pass

if __name__ == "__main__":
    find_static_resource_on_line_24(r"c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation")
