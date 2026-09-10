"use strict";

(function(){
  if(typeof state==="undefined"||typeof $!=="function")return;
  const panel=$("entityCatalogPanel");
  if(!panel)return;
  const own=Object.prototype.hasOwnProperty;
  state.entityPreview=state.entityPreview||{query:"",type:"all",selectedKey:""};

  function localizedText(value){
    if(typeof value==="string")return value.trim();
    if(!value||typeof value!=="object")return"";
    return String(value["zh-CN"]??value.zh??"").trim();
  }

  function readValue(source,keys){
    if(!source||typeof source!=="object")return"";
    for(const key of keys){
      const value=localizedText(source[key]);
      if(value)return value;
    }
    return"";
  }

  function readChineseValue(source,keys){
    const value=readValue(source,keys);
    return /[\u3400-\u9fff]/.test(value)?value:"";
  }

  function readRawValue(source,keys){
    if(!source||typeof source!=="object")return"";
    for(const key of keys){
      const value=source[key];
      if(value!==undefined&&value!==null&&String(value).trim()!=="")return value;
    }
    return"";
  }

  function readNames(value){
    if(!Array.isArray(value))return[];
    return value.map(item=>typeof item==="string"?item.trim():readChineseValue(item,["displayName","display_name","display","name_zh","name","label"])).filter(item=>/[\u3400-\u9fff]/.test(item));
  }

  function normalizeType(value,source){
    const type=String(value||"").toLowerCase();
    if(["hero","person","character","persona"].includes(type))return"hero";
    if(["clan","family","house"].includes(type))return"clan";
    if(["settlement","location","place","town","castle","village"].includes(type))return"settlement";
    if(source?.hero_id||source?.heroId)return"hero";
    if(source?.clan_id||source?.clanId||source?.family_code||source?.familyCode)return"clan";
    if(source?.settlement_code||source?.settlementCode||source?.settlement_type||source?.settlementType)return"settlement";
    return"unknown";
  }

  function normalizeEntity(source,index){
    if(!source||typeof source!=="object")return null;
    const nestedFamily=source.family||source.clan||{};
    const nestedMembers=source.members||source.memberNames||source.member_names||[];
    const type=normalizeType(readRawValue(source,["type","entityType","entity_type","kind"]),source);
    const name=readChineseValue(source,["displayName","display_name","display","display_name_zh","name_zh","personNameZh","person_name_zh","familyNameZh","family_name_zh","name","label","title"])||(type==="clan"?"未命名家族":type==="hero"?"未命名人物":type==="settlement"?"未命名地点":"未命名实体");
    const familyName=readChineseValue(source,["familyName","family_name","familyNameZh","family_name_zh","clanName","clan_name"])||readChineseValue(nestedFamily,["displayName","display_name","display","display_name_zh","name_zh","familyNameZh","family_name_zh","name","label"]);
    const members=readNames(readRawValue(source,["members","memberNames","member_names","memberNamesZh","member_names_zh"])||nestedMembers);
    const settlementType=String(readRawValue(source,["settlementType","settlement_type"])||"").toLowerCase();
    const culture=readChineseValue(source,["culture","culture_code","cultureCode"])||String(readRawValue(source,["culture_code","cultureCode"])||"");
    const ownerClan=readChineseValue(source,["ownerClan","owner_clan","owner_clan_code"])||String(readRawValue(source,["ownerClan","owner_clan","owner_clan_code"])||"");
    const boundSettlement=readChineseValue(source,["boundSettlement","bound_settlement"])||String(readRawValue(source,["boundSettlement","bound_settlement"])||"");
    const worldSource=String(readRawValue(source,["worldSource","world_source","source"])).toLowerCase();
    const availability=String(readRawValue(source,["runtimeAvailability","runtime_availability","availability"])).toLowerCase();
    const mappingStatus=String(readRawValue(source,["mappingStatus","mapping_status","status"])).toLowerCase();
    const duplicateName=readRawValue(source,["duplicateName","duplicate_name"])===true;
    const searchText=[name,familyName,settlementType,culture,ownerClan,boundSettlement,...members].join(" ").toLocaleLowerCase("zh-CN");
    return{key:String(index),type,name,familyName,members,settlementType,culture,ownerClan,boundSettlement,duplicateName,worldSource,availability,mappingStatus,searchText};
  }

  function readEntityCatalog(){
    const root=state.catalog;
    if(!root||typeof root!=="object")return null;
    const hasCatalog=own.call(root,"entityCatalog")||own.call(root,"entityCatalogAvailable")||own.call(root,"entityCatalogDiagnostics");
    if(!hasCatalog)return null;
    const rawCatalog=root.entityCatalog;
    const rawEntities=Array.isArray(rawCatalog)?rawCatalog:Array.isArray(rawCatalog?.entities)?rawCatalog.entities:Array.isArray(rawCatalog?.records)?rawCatalog.records:Array.isArray(root.entities)?root.entities:[];
    const explicitAvailable=own.call(root,"entityCatalogAvailable")?root.entityCatalogAvailable:rawCatalog&&typeof rawCatalog==="object"&&own.call(rawCatalog,"available")?rawCatalog.available:rawCatalog&&typeof rawCatalog==="object"&&own.call(rawCatalog,"valid")?rawCatalog.valid:undefined;
    const hasEntityCollection=Array.isArray(rawCatalog)||Array.isArray(rawCatalog?.entities)||Array.isArray(rawCatalog?.records)||Array.isArray(root.entities);
    const available=explicitAvailable===undefined?hasEntityCollection:Boolean(explicitAvailable);
    const diagnostics=root.entityCatalogDiagnostics||rawCatalog?.diagnostics||[];
    return{available,entities:available?rawEntities.map(normalizeEntity).filter(Boolean):[],diagnostics:Array.isArray(diagnostics)?diagnostics:[]};
  }

  function typeLabel(type){return type==="hero"?"人物":type==="clan"?"家族":type==="settlement"?"地点":"其他实体"}
  function typeLabelSettlement(settlementType){return settlementType==="town"?"城镇":settlementType==="castle"?"城堡":settlementType==="village"?"村庄":String(settlementType||"聚落")}

  function availabilityLabel(entity){
    if(entity.worldSource==="official_dlc"&&entity.availability==="not_installed")return"官方 DLC，当前未安装";
    if(entity.worldSource==="official_dlc"&&entity.availability==="installed")return"官方 DLC，当前已安装";
    if(entity.availability==="installed")return"当前游戏可用";
    if(entity.availability==="not_installed")return"当前未安装";
    if(entity.mappingStatus==="needs_review")return"需要复核";
    return entity.worldSource==="official_dlc"?"官方 DLC，可用性待确认":"可用性待确认";
  }

  function availabilityClass(entity){
    return entity.availability==="installed"?"good":entity.availability==="not_installed"||entity.mappingStatus==="needs_review"?"bad":"";
  }

  function filteredEntities(catalog){
    const query=String(state.entityPreview.query||"").trim().toLocaleLowerCase("zh-CN");
    const type=state.entityPreview.type||"all";
    return catalog.entities.filter(entity=>(type==="all"||entity.type===type)&&(!query||entity.searchText.includes(query)));
  }

  function renderAdvancedDiagnostics(catalog){
    const target=$("entityCatalogDiagnostics");
    if(state.mode!=="advanced"||!catalog.diagnostics.length){target.innerHTML="";return}
    target.innerHTML=`<details class="entity-diagnostics"><summary>高级目录诊断</summary>${catalog.diagnostics.map(diagnostic=>`<div class="entity-diagnostic"><strong>${h(diagnostic.message||"目录校验提示")}</strong><p>错误码：${h(diagnostic.code||"未提供")}${diagnostic.path?`<br>位置：${h(diagnostic.path)}`:""}${diagnostic.hash?`<br>指纹：${h(diagnostic.hash)}`:""}</p></div>`).join("")}</details>`;
  }

  function renderEntitySelection(catalog,entities){
    const target=$("entityCatalogSelection");
    const selectedEntity=entities.find(entity=>entity.key===state.entityPreview.selectedKey);
    if(!selectedEntity){state.entityPreview.selectedKey="";target.hidden=true;target.innerHTML="";return}
    const familyLine=selectedEntity.type==="hero"?(selectedEntity.familyName?`所属家族：${h(selectedEntity.familyName)}`:"所属家族：名称待补充"):selectedEntity.type==="settlement"?(selectedEntity.settlementType?`聚落类型：${h(typeLabelSettlement(selectedEntity.settlementType))}`:"聚落类型：待补充")+(selectedEntity.boundSettlement?`　所属：${h(selectedEntity.boundSettlement)}`:"")+(selectedEntity.culture?`　文化：${h(selectedEntity.culture)}`:"")+(selectedEntity.ownerClan?`　归属：${h(selectedEntity.ownerClan)}`:""):selectedEntity.members.length?`成员：${h(selectedEntity.members.slice(0,6).join("、"))}${selectedEntity.members.length>6?` 等 ${selectedEntity.members.length} 人`:""}`:"成员名单待补充";
    target.hidden=false;
    target.innerHTML=`<strong>${h(selectedEntity.name)}</strong><p>类型：${h(typeLabel(selectedEntity.type))}${selectedEntity.duplicateName?' <span class="chip warn">同名</span>':""}</p><p>${familyLine}</p><p>${h(availabilityLabel(selectedEntity))}</p><p>本批次只用于预览，保存人物/家族权限将在后续契约批次开放。</p>`;
  }

  const entityPageSize=40;

  function entityLimit(){const value=Number(state.entityPreview.limit);return Number.isFinite(value)&&value>=entityPageSize?value:entityPageSize}
  function entityIncreaseLimit(){state.entityPreview.limit=entityLimit()+entityPageSize}

  function entityPage(allEntities){
    const limit=entityLimit();
    if(allEntities.length<=limit)return allEntities;
    const page=allEntities.slice(0,limit);
    const selected=allEntities.find(entity=>entity.key===state.entityPreview.selectedKey);
    if(selected&&!page.includes(selected))page.push(selected);
    return page;
  }

  function renderEntityCatalogNotice(title,detail,listDetail){
    panel.hidden=false;
    const controls=$("entityCatalogControls");if(controls)controls.hidden=true;
    const count=$("entityCatalogCount");if(count)count.textContent="";
    const note=$("entityCatalogNote");if(note)note.innerHTML=`<strong>${h(title)}：</strong>${h(detail)}`;
    const list=$("entityCatalogList");if(list)list.innerHTML=`<div class="empty"><strong>${h(title)}</strong><p>${h(listDetail||detail)}</p></div>`;
    const selection=$("entityCatalogSelection");if(selection){selection.hidden=true;selection.innerHTML=""}
  }

  function renderEntityCatalogMore(allEntities){
    const list=$("entityCatalogList");
    if(!list)return;
    const remaining=allEntities.length-entityLimit();
    if(remaining>0)list.innerHTML+=`<button type="button" class="mini primary" data-entity-action="more">继续加载（还有 ${remaining} 个）</button>`;
  }

  function renderEntityCatalog(){
    const catalog=readEntityCatalog();
    if(!catalog){
      if(state.catalogLoading===true){renderEntityCatalogNotice("人物/家族目录加载中","正在读取人物、家族和地点目录；参考档案编辑不受影响，读取完成后这里会自动刷新。","正在读取人物、家族和地点目录，请稍候。");return}
      if(state.catalogError){renderEntityCatalogNotice("人物/家族目录暂不可用","参考档案编辑、校验、预览和导出都不受影响；目录恢复后这里会自动刷新。","暂时无法读取人物/家族目录（"+(state.catalogError.code?state.catalogError.code+"：" :"")+(state.catalogError.message||"目录接口没有返回可用结果。")+"）");return}
      panel.hidden=true;return
    }
    panel.hidden=false;
    const controls=$("entityCatalogControls");
    const search=$("entityCatalogSearch");
    const type=$("entityCatalogType");
    const note=$("entityCatalogNote");
    const list=$("entityCatalogList");
    const count=$("entityCatalogCount");
    renderAdvancedDiagnostics(catalog);
    if(!catalog.available){
      controls.hidden=true;
      count.textContent="";
      note.innerHTML="<strong>人物/家族目录暂不可用。</strong>现有档案编辑仍可正常使用，目录恢复后可在这里进行作者侧预览。";
      list.innerHTML="<div class=\"empty\"><strong>暂时无法读取人物/家族目录</strong><p>本批次不会扫描游戏目录，也不会影响现有世界书编辑。</p></div>";
      $("entityCatalogSelection").hidden=true;
      return;
    }
    controls.hidden=false;
    if(search&&!(typeof document!=="undefined"&&document.activeElement===search)&&search.value!==(state.entityPreview.query||""))search.value=state.entityPreview.query||"";
    if(type&&type.value!==(state.entityPreview.type||"all"))type.value=state.entityPreview.type||"all";
    note.textContent="本批次只用于预览，保存人物/家族权限将在后续契约批次开放。";
    const allEntities=filteredEntities(catalog);
    const entities=entityPage(allEntities);
    count.textContent=allEntities.length>entityLimit()?`${allEntities.length} 个结果 · 已显示 ${entityLimit()} 个`:`${allEntities.length} 个结果`;
    if(!entities.length){
      list.innerHTML=catalog.entities.length?"<div class=\"empty\"><strong>没有找到匹配的人物或家族</strong><p>可以清除搜索条件或改用另一种类型。</p><button class=\"mini primary\" type=\"button\" data-entity-action=\"clear\">清除筛选</button></div>":"<div class=\"empty\"><strong>目录中暂无可预览实体</strong><p>请先检查实体目录生成和发布状态。</p></div>";
      renderEntitySelection(catalog,entities);
      return;
    }
    list.innerHTML=entities.map(entity=>`<button type="button" class="entity-card ${entity.key===state.entityPreview.selectedKey?"selected":""}" data-entity-index="${h(entity.key)}" aria-pressed="${entity.key===state.entityPreview.selectedKey}"><strong>${h(entity.name)}</strong><p>${entity.type==="hero"?(entity.familyName?`所属家族：${h(entity.familyName)}`:"所属家族：名称待补充"):entity.type==="settlement"?(entity.settlementType?`聚落类型：${h(typeLabelSettlement(entity.settlementType))}`:"聚落类型：待补充")+(entity.boundSettlement?`　所属：${h(entity.boundSettlement)}`:"")+(entity.culture?`　文化：${h(entity.culture)}`:""):entity.members.length?`成员：${h(entity.members.slice(0,3).join("、"))}${entity.members.length>3?" 等":""}`:"成员名单待补充"}</p><span class="chip">${h(typeLabel(entity.type))}</span>${entity.duplicateName?'<span class="chip warn">同名</span>':""}<span class="chip ${availabilityClass(entity)}">${h(availabilityLabel(entity))}</span></button>`).join("");
    renderEntitySelection(catalog,entities);
    renderEntityCatalogMore(allEntities);
  }

  let entitySearchTimer=null;
  function entityQueueSearch(value){
    if(entitySearchTimer!==null)clearTimeout(entitySearchTimer);
    entitySearchTimer=setTimeout(()=>{entitySearchTimer=null;state.entityPreview.query=value;state.entityPreview.limit=entityPageSize;renderEntityCatalog()},200);
  }
  $("entityCatalogSearch").addEventListener("input",event=>{entityQueueSearch(event.target.value)});
  $("entityCatalogType").addEventListener("change",event=>{state.entityPreview.type=event.target.value;state.entityPreview.limit=entityPageSize;renderEntityCatalog()});
  $("entityCatalogList").addEventListener("click",event=>{
    const actionButton=event.target.closest("[data-entity-action]");
    if(actionButton?.dataset.entityAction==="more"){entityIncreaseLimit();renderEntityCatalog();return}
    if(actionButton?.dataset.entityAction==="clear"){state.entityPreview.query="";state.entityPreview.type="all";state.entityPreview.selectedKey="";state.entityPreview.limit=entityPageSize;renderEntityCatalog();return}
    const entityButton=event.target.closest("[data-entity-index]");
    if(!entityButton)return;
    state.entityPreview.selectedKey=entityButton.dataset.entityIndex;
    renderEntityCatalog();
  });

  const baseRenderAll=renderAll;
  renderAll=function(){baseRenderAll();renderEntityCatalog()};
  renderEntityCatalog();
})();
