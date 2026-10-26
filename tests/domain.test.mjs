import test from 'node:test';
import assert from 'node:assert/strict';
import {seedDemo,overview,previewProduction,produce,previewUndo,undo} from '../src/ProductionFlow.Api/wwwroot/domain.mjs';

test('demo shows synthetic orders, balances and an existing partial run',()=>{
 const s=seedDemo(), view=overview(s);assert.equal(view.orders.length,5);assert.equal(view.stats.produced,56);
});
test('production uses recipe, updates inventory, status and audit atomically',()=>{
 const s=seedDemo(), before=s.stock[4]; const {state,run}=produce(s,101,10,'tester','req-1');
 assert.equal(s.orders[0].produced,0); assert.equal(state.orders[0].produced,10);
 assert.equal(state.stock[4],before-25);assert.equal(state.stock[5],1460);
 assert.equal(state.orders[0].status,'Üretiliyor');assert.equal(run.quantity,10);
});
test('cannot overproduce or use unavailable material',()=>{
 const s=seedDemo();assert.equal(previewProduction(s,101,121).canProduce,false);
 assert.equal(previewProduction(s,101,120).canProduce,true);
 assert.equal(previewProduction(s,103,50).canProduce,true);
 assert.equal(previewProduction(s,103,51).canProduce,false);
 assert.throws(()=>produce(s,101,121),/Kalan/);
});
test('production undo fully restores quantities, preserves audit and prevents repeat',()=>{
 const s=seedDemo();const {state:after,run}=produce(s,101,10,'tester','req-2');
 assert.equal(previewUndo(after,run.id).canUndo,true);
 const {state:reverted}=undo(after,run.id,'Hatalı miktar','tester');
 assert.equal(reverted.stock[4],s.stock[4]);assert.equal(reverted.stock[1],s.stock[1]);
 assert.equal(reverted.orders[0].produced,0);assert.equal(reverted.orders[0].status,'Başladı');
 assert.equal(reverted.audit.length,s.audit.length+2);assert.equal(reverted.movements.length,s.movements.length+6);
 assert.equal(previewUndo(reverted,run.id).canUndo,false);assert.throws(()=>undo(reverted,run.id,'Tekrar geri alma'),/zaten/);
});
test('cannot undo when finished products are already consumed',()=>{
 const s=seedDemo();s.stock[2]=0;
 assert.equal(previewUndo(s,s.runs[0].id).canUndo,false);
 assert.throws(()=>undo(s,s.runs[0].id,'Stok yetersiz'),/yetersiz/);
});
test('duplicate idempotency key is rejected',()=>{
 const s=seedDemo();assert.throws(()=>produce(s,101,1,'tester',s.runs[0].requestId),/daha önce/);
});
test('invalid input fails without modifying state',()=>{
 const s=seedDemo(),copy=JSON.stringify(s);assert.throws(()=>produce(s,101,-5),/pozitif/);assert.equal(JSON.stringify(s),copy);
});
