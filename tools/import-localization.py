"""Validate external AI output as data. --apply is required to modify the catalog."""
from pathlib import Path
from collections import Counter
import argparse,json,re,runpy
ROOT=Path(__file__).resolve().parents[1]
def unique(pairs):
 result={}
 for key,value in pairs:
  if key in result:raise ValueError('Duplicate JSON field: '+key)
  result[key]=value
 return result
def load(path):
 path=Path(path)
 if path.stat().st_size>2*1024*1024:raise ValueError('JSON file exceeds 2 MB: '+str(path))
 return json.loads(path.read_text(encoding='utf-8-sig'),object_pairs_hook=unique)
def urls(text):return Counter(s.rstrip('.') for s in re.findall(r'https?://[A-Za-z0-9.\-:/?&=%#_+]+',text))
def numbers(text):return Counter(re.findall(r'(?<![A-Za-z])\d+(?:\.\d+)*',text))
def validate(paths,task):
 entries={e['id']:e for e in task['entries']};results={}
 for path in paths:
  data=load(path)
  if set(data)!={'schema','catalog_version','source_language','target_language','translations'}:raise ValueError('Unexpected response fields: '+str(path))
  for key,expected in [('schema','leaf-reader-localization-response/v1'),('catalog_version',task['catalog_version']),('source_language','zh-CN'),('target_language','en')]:
   if data.get(key)!=expected:raise ValueError('Wrong '+key+' in '+str(path))
  if not isinstance(data['translations'],list):raise ValueError('translations must be an array')
  for value in data['translations']:
   if not isinstance(value,dict) or set(value)!={'id','source','target'}:raise ValueError('Each entry must contain only id/source/target')
   ident=value['id']
   if not isinstance(ident,str) or ident not in entries or ident in results:raise ValueError('Unknown or duplicate id: '+str(ident))
   source=entries[ident]['source'];target=value['target']
   if value['source']!=source:raise ValueError('Source changed: '+ident)
   if not isinstance(target,str) or not target.strip():raise ValueError('Empty/non-string translation: '+ident)
   if len(target)>max(512,len(source)*8):raise ValueError('Translation unexpectedly long: '+ident)
   if any(ord(c)<32 and c not in '\n\t' or c in '\u2028\u2029' for c in target):raise ValueError('Unsupported control character: '+ident)
   if target.count('\n')!=source.count('\n') or [i for i,line in enumerate(target.split('\n')) if not line.strip()]!=[i for i,line in enumerate(source.split('\n')) if not line.strip()]:raise ValueError('Newline/blank line layout changed: '+ident)
   if entries[ident]['do_not_translate'] and target!=source:raise ValueError('Protected label changed: '+ident)
   for token in entries[ident]['protected_tokens']:
    if target.count(token)<source.count(token):raise ValueError('Protected token changed: '+ident+' / '+token)
   if urls(target)!=urls(source):raise ValueError('URL changed or added: '+ident)
   if numbers(target)!=numbers(source):raise ValueError('Numeric values changed: '+ident)
   if Counter(re.findall(r'\{(?:\d+|[A-Za-z_][\w.]*)\}',target))!=Counter(re.findall(r'\{(?:\d+|[A-Za-z_][\w.]*)\}',source)):raise ValueError('Placeholders changed: '+ident)
   leading=re.match(r'^[ \t]*',source).group();trailing=re.search(r'[ \t]*$',source).group()
   if leading and not target.startswith(leading) or trailing and not target.endswith(trailing):raise ValueError('Required boundary whitespace removed: '+ident)
   results[ident]=target
 missing=set(entries)-set(results)
 if missing:raise ValueError('Missing '+str(len(missing))+' entries: '+', '.join(sorted(missing)[:8]))
 return {e['source']:results[e['id']] for e in task['entries']}
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('responses',nargs='+');parser.add_argument('--apply',action='store_true');args=parser.parse_args()
 task=load(ROOT/'localization/translation-request.json');catalog=validate(args.responses,task)
 print('PASS: validated',len(catalog),'translations; catalog',task['catalog_version'])
 if args.apply:
  if not (ROOT/'android/src/org/leafreader').is_dir() or not (ROOT/'MainWindow.cs').is_file():raise ValueError('--apply must run inside the actual Leaf Reader source tree')
  path=ROOT/'localization/strings.json';temporary=path.with_suffix('.json.tmp');temporary.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');temporary.replace(path);runpy.run_path(str(ROOT/'tools/sync-localization.py'),run_name='__main__');print('Applied to both native catalogs. Build and UI checks are still required.')
if __name__=='__main__':
 try:main()
 except (ValueError,OSError,TypeError,KeyError) as ex:raise SystemExit('VALIDATION FAILED: '+str(ex))
