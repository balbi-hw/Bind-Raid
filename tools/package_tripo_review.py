from pathlib import Path
import sys
ROOT=Path('C:/Users/SSAFY/pjt/bindraid')
sys.path.insert(0,str(ROOT/'Builds/AnimationReview/VisualRework/python'))
from PIL import Image, ImageDraw
OUT=ROOT/'Builds/AnimationReview/TripoReplacement'
for name in ['Walk','Sprint','Dodge']:
    frames=[]
    for file in sorted((OUT/name).glob('*.png')):
        im=Image.open(file).convert('RGB');ImageDraw.Draw(im).text((16,16),f'{name} | frame {int(file.stem)} | Tripo skeleton',fill='white')
        frames.append(im)
    delay=42 if name=='Dodge' else 83
    frames[0].save(OUT/(name+'.gif'),save_all=True,append_images=frames[1:],duration=delay,loop=0)
sheet=Image.new('RGB',(1280,640),(24,26,30))
for i,f in enumerate([0,3,6,8,10,12,15,18]):
    im=Image.open(OUT/'Dodge'/f'{f:03d}.png').resize((320,320));ImageDraw.Draw(im).text((10,10),f'{f/24:.2f} s',fill='white');sheet.paste(im,((i%4)*320,(i//4)*320))
sheet.save(OUT/'roll-contact-sheet.jpg')
print('Packaged Walk.gif, Sprint.gif, Dodge.gif, roll-contact-sheet.jpg')
