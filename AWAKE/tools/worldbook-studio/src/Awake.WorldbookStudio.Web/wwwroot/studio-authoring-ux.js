"use strict";

function addReviewTargets(html){
  const issues=typeof localChecks==="function"?localChecks():[];
  let issueIndex=0;
  return html.replace(/data-action="jump-step" data-step="(\d+)"/g,(match)=>{
    const issue=issues[issueIndex++];
    return issue?`${match} data-target="${h(issue.target||"")}"`:match;
  });
}

const genericAiMessages=new Set(["操作失败。","操作失败","请求失败","请求失败。","未知错误","内部错误","服务器内部错误","AI 助手暂时无法完成这项操作，请稍后重试。","AI 请求没有完成。","本机 AI 请求没有完成。"]);

function isGenericAiMessage(message){
  if(!message)return true;
  if(message.length>400)return true;
  if(genericAiMessages.has(message))return true;
  return message.length<=14&&/^(?:请求失败|操作失败|服务器错误|内部错误|未知错误|HTTP\s*\d{3})/.test(message);
}

function safeAiErrorMessage(error,fallback){
  const messages={
    "WB-AI-CSRF-403":"本机安全会话已过期，请刷新页面后重试。",
    "WB-AI-PROVIDER-400":"云端 AI 配置不完整，请检查服务地址、模型和 API Key。",
    "WB-AI-PROVIDER-409":"当前 AI 来源不可用。用本机 AI 时请确认 Worker 已启动、地址与密钥已由提供方在启动前配置好（本界面没有填写本机 Worker 地址的入口）；也可以改用云端 Provider，或不使用 AI 继续编辑。详见随包 README_使用说明.txt 与 docs 目录的本地 Worker 配置说明。",
    "WB-AI-PROVIDER-404":"找不到当前的 AI 来源配置。请改用云端 Provider，或确认本机 Worker 已启动并已由提供方配置好地址与密钥（本界面没有填写入口）。",
    "WB-AI-REMOTE-502":"云端 AI 暂时没有返回可用结果，请稍后重试。",
    "WB-AI-FORMAT-JSON":"AI 返回内容无法识别，请重试或改用本机 Worker。",
    "WB-AI-DRAFT-GOAL-422":"请先说明希望整理什么内容。",
    "WB-AI-DRAFT-TIER-422":"请先选择内容范围，再开始生成草稿。",
    "WB-AI-DRAFT-PERSPECTIVES-413":"身份视角最多填写 3 个；不需要身份表达时请留空。",
    "WB-AI-DRAFT-EXPRESSIONS-422":"本次没有请求身份表达，AI 返回了越界内容；请重新生成。",
    "WB-CAS-409":"档案在保存前发生了变化，请重新读取后再保存。",
    "WB-DOC-READ-409":"档案在读取期间发生了变化，请重新读取后重试。",
    "WB-DOC-400":"档案操作信息不完整，请检查当前填写内容。",
    "WB-TAXONOMY-422":"二级主题或相关分类选择无效，请按界面提示重新选择。",
    "WB-TAXONOMY-CAS-400":"分类目录快照已失效，请刷新档案后再保存。",
    "WB-TAXONOMY-CAS-409":"分类目录已更新，请刷新档案后再保存。",
    "WB-TAXONOMY-500":"分类目录当前不可用，工作室已停止本次操作。",
    "WB-EDITOR-SOURCE-403":"来源型档案不能应用 AI 候选；如需改动，请进入高级模式手工维护。"
  };
  const code=String(error?.code||"");
  const detail=typeof error?.detail==="string"?error.detail.trim():"";
  if(detail)return detail;
  const serverMessage=typeof error?.message==="string"?error.message.trim():"";
  if(serverMessage&&serverMessage!==code&&!isGenericAiMessage(serverMessage))return serverMessage;
  if(messages[code])return messages[code];
  if(code.startsWith("WB-AI-DRAFT-PASS-"))return"AI 生成的这份草稿没有通过内容安全检查，本次没有落盘。请查看下方阻断原因，调整参考资料或生成要求后重试。";
  if(code.startsWith("WB-AI-DRAFT-STATE-"))return"这份草稿的状态已经变化（可能在其他窗口被改动或已经过期），请重新读取草稿后再继续。";
  if(code.startsWith("WB-AI-WORKER-"))return"本机 AI Worker 没有正常响应。请确认 Worker 已在本机启动，并已由提供方在启动前配置好它的地址与密钥；拿不到 Worker 时请改用云端 Provider，或不使用 AI 继续编辑、校验和导出。";
  if(code.startsWith("WB-AI-"))return"AI 助手暂时无法完成这项操作，请稍后重试。";
  if(code.startsWith("WB-DOC-"))return"档案操作没有完成，请检查档案状态后重试。";
  return fallback;
}

var providerLoadPromise=null;
loadProviders=async function(){
  if(providerLoadPromise)return providerLoadPromise;
  providerLoadPromise=(async()=>{
    try{
      const data=await requestJson("/api/ai/providers");
      state.providers=data.providers||[];
      renderAi();
    }catch(error){
      if(error.code!=="WB-AI-CSRF-403")toast(safeAiErrorMessage(error,"无法读取 AI Provider 状态，请稍后重试。"),"warn");
    }finally{
      providerLoadPromise=null;
    }
  })();
  return providerLoadPromise;
};

var baseRenderReview=renderReview;
renderReview=function(){return addReviewTargets(baseRenderReview())};

var baseRenderSelection=renderSelection;
renderSelection=function(){
  baseRenderSelection();
  const model=state.authorModel;
  if(!model)return;
  const hasSubdomain=Boolean(subdomainEntry(model.domain,model.subdomain));
  const related=(Array.isArray(model.relatedDomains)?model.relatedDomains:[]).map(domainLabel).filter(label=>label&&label!=="未分类");
  const taxonomyLine=hasSubdomain
    ? `<p>知识分类：${h(domainLabel(model.domain))} · 二级主题：${h(subdomainLabel(model.domain,model.subdomain))}</p>`
    : `<div class="diag warning"><strong>这份档案还没有二级主题。</strong><p>请在“基本信息”中选择一个二级主题，帮助程序更准确地整理和调取这份知识。</p></div>`;
  const relatedLine=related.length?`<p>相关分类：${h(related.join("、"))}</p>`:"";
  $("editorTitle").textContent=model.title||"未命名档案";
  $("editorSubtitle").textContent=`${model.sourceMode==="source"?"来源型档案，只读作者模式":"作者创建档案，可用中文表单编辑"} · ${domainLabel(model.domain)}${hasSubdomain?` · ${subdomainLabel(model.domain,model.subdomain)}`:" · 二级主题待补充"} · ${model.contentTier==="adult_optional"?"成人拓展":"基础内容"}`;
  $("selectionInfo").innerHTML=`<strong>${h(model.title||"未命名档案")}</strong><p>${h(model.summary||"尚未填写摘要")}</p>${taxonomyLine}${relatedLine}<div style="margin-top:7px">${sourceBadge(model)} <span class="chip">${h(model.contentTier==="adult_optional"?"成人拓展":"基础内容")}</span></div>`;
};

var baseRenderAi=renderAi;
renderAi=function(){
  baseRenderAi();
  const scope=$("aiScope");
  if(!scope||!state.aiScope)return;
  scope.innerHTML=`<div class="scope"><strong>本次发送范围</strong><p>${h(state.aiScope.note||"仅使用已保存档案的结构化内容。")}</p><p>档案：${h(state.authorModel?.title||"当前档案")} · 只发送已保存内容，不包含内部编号、版本号或注册表信息。</p></div>`;
};

var baseHandleError=handleError;
handleError=function(error){
  const safeError=new Error(safeAiErrorMessage(error,"操作失败。"));
  safeError.code=error?.code;
  safeError.status=error?.status;
  return baseHandleError(safeError);
};

$("docList").addEventListener("click",event=>{
  const actionButton=event.target.closest("[data-empty-action]");
  if(!actionButton)return;
  if(actionButton.dataset.emptyAction==="new"){
    openNew();
    return;
  }
  if(actionButton.dataset.emptyAction==="clear-filter"){
    $("filter").value="";
    renderDocuments();
  }
});

if(state.authorModel)renderAll();
function taxonomyGuidanceHtml(model,compact=false){
  const domain=domainEntry(model?.domain);
  if(!domain)return'<section class="taxonomy-guidance warning"><strong>还没有选择知识分类</strong><p>先选择政治、经济、文化、战争或地理，程序才能给出对应的写作提示。</p></section>';
  const sub=subdomainEntry(model.domain,model.subdomain);
  const examples=[...(sub?.examples||[]),...(domain.examples||[])].filter(Boolean).slice(0,4);
  const avoid=(domain.conflictHints||[]).filter(Boolean).slice(0,4);
  const title=sub?`${h(domain.label)} · ${h(sub.label)}`:h(domain.label);
  const direction=sub?.help||domain.help||'请围绕这份档案的主要知识范围填写。';
  const examplesHtml=examples.length?`<ul>${examples.map(value=>`<li>${h(value)}</li>`).join('')}</ul>`:'<p class="muted">暂时没有示例，按客观、直接、可核对的方式填写即可。</p>';
  const avoidHtml=avoid.length?`<ul>${avoid.map(value=>`<li>${h(value)}</li>`).join('')}</ul>`:'<p class="muted">没有额外提醒；不要把未经确认的推测写成客观事实。</p>';
  return`<section class="taxonomy-guidance ${compact?'compact':''}"><div class="guidance-heading"><strong>这份档案应该写什么</strong><span class="chip">${title}</span></div><div class="guidance-grid"><div><strong>写作方向</strong><p>${h(direction)}</p></div><div><strong>可以参考的写法</strong>${examplesHtml}</div><div><strong>常见误区</strong>${avoidHtml}</div></div></section>`;
}

const baseRenderBasicContent=renderBasic;
renderBasic=function(){
  const html=baseRenderBasicContent();
  return html.replace('<div class="form-grid">',taxonomyGuidanceHtml(state.authorModel)+'<div class="form-grid">');
};

const baseRenderFactsContent=renderFacts;
renderFacts=function(){
  const html=baseRenderFactsContent();
  const guidance=taxonomyGuidanceHtml(state.authorModel,true);
  const marker=html.includes('<article class="card">')?'<article class="card">':'<div class="empty">';
  return html.replace(marker,guidance+marker);
};
