import re, sys
sys.stdout.reconfigure(encoding='utf-8')
# Print public signatures with the first summary sentence.
for path in sys.argv[1:]:
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    print('=== ' + path)
    summary = []
    in_sum = False
    for ln in lines:
        s = ln.strip()
        if s.startswith('///'):
            t = s[3:].strip()
            if t.startswith('<summary>'):
                in_sum = True
                summary = []
                t = t[len('<summary>'):]
            if '</summary>' in t:
                t = t.split('</summary>')[0]
                summary.append(t)
                in_sum = False
                continue
            if in_sum:
                summary.append(t)
            continue
        if re.match(r'^(public|protected)\b', s) or re.match(r'^(static public)\b', s):
            head = s.split('=>')[0]
            if 'internal_m2n' in head or 'internal_n2m' in head:
                summary = []
                continue
            doc = ' '.join(x for x in summary if x).strip()
            doc = re.sub(r'<see cref="([^"]+)"\s*/>', r'\1', doc)
            doc = re.sub(r'<[^>]+>', '', doc)
            if len(doc) > 220:
                doc = doc[:220] + '...'
            print(s.split(' => ')[0].rstrip('{').rstrip() + ('   // ' + doc if doc else ''))
            summary = []
        elif s and not s.startswith('[') :
            pass
