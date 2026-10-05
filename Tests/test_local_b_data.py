"""Run after importing the local workbook. No service or original DB writes."""
import hashlib
import importlib.util
from pathlib import Path
import sqlite3
import unittest

spec=importlib.util.spec_from_file_location('local_b_data',Path(__file__).resolve().parents[1]/'Tools/local_b_data.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)

class LocalBTests(unittest.TestCase):
    def test_complete_cache_and_readonly(self):
        with b.connect() as con:
            self.assertEqual(con.execute('SELECT COUNT(*) FROM readings').fetchone()[0],864000)
            with self.assertRaises(sqlite3.OperationalError): con.execute('DELETE FROM readings')

    def test_first_second_exact_values_and_missing_estimates(self):
        with b.connect() as con:
            row=con.execute('SELECT * FROM readings ORDER BY at LIMIT 1').fetchone()
        self.assertEqual(row['at'],'2025-12-10T00:00:00+09:00')
        self.assertEqual(row['plant_mw'],514.795166015625)
        self.assertEqual(row['feed_tph'],38.10734558105469)
        self.assertEqual(row['motor_tag_raw'],47.186004638671875)
        s=b.snapshot(row)
        self.assertEqual(s['status'],'running')
        self.assertIsNone(s['wear'])
        self.assertIsNone(s['operating']['motorCurrentA'])
        self.assertIsNone(s['operating']['classifierRpm'])
        self.assertIsNone(s['reliability']['remainingHours'])

    def test_sampling_keeps_real_points_last_second_and_only_b(self):
        with b.connect() as con: data=b.dataset(con)
        self.assertFalse(data['scenario']);self.assertIsNone(data['costs']);self.assertIsNone(data['production'])
        self.assertEqual([m['machineId'] for m in data['machines']],['pulverizer_b'])
        samples=data['machines'][0]['snapshots']
        self.assertEqual(len(samples),2881)
        self.assertEqual(samples[1]['at'],'2025-12-10T00:05:00+09:00')
        self.assertEqual(samples[-1]['at'],'2025-12-19T23:59:59+09:00')
        self.assertTrue(all(s['wear'] is None for s in samples))

    def test_fingerprint_detects_single_cell_or_timestamp_change(self):
        values=['2025-12-10T00:00:00+09:00',1.0,None]
        def hash_row(row):
            h=hashlib.sha256();b.fingerprint(h,row);return h.digest()
        original=hash_row(values)
        self.assertNotEqual(original,hash_row([values[0],1.00000000001,None]))
        self.assertNotEqual(original,hash_row([values[0],1.0,0]))
        self.assertNotEqual(original,hash_row(['2025-12-10T00:00:01+09:00',1.0,None]))

if __name__=='__main__': unittest.main()
