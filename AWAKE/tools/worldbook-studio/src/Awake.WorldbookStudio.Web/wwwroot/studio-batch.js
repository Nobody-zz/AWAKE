"use strict";
(function(){
  const storageKey="awake.worldbook.studio.batch.v1.state";
  const layoutKey="awake.worldbook.studio.batch.v1.layout";
  const allowedExtensions=new Set([".txt",".md",".markdown",".yaml",".yml",".json"]);
  const batch={dialog:null,files:[],scan:null,localScan:null,manifest:null,report:null,reportLoading:false,reportError:null,reportRequest:null,providers:[],consent:null,consentToken:"",selectedItemId:"",selectedQueueIds:new Set(),details:new Map(),detailLoading:new Set(),detailErrors:new Map(),metadataApprovals:new Set(),pollTimer:null,pollBusy:false,startBusy:false,writeBusy:false,restoreAttempted:false,returnFocus:null,leftWidth:286,rightWidth:356,view:"upload",savedBatchId:""};
  const el=id=>document.getElementById(id);
  const pick=(object,...keys)=>{if(!object)return null;for(const key of keys)if(object[key]!==undefined&&object[key]!==null)return object[key];return null};
  const arr=value=>Array.isArray(value)?value:[];
  const str=(value,fallback="")=>value===null||value===undefined?fallback:String(value);
  const num=(value,fallback=0)=>Number.isFinite(Number(value))?Number(value):fallback;
  const data=response=>pick(response,"data")||{};
  const esc=value=>typeof h==="function"?h(value):String(value??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
  const json=value=>{try{return JSON.parse(value)}catch{return null}};
  const ext=name=>{const value=str(name).toLowerCase(),index=value.lastIndexOf(".");return index<0?"":value.slice(index)};
  const bytes=value=>value<1024?`${value} B`:value<1048576?`${(value/1024).toFixed(1)} KiB`:`${(value/1048576).toFixed(1)} MiB`;
  async function readLocalScanStats(){
    try{
      const stats={files:batch.files.length,bytes:batch.files.reduce((total,file)=>total+file.size,0),characters:0,paragraphs:0};
      for(const file of batch.files){
        const text=(await file.text()).replace(/^\uFEFF/,"").replace(/\r\n/g,"\n").replace(/\r/g,"\n").normalize("NFC");
        stats.characters+=text.length;
        let cursor=0;
        while(cursor<text.length){
          while(cursor<text.length&&/\s/.test(text[cursor]))cursor++;
          if(cursor>=text.length)break;
          const separator=text.indexOf("\n\n",cursor);
          stats.paragraphs++;
          cursor=separator<0?text.length:separator+2;
        }
      }
      return stats;
    }catch{
      return null;
    }
  }
  const bounded=(value,fallback,min,max)=>Number.isFinite(Number(value))?Math.max(min,Math.min(max,Number(value))):fallback;
  const sha256Hex=async value=>{const digest=await crypto.subtle.digest("SHA-256",new TextEncoder().encode(String(value)));return Array.from(new Uint8Array(digest),byte=>byte.toString(16).padStart(2,"0")).join("")};
  const batchId=()=>str(pick(batch.manifest,"batch_id","batchId"));
  const itemIds=()=>arr(pick(batch.manifest,"item_ids","itemIds")).map(str).filter(Boolean);
  const reportItems=()=>arr(pick(batch.report,"items")).map(item=>({...item,item_id:str(pick(item,"item_id","itemId"))}));
  const reportItem=id=>reportItems().find(item=>item.item_id===id)||{item_id:id,status:"queued",review_status:"unreviewed"};
  const counts=()=>{const raw=pick(batch.report,"counts")||{};return{total:num(raw.total,itemIds().length),queued:num(raw.queued),running:num(raw.running),review_pending:num(raw.review_pending),ready_to_create:num(raw.ready_to_create),created:num(raw.created),failed:num(raw.failed),unknown_result:num(raw.unknown_result),no_candidate:num(raw.no_candidate)}};
  const statusText=status=>({planned:"等待开始",queued:"等待提取",loading:"正在读取项目状态",extracting:"正在提取事实",facts_review:"等待事实审核",metadata_pending:"等待生成档案信息",metadata_running:"正在生成档案信息",ready_to_create:"等待生成草稿",created:"已生成待审核草稿",no_candidate:"没有形成事实候选",failed:"处理失败",unknown_result:"结果待确认",paused:"已暂停",cancelled:"已取消",completed:"批次完成",running:"正在处理",skipped:"已跳过"}[status]||"待查看");
  const statusClass=status=>status==="failed"||status==="unknown_result"?"bad":status==="created"||status==="ready_to_create"?"good":status==="running"||status==="extracting"||status==="metadata_running"?"active":"";
  const providerName=id=>id==="cloud"?"云端 AI Provider":"本机 AI Worker";
  const failureStatuses=new Set(["failed","unknown_result"]);
  function failureInfo(item){
    const status=str(pick(item,"status"));
    const code=str(pick(item,"error_code","last_error_code","lastErrorCode")).trim();
    const reason=str(pick(item,"status_reason","statusReason")).trim();
    const summary=str(pick(item,"message","last_error_message","lastErrorMessage","last_error","lastError"),"").trim()||({network_unknown:"网络请求中断，结果暂时无法确认。",provider_transient_error:"Provider 暂时没有完成处理，请确认后重试。",provider_permanent_error:"Provider 拒绝了本次请求，请检查错误分类和配置。",metadata_provider_transient_error:"档案信息生成暂时失败，请确认后重试。",metadata_provider_permanent_error:"档案信息生成被 Provider 拒绝，请检查错误摘要。"}[reason]||"服务端没有提供更详细的错误摘要。");
    const value=`${code} ${reason} ${summary}`.toUpperCase();
    let category=status==="unknown_result"?"结果待确认":"未分类错误";
    if(value.includes("AUTH")||value.includes("CREDENTIAL")||value.includes("SECRET"))category="认证与凭据";
    else if(value.includes("MODEL"))category="模型配置";
    else if(value.includes("PARAMETER")||value.includes("HTTP-400")||value.includes("HTTP-422")||value.includes("REQUEST-4"))category="请求参数";
    else if(value.includes("RATE")||value.includes("429"))category="Provider 限流";
    else if(value.includes("TIMEOUT")||value.includes("408"))category="请求超时";
    else if(value.includes("TRANSPORT")||value.includes("NETWORK")||value.includes("502"))category="网络连接";
    else if(value.includes("UPSTREAM")||value.includes("5XX")||value.includes("500"))category="Provider 服务";
    else if(value.includes("FORMAT")||value.includes("OUTPUT")||value.includes("RESPONSE"))category="返回格式";
    else if(value.includes("BIND")||value.includes("STALE")||value.includes("REVISION"))category="批次状态";
    else if(value.includes("CANCEL"))category="操作取消";
    return{status,code,reason,summary,category};
  }
  function failureItems(stage="facts"){
    return reportItems().filter(item=>{
      if(!failureStatuses.has(str(item.status)))return false;
      const detectedStage=failedStage(item);
      return detectedStage===stage||(stage==="facts"&&str(item.status)==="unknown_result"&&!detectedStage);
    });
  }
  function failureOverview(items,title="事实提取需要人工处理"){
    const failedCount=items.filter(item=>str(item.status)==="failed").length;
    const unknownCount=items.filter(item=>str(item.status)==="unknown_result").length;
    const first=items[0];
    const rows=items.slice(0,8).map((item,index)=>{
      const info=failureInfo(item);
      const itemId=str(pick(item,"item_id","itemId"));
      const titleText=itemTitle(itemId,itemIds().indexOf(itemId));
      return`<div class="batch-failure-row"><div class="batch-failure-row-head"><strong>${esc(titleText||`项目 ${index+1}`)}</strong><span class="batch-status bad">${esc(info.category)}</span></div><p>${esc(info.summary)}</p>${info.code?`<small class="batch-error-code">错误码：${esc(info.code)}</small>`:""}</div>`;
    }).join("");
    return`<div class="batch-card batch-error-card batch-failure-overview"><div class="batch-failure-overview-head"><strong>${esc(title)}</strong><span class="batch-failure-counts"><span>失败 ${failedCount}</span><span>待确认 ${unknownCount}</span></span></div><p>这些项目没有可直接进入下一步的结果。请先查看错误摘要，再由你确认是否重试；不会自动覆盖原始资料。</p><div class="batch-failure-list">${rows}</div>${first?`<div class="batch-failure-actions"><button class="primary" type="button" data-batch-action="open-item" data-item-id="${esc(str(pick(first,"item_id","itemId")))}">查看第一个失败项目</button><button class="ghost" type="button" data-batch-action="retry-item">人工确认后重试当前项目</button></div>`:""}</div>`;
  }
  let lastAutoSelectedFailureKey="";
  function syncFailureSelection(){
    const items=failureItems("facts"),ids=items.map(item=>str(pick(item,"item_id","itemId"))).filter(Boolean);
    if(!ids.length){lastAutoSelectedFailureKey="";return;}
    const key=`${batchId()}|${ids.join("|")}`;
    if(lastAutoSelectedFailureKey===key)return;
    if(batch.selectedQueueIds.size===0||Array.from(batch.selectedQueueIds).every(id=>ids.includes(id))){
      batch.selectedQueueIds=new Set(ids);
      if(!batch.selectedItemId||!ids.includes(batch.selectedItemId))batch.selectedItemId=ids[0];
      lastAutoSelectedFailureKey=key;
    }
  }

  function readStorage(){
    try{
      const saved=json(localStorage.getItem(storageKey)||""),layout=json(localStorage.getItem(layoutKey)||"");
      if(saved&&typeof saved.batchId==="string"&&/^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$/.test(saved.batchId)&&Date.now()-num(saved.savedAt)>0&&Date.now()-num(saved.savedAt)<2592000000)batch.savedBatchId=saved.batchId;
      else if(saved)localStorage.removeItem(storageKey);
      if(layout){batch.leftWidth=bounded(layout.left,286,230,430);batch.rightWidth=bounded(layout.right,356,310,480)}
    }catch{}
  }
  function remember(){if(!batchId())return;try{localStorage.setItem(storageKey,JSON.stringify({batchId:batchId(),savedAt:Date.now()}))}catch{}}
  function forget(){try{localStorage.removeItem(storageKey)}catch{}batch.savedBatchId=""}
  function setLayout(){if(!batch.dialog)return;batch.dialog.style.setProperty("--batch-queue-width",`${batch.leftWidth}px`);batch.dialog.style.setProperty("--batch-inspector-width",`${batch.rightWidth}px`);try{localStorage.setItem(layoutKey,JSON.stringify({left:batch.leftWidth,right:batch.rightWidth}))}catch{}}
  function notice(message,kind="info"){const node=el("batchNotice");if(!node)return;node.textContent=message;node.className=`batch-notice ${kind}`;node.hidden=!message}

  async function renewAiSession(){
    state.aiCsrf="";
    if(typeof refreshAiSession==="function")return refreshAiSession();
    if(typeof ensureAiSession==="function")return ensureAiSession();
    throw new Error("WB-AI-CSRF-403: 本机安全会话无法刷新。");
  }
  async function request(url,init={},retrySession=true){
    const headers=new Headers(init.headers||{});if(state.aiCsrf)headers.set("X-AWAKE-CSRF",state.aiCsrf);if(init.body&&!((typeof FormData!=="undefined")&&init.body instanceof FormData)&&!headers.has("Content-Type"))headers.set("Content-Type","application/json");
    const response=await fetch(url,{...init,headers}),raw=await response.text();let payload={};try{payload=raw?JSON.parse(raw):{}}catch{payload={message:raw}}
    if(!response.ok){
      const error=new Error(str(pick(payload,"message","error"),`请求失败（${response.status}）`));
      error.code=str(pick(payload,"error","code"),"WB-BATCH-HTTP-ERROR");error.status=response.status;
      const sessionFailure=["WB-AI-CSRF-403","WB-BATCH-CONSENT-403"].includes(error.code);
      if(retrySession&&sessionFailure&&!url.endsWith("/api/ai/session/bootstrap")){
        notice("本机安全会话已刷新，正在重试当前操作…","info");
        await renewAiSession();
        return request(url,init,false);
      }
      throw error;
    }
    return payload;
  }
  async function session(){if(typeof ensureAiSession==="function"){await ensureAiSession();return}if(state.aiCsrf)return;const result=await request("/api/ai/session/bootstrap",{method:"POST"});state.aiCsrf=str(pick(result,"csrfToken","csrf_token"))}
  function error(error){const messages={"WB-BATCH-UPLOAD-400":"请至少选择一个、最多选择 200 个参考文件。","WB-BATCH-UPLOAD-413":"参考资料超过大小限制，请拆分资料后重试。","WB-BATCH-UPLOAD-422":"参考资料格式或编码不受支持，请使用 UTF-8 的 TXT、Markdown、YAML 或 JSON。","WB-BATCH-REVISION-409":"批次刚刚发生了变化，已停止当前操作。请刷新后再试。","WB-BATCH-CONSENT-404":"读取授权已失效，请重新确认资料读取范围。","WB-BATCH-CONSENT-409":"读取授权已过期或已使用，请重新确认资料读取范围。","WB-BATCH-CONSENT-422":"读取授权不完整，请至少选择一个资料项目。","WB-BATCH-PROVIDER-422":"所选 AI Provider 尚未配置好，请先检查设置。","WB-BATCH-PROVIDER-500":"AI 处理没有完成，请检查 Provider 后重试。","WB-BATCH-METADATA-500":"档案信息生成失败，请重新授权后重试。","WB-BATCH-EVIDENCE-422":"事实证据与参考资料不一致，请回到事实审核。","WB-BATCH-STAGE-409":"当前项目还没有到达这一步，请刷新查看最新状态。","WB-BATCH-CONSENT-403":"本机安全会话刷新失败，请确认只打开了一个新版工作室窗口后点击刷新。","WB-BATCH-OWNER-403":"这个批次正在由另一个工作室窗口管理，请关闭旧窗口后点击“接管当前批次”。"};const message=messages[str(error?.code)]||str(error?.message,"批量操作没有完成，请刷新后重试。");notice(`${message}${error?.code?`（${error.code}）`:""}`,"error");if(typeof toast==="function")toast(message,"error")}
  function view(name){batch.view=name;document.querySelectorAll("[data-batch-view]").forEach(node=>node.hidden=node.dataset.batchView!==name);const hint=el("batchStageHint");if(hint)hint.textContent=name==="upload"?"先导入资料，AI 只会提取候选事实，不会直接发布正典。":name==="consent"?"确认 AI 读取范围后，才会开始批量处理。":"先检查证据，再决定是否接受事实和生成待审核草稿。";actions()}
  function providerParameters(){return{model:el("batchModelInput")?.value.trim()||"worldbook-batch-v1",temperature:Math.max(0,Math.min(2,Number(el("batchTemperatureInput")?.value||0.2))),max_output_tokens:Math.max(1,Math.min(32768,Math.floor(Number(el("batchMaxTokensInput")?.value||3000)))),reasoning_effort:"low"}}
  function providerId(){return el("batchProviderSelect")?.value||str(pick(pick(batch.manifest,"provider_selection"),"provider_id","providerId"),"local")}
  function localProviderGuidance(){return"本机 AI 需要随包之外的 Worker 服务：请确认 Worker 已在本机启动，且地址与密钥已由提供方在启动 Studio 之前配置好（当前界面没有填写本机 Worker 地址的入口）。拿不到 Worker 时，可以改用云端 Provider，或不使用 AI——档案编辑、校验、预览和导出仍然可用。详见随包 README_使用说明.txt 与 docs 目录下的本地 Worker 配置说明。"}
  function renderProviders(){const selected=providerId(),status=batch.providers.find(item=>str(pick(item,"providerId","provider_id"))===selected),node=el("batchProviderStatus"),hint=el("batchProviderHint");if(node)node.textContent=status?`${providerName(selected)}：${str(pick(status,"stateLabel","state_label","state"),"未知")}`:`${providerName(selected)}：尚未读取状态`;if(hint){const configured=str(pick(status,"state"))==="configured";hint.textContent=configured?`${providerName(selected)} 已配置。AI 只读取本批次勾选的参考资料。`:`${providerName(selected)} 尚未配置或不可用。`+(selected==="local"?localProviderGuidance():"");hint.className=`batch-help ${configured?"good":"warn"}`}}
  async function loadProviders(){try{const result=await request("/api/ai/providers");batch.providers=arr(pick(result,"providers"));renderProviders()}catch(errorValue){error(errorValue)}}
  function renderFiles(){const list=el("batchFileList"),button=el("batchScanButton");if(!list||!button)return;if(!batch.files.length){list.innerHTML="<div class=\"batch-empty\">还没有选择资料。</div>";button.disabled=true;return}const total=batch.files.reduce((sum,file)=>sum+file.size,0),invalid=batch.files.some(file=>file.size>16777216||!allowedExtensions.has(ext(file.name)));list.innerHTML=`<div class="batch-file-summary"><strong>${batch.files.length} 份资料，${bytes(total)}</strong><span>${invalid||total>67108864?"有资料需要处理后才能扫描。":"资料格式和大小检查通过。支持：TXT、Markdown、YAML、JSON。"}</span></div>${batch.files.map((file,index)=>`<div class="batch-file-row ${file.size>16777216||!allowedExtensions.has(ext(file.name))?"invalid":""}"><span>${esc(file.name)}</span><small>${bytes(file.size)} · ${esc(ext(file.name)||"无扩展名")}</small><button type="button" class="mini ghost" data-batch-file-remove="${index}">移除</button></div>`).join("")}`;button.disabled=batch.files.length>200||total>67108864||invalid}
  function addFiles(files){const merged=[...batch.files,...Array.from(files||[])],seen=new Set();batch.files=merged.filter(file=>{const key=`${file.name}\u0000${file.size}\u0000${file.lastModified}`;if(seen.has(key))return false;seen.add(key);return true}).slice(0,200);renderFiles()}
  function itemTitle(itemId,index){const detail=batch.details.get(itemId),source=arr(pick(detail,"source_units","sourceUnits"))[0],heading=arr(pick(source,"heading_path","headingPath")).filter(Boolean).join(" / ");return heading||`资料片段 ${index+1}`}
  function renderProgress(){const text=el("batchProgressText"),bar=el("batchProgressBar");if(batch.manifest&&!batch.report){if(text)text.textContent=batch.reportLoading?"正在读取项目状态…":"等待读取项目状态";if(bar)bar.style.width="0%";return}const c=counts(),done=c.total-c.queued-c.running;if(text)text.textContent=`${Math.max(0,done)} / ${c.total}`;if(bar)bar.style.width=`${c.total?Math.max(0,Math.min(100,done/c.total*100)):0}%`}
  function renderQueue(){
    const list=el("batchQueueList"),summary=el("batchQueueSummary");
    if(!list||!summary)return;
    if(!batch.manifest){
      const snapshots=arr(pick(batch.scan,"snapshot_ids","snapshotIds")),scanWarnings=arr(pick(batch.scan,"warnings"));
      summary.textContent=snapshots.length?`${snapshots.length} 个资料快照已扫描`:"还没有批量任务";
      list.innerHTML=snapshots.length?snapshots.map((_,index)=>`<div class="batch-queue-item"><span class="batch-queue-index">${index+1}</span><div><strong>资料快照 ${index+1}</strong><small>扫描结果已暂存</small></div></div>`).join("")+`<div class="batch-help"><strong>扫描摘要</strong><br>服务端已整理 ${snapshots.length} 份资料快照。段落和字符数会在建立批次后按项目查看。${scanWarnings.length?`<br>提示：${esc(scanWarnings.join("；"))}`:""}</div>`:"<div class=\"batch-empty\">先在右侧选择参考资料。</div>";
      return;
    }
    syncFailureSelection();
    const ids=itemIds(),c=counts(),loading=!batch.report;
    summary.textContent=loading?`${c.total} 个项目 · 正在读取状态`:`${c.total} 个项目 · 待审核 ${c.review_pending} · 失败 ${c.failed} · 待确认 ${c.unknown_result}`;
    list.innerHTML=ids.map((id,index)=>{
      const item=reportItem(id),displayStatus=loading?"loading":str(item.status),selected=batch.selectedItemId===id?" selected":"",checked=batch.selectedQueueIds.has(id)?" checked":"",info=!loading&&failureStatuses.has(displayStatus)?failureInfo(item):null;
      const queueHint=info?`${info.category} · ${info.summary}`:statusText(displayStatus);
      return`<div class="batch-queue-item${selected}"><input type="checkbox" data-batch-queue-check="${esc(id)}"${checked} aria-label="选择${esc(itemTitle(id,index))}"><button type="button" class="batch-queue-open" data-batch-action="open-item" data-item-id="${esc(id)}"><span class="batch-queue-index">${index+1}</span><span><strong>${esc(itemTitle(id,index))}</strong><small>${esc(queueHint)}</small></span><span class="batch-status ${statusClass(displayStatus)}">${esc(statusText(displayStatus))}</span></button></div>`;
    }).join("");
  }
  function renderScanSummary(){
    if(batch.manifest||!batch.scan)return;
    const summary=el("batchQueueList")?.querySelector(".batch-help");
    if(!summary)return;
    const local=batch.localScan||{};
    const warnings=arr(pick(batch.scan,"warnings"));
    const localText=Object.keys(local).length?`本地预览：${num(local.files,batch.files.length)} 份文件 · ${bytes(num(local.bytes,batch.files.reduce((total,file)=>total+file.size,0)))} · 约 ${num(local.paragraphs)} 个段落 · ${num(local.characters)} 个字符。<br><small>段落和字符数是上传前的本地预览统计，最终以服务端建立批次后的项目数为准。</small>`:"本地预览统计暂不可用，不影响服务器扫描结果。";
    summary.innerHTML=`<strong>扫描摘要</strong><br>服务端已整理 ${arr(pick(batch.scan,"snapshot_ids","snapshotIds")).length} 份资料快照。<br>${localText}${warnings.length?`<br>提示：${esc(warnings.join("；"))}`:""}`;
  }

  function renderConsent(){
    const list=el("batchConsentItems");
    if(!list)return;
    const ids=itemIds();
    const selectionKey=ids.join("|");
    const currentSelectionKey=list.dataset.batchConsentSelectionKey||"";
    const selectedIds=currentSelectionKey===selectionKey?new Set(consentIds()):new Set(ids);
    list.dataset.batchConsentSelectionKey=selectionKey;
    list.innerHTML=ids.map((id,index)=>`<label class="batch-consent-item"><input type="checkbox" data-batch-consent-check="${esc(id)}"${selectedIds.has(id)?" checked":""}><span><strong>资料片段 ${index+1}</strong><small>勾选后，这一段原文才会发送给所选 AI</small></span></label>`).join("")||"<div class=\"batch-empty\">建立批次后会显示可授权项目。</div>";
  }
  function consentIds(){return Array.from(document.querySelectorAll("[data-batch-consent-check]:checked")).map(node=>node.dataset.batchConsentCheck).filter(Boolean)}
  function queueIds(){return Array.from(document.querySelectorAll("[data-batch-queue-check]:checked")).map(node=>node.dataset.batchQueueCheck).filter(Boolean)}
  function syncBulkActions(){const wrapper=document.querySelector(".batch-bulk-actions"),button=el("batchBulkReviewButton");if(!wrapper||!button)return;const hasReview=Boolean(batch.manifest)&&counts().review_pending>0;const selectedReview=queueIds().some(id=>str(reportItem(id).status)==="facts_review");wrapper.hidden=!hasReview;button.disabled=!selectedReview||batch.writeBusy;button.title=selectedReview?"逐项接受已勾选项目的事实候选":"先在左侧勾选等待事实审核的项目"}
  function renderAll(){setLayout();renderFiles();renderQueue();renderScanSummary();renderConsent();renderProgress();renderReview();actions();const button=el("batchCreateButton");if(button){button.textContent=!batch.manifest?"建立批量任务":batch.reportLoading?"正在读取项目状态…":batch.reportError&&!batch.report?"重试读取项目状态":"确认并开始提取事实";button.disabled=Boolean(batch.manifest&&(batch.reportLoading||batch.startBusy||batch.writeBusy));button.title=batch.reportLoading?"正在读取批次项目状态，请稍候。":"确认后会授权勾选的资料并自动进入事实提取，不会出现另一个隐藏的下一步。"}}
  async function scan(){
    if(!batch.files.length)return notice("请先选择至少一份参考资料。","warning");
    const total=batch.files.reduce((sum,file)=>sum+file.size,0);
    if(batch.files.length>200||total>67108864||batch.files.some(file=>file.size>16777216||!allowedExtensions.has(ext(file.name))))return notice("资料数量、大小或格式不符合要求，请先处理标红项目。","warning");
    try{await session();const form=new FormData();batch.files.forEach(file=>{form.append("files",file,file.name);form.append("relative_path",file.webkitRelativePath||file.name);form.append("display_name",file.name)});notice("正在扫描参考资料…","info");const result=await request("/api/ai/authoring/batch/scan",{method:"POST",body:form});batch.scan=data(result);batch.localScan=await readLocalScanStats();batch.manifest=null;batch.report=null;batch.reportLoading=false;batch.reportError=null;batch.reportRequest=null;batch.consent=null;batch.consentToken="";notice("资料扫描完成。下一步是建立批次并确认 AI 读取范围。","good");view("consent");renderAll()}catch(errorValue){error(errorValue)}
  }
  async function createBatch(){
    if(!batch.scan)return notice("请先扫描参考资料。","warning");
    try{await session();notice("正在建立批量任务…","info");const result=await request("/api/ai/authoring/batch/create",{method:"POST",body:JSON.stringify({scan_id:str(pick(batch.scan,"scan_id","scanId")),expected_scan_hash:str(pick(batch.scan,"scan_hash","scanHash")),pipeline_revision:"batch-authoring.v1",provider_id:providerId(),model_parameters:providerParameters(),idempotency_key:await sha256Hex(`studio-${Date.now()}-${Math.random().toString(16).slice(2)}`)})});batch.manifest=data(result);batch.report=null;batch.reportLoading=true;batch.reportError=null;batch.consent=null;batch.consentToken="";remember();view("consent");renderAll();await refresh({silent:true});notice(batch.report?"批量任务已建立。请确认资料读取范围后开始提取事实。":"批量任务已建立，但项目状态读取失败，请点击“刷新批次状态”后重试。",batch.report?"good":"warning")}catch(errorValue){error(errorValue)}
  }
  async function refresh(options={}){
    if(!batchId())return batch.manifest;
    if(batch.reportRequest)return batch.reportRequest.promise;
    const requestState={batchId:batchId(),promise:null};
    batch.reportRequest=requestState;batch.reportLoading=true;batch.reportError=null;renderAll();
    const operation=(async()=>{
      try{
        const [manifest,report]=await Promise.all([request(`/api/ai/authoring/batch/${encodeURIComponent(requestState.batchId)}`),request(`/api/ai/authoring/batch/${encodeURIComponent(requestState.batchId)}/report`)]);
        if(batchId()!==requestState.batchId)return batch.manifest;
        batch.manifest=data(manifest);batch.report=data(report);batch.reportLoading=false;batch.reportError=null;remember();const items=reportItems(),selectedItem=items.find(item=>item.item_id===batch.selectedItemId);if(batch.selectedItemId&&(!selectedItem||(!["facts_review","metadata_pending","ready_to_create"].includes(str(selectedItem.status))&&!batch.details.has(selectedItem.item_id)&&!batch.detailLoading.has(selectedItem.item_id)&&!batch.detailErrors.has(selectedItem.item_id))))batch.selectedItemId="";if(!batch.selectedItemId){const item=items.find(value=>["facts_review","metadata_pending","ready_to_create"].includes(str(value.status)));if(item)batch.selectedItemId=item.item_id}renderAll();poll();const selectedReportItem=items.find(item=>item.item_id===batch.selectedItemId);if(selectedReportItem&&["facts_review","metadata_pending","ready_to_create"].includes(str(selectedReportItem.status))&&!batch.details.has(selectedReportItem.item_id))await openItem(selectedReportItem.item_id);return batch.manifest;
      }catch(errorValue){
        if(batch.reportRequest===requestState){batch.reportLoading=false;batch.reportError=errorValue;renderAll()}
        if(!options.silent)error(errorValue);return batch.manifest;
      }finally{if(batch.reportRequest===requestState)batch.reportRequest=null}
    })();
    requestState.promise=operation;
    return operation;
  }
  async function claimBatch(){
    if(!batch.manifest)return notice("当前还没有可接管的批次。","info");
    if(!confirm("确定接管这个批次吗？接管后，原有读取授权会失效，需要重新确认资料读取范围。"))return;
    try{
      await session();
      batch.writeBusy=true;actions();
      const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/claim`,{method:"POST",body:JSON.stringify({expected_revision:num(pick(batch.manifest,"revision"),-1),claim_reason:"manual_reclaim"})});
      batch.manifest=data(result);batch.report=null;batch.reportLoading=true;batch.reportError=null;batch.consent=null;batch.consentToken="";batch.details.clear();batch.detailLoading.clear();batch.detailErrors.clear();batch.metadataApprovals.clear();
      await refresh({silent:true});view("consent");renderAll();notice("已接管当前批次。请重新确认 AI 读取范围后继续。","good");
    }catch(errorValue){error(errorValue)}finally{batch.writeBusy=false;renderAll()}
  }
  async function consent(){
    if(!batch.manifest)throw new Error("尚未建立批次。");
    const ids=consentIds();if(!ids.length){notice("请至少勾选一个资料项目。取消全部选择不会默认读取全部资料。","warning");return null;}
    const manifest=await refresh({silent:true});const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/consent`,{method:"POST",body:JSON.stringify({expected_revision:num(pick(manifest,"revision"),-1),provider_id:providerId(),scope:"facts_and_metadata",model_parameters:providerParameters(),send_scope:"all_snapshots",authorized_item_ids:ids})});const resultData=data(result);batch.consent=pick(resultData,"consent")||{};batch.consentToken=str(pick(resultData,"consent_token","consentToken"));notice("本批次读取授权已记录。授权不会写入浏览器，刷新后需要重新确认。","good");renderAll();return batch.consent;
  }
  function failedStage(item){const code=str(pick(item,"error_code","last_error_code","lastErrorCode")).toUpperCase(),reason=str(pick(item,"status_reason","statusReason")).toLowerCase();if(reason.includes("metadata")||code.includes("METADATA"))return "metadata";if(reason.includes("facts")||reason.includes("network")||reason.includes("provider")||reason.includes("timeout")||code.includes("PROVIDER")||code.includes("FACT"))return "facts";return ""}
  function eligible(stage){return reportItems().filter(item=>{const status=str(item.status);if(stage==="metadata")return status==="metadata_pending";if(status==="queued")return true;return failureStatuses.has(status)&&failedStage(item)==="facts"}).map(item=>item.item_id)}
  async function start(stage){
    if(!batch.manifest||batch.startBusy)return;
    if(!batch.report){await refresh({silent:true});if(!batch.report)return notice(batch.reportLoading?"正在读取项目状态，请稍候再试。":"项目状态读取失败，请点击“刷新批次状态”后重试。",batch.reportLoading?"info":"error")}
    const targets=eligible(stage);if(!targets.length){const failures=failureItems(stage);if(failures.length){if(stage==="facts")return notice(`事实提取没有可直接继续的项目：失败 ${failures.filter(item=>str(item.status)==="failed").length}，待确认 ${failures.filter(item=>str(item.status)==="unknown_result").length}。请在右侧查看错误摘要并人工确认重试。`,"warning");return notice(`档案信息没有可直接继续的项目：失败 ${failures.filter(item=>str(item.status)==="failed").length}，待确认 ${failures.filter(item=>str(item.status)==="unknown_result").length}。请先查看项目详情并人工确认重试。`,"warning")}return notice(stage==="facts"?"没有等待提取事实的项目。":"没有等待生成档案信息的项目。","info");}
    try{await session();if(!batch.consentToken){const issued=await consent();if(!issued||!batch.consentToken)return;}const manifest=await refresh({silent:true});batch.startBusy=true;actions();notice(stage==="facts"?"事实提取请求已经发出；不要重复点击。":"档案信息生成请求已经发出；不要重复点击。","info");await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/start`,{method:"POST",body:JSON.stringify({expected_revision:num(pick(manifest,"revision"),-1),consent_token:batch.consentToken,claim_generation:num(pick(manifest,"claim_generation"),-1),consent_revision:num(pick(batch.consent,"revision"),-1),stage,target_item_ids:targets})});view("review");await refresh({silent:true});notice(stage==="facts"?"事实提取已结算，请逐项目审核证据。":"档案信息候选已生成，请确认后再建档。","good")}catch(errorValue){error(errorValue)}finally{batch.startBusy=false;renderAll()}
  }
  function poll(){const c=counts(),status=str(pick(batch.manifest,"status")),should=Boolean(batch.manifest)&&!["paused","cancelled","completed"].includes(status)&&(c.running>0||batch.startBusy);if(!should){if(batch.pollTimer)clearInterval(batch.pollTimer);batch.pollTimer=null;return}if(batch.pollTimer)return;batch.pollTimer=setInterval(async()=>{if(batch.pollBusy)return;batch.pollBusy=true;try{await refresh({silent:true})}finally{batch.pollBusy=false}},1800)}
  function stopPoll(){if(batch.pollTimer)clearInterval(batch.pollTimer);batch.pollTimer=null}
  async function openItem(itemId){if(!itemId||!batch.manifest)return;batch.selectedItemId=itemId;batch.details.delete(itemId);batch.detailErrors.delete(itemId);batch.detailLoading.add(itemId);renderQueue();renderReview();try{const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/items/${encodeURIComponent(itemId)}`);batch.details.set(itemId,data(result));view("review");renderQueue();renderReview()}catch(errorValue){batch.detailErrors.set(itemId,errorValue);error(errorValue)}finally{batch.detailLoading.delete(itemId);renderQueue();renderReview()}}
  async function loadItem(itemId){if(batch.details.has(itemId))return batch.details.get(itemId);const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/items/${encodeURIComponent(itemId)}`),detail=data(result);batch.details.set(itemId,detail);return detail}
  function renderReview(){
    const node=el("batchReviewContent");
    if(!node)return;
    renderProgress();
    if(!batch.selectedItemId){
      const c=counts(),failures=failureItems("facts");
      node.innerHTML=failures.length?failureOverview(failures):c.review_pending?"<div class=\"batch-empty\"><strong>请选择一个待审核项目</strong><p>左侧列表中的“等待事实审核”项目可以打开后逐条检查证据。</p></div>":"<div class=\"batch-empty\"><strong>当前没有待审核项目</strong><p>项目目前还没有可审核的事实。请在右侧确认读取范围并点击“提取客观事实”。</p></div>";
      return;
    }
    const detail=batch.details.get(batch.selectedItemId);
    if(!detail){
      const detailError=batch.detailErrors.get(batch.selectedItemId);
      if(detailError){
        node.innerHTML="<div class=\"batch-card batch-error-card\"><strong>读取项目详情失败</strong><p>"+esc(str(detailError.message,"暂时无法读取这个项目的详情。"))+"</p><button type=\"button\" data-batch-action=\"refresh-item\">重新读取项目详情</button></div>";
        return;
      }
      if(batch.detailLoading.has(batch.selectedItemId)){
        node.innerHTML="<div class=\"batch-loading\">正在读取项目详情…</div>";
        return;
      }
      const queuedItem=reportItem(batch.selectedItemId);
      node.innerHTML=failureStatuses.has(str(queuedItem.status))?failureOverview([queuedItem],"这个项目需要人工处理"):"<div class=\"batch-card\"><strong>"+esc(statusText(queuedItem.status))+"</strong><p>这个项目当前还没有开始读取详情。请先在右侧确认读取范围并启动对应步骤。</p></div>";
      return;
    }
    const item={...reportItem(batch.selectedItemId),...(pick(detail,"item")||{})};
    const facts=arr(pick(detail,"facts")),evidence=arr(pick(detail,"evidence")),metadata=arr(pick(detail,"metadata_results","metadataResults"))[0]||pick(item,"metadata_selection","metadataSelection"),status=str(pick(item,"status")),accepted=new Set(arr(pick(item,"accepted_fact_ids","acceptedFactIds")).map(str)),canReview=status==="facts_review",canMetadata=status==="ready_to_create"&&Boolean(metadata);
    const factsHtml=facts.length?facts.map((fact,index)=>{const id=str(pick(fact,"fact_id","factId")),quotes=evidence.filter(entry=>str(pick(entry,"fact_id","factId"))===id).map(entry=>`<div class="batch-evidence"><strong>原文证据</strong><span>${esc(pick(entry,"quote"))}</span></div>`).join(""),checked=accepted.has(id)||(canReview&&!accepted.size)?" checked":"";return`<article class="batch-fact-card ${canReview?"":"read-only"}"><div class="batch-fact-head"><label><input type="checkbox" data-batch-fact="${esc(id)}"${checked}${canReview?"":" disabled"}><strong>事实 ${index+1}</strong></label><span class="chip">${esc(pick(fact,"risk_level","riskLevel")||"待评估")}</span></div><p>${esc(pick(fact,"text"))}</p>${quotes}<details class="batch-advanced-id"><summary>高级信息</summary><code>${esc(id)}</code></details></article>`}).join(""):"<div class=\"batch-empty\">当前项目没有事实结果。</div>";
    const metadataHtml=metadata?`<section class="batch-subsection"><div class="batch-subsection-head"><h4>档案信息候选</h4><span class="chip">只允许采用当前候选</span></div><p class="batch-help">R14 批次阶段不能直接改写或留空候选。所属时代和确定程度请回到主编辑器补充。</p><div class="batch-metadata-grid"><label>档案标题<input readonly value="${esc(pick(metadata,"title"))}"></label><label>知识分类<input readonly value="${esc(pick(metadata,"domain"))}"></label><label>二级主题<input readonly value="${esc(pick(metadata,"subdomain"))}"></label><label class="full">档案摘要<textarea readonly>${esc(pick(metadata,"summary"))}</textarea></label></div>${canMetadata?`<button type="button" data-batch-action="approve-metadata" class="${batch.metadataApprovals.has(batch.selectedItemId)?"good":"primary"}">${batch.metadataApprovals.has(batch.selectedItemId)?"已确认采用当前候选":"确认采用当前候选"}</button>`:""}</section>`:"";
    const reviewHtml=canReview?`<section class="batch-review-toolbar"><label>风险级别<select id="batchRiskLevel"><option value="green">低风险</option><option value="yellow">需要复核</option><option value="red">高风险</option></select></label><label class="full">审核说明<textarea id="batchReviewerNote" placeholder="高风险事实必须填写说明；其他情况可留空。"></textarea></label><div class="batch-action-buttons"><button class="primary" type="button" data-batch-action="accept-item">接受勾选事实</button><button class="danger" type="button" data-batch-action="reject-item">退回这个项目</button></div></section>`:"";
    const resultNote=status==="created"?`<div class="batch-card batch-success-card"><strong>已生成待审核草稿</strong><p>请返回主编辑器补充所属时代、确定程度并逐档复核。</p></div>`:failureStatuses.has(status)?failureOverview([item],"这个项目需要人工处理"):"";
    node.innerHTML=`<div class="batch-detail-head"><div><span class="batch-detail-kicker">${esc(statusText(status))}</span><h3>${esc(itemTitle(batch.selectedItemId,itemIds().indexOf(batch.selectedItemId)))}</h3><p>AI 候选必须经过人工检查；来源证据来自批次参考资料。</p></div><button type="button" class="mini ghost" data-batch-action="refresh-item">刷新项目</button></div>${resultNote}<section class="batch-subsection"><div class="batch-subsection-head"><h4>客观事实</h4><span class="chip">${facts.length} 条</span></div>${factsHtml}</section>${metadataHtml}${reviewHtml}`;
  }
  function actions(){
    syncBulkActions();
    const node=el("batchActionContent");
    if(!node)return;
    renderProviders();
    if(!batch.manifest){
      if(!batch.scan){
        node.innerHTML="<div class=\"batch-empty\"><strong>先导入参考资料</strong><p>扫描前不会读取任何文件内容。</p></div>";
        return;
      }
      const snapshots=arr(pick(batch.scan,"snapshot_ids","snapshotIds"));
      node.innerHTML=`<div class="batch-action-card"><strong>资料已扫描</strong><p>${snapshots.length} 个资料快照可以建立批次。</p><button class="primary" type="button" data-batch-action="go-consent">进入读取授权</button></div><div class="batch-action-note">扫描只是整理资料，不会调用 AI。</div>`;
      return;
    }
    if(!batch.report){
      node.innerHTML=batch.reportLoading?"<div class=\"batch-action-card\"><strong>正在读取批次项目状态…</strong><p>项目列表和下一步按钮将在状态读取完成后出现，请稍候。</p></div>":"<div class=\"batch-action-card\"><strong>项目状态读取失败</strong><p>没有读取到项目状态，因此暂不允许开始 AI 提取。</p><button class=\"ghost\" type=\"button\" data-batch-action=\"refresh-batch\">重新读取项目状态</button></div>";
      return;
    }
    const c=counts(),status=str(pick(batch.manifest,"status")),approved=Array.from(batch.metadataApprovals).filter(id=>reportItem(id).status==="ready_to_create"),factTargets=eligible("facts"),metadataTargets=eligible("metadata"),factFailures=failureItems("facts"),buttons=[];
    if(factTargets.length){
      const retryCount=factTargets.filter(id=>failureStatuses.has(str(reportItem(id).status))).length;
      const label=retryCount===factTargets.length?"确认后重试事实项目":retryCount?"提取并重试客观事实":"提取客观事实";
      buttons.push(`<button class="primary" type="button" data-batch-action="start-facts"${batch.startBusy?" disabled":""}>${batch.startBusy?"正在提取…":label}</button>`);
    }
    if(c.review_pending)buttons.push(`<button type="button" data-batch-action="open-review">开始审核事实（${c.review_pending}）</button>`);
    if(metadataTargets.length)buttons.push(`<button class="primary" type="button" data-batch-action="start-metadata"${batch.startBusy?" disabled":""}>${batch.startBusy?"正在生成…":"生成档案信息"}</button>`);
    if(c.ready_to_create)buttons.push(`<button class="primary" type="button" data-batch-action="create-documents"${batch.writeBusy||!approved.length?" disabled":""}>生成待审核草稿（${approved.length||c.ready_to_create}）</button>`);
    if(!["paused","cancelled","completed"].includes(status)&&(c.running||factTargets.length||c.review_pending||metadataTargets.length||c.ready_to_create))buttons.push(`<button class="ghost" type="button" data-batch-action="pause">暂停后续处理</button>`);
    if(!["cancelled","completed"].includes(status))buttons.push(`<button class="danger" type="button" data-batch-action="cancel">取消批次</button>`);
    if(!batch.consentToken&&(factTargets.length||metadataTargets.length))buttons.unshift(`<button class="ghost" type="button" data-batch-action="renew-consent">重新确认读取范围</button>`);
    const failureCard=factFailures.length?failureOverview(factFailures,"事实提取失败或结果待确认"):"";
    node.innerHTML=`<div class="batch-action-card"><div class="batch-action-heading"><strong>${esc(providerName(providerId()))}</strong><span class="batch-status ${statusClass(status)}">${esc(statusText(status))}</span></div><p>已完成 ${c.created}，待审核 ${c.review_pending}，待建档 ${c.ready_to_create}，失败 ${c.failed}，待确认 ${c.unknown_result}。</p>${buttons.length?`<div class="batch-action-buttons">${buttons.join("")}</div>`:"<p class=\"batch-help\">当前没有需要立即执行的动作。可以从左侧打开项目查看详情。</p>"}</div>${failureCard}<div class="batch-action-note">暂停或取消不会承诺立即中断已经发出的同步 AI 请求；刷新后以服务端状态为准。</div>`;
  }
  function evidenceRisk(facts){const values=facts.map(fact=>str(pick(fact,"risk_level","riskLevel")));return values.includes("red")?"red":values.includes("yellow")?"yellow":"green"}
  async function reviewOne(itemId,factIds,reviewStatus,risk,note,rejection){const detail=batch.details.get(itemId)||await loadItem(itemId),item=pick(detail,"item")||{},facts=arr(pick(detail,"facts")),ids=factIds||facts.map(fact=>str(pick(fact,"fact_id","factId"))).filter(Boolean),decisions=reviewStatus==="accepted"?ids.map(id=>({fact_id:id,decision:"accept"})):[];await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/items/${encodeURIComponent(itemId)}/review`,{method:"POST",body:JSON.stringify({expected_item_revision:num(pick(item,"revision"),-1),fact_ids:reviewStatus==="accepted"?ids:[],risk_level:risk||evidenceRisk(facts),review_status:reviewStatus,reviewer_note:note||((risk||evidenceRisk(facts))==="red"?"存在高风险事实，已要求继续人工复核。":""),rejection_reason:rejection||"",fact_decisions:decisions})});batch.details.delete(itemId)}
  async function acceptItem(){const itemId=batch.selectedItemId,detail=batch.details.get(itemId);if(!detail)return;const ids=Array.from(document.querySelectorAll("[data-batch-fact]:checked")).map(node=>node.dataset.batchFact).filter(Boolean),risk=el("batchRiskLevel")?.value||evidenceRisk(arr(pick(detail,"facts"))),note=el("batchReviewerNote")?.value.trim()||"";if(!ids.length)return notice("至少勾选一条由原文支持的事实。","warning");if(risk==="red"&&!note)return notice("高风险事实必须填写审核说明。","warning");if(!confirm(`确定接受 ${ids.length} 条事实吗？未勾选事实不会进入草稿。`))return;try{batch.writeBusy=true;await reviewOne(itemId,ids,"accepted",risk,note,"");await refresh({silent:true});await openItem(itemId);notice("事实审核已保存。","good")}catch(errorValue){error(errorValue)}finally{batch.writeBusy=false;renderAll()}}
  async function rejectItem(){if(!batch.selectedItemId||!confirm("确定退回这个项目吗？退回后不会进入元数据和建档阶段。"))return;try{batch.writeBusy=true;await reviewOne(batch.selectedItemId,[],"rejected",el("batchRiskLevel")?.value||"yellow",el("batchReviewerNote")?.value.trim()||"","编辑者暂不接受当前事实候选。");await refresh({silent:true});await openItem(batch.selectedItemId);notice("项目已退回。","good")}catch(errorValue){error(errorValue)}finally{batch.writeBusy=false;renderAll()}}
  async function reviewSelected(){const ids=queueIds().filter(id=>str(reportItem(id).status)==="facts_review");if(!ids.length)return notice("请先勾选等待事实审核的项目。","warning");if(!confirm(`将逐项接受 ${ids.length} 个项目的全部事实；任一版本冲突会停止，已成功项目不会回滚。继续吗？`))return;try{batch.writeBusy=true;for(const id of ids){const detail=await loadItem(id),facts=arr(pick(detail,"facts")),risk=evidenceRisk(facts);await reviewOne(id,facts.map(fact=>str(pick(fact,"fact_id","factId"))),"accepted",risk,risk==="red"?"批量审核后仍需人工复核高风险事实。":"","");await refresh({silent:true})}notice(`已按顺序提交 ${ids.length} 个项目的事实审核。`,"good")}catch(errorValue){error(errorValue)}finally{batch.writeBusy=false;renderAll()}}
  function metadata(detail){const item=pick(detail,"item")||{},direct=pick(item,"metadata_selection","metadataSelection"),result=arr(pick(detail,"metadata_results","metadataResults"))[0],value=direct||result;if(!value)return null;return{title:str(value.title),summary:str(value.summary),domain:str(value.domain),subdomain:str(value.subdomain),metadata_selection_hash:str(pick(value,"metadata_selection_hash","metadataSelectionHash"))}}
  function approveMetadata(){if(!batch.selectedItemId)return;const detail=batch.details.get(batch.selectedItemId),item=pick(detail,"item")||{};if(str(item.status)!=="ready_to_create"||!metadata(detail))return;batch.metadataApprovals.add(batch.selectedItemId);renderReview();actions();notice("已确认采用当前档案信息候选。","good")}
  async function createDocuments(){const selected=queueIds().filter(id=>batch.metadataApprovals.has(id)&&str(reportItem(id).status)==="ready_to_create"),ids=selected.length?selected:batch.metadataApprovals.has(batch.selectedItemId)?[batch.selectedItemId]:[];if(!ids.length)return notice("请先打开待建档项目并确认采用当前候选。","warning");if(!confirm(`确定为 ${ids.length} 个项目生成待审核世界书草稿吗？不会自动发布正典。`))return;try{batch.writeBusy=true;const manifest=await refresh({silent:true}),inputs=[];for(const id of ids){const detail=batch.details.get(id)||await loadItem(id),item=pick(detail,"item")||{},selection=metadata(detail);if(selection&&selection.title&&selection.summary&&selection.domain&&selection.subdomain)inputs.push({item_id:id,expected_item_revision:num(item.revision,-1),fact_ids:arr(pick(item,"accepted_fact_ids","acceptedFactIds")).map(str),metadata_selection:selection})}if(!inputs.length)return notice("没有找到可提交的完整档案信息候选。","warning");const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/create-documents`,{method:"POST",body:JSON.stringify({expected_revision:num(pick(manifest,"revision"),-1),create_mode:"needs_review",items:inputs})}),items=arr(pick(data(result),"items")),created=items.filter(item=>["created","already_exists"].includes(str(item.status))).length;await refresh({silent:true});if(typeof loadDocuments==="function")await loadDocuments();notice(`已处理 ${created} 个待审核草稿。请返回主编辑器补充时代、确定程度并逐档复核。`,`good`)}catch(errorValue){error(errorValue)}finally{batch.writeBusy=false;renderAll()}}
  async function changeState(stateValue){if(!batch.manifest)return;const label=stateValue==="cancelled"?"取消":"暂停";if(!confirm(`确定${label}批次吗？已经发出的同步 AI 请求可能继续完成。`))return;try{batch.writeBusy=true;const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/${stateValue==="cancelled"?"cancel":"pause"}`,{method:"POST",body:JSON.stringify({expected_revision:num(pick(batch.manifest,"revision"),-1)})});batch.manifest=data(result);if(stateValue==="cancelled")batch.consentToken="";notice(`${label}请求已提交。请刷新查看服务端状态。`,`warning`);renderAll()}catch(errorValue){error(errorValue)}finally{batch.writeBusy=false}}
  async function retryItem(){if(!batch.selectedItemId)return;try{const detail=batch.details.get(batch.selectedItemId)||await loadItem(batch.selectedItemId),item=pick(detail,"item")||{},stage=failedStage(item);if(!stage)return notice("无法确定这个项目的失败阶段，请先刷新项目详情后再决定重试。","warning");if(!confirm(`确定重试这个项目的${stage==="metadata"?"档案信息":"事实提取"}吗？需要重新确认读取范围。`))return;batch.writeBusy=true;const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batchId())}/items/${encodeURIComponent(batch.selectedItemId)}/retry`,{method:"POST",body:JSON.stringify({expected_item_revision:num(item.revision,-1),manual_confirmation:true,stage})});batch.details.set(batch.selectedItemId,data(result));batch.consent=null;batch.consentToken="";await refresh({silent:true});notice("项目已排回待处理状态。下一次启动前需要重新授权。","good")}catch(errorValue){error(errorValue)}finally{batch.writeBusy=false;renderAll()}}
  async function restore(){if(batch.restoreAttempted||!batch.savedBatchId)return;batch.restoreAttempted=true;try{const result=await request(`/api/ai/authoring/batch/${encodeURIComponent(batch.savedBatchId)}`);batch.manifest=data(result);batch.report=null;batch.reportLoading=true;batch.reportError=null;const selection=pick(batch.manifest,"provider_selection")||{},parameters=pick(selection,"model_parameters","modelParameters")||{};if(el("batchProviderSelect"))el("batchProviderSelect").value=str(pick(selection,"provider_id","providerId"),"local");if(el("batchModelInput")&&parameters.model)el("batchModelInput").value=parameters.model;if(el("batchTemperatureInput")&&parameters.temperature!==undefined)el("batchTemperatureInput").value=parameters.temperature;if(el("batchMaxTokensInput")&&parameters.max_output_tokens!==undefined)el("batchMaxTokensInput").value=parameters.max_output_tokens;batch.consent=null;batch.consentToken="";await refresh({silent:true});view("review");notice(batch.report?"已恢复上次批次状态。出于安全原因，读取授权不会保存在浏览器中，请重新确认后继续。":"批次已恢复，但项目状态读取失败，请点击“刷新批次状态”后重试。",batch.report?"info":"warning");renderAll()}catch{forget();notice("上次批次已失效或无法读取，请重新选择资料。","warning")}}
  function reset(){stopPoll();batch.files=[];batch.scan=null;batch.localScan=null;batch.manifest=null;batch.report=null;batch.reportLoading=false;batch.reportError=null;batch.reportRequest=null;batch.consent=null;batch.consentToken="";batch.selectedItemId="";batch.selectedQueueIds=new Set();batch.details.clear();batch.detailLoading.clear();batch.detailErrors.clear();batch.metadataApprovals.clear();batch.view="upload";forget();if(el("batchFileInput"))el("batchFileInput").value="";view("upload");renderAll();notice("已准备新的批量任务。旧批次仍保留在工作区。","info")}
  function close(){stopPoll();if(batch.dialog?.open)batch.dialog.close();if(batch.returnFocus?.focus)batch.returnFocus.focus();batch.returnFocus=null}
  function open(){if(state.dirty&&!confirm("当前档案有未保存修改。批量工作台不会带走这些修改，确定继续打开吗？"))return;batch.returnFocus=document.activeElement;if(!batch.dialog.open)batch.dialog.showModal();renderAll();if(!batch.manifest&&!batch.restoreAttempted)restore();el("batchCloseButton")?.focus()}
  function action(event){const button=event.target.closest("[data-batch-action]");if(!button)return;const name=button.dataset.batchAction;if(name==="open-item")openItem(button.dataset.itemId);else if(name==="go-consent")view("consent");else if(name==="refresh-batch")refresh();else if(name==="start-facts"||name==="renew-consent")start("facts");else if(name==="start-metadata")start("metadata");else if(name==="open-review"){view("review");const item=reportItems().find(value=>value.status==="facts_review");if(item&&!batch.selectedItemId)openItem(item.item_id)}else if(name==="accept-item")acceptItem();else if(name==="reject-item")rejectItem();else if(name==="review-selected")reviewSelected();else if(name==="approve-metadata")approveMetadata();else if(name==="create-documents")createDocuments();else if(name==="pause")changeState("paused");else if(name==="cancel")changeState("cancelled");else if(name==="retry-item")retryItem();else if(name==="refresh-item")openItem(batch.selectedItemId);else if(name==="help")notice("操作顺序：扫描资料 → 建立批次 → 确认读取范围 → 提取事实 → 审核事实 → 生成档案信息 → 确认候选 → 生成待审核草稿。","info")}
  function resizers(){document.querySelectorAll("[data-batch-resizer]").forEach(node=>{const side=node.dataset.batchResizer;let startX=0,startWidth=0;node.addEventListener("pointerdown",event=>{event.preventDefault();startX=event.clientX;startWidth=side==="left"?batch.leftWidth:batch.rightWidth;node.classList.add("is-dragging");node.setPointerCapture?.(event.pointerId)});node.addEventListener("pointermove",event=>{if(!node.classList.contains("is-dragging"))return;const delta=event.clientX-startX;if(side==="left")batch.leftWidth=bounded(startWidth+delta,286,230,430);else batch.rightWidth=bounded(startWidth-delta,356,310,480);setLayout()});const end=event=>{node.classList.remove("is-dragging");node.releasePointerCapture?.(event.pointerId)};node.addEventListener("pointerup",end);node.addEventListener("pointercancel",end);node.addEventListener("keydown",event=>{if(!["ArrowLeft","ArrowRight"].includes(event.key))return;event.preventDefault();const delta=event.key==="ArrowRight"?16:-16;if(side==="left")batch.leftWidth=bounded(batch.leftWidth+delta,286,230,430);else batch.rightWidth=bounded(batch.rightWidth-delta,356,310,480);setLayout()})})}
  function bind(){
    batch.dialog=el("batchWorkbenchDialog");if(!batch.dialog)return;
    el("batchWorkbenchButton")?.addEventListener("click",open);el("batchSidebarButton")?.addEventListener("click",open);el("batchCloseButton")?.addEventListener("click",close);el("batchNewButton")?.addEventListener("click",reset);el("batchRefreshButton")?.addEventListener("click",()=>refresh());el("batchHelpButton")?.addEventListener("click",()=>notice("AI 只负责提取候选。你需要检查原文证据、确认事实，再生成待审核草稿。","info"));el("batchClaimButton")?.addEventListener("click",claimBatch);el("batchScanButton")?.addEventListener("click",scan);el("batchCreateButton")?.addEventListener("click",()=>{if(!batch.manifest){createBatch();return}if(batch.reportError&&!batch.report){refresh();return}start("facts")});el("batchSelectAllButton")?.addEventListener("click",()=>document.querySelectorAll("[data-batch-consent-check]").forEach(input=>input.checked=true));el("batchFileInput")?.addEventListener("change",event=>addFiles(event.target.files));
    el("batchDropzone")?.addEventListener("click",()=>el("batchFileInput")?.click());el("batchDropzone")?.addEventListener("keydown",event=>{if(event.key==="Enter"||event.key===" "){event.preventDefault();el("batchFileInput")?.click()}});el("batchDropzone")?.addEventListener("dragover",event=>{event.preventDefault();el("batchDropzone").classList.add("is-dragging")});el("batchDropzone")?.addEventListener("dragleave",()=>el("batchDropzone")?.classList.remove("is-dragging"));el("batchDropzone")?.addEventListener("drop",event=>{event.preventDefault();el("batchDropzone")?.classList.remove("is-dragging");addFiles(event.dataTransfer.files)});
    el("batchProviderSelect")?.addEventListener("change",renderProviders);el("batchActionContent")?.addEventListener("click",action);el("batchReviewContent")?.addEventListener("click",action);el("batchQueueList")?.addEventListener("click",action);el("batchQueueList")?.addEventListener("change",event=>{if(event.target.matches("[data-batch-queue-check]")){batch.selectedQueueIds=new Set(queueIds());actions()}});el("batchFileList")?.addEventListener("click",event=>{const button=event.target.closest("[data-batch-file-remove]");if(!button)return;batch.files.splice(Number(button.dataset.batchFileRemove),1);renderFiles()});batch.dialog.addEventListener("cancel",event=>{event.preventDefault();close()});batch.dialog.addEventListener("close",()=>{stopPoll();if(batch.returnFocus?.focus)batch.returnFocus.focus();batch.returnFocus=null});resizers();readStorage();
  }
  bind();
  el("batchBulkReviewButton")?.addEventListener("click",reviewSelected);
  session().then(loadProviders).catch(()=>{});
})();

