"use strict";

const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");
const {webcrypto}=require("node:crypto");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web","wwwroot");
const batchSource=fs.readFileSync(path.join(webRoot,"studio-batch.js"),"utf8");

class TestHeaders{
  constructor(){this.values=new Map()}
  set(key,value){this.values.set(String(key).toLowerCase(),String(value))}
  get(key){return this.values.get(String(key).toLowerCase())||null}
  has(key){return this.values.has(String(key).toLowerCase())}
}

class TestElement{
  constructor(){
    this.value="";
    this.disabled=false;
    this.hidden=false;
    this.open=true;
    this.innerHTML="";
    this.textContent="";
    this.dataset={};
    this.style={setProperty(){}};
    this.classList={add(){},remove(){},toggle(){}};
  }
  addEventListener(){}
  close(){this.open=false}
  showModal(){this.open=true}
  focus(){}
  querySelector(){return null}
}

function response(payload,status=200){
  return {ok:status>=200&&status<300,status,text:async()=>JSON.stringify(payload)};
}

function makeHarness(options={}){
  const elements=new Map();
  const notices=[];
  const requests=[];
  const localStorageValues=new Map();
  let initialReportResolve=null;
  let holdInitialReport=options.holdInitialReport??true;
  let startCalls=0;

  const getElement=id=>{
    if(!elements.has(id))elements.set(id,new TestElement());
    return elements.get(id);
  };

  const report={items:[{item_id:"item-1",status:"queued",review_status:"unreviewed"}],counts:{total:1,queued:1,running:0,review_pending:0,ready_to_create:0,created:0,failed:0,unknown_result:0}};
  const manifest={batch_id:"batch-1",item_ids:["item-1"],revision:1,claim_generation:1,status:"planned"};

  const context={
    console,
    crypto:webcrypto,
    Headers:TestHeaders,
    TextEncoder,
    AbortController,
    setInterval,
    clearInterval,
    state:{aiCsrf:options.csrfToken||"",dirty:false},
    window:null,
    document:{
      activeElement:getElement("batchWorkbenchButton"),
      getElementById:getElement,
      querySelectorAll(selector){
        if(selector===".batch-bulk-actions")return[getElement("batchBulkActions")];
        return[];
      },
      querySelector(selector){
        if(selector===".batch-bulk-actions")return getElement("batchBulkActions");
        return null;
      }
    },
    localStorage:{
      getItem:key=>localStorageValues.get(key)||null,
      setItem:(key,value)=>localStorageValues.set(key,String(value)),
      removeItem:key=>localStorageValues.delete(key)
    },
    confirm(){return true},
    toast(message){notices.push(String(message))},
    ensureAiSession:async()=>{},
    refreshAiSession:async()=>{
      refreshCalls+=1;
      context.state.aiCsrf="fresh-token";
    },
    fetch(url,init={}){
      const request={url:String(url)};
      requests.push(request);
      if(request.url.endsWith("/report")&&holdInitialReport){
        return new Promise(resolve=>{initialReportResolve=()=>resolve(response({data:report}))});
      }
      if(request.url.endsWith("/start")){
        startCalls+=1;
        if(options.rejectStaleSession&&init.headers?.get?.("X-AWAKE-CSRF")==="stale-token"){
          return Promise.resolve(response({error:"WB-BATCH-CONSENT-403",message:"当前操作没有有效授权。"},403));
        }
        return Promise.resolve(response({data:{status:"running"}}));
      }
      if(request.url.endsWith("/batch/create"))return Promise.resolve(response({data:manifest}));
      if(request.url.endsWith("/providers"))return Promise.resolve(response({providers:[]}));
      if(request.url.includes("/report"))return Promise.resolve(response({data:report}));
      if(request.url.includes("/items/"))return Promise.resolve(response({data:{item:{item_id:"item-1",status:"facts_review"},facts:[{fact_id:"fact-1",text:"事实内容"}],evidence:[]}}));
      if(request.url.includes("/batch/"))return Promise.resolve(response({data:manifest}));
      return Promise.resolve(response({}));
    }
  };
  context.window=context;
  let refreshCalls=0;

  const instrumented=batchSource.replace("  bind();","  globalThis.__batchTest={batch,renderAll,refresh,start,createBatch,eligible,actions};\n  bind();");
  vm.runInNewContext(instrumented,context,{filename:"studio-batch.js"});

  return {
    api:context.__batchTest,
    getElement,
    notices,
    requests,
    manifest,
    report,
    state:context.state,
    get refreshCalls(){return refreshCalls},
    get startCalls(){return startCalls},
    async releaseInitialReport(){
      holdInitialReport=false;
      assert.ok(initialReportResolve,"the initial report request should be pending");
      initialReportResolve();
      await new Promise(resolve=>setImmediate(resolve));
    }
  };
}

async function run(name,action){
  await action();
  process.stdout.write("PASS "+name+"\n");
}

async function waitForReportRequest(harness){
  const deadline=Date.now()+5000;
  while(Date.now()<deadline){
    if(harness.requests.some(request=>request.url.endsWith("/report")))return;
    await new Promise(resolve=>setImmediate(resolve));
  }
  assert.fail("the batch report request should be issued");
}

(async()=>{
  await run("creating a batch keeps the next action gated while its report is loading",async()=>{
    const harness=makeHarness();
    const {api}=harness;
    api.batch.scan={scan_id:"scan-1",scan_hash:"hash-1"};
    const operation=api.createBatch();
    await waitForReportRequest(harness);
    assert.equal(api.batch.reportLoading,true);
    assert.equal(harness.getElement("batchCreateButton").disabled,true);
    assert.match(harness.getElement("batchCreateButton").textContent,/读取项目状态/);
    await harness.releaseInitialReport();
    await operation;
    assert.equal(api.batch.reportLoading,false);
    assert.ok(api.batch.report);
  });

  await run("batch action button stays disabled until report is ready",async()=>{
    const harness=makeHarness();
    const {api}=harness;
    api.batch.manifest=harness.manifest;
    api.batch.report=null;
    api.batch.reportLoading=true;
    api.renderAll();
    assert.equal(harness.getElement("batchCreateButton").disabled,true);
    assert.match(harness.getElement("batchCreateButton").textContent,/读取项目状态/);
  });

  await run("starting facts waits for the in-flight report instead of claiming the queue is empty",async()=>{
    const harness=makeHarness();
    const {api}=harness;
    api.batch.manifest=harness.manifest;
    api.batch.report=null;
    api.batch.consentToken="consent-token";
    api.batch.consent={revision:1};
    const refreshOperation=api.refresh({silent:true});
    await new Promise(resolve=>setImmediate(resolve));
    const startOperation=api.start("facts");
    await new Promise(resolve=>setImmediate(resolve));
    assert.equal(harness.startCalls,0);
    assert.equal(harness.notices.some(message=>message.includes("没有等待提取事实")),false);
    await harness.releaseInitialReport();
    await Promise.all([refreshOperation,startOperation]);
    assert.equal(harness.startCalls,1);
  });

  await run("queued-only refresh does not select a project or show a false detail loader",async()=>{
    const harness=makeHarness({holdInitialReport:false});
    const {api}=harness;
    api.batch.manifest=harness.manifest;
    await api.refresh({silent:true});
    assert.equal(api.batch.selectedItemId,"");
    assert.equal(api.batch.details.has("item-1"),false);
    assert.equal(harness.requests.some(request=>request.url.includes("/items/item-1")),false);
    assert.doesNotMatch(harness.getElement("batchReviewContent").innerHTML,/正在读取项目详情/);
    assert.match(harness.getElement("batchReviewContent").innerHTML,/当前没有待审核项目|选择一个项目/);
  });

  await run("a queued project without an active detail request shows its waiting status",async()=>{
    const harness=makeHarness({holdInitialReport:false});
    const {api}=harness;
    api.batch.manifest=harness.manifest;
    api.batch.report=harness.report;
    api.batch.selectedItemId="item-1";
    api.renderAll();
    assert.doesNotMatch(harness.getElement("batchReviewContent").innerHTML,/正在读取项目详情/);
    assert.match(harness.getElement("batchReviewContent").innerHTML,/等待提取/);
  });

  await run("no-candidate batch items expose an explicit terminal status",async()=>{
    const harness=makeHarness({holdInitialReport:false});
    harness.report.items[0].status="no_candidate";
    harness.report.items[0].review_status="no_candidate";
    harness.report.counts.queued=0;
    harness.report.counts.no_candidate=1;
    harness.api.batch.manifest=harness.manifest;
    harness.api.batch.report=harness.report;
    harness.api.batch.selectedItemId="item-1";
    harness.api.renderAll();
    assert.match(harness.getElement("batchReviewContent").innerHTML,/没有形成事实候选/);
  });

  await run("restored batch without scan still exposes extraction actions",async()=>{
    const harness=makeHarness({holdInitialReport:false});
    const {api}=harness;
    api.batch.manifest=harness.manifest;
    api.batch.report=harness.report;
    api.batch.scan=null;
    api.batch.consentToken="";
    api.actions();
    const html=harness.getElement("batchActionContent").innerHTML;
    assert.doesNotMatch(html,/先导入参考资料/);
    assert.match(html,/提取客观事实/);
    assert.match(html,/重新确认读取范围/);
  });

  await run("facts review refresh loads the selected item detail",async()=>{
    const harness=makeHarness({holdInitialReport:false});
    const {api}=harness;
    harness.report.items[0].status="facts_review";
    harness.report.counts.queued=0;
    harness.report.counts.review_pending=1;
    api.batch.manifest=harness.manifest;
    await api.refresh({silent:true});
    assert.equal(api.batch.selectedItemId,"item-1");
    assert.equal(api.batch.details.has("item-1"),true);
    assert.equal(harness.requests.some(request=>request.url.includes("/items/item-1")),true);
  });

  await run("a stale AI session is renewed before retrying batch start",async()=>{
    const harness=makeHarness({csrfToken:"stale-token",rejectStaleSession:true,holdInitialReport:false});
    const {api}=harness;
    api.batch.manifest=harness.manifest;
    api.batch.report=harness.report;
    api.batch.consentToken="consent-token";
    api.batch.consent={revision:1};
    await api.start("facts");
    assert.equal(harness.refreshCalls,1);
    assert.equal(harness.state.aiCsrf,"fresh-token");
    assert.equal(harness.startCalls,2);
  });
})();
