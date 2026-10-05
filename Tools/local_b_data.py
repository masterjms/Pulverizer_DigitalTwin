"""Read-only local B history API; explicit, verified Excel import into a separate SQLite cache."""
from __future__ import annotations
import argparse
from collections import Counter
from contextlib import closing
from datetime import datetime, timezone
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import math
from pathlib import Path
import sqlite3
import struct
from urllib.parse import parse_qs, urlparse

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'Data' / 'local-b'
DB = DATA / 'history.sqlite'
MARCH_DB = ROOT / 'Data' / 'march-b-review' / 'history.sqlite'
FIELDS = ['plant_mw', 'motor_status', 'motor_tag_raw', 'inlet_temp_c',
          'outlet_temp_c', 'bowl_dp_mmh2o', 'classifier_demand_pct',
          'classifier_speed_raw', 'classifier_bias_raw', 'tramp_valve_open',
          'hopper_temp_c', 'journal_set_psi', 'journal_actual_psi', 'feed_tph']
TAGS = ['3DW', '3PULV-B-SU', '3HFC20AE001-XM01', '3655-1544', '3655-1788',
        '3655-2259', '3CA-MRSB-MA', '3HFC20CS011XM01', '3CA-MRSB-BIAS',
        '3HFC27AA001-XB32', '3AH-TE-612', '3MILLB-JOUR-PR', '3HFE20PT309', '3HFB20CW001XM01']

def fingerprint(h, row):
    h.update(row[0].encode('ascii') + b'\0')
    for value in row[1:]:
        h.update(b'N' if value is None else b'V' + struct.pack('>d', float(value)))

def connect(path=None):
    con = sqlite3.connect((path or DB).as_uri() + '?mode=ro', uri=True)
    con.row_factory = sqlite3.Row
    con.execute('PRAGMA query_only=ON')
    return con

def import_workbook(path):
    from openpyxl import load_workbook
    DATA.mkdir(parents=True, exist_ok=True)
    if DB.exists():
        raise RuntimeError('Verified cache already exists. Keep it; choose a separate cache for another source.')
    digest = hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()
    tmp = DATA / 'history.importing.sqlite'
    if tmp.exists():
        raise RuntimeError('An incomplete import exists; inspect it before retrying.')
    con = sqlite3.connect(tmp)
    con.execute('CREATE TABLE readings (at TEXT PRIMARY KEY,' + ','.join(f'{f} REAL' for f in FIELDS) + ') WITHOUT ROWID')
    con.execute('CREATE TABLE metadata (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL)')
    book = load_workbook(path, read_only=True, data_only=True)
    sheet = book['Data Normalize']
    rows = sheet.iter_rows(min_col=4, max_col=18, values_only=True)
    headers = [next(rows) for _ in range(18)]
    if list(headers[0][1:]) != [f'BRDP1.BR3.{t}/PLOT' for t in TAGS]:
        raise ValueError('Workbook tag order differs from the verified B mapping')
    expected = headers[17][0]
    h = hashlib.sha256(); n = 0; previous = None; first = None; batch = []
    gaps = Counter(); states = Counter(); nulls = [0]*14
    mins = [None]*14; maxs = [None]*14
    sql = 'INSERT INTO readings VALUES (' + ','.join('?' for _ in range(15)) + ')'
    for excel_row, raw in enumerate(rows, 19):
        if not isinstance(raw[0], datetime) or raw[0].microsecond:
            raise ValueError(f'Invalid timestamp at Excel row {excel_row}')
        # Excel has no timezone. Retain original wall-clock time and explicitly assume plant KST.
        stamp = raw[0].isoformat(timespec='seconds') + '+09:00'
        if previous is not None:
            seconds = (raw[0]-previous).total_seconds()
            if seconds <= 0: raise ValueError(f'Duplicate/out-of-order timestamp: row {excel_row}')
            gaps[str(seconds)] += 1
        else: first = stamp
        previous = raw[0]
        values = []
        for i, value in enumerate(raw[1:]):
            if value is not None and (not isinstance(value, (int,float)) or not math.isfinite(value)):
                raise ValueError(f'Invalid numeric value at row {excel_row}, tag {i}')
            values.append(None if value is None else float(value))
            if value is None: nulls[i] += 1
            else:
                mins[i] = value if mins[i] is None else min(mins[i],value)
                maxs[i] = value if maxs[i] is None else max(maxs[i],value)
        row = (stamp,*values); fingerprint(h,row); batch.append(row); n += 1
        states[str(values[1])] += 1
        if len(batch) == 10000:
            con.executemany(sql,batch); con.commit(); batch.clear()
        if n % 100000 == 0: print(f'Imported {n:,} rows', flush=True)
    con.executemany(sql,batch); con.commit(); book.close()
    if n != expected: raise ValueError(f'Header count {expected} != actual count {n}')
    check = hashlib.sha256(); count = 0
    for row in con.execute('SELECT * FROM readings ORDER BY at'):
        fingerprint(check,row); count += 1
    if count != n or check.hexdigest() != h.hexdigest():
        raise ValueError('Full SQLite round-trip verification failed')
    report = dict(service='nextro-local-b', sourcePath=str(path), workbookSha256=digest,
        sheet='Data Normalize', range=f'D19:R{n+18}', rows=n, first=first, last=stamp,
        sourceTimezone='Excel has no timezone; interpreted as Asia/Seoul (+09:00). No wall-clock shift.',
        sourceRowFingerprint=h.hexdigest(), cacheRowFingerprint=check.hexdigest(),
        verifiedNumericCells=n*14, exactRoundTrip=True, intervalsSeconds=dict(gaps), motorStates=dict(states),
        columns=[dict(field=f,tag=headers[0][i+1],description=headers[1][i+1],
            exportUnit=headers[2][i+1],nulls=nulls[i],min=mins[i],max=maxs[i]) for i,f in enumerate(FIELDS)],
        warnings=['MOTOR POWER export unit is CU.M/H; reference dictionary says A. No current/power conversion applied.',
            'Classifier export has no unit. Raw value retained; RPM is not asserted by this adapter.',
            'No wear, maintenance, replacement baseline date, cost or production target columns. No invented estimates.'],
        importedAt=datetime.now(timezone.utc).isoformat())
    con.execute('INSERT INTO metadata VALUES (1,?)',(json.dumps(report,ensure_ascii=False),))
    con.commit(); con.close(); tmp.rename(DB)
    (DATA/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:report[k] for k in ['rows','first','last','workbookSha256','exactRoundTrip','intervalsSeconds','motorStates']},ensure_ascii=False),flush=True)

def snapshot(row):
    # Unknown units and wear are intentionally null; never turn missing wear into 0% wear.
    # Below-zero analog export values remain in raw; they are not valid positive physical metrics.
    positive = lambda key: row[key] if row[key] is not None and row[key] >= 0 else None
    return dict(at=row['at'], source='measured', status='running' if row['motor_status']==1 else 'stopped' if row['motor_status']==4 else 'fault' if row['motor_status']==20 else 'unknown',
        wear=None, operating=dict(plantMw=positive('plant_mw'),motorCurrentA=None,feedTph=positive('feed_tph'),
            amwi=None,journalSetPsi=positive('journal_set_psi'),journalActualPsi=positive('journal_actual_psi'),
            bowlDpMmH2o=positive('bowl_dp_mmh2o'),classifierRpm=None,specificEnergyKwhPerTon=None),
        reliability=dict(wearMeanMm=None,wearRateUmPerHour=None,remainingHours=None,replacementAt=None,
            cycleOperatingHours=None,basis='운전 기록 연결 완료. 마모 측정 기준·교체 한계 확인 및 예측 계산 연결 전으로, 잔여수명은 미산정입니다.'))

def march_events():
    # Document event and exact observed export transitions. No inferred severity or wear effect.
    rows = [
        ('inspection', '08:00:00', '롤타이어 마모도 측정', 'maintenance', '정비 이력 원문: 2026-03-27 08:00~16:00. 3개 롤의 위치별 마모 측정일과 일치합니다. 교체 기록이 아니므로 수명을 초기화하지 않습니다.'),
        ('stopped', '08:45:24', '모터 상태 4 확인', 'operation', '운전 엑셀에서 상태값 4가 처음 나타난 시각입니다. 정의서상 정지. 직전 91초는 소수값으로 전환되어 실제 정지 순간과 차이가 있을 수 있습니다.'),
        ('code20', '10:11:41', '모터 상태 코드 20 확인', 'alarm', '정의서는 1·4 이외를 Fault로 분류합니다. 코드 20의 구체적 의미와 원인은 미확인입니다. 심각도나 마모 기여도를 임의로 부여하지 않았습니다.'),
        ('running', '15:36:52', '모터 상태 1 복귀', 'operation', '운전 엑셀에서 상태값이 1로 복귀한 시각입니다. 상태 전환에 소수값이 포함되어 실제 기동 순간을 확정하는 값은 아닙니다.'),
    ]
    return [dict(id='march-'+key, at='2026-03-27T'+clock+'+09:00', title=title, kind=kind,
        source='measured', partId=None, zone=None, severity=None, wearMm=None,
        beforeRateUmPerHour=None, afterRateUmPerHour=None, note=note) for key,clock,title,kind,note in rows]

def dataset(con, march=False):
    meta = json.loads(con.execute('SELECT json FROM metadata').fetchone()[0])
    # Keep all seconds in the database; overview is an exact point every five minutes, not an average.
    rows = list(con.execute("SELECT * FROM readings WHERE substr(at,15,5) IN ('00:00','05:00','10:00','15:00','20:00','25:00','30:00','35:00','40:00','45:00','50:00','55:00') ORDER BY at"))
    last = con.execute('SELECT * FROM readings ORDER BY at DESC LIMIT 1').fetchone()
    if rows[-1]['at'] != last['at']: rows.append(last)
    events = march_events() if march else []
    for event in events:
        row = con.execute('SELECT * FROM readings WHERE at=?', (event['at'],)).fetchone()
        if row is not None: rows.append(row)
    rows = sorted({r['at']:r for r in rows}.values(), key=lambda r:r['at'])
    return dict(schemaVersion=2,title=f"미분기 B · {meta['first'][:10]}~{meta['last'][:10]} · {meta['rows']:,}초 기록",
        generatedAt=meta['importedAt'],scenario=False,baselineHours=20000,
        machines=[dict(machineId='pulverizer_b',maxGapSeconds=1,snapshots=[snapshot(r) for r in rows],
            events=events,maintenance=[dict(id=e['id'],at=e['at'],component='롤타이어 세트',action=e['title'],note=e['note'],source=e['source']) for e in events if e['kind']=='maintenance'],correlation=None)],production=None,costs=None)

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        url = urlparse(self.path)
        try:
            march = url.path.startswith('/api/local-b/march/')
            route = url.path.replace('/api/local-b/march/', '/api/local-b/', 1) if march else url.path
            with closing(connect(MARCH_DB if march else None)) as con:
                if route == '/api/local-b/health': result = {'service':'nextro-local-b','readOnly':True}
                elif route == '/api/local-b/audit': result = json.loads(con.execute('SELECT json FROM metadata').fetchone()[0])
                elif route == '/api/local-b/workspace': result = dataset(con, march)
                elif route == '/api/local-b/point':
                    at = parse_qs(url.query).get('at',[''])[0]
                    row = con.execute('SELECT * FROM readings WHERE at=?',(at,)).fetchone()
                    if row is None: self.send_error(404,'No exact recorded timestamp'); return
                    result = {'snapshot':snapshot(row),'raw':dict(row)}
                else: self.send_error(404); return
            body = json.dumps(result,ensure_ascii=False,allow_nan=False,separators=(',',':')).encode('utf-8')
            self.send_response(200); self.send_header('Content-Type','application/json; charset=utf-8')
            self.send_header('Content-Length',str(len(body))); self.send_header('Cache-Control','no-store')
            self.send_header('X-Content-Type-Options','nosniff'); self.end_headers(); self.wfile.write(body)
        except (sqlite3.Error, OSError): self.send_error(503,'Verified B cache is unavailable')

if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--import-xlsx',type=Path); parser.add_argument('--serve',action='store_true')
    args=parser.parse_args()
    if args.import_xlsx: import_workbook(args.import_xlsx)
    elif args.serve:
        if not DB.exists(): raise SystemExit('Import and verify the workbook first.')
        print('B history read-only API: http://127.0.0.1:8143',flush=True)
        ThreadingHTTPServer(('127.0.0.1',8143),Handler).serve_forever()
    else: parser.print_help()
