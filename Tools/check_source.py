"""Asset/source checks only. This does not compile C# or run Unity."""
from pathlib import Path
from PIL import Image
import json,re
root=Path(__file__).resolve().parents[1]
parts=json.loads((root/'Tools/parts.json').read_text())
for name in parts:
 im=Image.open(root/f'Assets/Art/Neulgui/{name}.png')
 assert im.size==(301,280) and im.mode=='RGBA'
 assert im.getchannel('A').getbbox(),name
for p in root.glob('Assets/**/*.cs'):
 s=p.read_text();s=re.sub(r'//[^\n]*|"(?:[^"\\]|\\.)*"','',s)
 stack=[]
 for c in s:
  if c in '{([':stack.append(c)
  elif c in '})]':assert stack and stack.pop()=={'}':'{',')':'(',']':'['}[c],p
 assert not stack,p
assert (root/'ProjectSettings/ProjectVersion.txt').read_text().strip()=='m_EditorVersion: 6000.3.26f1'
json.loads((root/'Packages/manifest.json').read_text())
print('PASS: 9 non-empty RGBA parts, project metadata, C# delimiter checks. NOT a C# compilation or Unity execution test.')
