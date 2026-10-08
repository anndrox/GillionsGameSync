"""Rasterize actual candidate ImGui triangles; no UI recreation or OS input."""
import argparse,json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
parser=argparse.ArgumentParser();parser.add_argument('directory',type=Path)
parser.add_argument('--background',choices=['dark','bright'],default='dark');args=parser.parse_args()
for path in sorted(args.directory.glob('*.draw.json')):
    data=json.loads(path.read_text())
    atlas=np.frombuffer((args.directory/'font.rgba').read_bytes(),dtype=np.uint8).reshape(data['atlasHeight'],data['atlasWidth'],4)/255
    branding=np.array(Image.open(args.directory/'branding.png').convert('RGBA'),dtype=float)/255
    canvas=np.empty((data['height'],data['width'],3),dtype=np.float64)
    canvas[:]=[.86,.83,.72] if args.background=='bright' else [.055,.065,.085]
    xmax=ymax=0
    for tri in data['triangles']:
        tex=branding if tri['texture']==2 else atlas
        tex_height,tex_width=tex.shape[:2]
        verts=np.array(tri['v'],dtype=float);a,b,c=verts[:,:2]
        low=np.maximum(np.floor(verts[:,:2].min(axis=0)),tri['clip'][:2]).astype(int)
        high=np.minimum(np.ceil(verts[:,:2].max(axis=0)),tri['clip'][2:]).astype(int)
        x0,y0=np.maximum(low,0);x1,y1=np.minimum(high,[data['width'],data['height']])
        if x1<=x0 or y1<=y0:continue
        denom=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(denom)<1e-9:continue
        yy,xx=np.mgrid[y0:y1,x0:x1];xx=xx+.5;yy=yy+.5
        u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/denom
        v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/denom
        weights=np.stack([u,v,1-u-v],axis=-1);inside=np.ones(xx.shape,dtype=bool)
        orientation=1 if denom>0 else -1
        for p,q in [(a,b),(b,c),(c,a)]:
            edge=((q[0]-p[0])*(yy-p[1])-(q[1]-p[1])*(xx-p[0]))*orientation
            dx=(q[0]-p[0])*orientation;dy=(q[1]-p[1])*orientation
            inside&=(edge>1e-8)|((np.abs(edge)<=1e-8)&((dy<0)|((dy==0)&(dx>0))))
        uv=weights@verts[:,2:4];tx=uv[:,:,0]*tex_width-.5;ty=uv[:,:,1]*tex_height-.5
        ix=np.floor(tx).astype(int);iy=np.floor(ty).astype(int);fx=tx-ix;fy=ty-iy
        def sample(dx,dy):return tex[np.clip(iy+dy,0,tex_height-1),np.clip(ix+dx,0,tex_width-1)]
        texture=sample(0,0)*(1-fx[:,:,None])*(1-fy[:,:,None])+sample(1,0)*fx[:,:,None]*(1-fy[:,:,None])+sample(0,1)*(1-fx[:,:,None])*fy[:,:,None]+sample(1,1)*fx[:,:,None]*fy[:,:,None]
        rgba=texture*(weights@verts[:,4:8]/255);alpha=rgba[:,:,3:4]*inside[:,:,None]
        canvas[y0:y1,x0:x1]=rgba[:,:,:3]*alpha+canvas[y0:y1,x0:x1]*(1-alpha)
        xmax=max(xmax,x1);ymax=max(ymax,y1)
    image=Image.fromarray(np.round(np.clip(canvas,0,1)*255).astype('uint8')).crop((0,0,min(data['width'],xmax+24),min(data['height'],ymax+50)))
    annotation=ImageDraw.Draw(image)
    annotation.rectangle((0,image.height-32,image.width,image.height),fill=(14,17,22))
    annotation.text((10,image.height-25),'CONTROLLED COMPILED RENDER / synthetic / not FFXIV',fill=(230,230,230))
    suffix='-bright.png' if args.background=='bright' else '.png'
    image.save(path.with_name(path.name.replace('.draw.json',suffix)))
print('Controlled PNGs rendered from actual ImGui triangles; not live FFXIV evidence.')
