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

# Look for mentions of "Gaps", "Future", "Limitation", "Phase"
for i, p in enumerate(non_empty):
    p_lower = p.lower()
    if any(k in p_lower for k in ["explicit gaps", "gap", "future", "limitation", "provisional", "giai đoạn", "roadmap"]):
        if len(p) < 150:
            print(f"[{i}] {p}")
