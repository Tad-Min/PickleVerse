"""Read-only comparison of non-UI Unity scene objects against Git HEAD."""
import collections, pathlib, re, subprocess, json
root=pathlib.Path(__file__).resolve().parents[2]
def blocks(text):
    return {m[1]:(m[0],m[2]) for m in re.findall(r'^--- !u!(\d+) &(-?\d+)\r?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
results=[]
for scene in ['BotMatch','MultiplayerCourt']:
    path=f'My project/Assets/Scenes/{scene}.unity'
    old=blocks(subprocess.check_output(['git','show','HEAD:'+path],cwd=root).decode('utf-8'))
    new=blocks((root/path).read_text(encoding='utf-8-sig'))
    ui_roots={re.search(r'm_GameObject: \{fileID: (-?\d+)\}',body).group(1) for ident,(kind,body) in old.items() if kind=='223'}
    transforms={ident:(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',body).group(1),re.search(r'm_Father: \{fileID: (-?\d+)\}',body).group(1)) for ident,(kind,body) in old.items() if kind in ['4','224']}
    ui_transforms={ident for ident,(go,parent) in transforms.items() if go in ui_roots}
    while True:
        expanded=ui_transforms|{ident for ident,(go,parent) in transforms.items() if parent in ui_transforms}
        if expanded==ui_transforms:break
        ui_transforms=expanded
    ui_objects={transforms[ident][0] for ident in ui_transforms}
    changes=[]
    for ident,(kind,body) in old.items():
        if ident not in new:
            changes.append({'id':ident,'type':kind,'removed':True});continue
        go=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',body)
        non_ui_script=kind=='114' and go and go.group(1) not in ui_objects
        if (kind in ['4','20','21','23','33','54','64','65','108','157'] or non_ui_script) and body!=new[ident][1]:
            a=body.splitlines();b=new[ident][1].splitlines()
            delta=[line for line in __import__('difflib').unified_diff(a,b,n=1) if len(line)<200]
            changes.append({'id':ident,'type':kind,'delta':delta[:20]})
    results.append({'scene':scene,'oldObjects':len(old),'newObjects':len(new),'nonUiChanges':changes})
out=root/'UnityWork/MatchHud20261020/scene-audit.json'
out.write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([{'scene':r['scene'],'oldObjects':r['oldObjects'],'newObjects':r['newObjects'],'nonUiChanges':len(r['nonUiChanges'])} for r in results]))
