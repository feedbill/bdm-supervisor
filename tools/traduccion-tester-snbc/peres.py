import struct, re, sys, json
TYPES={1:'CURSOR',2:'BITMAP',3:'ICON',4:'MENU',5:'DIALOG',6:'STRING',9:'ACCEL',10:'RCDATA',12:'GROUP_CURSOR',14:'GROUP_ICON',16:'VERSION',24:'MANIFEST'}
class PE:
    def __init__(s,path):
        s.d=bytearray(open(path,'rb').read()); d=s.d
        s.pe=struct.unpack_from('<I',d,0x3c)[0]; s.nsec=struct.unpack_from('<H',d,s.pe+6)[0]; s.opt=struct.unpack_from('<H',d,s.pe+20)[0]
        s.secs=[]
        for i in range(s.nsec):
            o=s.pe+24+s.opt+i*40; name=bytes(d[o:o+8]).rstrip(b'\0').decode(); vs,va,rs,ro=struct.unpack_from('<IIII',d,o+8); s.secs.append([name,va,vs,ro,rs,o])
        s.rsrc=[x for x in s.secs if x[0]=='.rsrc'][0]
    def rva2off(s,r):
        for name,va,vs,ro,rs,o in s.secs:
            if va<=r<va+max(vs,rs): return r-va+ro
    def readdir(s,off):
        d=s.d; nn,ni=struct.unpack_from('<HH',d,off+12); ents=[]
        for i in range(nn+ni):
            nid,ofs=struct.unpack_from('<II',d,off+16+i*8); ents.append((nid,ofs))
        return ents
    def name_at(s,base,nid):
        if nid&0x80000000:
            o=base+(nid&0x7fffffff); L=struct.unpack_from('<H',s.d,o)[0]; return ('name',bytes(s.d[o+2:o+2+2*L]).decode('utf-16le'))
        return ('id',nid)
    def tree(s):
        base=s.rsrc[3]; out=[]
        for tid,tofs in s.readdir(base):
            for nid,nofs in s.readdir(base+(tofs&0x7fffffff)):
                for lid,lofs in s.readdir(base+(nofs&0x7fffffff)):
                    drva,size,cp=struct.unpack_from('<III',s.d,base+lofs)
                    out.append(dict(type=s.name_at(base,tid),name=s.name_at(base,nid),lang=lid,cp=cp,data=bytes(s.d[s.rva2off(drva):s.rva2off(drva)+size])))
        return out

def rd_sz(b,p):
    e=p
    while b[e:e+2]!=b'\0\0': e+=2
    return b[p:e].decode('utf-16le'), e+2
def rd_sz_or_ord(b,p):
    w=struct.unpack_from('<H',b,p)[0]
    if w==0xFFFF: return ('ord',struct.unpack_from('<H',b,p+2)[0]), p+4
    if w==0: return ('sz',''), p+2
    t,p=rd_sz(b,p); return ('sz',t),p
def wr_sz(t): return t.encode('utf-16le')+b'\0\0'
def wr_sz_or_ord(v):
    k,x=v
    return struct.pack('<HH',0xFFFF,x) if k=='ord' else wr_sz(x)
def align4(b): return b+b'\0'*((-len(b))%4)

def parse_dialog(b):
    ex = struct.unpack_from('<H',b,2)[0]==0xFFFF
    p=0; dlg={'ex':ex}
    if ex:
        dlg['ver'],dlg['sig'],dlg['helpID'],dlg['exStyle'],dlg['style'],n,x,y,cx,cy=struct.unpack_from('<HHIIIHhhhh',b,0); p=26
    else:
        dlg['style'],dlg['exStyle'],n,x,y,cx,cy=struct.unpack_from('<IIHhhhh',b,0); p=18
    dlg['rect']=(x,y,cx,cy)
    dlg['menu'],p=rd_sz_or_ord(b,p); dlg['cls'],p=rd_sz_or_ord(b,p); dlg['title'],p=rd_sz(b,p)
    if dlg['style']&0x40 or dlg['style']&0x48:  # DS_SETFONT / DS_SHELLFONT
        if ex:
            dlg['pt'],dlg['weight'],dlg['italic'],dlg['charset']=struct.unpack_from('<HHBB',b,p); p+=6
        else:
            dlg['pt']=struct.unpack_from('<H',b,p)[0]; p+=2
        dlg['face'],p=rd_sz(b,p)
    items=[]
    for i in range(n):
        p+=(-p)%4; it={}
        if ex:
            it['helpID'],it['exStyle'],it['style'],x,y,cx,cy,it['id']=struct.unpack_from('<IIIhhhhI',b,p); p+=24
        else:
            it['style'],it['exStyle'],x,y,cx,cy,it['id']=struct.unpack_from('<IIhhhhH',b,p); p+=18
        it['rect']=(x,y,cx,cy)
        it['cls'],p=rd_sz_or_ord(b,p); it['title'],p=rd_sz_or_ord(b,p)
        ec=struct.unpack_from('<H',b,p)[0]; p+=2
        it['extra']=b[p:p+ec] if ec else b''; p+=ec
        items.append(it)
    dlg['items']=items; return dlg

def build_dialog(dlg):
    ex=dlg['ex']; x,y,cx,cy=dlg['rect']; n=len(dlg['items'])
    if ex: b=struct.pack('<HHIIIHhhhh',dlg['ver'],dlg['sig'],dlg['helpID'],dlg['exStyle'],dlg['style'],n,x,y,cx,cy)
    else: b=struct.pack('<IIHhhhh',dlg['style'],dlg['exStyle'],n,x,y,cx,cy)
    b+=wr_sz_or_ord(dlg['menu'])+wr_sz_or_ord(dlg['cls'])+wr_sz(dlg['title'])
    if 'face' in dlg:
        if ex: b+=struct.pack('<HHBB',dlg['pt'],dlg['weight'],dlg['italic'],dlg['charset'])
        else: b+=struct.pack('<H',dlg['pt'])
        b+=wr_sz(dlg['face'])
    for it in dlg['items']:
        b=align4(b); x,y,cx,cy=it['rect']
        if ex: b+=struct.pack('<IIIhhhhI',it['helpID'],it['exStyle'],it['style'],x,y,cx,cy,it['id'])
        else: b+=struct.pack('<IIhhhhH',it['style'],it['exStyle'],x,y,cx,cy,it['id'])
        b+=wr_sz_or_ord(it['cls'])+wr_sz_or_ord(it['title'])+struct.pack('<H',len(it['extra']))+it['extra']
    return b

def parse_strings(b):
    out=[]; p=0
    for i in range(16):
        L=struct.unpack_from('<H',b,p)[0]; p+=2; out.append(b[p:p+2*L].decode('utf-16le')); p+=2*L
    return out
def build_strings(ss):
    b=b''
    for t in ss: e=t.encode('utf-16le'); b+=struct.pack('<H',len(e)//2)+e
    return b

if __name__=='__main__':
    pe=PE(sys.argv[1]); res=pe.tree(); cjk=re.compile(r'[一-鿿]')
    uniq={}
    for r in res:
        if r['type']==('id',5):
            dlg=parse_dialog(r['data'])
            for t in [dlg['title']]+[it['title'][1] for it in dlg['items'] if it['title'][0]=='sz']:
                if cjk.search(t): uniq.setdefault(t,0); uniq[t]+=1
            print("DLG",r['name'],"font:",dlg.get('face'),dlg.get('charset'),"title:",dlg['title'],"items:",len(dlg['items']),file=sys.stderr)
        elif r['type']==('id',6):
            for t in parse_strings(r['data']):
                if cjk.search(t): uniq.setdefault(t,0); uniq[t]+=1
    print(json.dumps(sorted(uniq.keys()),ensure_ascii=False,indent=0))
