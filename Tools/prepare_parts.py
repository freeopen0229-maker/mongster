"""Extract manually authored masks from the existing atlas. No image generation."""
import json,re,math,sys
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
def polygon(path):
 tokens=re.findall(r'[MLQZ]|-?\d+(?:\.\d+)?',path);i=0;pts=[];cur=(0,0)
 while i<len(tokens):
  if tokens[i] in ('M','L','Q','Z'):
   cmd=tokens[i];i+=1
  if cmd=='Z':break
  if cmd in ('M','L'):
   cur=(float(tokens[i]),float(tokens[i+1]));i+=2;pts.append(cur)
  elif cmd=='Q':
   q=(float(tokens[i]),float(tokens[i+1]));p=(float(tokens[i+2]),float(tokens[i+3]));i+=4
   for n in range(1,17):
    t=n/16;pts.append(((1-t)**2*cur[0]+2*(1-t)*t*q[0]+t*t*p[0],(1-t)**2*cur[1]+2*(1-t)*t*q[1]+t*t*p[1]))
   cur=p
  else:raise ValueError(cmd)
 return pts
if __name__=='__main__':
 atlas=Image.open(sys.argv[1]).convert('RGBA');src=Image.new('RGBA',(301,280));src.paste(atlas,(-35,-106));parts=json.loads((ROOT/'Tools/parts.json').read_text())
 for name,path in parts.items():
  m=Image.new('L',(1204,1120));ImageDraw.Draw(m).polygon([(x*4,y*4) for x,y in polygon(path)],fill=255);m=m.resize(src.size,Image.Resampling.LANCZOS)
  import numpy as np
  data=np.array(src);data[:,:,3]=(data[:,:,3].astype('uint16')*np.array(m).astype('uint16')//255).astype('uint8');Image.fromarray(data).save(ROOT/'Assets/Art/Neulgui'/f'{name}.png')
 # Neutral fills only for surfaces hidden behind original painted parts.
 for name,color in [('joint','#E8AC50'),('bodyPatch','#EDB75F')]:
  im=Image.new('RGBA',(100,100));d=ImageDraw.Draw(im);d.ellipse((2,2,98,98),fill=color,outline='#9A6837',width=3);im.save(ROOT/'Assets/Art/Neulgui'/f'{name}.png')
 for name,fill in [('shadow',(55,78,54,35)),('ground',(212,224,187,255))]:
  im=Image.new('RGBA',(256,64));ImageDraw.Draw(im).ellipse((0,0,255,63),fill=fill);im.save(ROOT/'Assets/Art/Neulgui'/f'{name}.png')
 # Authored closed-eye overlays in the exact face coordinate frame.
 im=Image.new('RGBA',(301,280));d=ImageDraw.Draw(im)
 for box in [(152,49,181,84),(205,49,222,83)]:
  d.ellipse(box,fill='#F4CA80');x0,y0,x1,y1=box;cy=(y0+y1)//2;d.arc((x0+3,cy-5,x1-3,cy+4),0,180,fill='#694222',width=2)
 im.save(ROOT/'Assets/Art/Neulgui/eyesClosed.png')
 print('Prepared',len(parts),'painted parts + joints, patch, eyes and shadow.')
