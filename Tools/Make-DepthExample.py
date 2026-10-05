"""Explicit TEST ONLY UI fixture. Not a wear model, real measurement, or operating plan."""
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path

start = datetime(2025, 5, 1, 9, tzinfo=timezone(timedelta(hours=9)))
stamp = lambda day: (start + timedelta(days=day)).isoformat()
operating_keys = ['plantMw','motorCurrentA','feedTph','amwi','journalSetPsi','journalActualPsi','bowlDpMmH2o','classifierRpm','specificEnergyKwhPerTon']
reliability_keys = ['wearMeanMm','wearRateUmPerHour','remainingHours','replacementAt','cycleOperatingHours']
machines = []
for i, letter in enumerate('abcdef'):
    machine = {'machineId':f'pulverizer_{letter}','maxGapSeconds':86400,'snapshots':[],'events':[],'maintenance':[],'correlation':None}
    for day in range(48):
        status = 'unknown' if letter=='f' else 'stopped' if (i+day)%7==0 else 'running'
        wear = None
        operating = dict.fromkeys(operating_keys)
        reliability = dict.fromkeys(reliability_keys)
        reliability['basis'] = ''
        if letter=='b':
            rows = [{'componentId':f'tire_0{r+1}','wearMm':[round(7+(day%24)*.35+r*.7+z*.9,2) for z in range(10)],'valid':[True]*10} for r in range(3)]
            if day<4: rows[2]['valid'][9]=False
            wear = {'schemaVersion':1,'machineId':machine['machineId'],'asOf':stamp(day),'source':'scenario','displayMode':'scenario','running':status=='running','bowlRpm':41.6 if status=='running' else 0,'classifierRpm':72 if status=='running' else 0,'wearLimitMm':38,'rollers':rows}
            operating.update(plantMw=480, motorCurrentA=41, feedTph=37 if status=='running' else 0, amwi=1.05 if day<7 else 1.22, journalSetPsi=950, journalActualPsi=945 if day<7 else 1010, bowlDpMmH2o=185, classifierRpm=72 if status=='running' else 0)
            reliability.update(wearMeanMm=round(sum(sum(r['wearMm']) for r in rows)/30,2),wearRateUmPerHour=1.5 if day<7 else 2.1,remainingHours=7000-day*20,replacementAt=stamp(day+(120-day*4 if day<24 else 180-(day-24)*4)),cycleOperatingHours=12000+day*20,basis='가상 UI 시험값. 마모 모델이나 실제 교체 권고 결과가 아닙니다.')
        machine['snapshots'].append({'at':stamp(day),'source':'scenario','status':status,'wear':wear,'operating':operating,'reliability':reliability})
    if letter=='b':
        for day, title, kind in [(7,'저널 압력 변화 (가상)','operation'),(14,'롤 표면 점검 (가상)','maintenance'),(19,'급탄 조건 변화 (가상)','operation')]:
            machine['events'].append({'id':f'test-event-{day}','severity':'high' if day==7 else 'medium' if day==19 else 'low','at':stamp(day),'title':title,'kind':kind,'source':'scenario','partId':'tire_02','zone':8,'wearMm':None,'beforeRateUmPerHour':1.5 if day==7 else None,'afterRateUmPerHour':2.1 if day==7 else None,'note':'화면 연동 시험용 이벤트입니다. 실제 원인이나 마모 기여도를 뜻하지 않습니다.'})
        machine['events'].append({'id':'test-replacement','at':stamp(24),'title':'롤타이어 세트 교체 (가상)','kind':'replacement','source':'scenario','partId':None,'zone':None,'severity':None,'wearMm':None,'beforeRateUmPerHour':None,'afterRateUmPerHour':None,'note':'교체 주기 분리 동작을 확인하는 가상 기록입니다. 실제 교체 이력이 아닙니다.'})
        machine['lifeCurves']=[{'componentId':None,'zone':None,'points':[{'at':stamp(day),'remainingDays':max(0,(120-day*4 if day<24 else 180-(day-24)*4) if day<48 else 135-day),'source':'scenario' if day<48 else 'forecast','cycleId':'example-cycle-1' if day<24 else 'example-cycle-2'} for day in range(136)]}]
        machine['maintenance']=[{'id':'test-inspection','at':stamp(14),'component':'롤타이어 세트','action':'표면 점검 (가상)','note':'정비 이력 표시 시험. 실제 정비 기록이 아닙니다.','source':'scenario'}]
    machines.append(machine)
dataset={'schemaVersion':2,'title':'TEST ONLY · 3단계 화면 검토용 가상 기록','generatedAt':stamp(47),'scenario':True,'baselineHours':20000,'machines':machines,
         'production':{'date':'2025-05-24','asOf':stamp(23),'targetMwh':12000,'actualMwh':9600,'source':'scenario','basis':'가상 생산 목표/실적, 실제 발전 실적 아님'},
         'costs':{'period':'가상 동일 1년 비교','asOf':stamp(23),'baselineKrw':72000000,'plannedKrw':65000000,'source':'scenario','basis':'비용 카드 표시 시험, 실제 비용 계산 아님'}}
path=Path(__file__).resolve().parents[1] / 'Frontend/public/ce883-data/TEST_ONLY_depth_example.json'
path.parent.mkdir(parents=True,exist_ok=True)
path.write_text(json.dumps(dataset,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
print(f'{path}: {path.stat().st_size} bytes')
