from pathlib import Path
import json,tempfile,importlib.util,copy,hashlib,zipfile,shutil
r=Path(__file__).resolve().parents[1];tool=r/'tools/import-localization.py'
# Test the validator using dedicated fixture data, not translating application strings.
spec=importlib.util.spec_from_file_location('locale_import',tool);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
entries=[{'id':'a','source':'Value {0}: 200 MB\nURL https://example.com','do_not_translate':False,'protected_tokens':['{0}','MB','https://example.com']},{'id':'b','source':'  Value  ','do_not_translate':False,'protected_tokens':[]}]
task={'catalog_version':'test','entries':entries};reply={'schema':'leaf-reader-localization-response/v1','catalog_version':'test','source_language':'zh-CN','target_language':'en','translations':[{'id':e['id'],'source':e['source'],'target':e['source']} for e in entries]}
checks=0
with tempfile.TemporaryDirectory(dir=r/'tools',prefix='locale-fixture-') as tmp:
 path=Path(tmp)/'response.json'
 def validate(value):path.write_text(json.dumps(value),encoding='utf-8');return module.validate([path],task)
 assert len(validate(reply))==2;checks+=1
 cases=[]
 def edited(action):x=copy.deepcopy(reply);action(x);cases.append(x)
 edited(lambda x:x.update(catalog_version='wrong'))
 edited(lambda x:x['translations'][0].update(source='changed'))
 edited(lambda x:x['translations'][0].update(target=''))
 edited(lambda x:x['translations'][0].update(target='Value {1}: 200 MB\nURL https://example.com'))
 edited(lambda x:x['translations'][0].update(target='Value {0}: 201 MB\nURL https://example.com'))
 edited(lambda x:x['translations'][0].update(target='Value {0}: 200 MB URL https://example.com'))
 edited(lambda x:x['translations'][0].update(target='Value {0}: 200 MB\nURL https://other.example.com'))
 edited(lambda x:x['translations'][1].update(target='Value'))
 edited(lambda x:x['translations'].pop())
 edited(lambda x:x['translations'].append(x['translations'][0]))
 edited(lambda x:x.update(extra='unexpected'))
 edited(lambda x:x['translations'][0].update(target='Value {0}: 200 MB\nURL https://example.com\x00'))
 for value in cases:
  try:validate(value)
  except ValueError:checks+=1
  else:raise AssertionError('Invalid translation response accepted')
 path.write_text('{"a":1,"a":2}',encoding='utf-8')
 try:module.load(path)
 except ValueError:checks+=1
 else:raise AssertionError('Duplicate JSON keys accepted')
 # Merge actual batch structure using fixture outputs, with no application catalog changes.
 parts=[]
 for n,rows in enumerate([reply['translations'][:1],reply['translations'][1:]]):
  batch=dict(reply);batch['translations']=rows;file=Path(tmp)/f'batch-{n}.json';file.write_text(json.dumps(batch),encoding='utf-8');parts.append(file)
 assert len(module.validate(parts,task))==2;checks+=1
print('PASS localization validator:',checks,'checks; no catalog applied')
