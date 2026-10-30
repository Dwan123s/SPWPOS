import zipfile
import xml.etree.ElementTree as ET

with zipfile.ZipFile('SPWPOS_RDS.docx', 'r') as z:
    with z.open('word/document.xml') as f:
        tree = ET.parse(f)
        root = tree.getroot()

ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

paragraphs = []
for p in root.iter(f"{{{ns['w']}}}p"):
    texts = [t.text for t in p.iter(f"{{{ns['w']}}}t") if t.text]
    if texts:
        paragraphs.append("".join(texts).strip())

non_empty = [p for p in paragraphs if p]

for i in range(5316, min(5380, len(non_empty))):
    print(f"[{i}] {non_empty[i]}")
