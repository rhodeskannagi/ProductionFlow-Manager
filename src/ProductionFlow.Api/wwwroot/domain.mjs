// Independent synthetic demo engine. No company data and no SQL connection.
const round = (n) => Math.round((Number(n) + Number.EPSILON) * 10000) / 10000;
export const makeId = () => globalThis.crypto?.randomUUID?.() ?? `run-${Date.now()}-${Math.random()}`;

export function seedDemo() {
  const products = [
    {id:1,sku:'YM-ALFA-120',name:'Alfa Yarı Mamul Panel',unit:'adet',kind:'finished'},
    {id:2,sku:'YM-BETA-090',name:'Beta Montaj Profili',unit:'adet',kind:'finished'},
    {id:3,sku:'YM-GAMA-200',name:'Gama Gövde Modülü',unit:'adet',kind:'finished'},
    {id:4,sku:'HM-ALU-6063',name:'Alüminyum Profil',unit:'kg',kind:'material'},
    {id:5,sku:'HM-BAG-001',name:'Bağlantı Elemanı',unit:'adet',kind:'material'},
    {id:6,sku:'HM-CONTA-02',name:'Sızdırmazlık Contası',unit:'m',kind:'material'},
    {id:7,sku:'HM-LEV-03',name:'Alüminyum Levha',unit:'m²',kind:'material'}
  ];
  const recipes = [
    {productId:1,materialId:4,perUnit:2.5},{productId:1,materialId:5,perUnit:4},
    {productId:2,materialId:4,perUnit:1.2},{productId:2,materialId:6,perUnit:0.7},
    {productId:3,materialId:7,perUnit:1.8},{productId:3,materialId:5,perUnit:8}
  ];
  const orders = [
    {id:101,code:'ÜE-260101',productId:1,planned:120,produced:0,status:'Başladı',createdAt:'2026-10-12T08:15:00Z'},
    {id:102,code:'ÜE-260102',productId:2,planned:80,produced:24,status:'Üretiliyor',createdAt:'2026-10-13T09:42:00Z'},
    {id:103,code:'ÜE-260103',productId:3,planned:50,produced:0,status:'Başladı',createdAt:'2026-10-14T07:30:00Z'},
    {id:104,code:'ÜE-260104',productId:1,planned:32,produced:32,status:'Kapandı',createdAt:'2026-10-14T12:00:00Z'},
    {id:105,code:'ÜE-260105',productId:2,planned:65,produced:0,status:'Başladı',createdAt:'2026-10-15T11:20:00Z'}
  ];
  // Existing runs are seeded with matching balances and ledger events.
  const runs = [
    {id:'f1111111-1111-4111-8111-111111111111',requestId:'a1111111-1111-4111-8111-111111111111',orderId:102,quantity:24,createdAt:'2026-10-15T10:12:00Z',reversedAt:null,reason:null,operator:'Demo Operatör'},
    {id:'f2222222-2222-4222-8222-222222222222',requestId:'a2222222-2222-4222-8222-222222222222',orderId:104,quantity:32,createdAt:'2026-10-15T13:45:00Z',reversedAt:null,reason:null,operator:'Demo Operatör'}
  ];
  const stock = {1:32,2:24,3:0,4:800,5:1500,6:220,7:210};
  const movements = [];
  for(const r of runs){
    const order = orders.find(o=>o.id===r.orderId);
    movements.push({id:`${r.id}-in`,runId:r.id,productId:order.productId,delta:r.quantity,type:'Üretim girişi',at:r.createdAt});
    for(const rec of recipes.filter(x=>x.productId===order.productId)){
      movements.push({id:`${r.id}-${rec.materialId}`,runId:r.id,productId:rec.materialId,delta:-round(r.quantity*rec.perUnit),type:'Hammadde çıkışı',at:r.createdAt});
    }
  }
  return {version:1,products,recipes,orders,runs,stock,movements,audit:[{id:'seed',at:'2026-10-15T13:45:00Z',action:'DEMO_SEED',details:'Tamamen örnek veriler yüklendi'}]};
}

export function statusFor(produced, planned) {
  return produced <= 0 ? 'Başladı' : produced >= planned ? 'Kapandı' : 'Üretiliyor';
}
export function overview(state) {
  const byId = Object.fromEntries(state.products.map(p=>[p.id,p]));
  const orders = state.orders.map(o=>({...o,productName:byId[o.productId].name,sku:byId[o.productId].sku}));
  const inventory = state.products.map(p=>({...p,onHand:round(state.stock[p.id]??0)}));
  const runs = [...state.runs].reverse().map(r=>({...r,orderCode:state.orders.find(o=>o.id===r.orderId)?.code??'?',productName:byId[state.orders.find(o=>o.id===r.orderId)?.productId]?.name??'?'}));
  return {orders,inventory,runs,stats:{orders:orders.length,active:orders.filter(o=>o.status!=='Kapandı').length,completed:orders.filter(o=>o.status==='Kapandı').length,produced:round(orders.reduce((s,o)=>s+o.produced,0)),reversedRuns:runs.filter(r=>r.reversedAt).length},audit:state.audit.slice().reverse()};
}
export function previewProduction(state, orderId, quantity) {
  const order = state.orders.find(o=>o.id===Number(orderId));
  if(!order) throw new Error('Üretim emri bulunamadı.');
  if(!Number.isFinite(Number(quantity)) || Number(quantity)<=0 || !Number.isInteger(Number(quantity))) throw new Error('Üretim miktarı pozitif tam sayı olmalı.');
  const qty=Number(quantity), remaining=round(order.planned-order.produced);
  const materials=state.recipes.filter(r=>r.productId===order.productId).map(r=>{
    const product=state.products.find(p=>p.id===r.materialId);
    const needed=round(qty*r.perUnit), available=round(state.stock[r.materialId]??0);
    return {productId:r.materialId,sku:product.sku,name:product.name,unit:product.unit,needed,available,enough:available>=needed};
  });
  const warnings=[];
  if(qty>remaining) warnings.push(`Kalan üretim miktarı ${remaining} adet.`);
  if(materials.length===0) warnings.push('Bu mamul için reçete tanımlanmamış.');
  if(materials.some(m=>!m.enough)) warnings.push('Bazı hammaddeler yetersiz.');
  return {orderId:order.id,orderCode:order.code,quantity:qty,remaining,materials,canProduce:warnings.length===0,warnings};
}
export function produce(state,orderId,quantity,operator='Demo Kullanıcı',requestId=makeId()){
  if(state.runs.some(r=>r.requestId===requestId)) throw new Error('Bu işlem anahtarı daha önce kullanılmış.');
  const check=previewProduction(state,orderId,quantity);
  if(!check.canProduce) throw new Error(check.warnings.join(' '));
  const next=structuredClone(state), order=next.orders.find(o=>o.id===Number(orderId));
  const now=new Date().toISOString(); const run={id:makeId(),requestId,orderId:order.id,quantity:check.quantity,createdAt:now,reversedAt:null,reason:null,operator};
  for(const mat of check.materials){next.stock[mat.productId]=round(next.stock[mat.productId]-mat.needed);next.movements.push({id:makeId(),runId:run.id,productId:mat.productId,delta:-mat.needed,type:'Hammadde çıkışı',at:now});}
  next.stock[order.productId]=round(next.stock[order.productId]+check.quantity);
  next.movements.push({id:makeId(),runId:run.id,productId:order.productId,delta:check.quantity,type:'Üretim girişi',at:now});
  order.produced=round(order.produced+check.quantity);order.status=statusFor(order.produced,order.planned);
  next.runs.push(run);next.audit.push({id:makeId(),at:now,action:'PRODUCE',details:`${order.code} · ${check.quantity} adet · ${operator}`});
  return {state:next,run};
}
export function previewUndo(state,runId){
  const run=state.runs.find(r=>r.id===runId);
  if(!run) throw new Error('Üretim kaydı bulunamadı.');
  const order=state.orders.find(o=>o.id===run.orderId),finished=round(state.stock[order.productId]??0);
  const warnings=[];
  if(run.reversedAt) warnings.push('Bu işlem zaten geri alınmış.');
  if(finished<run.quantity) warnings.push(`Mamül stoğu yetersiz: ${finished} adet var, ${run.quantity} adet gerekiyor.`);
  return {runId,orderId:order.id,orderCode:order.code,quantity:run.quantity,finishedAvailable:finished,canUndo:warnings.length===0,warnings};
}
export function undo(state,runId,reason,operator='Demo Kullanıcı'){
  if(typeof reason!=='string'||reason.trim().length<8) throw new Error('Geri alma nedeni en az 8 karakter olmalı.');
  const check=previewUndo(state,runId);
  if(!check.canUndo) throw new Error(check.warnings.join(' '));
  const next=structuredClone(state),run=next.runs.find(r=>r.id===runId),order=next.orders.find(o=>o.id===run.orderId);
  const now=new Date().toISOString();
  for(const m of next.movements.filter(m=>m.runId===runId).slice()){
    next.stock[m.productId]=round((next.stock[m.productId]??0)-m.delta);
    next.movements.push({id:makeId(),runId,productId:m.productId,delta:-m.delta,type:'Geri alma düzeltmesi',at:now});
  }
  order.produced=round(order.produced-run.quantity);order.status=statusFor(order.produced,order.planned);
  run.reversedAt=now;run.reason=reason.trim();run.reversedBy=operator;
  next.audit.push({id:makeId(),at:now,action:'UNDO',details:`${order.code} · ${run.quantity} adet · ${reason.trim()}`});
  return {state:next,run};
}


// Read-only operational diagnostics. Baseline stock is intentionally NOT reconstructed:
// incoming purchases/shipments are outside this intentionally small demo's ledger scope.
export function auditIntegrity(state) {
  const issues=[];
  const add=(severity,code,reference,details)=>issues.push({severity,code,reference,details});
  const eps=0.00001;
  for(const order of state.orders){
    const fromRuns=round(state.runs.filter(r=>r.orderId===order.id&&!r.reversedAt).reduce((n,r)=>n+r.quantity,0));
    if(Math.abs(fromRuns-order.produced)>eps) add('error','ORDER_QUANTITY',order.code,`Aktif üretim toplamı ${fromRuns}; emirde kayıtlı ${order.produced}.`);
    if(statusFor(order.produced,order.planned)!==order.status) add('error','ORDER_STATUS',order.code,`Beklenen durum ${statusFor(order.produced,order.planned)}, kayıtlı ${order.status}.`);
  }
  for(const p of state.products){
    if(!Number.isFinite(state.stock[p.id])||state.stock[p.id]<0) add('error','INVALID_STOCK',p.sku,'Stok bakiyesi negatif veya geçersiz.');
  }
  for(const run of state.runs){
    const order=state.orders.find(o=>o.id===run.orderId);
    if(!order){add('error','ORPHAN_RUN',run.id,'Üretim emri bulunamadı.');continue;}
    const original=state.movements.filter(m=>m.runId===run.id&&m.type!=='Geri alma düzeltmesi');
    const produced=original.filter(m=>m.type==='Üretim girişi'&&m.productId===order.productId);
    const total=round(produced.reduce((n,m)=>n+m.delta,0));
    if(produced.length!==1||Math.abs(total-run.quantity)>eps)
      add('error','RUN_OUTPUT',run.id,`Üretim girişi ${total} (${produced.length} hareket); beklenen ${run.quantity}.`);
    for(const rec of state.recipes.filter(x=>x.productId===order.productId)){
      const movements=original.filter(m=>m.productId===rec.materialId&&m.type==='Hammadde çıkışı');
      const actual=round(-movements.reduce((n,m)=>n+m.delta,0));
      const expected=round(run.quantity*rec.perUnit);
      if(Math.abs(actual-expected)>eps) add('error','BOM_CONSUMPTION',run.id,`Hammadde ${state.products.find(p=>p.id===rec.materialId)?.sku??rec.materialId}: tüketim ${actual}, reçete ${expected}.`);
    }
    const reversals=state.movements.filter(m=>m.runId===run.id&&m.type==='Geri alma düzeltmesi');
    const ids=new Set([...original,...reversals].map(m=>m.productId));
    for(const productId of ids){
      const orig=round(original.filter(m=>m.productId===productId).reduce((n,m)=>n+m.delta,0));
      const rev=round(reversals.filter(m=>m.productId===productId).reduce((n,m)=>n+m.delta,0));
      if((run.reversedAt&&Math.abs(orig+rev)>eps)||(!run.reversedAt&&Math.abs(rev)>eps))
        add('error','REVERSAL_LEDGER',run.id,`Stok ${productId}: orijinal net ${orig}, ters hareket ${rev}.`);
    }
    if(!run.reversedAt&&(state.stock[order.productId]??0)<run.quantity)
      add('warning','UNDO_BLOCKED',run.id,`Mevcut mamul stoğu ${state.stock[order.productId]??0}; geri almak için ${run.quantity} gerekli.`);
  }
  issues.sort((a,b)=>a.severity.localeCompare(b.severity)||a.code.localeCompare(b.code)||a.reference.localeCompare(b.reference));
  return {checkedAt:new Date().toISOString(),checkedOrders:state.orders.length,checkedRuns:state.runs.length,issues,errors:issues.filter(x=>x.severity==='error').length,warnings:issues.filter(x=>x.severity==='warning').length};
}

export function integrityCsv(report){
  const cell=(v)=>{let s=String(v??'');if(/^[\s]*[=+@\-]/.test(s)) s="'"+s;return '"'+s.replaceAll('"','""')+'"';};
  const rows=[['Seviye','Kontrol','Referans','Detay'],...report.issues.map(x=>[x.severity,x.code,x.reference,x.details])];
  return '\ufeff'+rows.map(x=>x.map(cell).join(';')).join('\r\n')+'\r\n';
}
