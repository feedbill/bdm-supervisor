import struct, re, sys
sys.path.insert(0,'/tmp/vdmtr')
from peres import *
from dict import D
cjk=re.compile(r'[一-鿿]')
def tr(t):
    if not cjk.search(t): return t
    if t in D: return D[t]
    print("SIN TRADUCCION:",repr(t)); return t

src='/tmp/DeviceTestorBySP.exe'; dst='/tmp/DeviceTestorBySP_EN.exe'
pe=PE(src); d=pe.d; res=pe.tree()
# ---- translate dialog + string resources
for r in res:
    if r['type']==('id',5):
        dlg=parse_dialog(r['data']); dlg['title']=tr(dlg['title'])
        if 'charset' in dlg and dlg['charset']==134: dlg['charset']=0
        for it in dlg['items']:
            if it['title'][0]=='sz': it['title']=('sz',tr(it['title'][1]))
        if r['name']==('id',102):
            labels=[it for it in dlg['items'] if it['title']==('sz','Status')]
            for lb in labels:
                x,y,cx,cy=lb['rect']; delta=16; lb['rect']=(x,y,cx+delta,cy)
                for it in dlg['items']:
                    ix,iy,icx,icy=it['rect']
                    if it is not lb and abs(iy-y)<=6 and ix>x and ix<x+cx+delta+4: it['rect']=(ix+delta,iy,icx-delta,icy)
        r['data']=build_dialog(dlg)
    elif r['type']==('id',6):
        r['data']=build_strings([tr(t) for t in parse_strings(r['data'])])
    elif r['type']==('id',4):  # MENU: translate inline UTF-16 strings
        b=bytearray(r['data']); # rebuild via regex on UTF-16 strings terminated by \0\0 on even boundary
        out=bytearray(); p=0
        # menu template: header(4) then items: flags(2) [id(2)] string\0; menuex is different; detect
        ver=struct.unpack_from('<H',b,0)[0]
        if ver==1:  # MENUEX: not handled, keep
            pass
        else:
            out+=b[:4]; p=4
            while p<len(b):
                fl=struct.unpack_from('<H',b,p)[0]; out+=b[p:p+2]; p+=2
                if not fl&0x10: out+=b[p:p+2]; p+=2
                s,p2=rd_sz(b,p); out+=wr_sz(tr(s)); p=p2
            r['data']=bytes(out)
# ---- serialize resource tree
def enc_name(s): e=s.encode('utf-16le'); return struct.pack('<H',len(e)//2)+e
def key(k): return (0 if k[0]=='name' else 1, k[1] if k[0]=='id' else 0, k[1] if k[0]=='name' else '')
tree={}
for r in res: tree.setdefault(r['type'],{}).setdefault(r['name'],{})[r['lang']]=r
# layout: level1 dir, level2 dirs, level3 dirs, data entries, name strings, data
def dir_size(n): return 16+8*n
L1=sorted(tree.keys(),key=key)
off=dir_size(len(L1)); l2off={}; l3off={}
for t in L1:
    l2off[t]=off; off+=dir_size(len(tree[t]))
for t in L1:
    for n in sorted(tree[t].keys(),key=key):
        l3off[(t,n)]=off; off+=dir_size(len(tree[t][n]))
dataent={}
for t in L1:
    for n in sorted(tree[t].keys(),key=key):
        for lg in sorted(tree[t][n]): dataent[(t,n,lg)]=off; off+=16
names={}
for t in L1:
    if t[0]=='name' and t[1] not in names: names[t[1]]=off; off+=2+2*len(t[1])
    for n in tree[t]:
        if n[0]=='name' and n[1] not in names: names[n[1]]=off; off+=2+2*len(n[1])
off+=(-off)%8; datastart=off
datapos={}
for t in L1:
    for n in sorted(tree[t].keys(),key=key):
        for lg in sorted(tree[t][n]):
            datapos[(t,n,lg)]=off; off+=len(tree[t][n][lg]['data']); off+=(-off)%8
total=off
# choose new section
sec_align=struct.unpack_from('<I',d,pe.pe+24+32)[0]; file_align=struct.unpack_from('<I',d,pe.pe+24+36)[0]
last=max(pe.secs,key=lambda s:s[1]); newrva=(last[1]+max(last[2],last[4])+sec_align-1)//sec_align*sec_align
newraw=(len(d)+file_align-1)//file_align*file_align
blob=bytearray(total)
def ent_id(k,o): 
    if k[0]=='name': return struct.pack('<II',0x80000000|names[k[1]],o)
    return struct.pack('<II',k[1],o)
def wdir(o,keys,sub,isdir):
    nn=sum(1 for k in keys if k[0]=='name'); ni=len(keys)-nn
    struct.pack_into('<IIHHHH',blob,o,0,0,0,0,nn,ni)
    for i,k in enumerate(keys):
        so=sub(k); blob[o+16+i*8:o+24+i*8]=ent_id(k,(0x80000000|so) if isdir else so)
wdir(0,L1,lambda t:l2off[t],True)
for t in L1:
    ns=sorted(tree[t].keys(),key=key); wdir(l2off[t],ns,lambda n:l3off[(t,n)],True)
    for n in ns:
        lgs=sorted(tree[t][n]); 
        struct.pack_into('<IIHHHH',blob,l3off[(t,n)],0,0,0,0,0,len(lgs))
        for i,lg in enumerate(lgs): struct.pack_into('<II',blob,l3off[(t,n)]+16+i*8,lg,dataent[(t,n,lg)])
        for lg in lgs:
            r=tree[t][n][lg]; struct.pack_into('<IIII',blob,dataent[(t,n,lg)],newrva+datapos[(t,n,lg)],len(r['data']),r['cp'],0)
            blob[datapos[(t,n,lg)]:datapos[(t,n,lg)]+len(r['data'])]=r['data']
for s,o in names.items(): e=enc_name(s); blob[o:o+len(e)]=e
# append section
out=bytearray(d); out+=b'\0'*(newraw-len(out)); out+=blob; rawsize=(total+file_align-1)//file_align*file_align; out+=b'\0'*(rawsize-total)
hdr_off=pe.pe+24+pe.opt+pe.nsec*40
assert hdr_off+40<=min(s[3] for s in pe.secs), "no hay lugar para la cabecera de seccion"
struct.pack_into('<8sIIIIIIHHI',out,hdr_off,b'.rsrc2',total,newrva,rawsize,newraw,0,0,0,0,0x40000040)
struct.pack_into('<H',out,pe.pe+6,pe.nsec+1)
# SizeOfImage
struct.pack_into('<I',out,pe.pe+24+56,(newrva+total+sec_align-1)//sec_align*sec_align)
# data directory[2] resource
dd=pe.pe+24+96+2*8; struct.pack_into('<II',out,dd,newrva,total)
# ---- in-place GBK status strings (.rdata) : 8-byte slots
patch={'在线':'Online','离线':'Offline','掉电':'PwrOff','无设备':'NoDev','故障':'HWError','用户错':'UsrErr','忙':'Bsy','未知':'Unknown','成功':'Success','失败':'Failed'}
for zh,en in patch.items():
    g=zh.encode('gbk'); 
    for m in re.finditer(re.escape(g)+rb'\x00',bytes(out)):
        o=m.start()
        if out[o-1]!=0: continue
        slot=len(g); 
        while out[o+slot]==0 and slot<8: slot+=1
        if len(en)+1>slot: print("no entra",zh,en,slot); continue
        out[o:o+slot]=en.encode()+b'\0'*(slot-len(en)); print("patch",zh,"->",en)
# checksum: zero it (loader ignores for non-driver)
struct.pack_into('<I',out,pe.pe+24+64,0)
open(dst,'wb').write(out); print("OK",dst,len(out),"rsrc total",total)
