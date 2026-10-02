#!/usr/bin/env python3
"""核对三个发行包的身份、持久化顺序、补丁清单与数值；不运行模组代码。"""
from pathlib import Path
import hashlib, json, subprocess

root=Path(__file__).resolve().parents[1]
repos={'LibraryOfRuina':root,'ActLikeIt2':root/'build/ActLikeIt2-src','LibraryOfRuinaLib':root/'build/LibraryOfRuinaLib'}
targets=('0.107.1','0.111.0')
tool_project=root/'tools/DualTargetAudit/DualTargetAudit.csproj'
subprocess.run(['dotnet','build',str(tool_project),'-c','Release','--nologo'],check=True)
tool=root/'tools/DualTargetAudit/bin/Release/net9.0/DualTargetAudit.dll'
output=root/'build/dual-version-audit'
def prop(name,target):
    return subprocess.check_output(['dotnet','msbuild',str(root/'LibraryOfRuina.csproj'),'-nologo','-p:CompatibilityTarget='+target,'-getProperty:'+name],text=True).strip()
def run(*args):subprocess.run(['dotnet',str(tool),*map(str,args)],check=True)
for target in targets:
    ritsu=Path(prop('RitsuLibRoot',target))
    dirs=[Path(prop('Sts2DataDir',target)),ritsu/'compat'/target,ritsu/'shared']+[repo/'build/dual-release'/id/'lib'/target for id,repo in repos.items()]
    for id,repo in repos.items():
        run(repo/'build/dual-release'/id/'lib'/target/(id+'.dll'),output/target/id,*dirs)

report={'targets':list(targets),'mods':{},'limitations':['这是离线元数据与源码数值比对，不是存档读写或双机联机验证。','0.107.1 未做运行时加载验证。']}
for id,repo in repos.items():
    entry={}
    for name in ['models','saved_property_order','patch_ids','constant_fields']:
        a=(output/targets[0]/id/(name+'.txt')).read_bytes(); b=(output/targets[1]/id/(name+'.txt')).read_bytes()
        assert a==b,f'{id} 的 {name} 不一致'
        entry[name]={'equal':True,'rows':len(a.strip().splitlines()),'sha256':hashlib.sha256(a).hexdigest()}
    run('--source',repo,output/'source'/id)
    def numeric(target):
        return dict(line.split('\t',1) for line in (output/'source'/id/('source_numbers.'+target+'.txt')).read_text().splitlines())
    a,b=map(numeric,targets)
    differences={key:{targets[0]:a.get(key),targets[1]:b.get(key)} for key in sorted(a.keys()|b.keys()) if a.get(key)!=b.get(key)}
    # 原版 RNG 从 seed/counter 改为四段状态；新版本保持原有 FNV 混合，旧版本直接使用 seed/counter。
    # 此例外仅限适配器，不能接受任何玩法成员的数值差异。
    allowed={'src/core/compat/GameApi.cs|GameApi.CreateDetachedRng'} if id=='LibraryOfRuina' else set()
    assert set(differences)<=allowed,f'{id} 存在未审查的源码数值差异：{differences}'
    entry['source_numbers']={'members_0.107.1':len(a),'members_0.111.0':len(b),'gameplay_equal':True,'adapter_differences':differences}
    entry['static_patch_targets_resolved']=True
    entry['dynamic_patch_target_classes']=sum('dynamic/manual' in line for line in (output/targets[0]/id/'patch_targets.txt').read_text().splitlines())
    entry['source_commit']=subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()
    report['mods'][id]=entry
(output/'comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
