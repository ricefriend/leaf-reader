from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
entries=json.loads((root/'localization/strings.json').read_text(encoding='utf-8'))
def quoted(value):return json.dumps(value,ensure_ascii=False)
cs='using System;\nusing System.Collections.Generic;\nnamespace LeafReader { public static class L10n {\n public static bool English;\n public static string Pick(string zh,string en){return English?T(zh):zh;}\n public static string T(string text){string result;return English&&text!=null&&Map.TryGetValue(text,out result)?result:text;}\n static readonly Dictionary<string,string> Map=new Dictionary<string,string> {\n'
java='package org.leafreader;\nimport java.util.*;\nfinal class L10n {static volatile String language="zh";static String pick(String zh,String en){return language.equals("en")?t(zh):zh;}static String t(String text){String result=MAP.get(text);return language.equals("en")&&result!=null?result:text;}static final Map<String,String> MAP=new HashMap<>();static {\n'
for zh,en in entries.items():
 if not en:raise ValueError('Empty translation: '+zh)
 cs+=' {'+quoted(zh)+','+quoted(en)+'},\n'
 java+=' MAP.put('+quoted(zh)+','+quoted(en)+');\n'
(root/'L10n.cs').write_text(cs+' };}}\n',encoding='utf-8')
(root/'android/src/org/leafreader/L10n.java').write_text(java+' }}\n',encoding='utf-8')
print('Generated native catalogs:',len(entries),'entries')
