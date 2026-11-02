import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Builds/AnimationReview/VisualRework/python'))
from PIL import Image
out=Path(__file__).resolve().parents[1]/'Builds/AnimationReview/AxeHeavy'
for prefix, filename in [('StrongAttack-', 'strong-attack.gif'), ('StrongAttack-side-', 'strong-attack-side.gif')]:
    frames=[]
    for i in range(37):
        with Image.open(out/'Unity'/f'{prefix}{i}.png') as im:
            frames.append(im.resize((480,480)).convert('RGB'))
    frames[0].save(out/filename,save_all=True,append_images=frames[1:],duration=[42]*36+[450],loop=0)
    print(out/filename)
