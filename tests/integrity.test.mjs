import test from 'node:test';
import assert from 'node:assert/strict';
import {seedDemo,produce,undo,auditIntegrity,integrityCsv} from '../src/ProductionFlow.Api/wwwroot/domain.mjs';

test('clean seeded state: all checks pass',()=>{
 const report=auditIntegrity(seedDemo());
 assert.equal(report.checkedOrders,5);
 assert.equal(report.checkedRuns,2);
 assert.equal(report.errors,0);
 assert.equal(report.warnings,0);
 assert.deepEqual(report.issues,[]);
});
test('a valid production and reversal keep ledger integrity',()=>{
 let state=seedDemo();
 const result=produce(state,101,6,'tester','test-production');state=result.state;
 assert.equal(auditIntegrity(state).errors,0);
 state=undo(state,result.run.id,'Üretim kayıt testi','tester').state;
 assert.equal(auditIntegrity(state).errors,0);
 assert.equal(auditIntegrity(state).warnings,0);
});
test('detects changed order quantity',()=>{
 const state=seedDemo();state.orders[1].produced=23;
 assert.ok(auditIntegrity(state).issues.some(x=>x.code==='ORDER_QUANTITY'&&x.reference==='ÜE-260102'));
});
test('detects wrong status independently',()=>{
 const state=seedDemo();state.orders[0].status='Kapandı';
 assert.ok(auditIntegrity(state).issues.some(x=>x.code==='ORDER_STATUS'));
});
test('detects incorrect BOM consumption for specific production run',()=>{
 const state=seedDemo();state.movements.find(x=>x.type==='Hammadde çıkışı').delta=-1;
 assert.ok(auditIntegrity(state).issues.some(x=>x.code==='BOM_CONSUMPTION'));
});
test('detects wrong number of finished-goods receipts',()=>{
 const state=seedDemo();state.movements=state.movements.filter(x=>x.type!=='Üretim girişi'||x.runId!==state.runs[0].id);
 assert.ok(auditIntegrity(state).issues.some(x=>x.code==='RUN_OUTPUT'));
});
test('detects broken compensating ledger on reversal',()=>{
 let state=seedDemo();const id=state.runs[0].id;
 state=undo(state,id,'Hatalı üretim girişi','tester').state;
 state.movements.find(x=>x.runId===id&&x.type==='Geri alma düzeltmesi').delta+=1;
 assert.ok(auditIntegrity(state).issues.some(x=>x.code==='REVERSAL_LEDGER'));
});
test('reports blocked undo as a warning, not corrupted historical data',()=>{
 const state=seedDemo();state.stock[2]=0;
 const report=auditIntegrity(state);
 assert.equal(report.errors,0);
 assert.equal(report.warnings,1);
 assert.equal(report.issues[0].code,'UNDO_BLOCKED');
});
test('CSV is UTF-8 BOM, quote-safe and spreadsheet-injection safe',()=>{
 const csv=integrityCsv({issues:[{severity:'error',code:'ORDER_STATUS',reference:'=1+1',details:'test;"quoted"'}]});
 assert.ok(csv.startsWith('\ufeff'));
 assert.match(csv,/"'=1\+1"/);
 assert.match(csv,/"test;""quoted"""/);
});
