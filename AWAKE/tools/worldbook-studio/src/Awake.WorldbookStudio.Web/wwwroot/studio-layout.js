"use strict";
(function(){
  const shell=document.querySelector(".shell");
  if(!shell)return;
  const storageKey="awake.worldbook.studio.shell.layout.v1";
  const limits={left:[210,320],right:[280,420]};
  const layout={left:270,right:330};
  const clamp=(value,[min,max],fallback)=>Number.isFinite(Number(value))?Math.max(min,Math.min(max,Number(value))):fallback;
  const setValue=(side,value)=>{
    layout[side]=clamp(value,limits[side],layout[side]);
    shell.style.setProperty(side==="left"?"--shell-sidebar-width":"--shell-inspector-width",`${layout[side]}px`);
  };
  const save=()=>{try{localStorage.setItem(storageKey,JSON.stringify(layout))}catch{}};
  const apply=()=>{setValue("left",layout.left);setValue("right",layout.right);document.querySelectorAll("[data-shell-resizer]").forEach(node=>{const side=node.dataset.shellResizer;node.setAttribute("aria-valuenow",String(layout[side]));node.setAttribute("aria-valuemin",String(limits[side][0]));node.setAttribute("aria-valuemax",String(limits[side][1]))})};
  try{const saved=JSON.parse(localStorage.getItem(storageKey)||"");if(saved){layout.left=clamp(saved.left,limits.left,layout.left);layout.right=clamp(saved.right,limits.right,layout.right)}}catch{}
  document.querySelectorAll("[data-shell-resizer]").forEach(node=>{
    const side=node.dataset.shellResizer;
    let startX=0,startWidth=0;
    node.addEventListener("pointerdown",event=>{event.preventDefault();startX=event.clientX;startWidth=layout[side];node.classList.add("is-dragging");node.setPointerCapture?.(event.pointerId)});
    node.addEventListener("pointermove",event=>{if(!node.classList.contains("is-dragging"))return;const delta=event.clientX-startX;setValue(side,side==="left"?startWidth+delta:startWidth-delta);apply()});
    const end=event=>{if(!node.classList.contains("is-dragging"))return;node.classList.remove("is-dragging");node.releasePointerCapture?.(event.pointerId);save()};
    node.addEventListener("pointerup",end);node.addEventListener("pointercancel",end);
    node.addEventListener("keydown",event=>{if(!["ArrowLeft","ArrowRight"].includes(event.key))return;event.preventDefault();setValue(side,side==="left"?layout[side]+(event.key==="ArrowRight"?16:-16):layout[side]-(event.key==="ArrowRight"?16:-16));apply();save()});
  });
  apply();

  // 右侧检查栏：面板可折叠，减少堆叠拥挤
  const panels=[...document.querySelectorAll(".inspector > .panel")];
  const collapseKey="awake.worldbook.studio.inspector.collapsed.v1";
  let collapsed={};
  try{collapsed=JSON.parse(localStorage.getItem(collapseKey)||"{}")||{}}catch{collapsed={}}
  const panelKey=(panel,index)=>panel.id||panel.dataset.panelKey||("panel-"+index);
  const applyCollapsed=()=>panels.forEach((panel,index)=>{
    const head=panel.querySelector(":scope > h3, :scope > .head-row");
    if(!head)return;
    const key=panelKey(panel,index);
    panel.dataset.panelKey=key;
    const isCollapsed=collapsed[key]===true;
    panel.classList.toggle("is-collapsed",isCollapsed);
    head.setAttribute("role","button");
    head.setAttribute("tabindex","0");
    head.setAttribute("aria-expanded",String(!isCollapsed));
  });
  panels.forEach((panel,index)=>{
    const head=panel.querySelector(":scope > h3, :scope > .head-row");
    if(!head)return;
    const toggle=()=>{const key=panelKey(panel,index);collapsed[key]=!(collapsed[key]===true);panel.dataset.panelKey=key;try{localStorage.setItem(collapseKey,JSON.stringify(collapsed))}catch{}applyCollapsed()};
    head.addEventListener("click",event=>{if(event.target.closest("button,select,input,a"))return;toggle()});
    head.addEventListener("keydown",event=>{if(event.key==="Enter"||event.key===" "){event.preventDefault();toggle()}});
  });
  applyCollapsed();
})();
