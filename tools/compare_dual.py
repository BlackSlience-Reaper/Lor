#!/usr/bin/env python3
"""核对三个发行包的身份、持久化顺序、补丁目标与数值。不运行模组初始化；补丁目标检查只在独立加载上下文里执行
补丁类的 Prepare/TargetMethod(s)，不安装补丁。"""
from pathlib import Path
import hashlib, json, re, subprocess, sys

root=Path(__file__).resolve().parents[1]
repos={'LibraryOfRuina':root,'ActLikeIt2':root/'build/ActLikeIt2-src','LibraryOfRuinaLib':root/'build/LibraryOfRuinaLib'}
targets=('0.107.1','0.111.0')
tools={}
for name in ('DualTargetAudit','PatchTargetCheck'):
    subprocess.run(['dotnet','build',str(root/'tools'/name/(name+'.csproj')),'-c','Release','--nologo'],check=True)
    tools[name]=root/'tools'/name/'bin/Release/net9.0'/(name+'.dll')
output=root/'build/dual-version-audit'
output.mkdir(parents=True,exist_ok=True)
def prop(name,target):
    return subprocess.check_output(['dotnet','msbuild',str(root/'LibraryOfRuina.csproj'),'-nologo','-p:CompatibilityTarget='+target,'-getProperty:'+name],text=True).strip()
def run(tool,*args):
    return subprocess.run(['dotnet',str(tools[tool]),*map(str,args)]).returncode==0
failures=[]
# 例外表是本体的；前置库的类名不会出现在里面，传空表，免得被当成过期条目。
no_exceptions=output/'no_exceptions.txt'
no_exceptions.write_text('# 前置库不登记例外\n')
static_ok={}; dynamic_ok={}
for target in targets:
    ritsu=Path(prop('RitsuLibRoot',target))
    dirs=[Path(prop('Sts2DataDir',target)),ritsu/'compat'/target,ritsu/'shared']+[repo/'build/dual-release'/id/'lib'/target for id,repo in repos.items()]
    for id,repo in repos.items():
        dll=repo/'build/dual-release'/id/'lib'/target/(id+'.dll')
        static_ok[id,target]=run('DualTargetAudit',dll,output/target/id,*dirs)
        exceptions=root/'tools/PatchTargetCheck/exceptions.txt' if id=='LibraryOfRuina' else no_exceptions
        dynamic_ok[id,target]=run('PatchTargetCheck',target,dll,exceptions,output/target/id/'patch_target_check.txt',*dirs)

# 源码数值的按目标差异只放行确切取值：原版 RNG 从 seed/counter 改为四段状态，新版本保留 FNV 混合（四次 FNV 素数），
# 旧版本直接使用 seed/counter、没有数值字面量。伊织战斗结束补丁在 0.111.0 选 EndCombatInternal(CombatTurnState)
# （参数个数 1、下标 0），0.107.1 只有无参版本。其他成员、或这些成员换了取值，都要重新审查。
pinned_numbers={'LibraryOfRuina':{
    'src/core/compat/GameApi.cs|GameApi.CreateDetachedRng':{'0.107.1':None,'0.111.0':','.join(['1099511628211UL']*4)},
    'src/content/specialguests/Iori/IoriSpecialGuestEncounters.cs|IoriSpecialGuestBgmCombatEndPatch.TargetMethod':{'0.107.1':None,'0.111.0':'1,0'},
}}
# 编译器生成名里的序号随同文件的其他成员增减而变，不代表逻辑变化。
generated=[(re.compile(r'd__\d+'),'d__N'),(re.compile(r'b__\d+_\d+'),'b__N'),(re.compile(r'<>c__DisplayClass\d+_\d+'),'<>c__DisplayClassN'),
           (re.compile(r'g__(\w+)\|\d+_\d+'),r'g__\1|N'),(re.compile(r'<RegexGenerator_g>F[0-9A-F]+__'),'<RegexGenerator_g>__')]
def il_numbers(target,id):
    rows={}
    for line in (output/target/id/'il_numbers.txt').read_text().splitlines():
        key,value=line.split('\t',1)
        for pattern,replacement in generated: key=pattern.sub(replacement,key)
        rows.setdefault(key,[]).append(value)
    return {key:sorted(values) for key,values in rows.items()}
reviewed={}
for line in (root/'tools/dual_target_il_reviewed.txt').read_text().splitlines():
    if line and not line.startswith('#'):
        id,key,reason=line.split('\t',2); reviewed[id,key]=reason

report={'targets':list(targets),'mods':{},'limitations':['这是离线元数据、源码与补丁目标解析比对，不是存档读写或双机联机验证。','补丁目标检查执行了 Prepare/TargetMethod(s)，但没有实际安装补丁。','0.107.1 未做运行时加载验证。']}
for id,repo in repos.items():
    entry={}
    for name in ['models','saved_property_order','patch_ids','constant_fields']:
        a=(output/targets[0]/id/(name+'.txt')).read_bytes(); b=(output/targets[1]/id/(name+'.txt')).read_bytes()
        if a!=b: failures.append(f'{id} 的 {name} 两个目标不一致')
        entry[name]={'equal':a==b,'rows':len(a.strip().splitlines()),'sha256':hashlib.sha256(a).hexdigest()}
    subprocess.run(['dotnet',str(tools['DualTargetAudit']),'--source',str(repo),str(output/'source'/id)],check=True)
    def manifest(kind,target):
        return dict(line.split('\t',1) for line in (output/'source'/id/(kind+'.'+target+'.txt')).read_text().splitlines())
    def differing(kind):
        a,b=(manifest(kind,t) for t in targets)
        return a,b,{key:{targets[0]:a.get(key),targets[1]:b.get(key)} for key in sorted(a.keys()|b.keys()) if a.get(key)!=b.get(key)}
    a,b,numbers=differing('source_numbers')
    if numbers!=pinned_numbers.get(id,{}): failures.append(f'{id} 的源码数值差异与钉死的取值不符：{numbers}')
    entry['source_numbers']={'members_'+targets[0]:len(a),'members_'+targets[1]:len(b),'differences':numbers}
    # 完整 token 流按成员比较：数值以外的按目标差异（(uint) 截断、换用的 API、签名）在这里列出供审查。
    a,b,tokens=differing('source_tokens')
    entry['source_tokens']={'members_'+targets[0]:len(a),'members_'+targets[1]:len(b),'target_specific_members':sorted(tokens)}
    # 有按目标源码差异的类型（取外层类型名）；IL 数值差异落在这些类型里视为由源码解释。
    specific_types={key.split('|',1)[1].split('.')[0] for key in tokens}-{''}
    a,b=(il_numbers(t,id) for t in targets)
    il_differences=sorted(key for key in a.keys()|b.keys() if a.get(key)!=b.get(key))
    outer=lambda key:key.split('::')[0].split('/')[0].rsplit('.',1)[-1].split('`')[0]
    unexplained=[key for key in il_differences if outer(key) not in specific_types]
    for key in unexplained:
        if (id,key) not in reviewed: failures.append(f'{id} 的 IL 数值按目标不同，源码却没有按目标的差异，需审查后登记到 tools/dual_target_il_reviewed.txt：{key} {targets[0]}={a.get(key)} {targets[1]}={b.get(key)}')
    for (rid,key),reason in reviewed.items():
        if rid==id and key not in unexplained: failures.append(f'tools/dual_target_il_reviewed.txt 的条目已不再出现差异或已由源码解释，删掉它：{id}\t{key}')
    entry['il_numbers']={'differing_methods':len(il_differences),'explained_by_source':len(il_differences)-len(unexplained),'reviewed':{key:reviewed.get((id,key)) for key in unexplained}}
    entry['static_patch_targets_resolved']={t:static_ok[id,t] for t in targets}
    entry['patch_target_check_passed']={t:dynamic_ok[id,t] for t in targets}
    for t in targets:
        if not static_ok[id,t]: failures.append(f'{id} {t} 的静态补丁目标或参数注入有问题，见 {output/t/id/"missing_patch_targets.txt"}')
        if not dynamic_ok[id,t]: failures.append(f'{id} {t} 的补丁目标解析失败，见上方 PatchTargetCheck 输出与 {output/t/id/"patch_target_check.txt"}')
    entry['dynamic_patch_target_classes']=sum('dynamic/manual' in line for line in (output/targets[0]/id/'patch_targets.txt').read_text().splitlines())
    bundle=json.loads((repo/'build/dual-release'/id/'bundle-evidence.json').read_text())
    entry['binary_source_commit']=bundle['sourceCommit']
    entry['binary_source_dirty']=bundle['sourceDirty']
    entry['implementation_sha256']={target:bundle['files']['lib/'+target+'/'+id+'.dll'] for target in targets}
    entry['inspected_source_commit']=subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()
    entry['inspected_tracked_source_dirty']=bool(subprocess.check_output(['git','-C',str(repo),'status','--porcelain','--untracked-files=no'],text=True).strip())
    report['mods'][id]=entry
report['passed']=not failures
report['failures']=failures
(output/'comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
if failures:
    print('\n'.join(['双版本比对失败：']+failures),file=sys.stderr)
    sys.exit(1)
