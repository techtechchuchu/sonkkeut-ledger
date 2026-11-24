/* 제조현장의 암묵지 기록·활용 플랫폼 업무 포털 (시연용).
   기업에서 쓰는 지식 공유 시스템의 구조를 보여 준다: 대시보드 · 노하우 라이브러리 · 등록 · 검토·승인 · 보상 원장 · 3D 훈련.
   데이터는 이 브라우저의 저장소에만 있다. 실제 도입 시에는 회사 서버와 로그인으로 바뀌는 자리다.
   게시된 노하우는 3D 훈련(같은 주소의 /training)이 읽는 저장소(tacit-agent-v2)에 들어간다. */
(function () {
  "use strict";
  // 3D 태블릿 안에 띄운 화면(?embed=1)에서는 3D 메뉴와 소개 링크를 숨긴다 (portal.css 의 .embed)
  if (/[?&]embed=1/.test(location.search)) {
    document.documentElement.classList.add("embed");
    try { if (!sessionStorage.getItem("tacit-embed-opened")) { sessionStorage.setItem("tacit-embed-opened", "1"); history.replaceState(null, "", location.pathname + location.search + "#/agent"); } } catch (e) { /* 무시 */ }
  }
  const D = window.APP_DATA, SIM = window.SIM;
  const LS_RULES = "tacit-agent-v2", LS_QUEUE = "tacit-portal-queue", LS_LOG = "tacit-portal-log", LS_ROLE = "tacit-portal-role", LS_SESSION = "tacit-portal-session";
  const PROCS = ["injection", "welding", "mold"];
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s == null ? "" : s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const won = (v) => Math.round(v).toLocaleString("ko-KR") + "원";

  /* ---------- 저장소 ---------- */
  function jget(k, def) { try { const v = JSON.parse(localStorage.getItem(k)); return v == null ? def : v; } catch (e) { return def; } }
  function jset(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* 저장소를 못 쓰면 이번 화면에서만 유지 */ } }
  function seed() { return D.rules.filter((r) => PROCS.indexOf(r.process) >= 0).map((r) => Object.assign({}, r)); }
  function getRules() {
    let s = jget(LS_RULES, null);
    if (!Array.isArray(s) || !s.length) { s = seed(); jset(LS_RULES, s); }
    return s;
  }
  const setRules = (l) => jset(LS_RULES, l);
  const getQueue = () => jget(LS_QUEUE, []);
  const setQueue = (q) => jset(LS_QUEUE, q);

  /* ---------- 언어 (i18n.js). 한국어가 기본이고, 다른 언어는 번역이 있는 문구만 바뀐다 ---------- */
  const I = window.I18N || { langs: [{ code: "ko", label: "한국어" }], dict: {}, rules: {} };
  const LS_LANG = "tacit-portal-lang";
  let lang = jget(LS_LANG, "ko");
  try { const ql = new URLSearchParams(location.search).get("lang"); if (ql && (ql === "ko" || I.dict[ql])) { lang = ql; jset(LS_LANG, lang); } } catch (e) { /* 무시 */ }
  if (lang !== "ko" && !I.dict[lang]) lang = "ko";
  const fmt = (s, v) => String(s).replace(/\{(\w+)\}/g, (m, k) => (v && v[k] != null ? v[k] : m));
  const nko = () => (lang !== "ko" && I.dict[lang]) || null;
  // t(키, 한국어 문구, 값): 한국어면 그대로, 다른 언어면 번역 (번역이 없으면 한국어)
  const t = (key, ko, v) => { const d = nko(); const s = d && d.ui && d.ui[key]; return fmt(s != null ? s : ko, v); };
  const procName = (p) => { const d = nko(); return (d && d.proc && d.proc[p]) || D.processes[p].name; };
  const factorLabel = (k) => { const d = nko(); return (d && d.factor && d.factor[k]) || D.factors[k].label; };
  // 노하우 본문(증상·확인 순서·원인): 번역이 있으면 번역, 새로 등록한 노하우처럼 번역이 없으면 한국어 그대로
  const rt = (r, f) => { const d = nko(); const x = d && I.rules[lang] && I.rules[lang][r.id]; const k = { defectType: "d", action: "a", cause: "c" }[f]; return (x && x[k]) || r[f]; };
  function log(text) {
    const l = jget(LS_LOG, []);
    l.unshift({ t: Date.now(), text });
    jset(LS_LOG, l.slice(0, 30));
  }
  function nextId() {
    let max = 0;
    getRules().concat(getQueue()).forEach((r) => { const n = parseInt(String(r.id).replace(/\D/g, ""), 10); if (n > max) max = n; });
    return "TF-" + String(max + 1).padStart(3, "0");
  }
  const expertLabel = (id) => { const d = nko(); return (d && d.experts && d.experts[id]) || D.experts[id] || t("newExpert", "신규 등록 숙련자"); };
  function condText(r) {
    const f = D.factors[r.factor];
    const dc = nko(); if (dc && dc.cond && dc.cond[r.factor]) return fmt(dc.cond[r.factor], { n: r.threshold });
    return f.varLabel + " " + r.threshold + f.unit + (f.op === ">=" ? " 이상" : " 이하");
  }
  function hhmm(t) { const d = new Date(t); return String(d.getHours()).padStart(2, "0") + ":" + String(d.getMinutes()).padStart(2, "0"); }

  function toast(text, err) {
    const t = document.createElement("div");
    t.className = "toast" + (err ? " err" : "");
    t.setAttribute("role", "status");
    t.textContent = text;
    document.body.appendChild(t);
    setTimeout(() => t.classList.add("out"), 3800);
    setTimeout(() => t.remove(), 4150);
  }

  /* ---------- 역할 · 메뉴 ---------- */
  const ROLES = {
    admin: { name: "관리자", av: "관", user: "품질경영팀", sub: "관리자", menus: ["dash", "agent", "lib", "review", "ledger", "train"] },
    expert: { name: "숙련자", av: "박", user: "박 조장", sub: "숙련자 · 사출 27년차", menus: ["agent", "lib", "new", "ledger", "train"] },
    worker: { name: "현장 근로자", av: "김", user: "김민준", sub: "현장 근로자 · 입사 4주차", menus: ["agent", "lib", "train"] }
  };
  // 메뉴 아이콘 (24px 선 그림)
  const ICON = {
    agent: '<path d="M4.5 5.5A2 2 0 0 1 6.5 3.5h11a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H11l-4.5 4v-4h0a2 2 0 0 1-2-2z"/><path d="M8.5 8h7M8.5 11.5h4.5"/>',
    dash: '<rect x="3.5" y="3.5" width="7" height="9" rx="1.6"/><rect x="13.5" y="3.5" width="7" height="5" rx="1.6"/><rect x="13.5" y="12.5" width="7" height="8" rx="1.6"/><rect x="3.5" y="16.5" width="7" height="4" rx="1.6"/>',
    lib: '<path d="M4 5.6A1.6 1.6 0 0 1 5.6 4H11v16H5.6A1.6 1.6 0 0 1 4 18.4z"/><path d="M20 5.6A1.6 1.6 0 0 0 18.4 4H13v16h5.4a1.6 1.6 0 0 0 1.6-1.6z"/>',
    new: '<rect x="3.5" y="3.5" width="17" height="17" rx="4.5"/><path d="M12 8v8M8 12h8"/>',
    review: '<circle cx="12" cy="12" r="8.5"/><path d="M8.4 12.3l2.5 2.5 4.7-5"/>',
    ledger: '<path d="M6 3.5h12v17l-3-2-3 2-3-2-3 2z"/><path d="M9 8.5h6M9 12h6"/>',
    train: '<path d="M12 3l8 4.5v9L12 21l-8-4.5v-9z"/><path d="M12 12l8-4.5M12 12v9M12 12L4 7.5"/>'
  };
  const PAGES = {
    agent: { title: "현장 에이전트", desc: "용어를 몰라도 본 그대로 적으면, 숙련자가 남긴 확인 순서를 찾아 드립니다.", render: pAgent },
    dash: { title: "대시보드", desc: "게시된 노하우와 활용 현황을 한눈에 봅니다.", render: pDash },
    lib: { title: "노하우 라이브러리", desc: "현장에서 쓰는 확인 순서를 증상과 공정으로 찾습니다.", render: pLib },
    new: { title: "노하우 등록", desc: "숙련자의 판단을 증상, 확인 순서, 원인 순서로 남깁니다.", render: pNew },
    review: { title: "검토·승인", desc: "등록되거나 공유된 노하우를 확인하고 게시 여부를 정합니다.", render: pReview },
    ledger: { title: "공유·보상 원장", desc: "활용 횟수로 원작성자의 기여를 계산하고 인센티브 재원을 나눕니다.", render: pLedger },
    train: { title: "3D 현장 훈련", desc: "3D 공장에서 야간 교대를 체험합니다.", render: pTrain }
  };
  let role = jget(LS_ROLE, "admin");
  if (!ROLES[role]) role = "admin";
  // 소개 페이지의 링크에서 역할을 정해 바로 들어올 수 있다 (예: portal.html?role=worker#/lib)
  try { const qr = new URLSearchParams(location.search).get("role"); if (qr && ROLES[qr]) { role = qr; jset(LS_ROLE, role); } } catch (e) { /* 무시 */ }

  function route() {
    const m = location.hash.match(/^#\/(\w+)/);
    const menus = ROLES[role].menus;
    return m && menus.indexOf(m[1]) >= 0 ? m[1] : menus[0];
  }

  // 역할 이름·사용자 표기, 페이지 제목·설명 (번역이 있으면 번역)
  const roleOf = (r) => { const d = nko(); return d && d.roles && d.roles[r] ? d.roles[r] : ROLES[r]; };
  const pageText = (k) => { const d = nko(); const p = d && d.pages && d.pages[k]; return { title: (p && p.title) || PAGES[k].title, desc: (p && p.desc) || PAGES[k].desc }; };
  // 고정 문구(data-i18n)와 역할 버튼, 언어 선택
  function applyStatic() {
    document.documentElement.lang = lang;
    document.querySelectorAll("[data-i18n]").forEach((el) => { if (el.dataset.ko == null) el.dataset.ko = el.textContent; el.textContent = t(el.dataset.i18n, el.dataset.ko); });
    document.querySelectorAll("[data-i18n-ph]").forEach((el) => { if (el.dataset.ko == null) el.dataset.ko = el.getAttribute("placeholder") || ""; el.setAttribute("placeholder", t(el.dataset.i18nPh, el.dataset.ko)); });
    document.querySelectorAll(".role button").forEach((b) => { b.textContent = roleOf(b.dataset.role).name; });
    const sel = $("lang");
    if (sel) {
      if (!sel.options.length) {
        sel.innerHTML = I.langs.filter((l) => l.code === "ko" || I.dict[l.code]).map((l) => "<option value='" + l.code + "'>" + l.label + "</option>").join("");
        sel.addEventListener("change", () => { lang = sel.value; jset(LS_LANG, lang); render(); });
      }
      sel.value = lang;
      sel.hidden = sel.options.length < 2;
    }
  }

  function renderShell() {
    const cur = route();
    const pending = getQueue().filter((q) => q.status === "review").length;
    $("menu").innerHTML = ROLES[role].menus.map((k) =>
      "<a href='#/" + k + "'" + (k === cur ? " aria-current='page'" : "") + "><svg viewBox='0 0 24 24' aria-hidden='true'>" + ICON[k] + "</svg>" + pageText(k).title +
      (k === "review" && pending ? "<span class='count'>" + pending + "</span>" : "") + "</a>").join("");
    document.querySelectorAll(".role button").forEach((b) => b.setAttribute("aria-pressed", b.dataset.role === role ? "true" : "false"));
    $("who").innerHTML = "<span class='av'>" + ROLES[role].av + "</span><span class='wn'><b>" + esc(roleOf(role).user) + "</b><small>" + esc(roleOf(role).sub) + "</small></span>";
    $("page-title").textContent = pageText(cur).title;
    applyStatic();
    document.title = pageText(cur).title + " · 울산정밀 업무 포털";
    $("side").classList.remove("open");
  }

  function render() {
    renderShell();
    const cur = route();
    $("view").innerHTML = "<div class='ph'><div><h1>" + pageText(cur).title + "</h1><p>" + pageText(cur).desc + "</p></div></div><div id='page-body'></div>";
    PAGES[cur].render($("page-body"));
    animateNumbers($("page-body"));
    window.scrollTo(0, 0);
  }

  // 숫자가 0에서 올라가며 자리를 잡는다 (data-count). 움직임을 줄인 설정이면 바로 최종값을 보여 준다
  function animateNumbers(root) {
    const reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    root.querySelectorAll("[data-count]").forEach((el) => {
      const to = parseFloat(el.dataset.count), dec = parseInt(el.dataset.dec || "0", 10), suf = el.dataset.suffix || "", isWon = el.dataset.won === "1";
      const fmt = (v) => (isWon ? Math.round(v).toLocaleString("ko-KR") : v.toFixed(dec)) + suf;
      if (reduce) { el.textContent = fmt(to); return; }
      const t0 = performance.now(), dur = 900;
      el.textContent = fmt(0);
      const step = (t) => { const k = Math.min(1, (t - t0) / dur), e = 1 - Math.pow(1 - k, 3); el.textContent = fmt(to * e); if (k < 1) requestAnimationFrame(step); };
      requestAnimationFrame(step);
    });
  }

  // 상단 검색: 노하우 라이브러리에서 바로 찾아 준다
  $("gs").addEventListener("submit", (e) => {
    e.preventDefault();
    libState.q = $("gs-in").value;
    $("gs-in").value = "";
    if (location.hash === "#/lib") render(); else location.hash = "#/lib";
  });

  document.querySelectorAll(".role button").forEach((b) => b.addEventListener("click", () => {
    role = b.dataset.role; jset(LS_ROLE, role);
    try { localStorage.setItem(LS_SESSION, role); } catch (e) { /* 무시 */ }
    if (ROLES[role].menus.indexOf(route()) < 0) location.hash = "#/" + ROLES[role].menus[0];
    render();
  }));
  window.addEventListener("hashchange", () => { if (!/share=/.test(location.hash)) render(); });
  $("menu-btn").addEventListener("click", () => $("side").classList.toggle("open"));

  /* ---------- 공통 조각 카드 ---------- */
  function statusTag(s) {
    if (s === "review") return "<span class='tag w'>검토 대기</span>";
    if (s === "approved") return "<span class='tag g'>게시됨</span>";
    if (s === "rejected") return "<span class='tag r'>반려</span>";
    return "";
  }
  function fragCard(r, opts) {
    opts = opts || {};
    return "<article class='card frag' style='--i:" + (opts.i || 0) + "'>" +
      "<div class='frag-top'><b>" + esc(r.id) + "</b><span class='tag'>" + esc(procName(r.process)) + "</span>" +
      (r.active === false ? "<span class='tag r'>" + t("stopped", "게시 중단") + "</span>" : "") + (opts.status ? statusTag(opts.status) : "") + "</div>" +
      "<h3>" + esc(rt(r, "defectType")) + "</h3>" +
      "<div class='cond'>" + t("cond", "발생 상황:") + " " + esc(condText(r)) + "</div>" +
      "<div class='act'>" + esc(rt(r, "action")) + "</div>" +
      (r.cause ? "<div class='cause'>" + t("causeLbl", "원인:") + " " + esc(rt(r, "cause")) + "</div>" : "") +
      (opts.extra || "") +
      "<div class='foot'><span>" + esc(r.author) + " · " + esc(expertLabel(r.author)) + "</span><span class='sp'></span>" +
      (opts.noUses ? "" : "<span class='tag o'>" + t("uses", "활용 {n}회", { n: r.uses || 0 }) + "</span>") +
      (opts.noDetail ? "" : "<button type='button' class='btn sm' data-detail='" + esc(r.id) + "'>" + t("detailBtn", "상세") + "</button>") +
      "</div></article>";
  }

  /* ---------- 1. 대시보드 ---------- */
  function pDash(el) {
    const rules = getRules(), q = getQueue();
    const live = rules.filter((r) => r.active !== false);
    const uses = rules.reduce((s, r) => s + (r.uses || 0), 0);
    const pending = q.filter((x) => x.status === "review").length;
    const inp = { process: "injection", temp: 12, humidity: 72, age: 15, tenure: 4, onboardWeeks: D.processes.injection.onboardWeeks,
      attrition: 36.6, hires: 3, volume: 40000, unitLoss: 1200, hourLoss: 150000, replaceCost: 8000000, onboardCap: 40, hazardRatio: 2.5 };
    const res = SIM.simulate(D, inp, live);
    const std = res.std.now.defect, tac = res.tacit.now.defect, diff = std - tac;
    const top = rules.slice().sort((a, b) => b.uses - a.uses).slice(0, 5);
    const maxU = Math.max(1, top[0] ? top[0].uses : 1);
    const lg = jget(LS_LOG, []).slice(0, 8);

    el.innerHTML =
      "<div class='dash-top'>" +
      "<section class='dash-hero'>" +
      "<div class='dh-col'><small>오늘 밤 예상 불량률</small><b class='dh-big' data-count='" + tac.toFixed(1) + "' data-dec='1' data-suffix='%'>" + tac.toFixed(1) + "%</b>" +
      "<span class='dh-delta'>" + (diff > 0.05 ? "작업표준서만 볼 때보다 −" + diff.toFixed(1) + "%p" : "작업표준서와 차이 없음") + "</span></div>" +
      "<div class='dh-col'><small>누적 활용</small><b class='dh-mid' data-count='" + uses + "' data-suffix='회'>" + uses + "회</b><span class='dh-delta muted'>현장 근로자가 따른 횟수</span></div>" +
      "<p class='dh-note'>사출 1라인 · 입사 4주차 · 외기 12℃ · 습도 72% 기준의 예시 가정값</p>" +
      "</section>" +
      "<div class='dash-side'>" +
      "<div class='dash-mini'><small>게시된 노하우</small><b data-count='" + live.length + "' data-suffix='건'>" + live.length + "건</b></div>" +
      "<div class='dash-mini" + (pending ? " on" : "") + "'><small>검토 대기</small><b data-count='" + pending + "' data-suffix='건'>" + pending + "건</b></div>" +
      "</div></div>" +
      "<section class='card dash-chart'><h2>활용이 많은 노하우</h2>" +
      "<div class='vbars'>" + top.map((r, i) =>
        "<div class='vb'><span class='vb-v'>" + r.uses + "회</span><div class='vb-bar" + (i === 0 ? " top" : "") + "' style='--i:" + i + ";height:" + Math.max(8, Math.round(r.uses / maxU * 150)) + "px'></div>" +
        "<span class='vb-l'>" + esc(r.id) + "</span><span class='vb-s'>" + esc(r.defectType) + "</span></div>").join("") +
      "</div></section>" +
      "<details class='card dash-more'><summary>최근 활동 보기</summary>" +
      (lg.length ? "<ul class='log'>" + lg.map((x) => "<li><time>" + hhmm(x.t) + "</time><span>" + esc(x.text) + "</span></li>").join("") + "</ul>" : "<p class='hint'>아직 활동이 없어요.</p>") +
      "</details>";
  }

  /* ---------- 2. 노하우 라이브러리 ---------- */
  const libState = { q: "", proc: "", factor: "", sort: "uses" };
  const AGENT_SAMPLES = ["하얀 물결 같은 게 생겨요", "제품이 휘어졌어요", "표면이 움푹 꺼졌어요"];
  function pLib(el) {
    el.innerHTML =
      (lang !== "ko" && nko() && nko().notice ? "<p class='lang-note'>" + esc(nko().notice) + "</p>" : "") +
      "<div class='toolbar'>" +
      "<input type='search' id='lib-q' placeholder='" + esc(t("libSearch", "증상·확인 순서·작성자로 검색")) + "' value='" + esc(libState.q) + "'>" +
      "<select id='lib-proc' aria-label='공정'><option value=''>" + t("allProc", "모든 공정") + "</option>" + PROCS.map((p) => "<option value='" + p + "'" + (libState.proc === p ? " selected" : "") + ">" + esc(procName(p)) + "</option>").join("") + "</select>" +
      "<select id='lib-factor' aria-label='발생 상황'><option value=''>" + t("allCond", "모든 발생 상황") + "</option>" + Object.keys(D.factors).map((k) => "<option value='" + k + "'" + (libState.factor === k ? " selected" : "") + ">" + esc(factorLabel(k)) + "</option>").join("") + "</select>" +
      "<select id='lib-sort' aria-label='정렬'><option value='uses'" + (libState.sort === "uses" ? " selected" : "") + ">" + t("sortUses", "활용 많은 순") + "</option><option value='new'" + (libState.sort === "new" ? " selected" : "") + ">" + t("sortNew", "최근 등록순") + "</option></select>" +
      "</div><div class='grid cards' id='lib-list'></div>";
    const draw = () => {
      let list = getRules().filter((r) => role === "admin" || r.active !== false);
      const q = libState.q.trim().toLowerCase();
      if (q) list = list.filter((r) => [r.id, r.defectType, r.action, r.cause, rt(r, "defectType"), rt(r, "action"), rt(r, "cause"), r.author, expertLabel(r.author)].join(" ").toLowerCase().indexOf(q) >= 0);
      if (libState.proc) list = list.filter((r) => r.process === libState.proc);
      if (libState.factor) list = list.filter((r) => r.factor === libState.factor);
      list.sort(libState.sort === "uses" ? (a, b) => b.uses - a.uses : (a, b) => (b.id > a.id ? 1 : -1));
      $("lib-list").innerHTML = list.length ? list.map((r, i) => fragCard(r, { i })).join("") : "<div class='card empty' style='grid-column:1/-1'>" + t("empty", "조건에 맞는 노하우가 없어요.") + "</div>";
    };
    $("lib-q").addEventListener("input", (e) => { libState.q = e.target.value; draw(); });
    $("lib-proc").addEventListener("change", (e) => { libState.proc = e.target.value; draw(); });
    $("lib-factor").addEventListener("change", (e) => { libState.factor = e.target.value; draw(); });
    $("lib-sort").addEventListener("change", (e) => { libState.sort = e.target.value; draw(); });
    draw();
  }

  /* ---------- 현장 에이전트: 3D 태블릿에서 보던 대화 화면을 플랫폼 안에 그대로 둔다 ----------
     본 그대로 적으면 증상 사전으로 알아듣고, 숙련자가 남긴 노하우 카드를 대화 안에 보여 준다.
     카드의 '이 순서대로 점검했어요'가 활용 기록이고, 3D 공장은 이 기록을 읽어 그 노하우를 적용한 것으로 생산한다.
     (데모는 표현 사전으로 찾고, 실제 서비스에서는 언어 모델이 찾는다) */
  const agentChat = [];
  const EMBED = document.documentElement.classList.contains("embed");
  function agentHello() {
    if (role === "expert") return "조장님, 현장 에이전트예요. 남기실 노하우가 있으면 아래 '노하우 남기기'를 눌러 주세요. 증상을 적으면 이미 남긴 노하우도 찾아 드려요.";
    if (role === "admin") return "현장 에이전트예요. 현장 근로자가 보는 화면 그대로예요. 증상을 본 그대로 적으면 게시된 노하우를 찾아 드려요.";
    return "김민준 님, 현장 에이전트예요. 용어를 몰라도 본 그대로 적어 주세요. 조장님들이 남긴 확인 순서를 찾아 드려요.";
  }
  function agentCard(r) {
    return "<article class='ap-card'><div class='ap-top'><b>" + esc(r.id) + "</b><span class='tag'>" + esc(procName(r.process)) + "</span><span class='sp'></span><span class='tag o'>" + t("uses", "활용 {n}회", { n: r.uses || 0 }) + "</span></div>" +
      "<h3>" + esc(rt(r, "defectType")) + "</h3>" +
      "<div class='cond'>" + t("cond", "발생 상황:") + " " + esc(condText(r)) + "</div>" +
      "<div class='act'>" + esc(rt(r, "action")) + "</div>" +
      (r.cause ? "<div class='cause'>" + t("causeLbl", "원인:") + " " + esc(rt(r, "cause")) + "</div>" : "") +
      "<div class='ap-foot'><span>" + esc(r.author) + " · " + esc(expertLabel(r.author)) + "</span><span class='sp'></span>" +
      "<button type='button' class='btn sm' data-detail='" + esc(r.id) + "'>" + t("detailBtn", "상세") + "</button>" +
      (role === "worker" ? "<button type='button' class='btn sm good' data-agent-follow='" + esc(r.id) + "'>이 순서대로 점검했어요</button>" : "") +
      "</div></article>";
  }
  function pAgent(el) {
    if (!agentChat.length) agentChat.push({ k: "ag", text: agentHello() });
    const has = (k) => ROLES[role].menus.indexOf(k) >= 0;
    el.innerHTML = "<section class='agentpad'>" +
      "<div class='ap-msgs' id='ap-msgs' aria-live='polite'></div>" +
      "<div class='ap-chips' id='ap-chips'>" +
      AGENT_SAMPLES.map((x) => "<button type='button' class='chip' data-say='" + esc(x) + "'>" + esc(x) + "</button>").join("") +
      (has("new") ? "<a class='chip nav' href='#/new'>노하우 남기기</a>" : "") +
      (has("ledger") ? "<a class='chip nav' href='#/ledger'>보상 원장</a>" : "") +
      "<a class='chip nav' href='#/lib'>라이브러리 전체</a>" +
      "<button type='button' class='chip nav' data-agent-reset>처음부터</button></div>" +
      "<form class='ap-form' id='ap-form'><input id='ap-in' type='text' autocomplete='off' maxlength='120' placeholder='예: 하얀 물결 같은 게 생겨요' aria-label='현장 에이전트에게 말하기'><button type='submit' class='btn primary'>보내기</button></form>" +
      "</section>";
    const box = $("ap-msgs");
    const draw = () => {
      const rules = getRules();
      box.innerHTML = agentChat.map((m) => {
        if (m.k === "me") return "<div class='ap-msg me'>" + esc(m.text) + "</div>";
        if (m.k === "note") return "<div class='ap-msg note'>" + esc(m.text) + "</div>";
        if (m.k === "cards") return "<div class='ap-cards'>" + m.ids.map((id) => rules.find((r) => r.id === id)).filter(Boolean).map(agentCard).join("") + "</div>";
        return "<div class='ap-msg ag'>" + esc(m.text) + (m.small ? "<small>" + esc(m.small) + "</small>" : "") + "</div>";
      }).join("");
      box.scrollTop = box.scrollHeight;
    };
    const ask = (text) => {
      text = String(text || "").trim(); if (!text) return;
      agentChat.push({ k: "me", text });
      let hit = null; D.symptoms.forEach((x) => { if (!hit && new RegExp(x.words).test(text)) hit = x; });
      const live = getRules().filter((r) => r.active !== false);
      const list = hit ? live.filter((r) => r.id === hit.rule || r.defectType === hit.name) : [];
      if (hit && list.length) {
        agentChat.push({ k: "ag", text: "'" + text + "' → '" + hit.name + "' 증상으로 이해했어요. 조장님들이 남긴 확인 순서가 " + list.length + "건 있어요. 조건은 건드리지 말고 아래 순서대로 확인하세요.", small: "예시 동작이에요. 데모는 표현 사전으로 찾고, 실제 서비스에서는 언어 모델이 찾아요." });
        agentChat.push({ k: "cards", ids: list.map((r) => r.id) });
      } else {
        agentChat.push({ k: "ag", text: "아직 찾지 못했어요. 본 그대로 조금만 더 적어 주세요. 예: 하얀 물결 같은 게 생겨요 / 제품이 휘어졌어요 / 표면이 움푹 꺼졌어요" });
      }
      draw();
    };
    $("ap-form").addEventListener("submit", (e) => { e.preventDefault(); const v = $("ap-in").value; $("ap-in").value = ""; ask(v); });
    $("ap-chips").addEventListener("click", (e) => {
      const c = e.target.closest("[data-say]"); if (c) { ask(c.dataset.say); return; }
      if (e.target.closest("[data-agent-reset]")) { agentChat.length = 0; agentChat.push({ k: "ag", text: agentHello() }); draw(); }
    });
    box.addEventListener("click", (e) => {
      const fw = e.target.closest("[data-agent-follow]"); if (!fw) return;
      // 활용 1회: 확인 순서대로 점검했다고 기록하면 원작성자의 활용 횟수가 오른다 (원장과 3D 공장에 그대로 반영)
      const list = getRules(), r = list.find((x) => x.id === fw.dataset.agentFollow);
      if (!r) return;
      r.uses = (r.uses || 0) + 1; setRules(list);
      log("김민준 씨가 " + r.id + " (" + r.defectType + ") 순서대로 점검 → " + r.author + " 활용 " + r.uses + "회");
      agentChat.push({ k: "note", text: r.id + " 활용을 기록했어요. " + r.author + "님의 활용 횟수가 " + r.uses + "회가 됐어요." + (EMBED ? " 태블릿을 내려놓고 100쇼트를 생산해 보세요." : "") });
      draw();
    });
    draw();
  }

  // 상세 창
  document.addEventListener("click", (e) => {
    const b = e.target.closest("[data-detail]");
    if (b) openDetail(b.dataset.detail);
  });
  function openDetail(id) {
    const r = getRules().find((x) => x.id === id) || getQueue().find((x) => x.id === id);
    if (!r) return;
    const dlg = $("detail");
    dlg.innerHTML = "<div class='inner'><div class='frag-top'><b>" + esc(r.id) + "</b><span class='tag'>" + esc(procName(r.process)) + "</span>" +
      (r.status ? statusTag(r.status) : (r.active === false ? "<span class='tag r'>" + t("stopped", "게시 중단") + "</span>" : "<span class='tag g'>" + t("live", "게시 중") + "</span>")) + "</div>" +
      "<h2 style='margin:0'>" + esc(rt(r, "defectType")) + "</h2>" +
      "<dl><dt>" + t("dCond", "발생 상황") + "</dt><dd>" + esc(condText(r)) + "</dd><dt>" + t("dAction", "확인 순서") + "</dt><dd>" + esc(rt(r, "action")) + "</dd><dt>" + t("dCause", "원인") + "</dt><dd>" + esc(r.cause ? rt(r, "cause") : t("dNone", "기록 없음")) + "</dd>" +
      "<dt>" + t("dAuthor", "원작성자") + "</dt><dd>" + esc(r.author) + " · " + esc(expertLabel(r.author)) + "</dd><dt>" + t("dUses", "활용 횟수") + "</dt><dd>" + t("dUsesVal", "{n}회", { n: r.uses || 0 }) + "</dd>" +
      "<dt>" + t("dEffect", "예상 효과") + "</dt><dd>" + t("dEffectVal", "불량 완화 {a}p · 다운타임 완화 {b}p (현장 데이터가 쌓이면 보정되는 추정값)", { a: r.mDefect, b: r.mDown }) + "</dd>" +
      (lang !== "ko" && rt(r, "action") !== r.action ? "<dt>" + t("dKo", "한국어 원문") + "</dt><dd class='ko-src'>" + esc(r.defectType) + " — " + esc(r.action) + "</dd>" : "") +
      (r.reason ? "<dt>반려 사유</dt><dd>" + esc(r.reason) + "</dd>" : "") + "</dl>" +
      "<div class='actions' style='display:flex;gap:8px;flex-wrap:wrap'>" +
      (role === "worker" && !r.status && r.active !== false ? "<button type='button' class='btn good' data-followed='" + esc(r.id) + "'>" + t("followBtn", "이 순서대로 점검했어요 (활용 기록)") + "</button>" : "") +
      "<button type='button' class='btn primary' data-copy='" + esc(r.id) + "'>" + t("copyBtn", "링크 복사") + "</button>" +
      (role === "admin" && !r.status ? "<button type='button' class='btn' data-toggle='" + esc(r.id) + "'>" + (r.active === false ? "다시 게시" : "게시 중단") + "</button>" : "") +
      "<button type='button' class='btn' data-close>" + t("closeBtn", "닫기") + "</button></div></div>";
    if (!dlg.open) dlg.showModal();
  }
  $("detail").addEventListener("click", (e) => {
    if (e.target === $("detail") || e.target.closest("[data-close]")) { $("detail").close(); return; }
    const c = e.target.closest("[data-copy]");
    if (c) copyLink(c.dataset.copy);
    const fw = e.target.closest("[data-followed]");
    if (fw) {
      // 활용 1회: 신입이 확인 순서대로 점검했다고 기록하면 원작성자의 활용 횟수가 오른다 (원장에 그대로 반영)
      const list = getRules(), r = list.find((x) => x.id === fw.dataset.followed);
      if (r) { r.uses = (r.uses || 0) + 1; setRules(list); log("김민준 씨가 " + r.id + " (" + r.defectType + ") 순서대로 점검 → " + r.author + " 활용 " + r.uses + "회"); toast(lang !== "ko" ? t("followToast", "", { id: r.id, au: r.author, n: r.uses }) : r.id + " 활용을 기록했어요. " + r.author + "님의 활용 횟수가 " + r.uses + "회가 됐어요."); openDetail(r.id); }
    }
    const tg = e.target.closest("[data-toggle]");
    if (tg) {
      const list = getRules(), r = list.find((x) => x.id === tg.dataset.toggle);
      if (r) { r.active = r.active === false; setRules(list); log(r.id + (r.active ? " 다시 게시" : " 게시 중단")); $("detail").close(); render(); }
    }
  });

  /* ---------- 공유 링크 (서버 없이 링크에 내용을 담는다) ---------- */
  const SHARE_KEYS = ["process", "factor", "threshold", "action", "defectType", "cause", "author"];
  function encode(r) {
    const o = {}; SHARE_KEYS.forEach((k) => { o[k] = r[k]; });
    return btoa(unescape(encodeURIComponent(JSON.stringify(o)))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  }
  function decode(code) {
    let b = String(code).replace(/-/g, "+").replace(/_/g, "/");
    while (b.length % 4) b += "=";
    return JSON.parse(decodeURIComponent(escape(atob(b))));
  }
  const codeOf = (text) => { const m = String(text).match(/share=([A-Za-z0-9_\-]+)/); return m ? m[1] : String(text).trim(); };
  const clip = (s, n) => String(s == null ? "" : s).slice(0, n);
  function copyLink(id) {
    const r = getRules().find((x) => x.id === id) || getQueue().find((x) => x.id === id);
    if (!r) return;
    const url = location.origin + location.pathname + "#share=" + encode(r);
    const done = () => toast(r.id + " 링크를 복사했어요. 팀원에게 보내 주세요.");
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(url).then(done, () => window.prompt("이 링크를 복사하세요", url));
    else window.prompt("이 링크를 복사하세요", url);
  }
  // 공유받은 조각은 바로 게시하지 않고 검토 대기로 들어간다
  function importShared(code) {
    let o;
    try { o = decode(code); } catch (e) { return { ok: false, msg: "링크나 코드를 읽지 못했어요. 끝까지 복사했는지 확인해 주세요." }; }
    if (!o || PROCS.indexOf(o.process) < 0 || !D.factors[o.factor] || !isFinite(Number(o.threshold)) || !o.action || !o.defectType)
      return { ok: false, msg: "이 포털의 노하우 형식이 아니에요." };
    const act = clip(o.action, 200), dt = clip(o.defectType, 30), au = clip(o.author, 12) || "미지정";
    if (getRules().concat(getQueue()).some((r) => r.action === act && r.defectType === dt && r.author === au))
      return { ok: true, dup: true, msg: "이미 가지고 있는 노하우예요." };
    const item = { id: nextId(), process: o.process, factor: o.factor, threshold: Number(o.threshold), action: act, defectType: dt, cause: clip(o.cause, 80),
      mDefect: 0.4, mDown: 0.2, author: au, uses: 0, active: true, status: "review", source: "공유받음", at: Date.now() };
    const q = getQueue(); q.unshift(item); setQueue(q);
    log("공유받은 노하우 " + item.id + " (" + item.defectType + ") 검토 대기");
    return { ok: true, msg: item.id + " (" + item.defectType + ")을(를) 검토 대기로 받았어요." };
  }

  /* ---------- 3. 노하우 등록 ---------- */
  const SAMPLE = { process: "injection", defectType: "은줄·기포", factor: "humidity", threshold: 70,
    action: "건조기 온도·시간이 표준을 채웠는지 보고, 원료 포대 개봉 시간을 확인한다. 조건은 건드리지 않는다", cause: "원료가 습기를 먹음",
    speech: "장마철 밤에 제품에 하얀 줄 생기면 건조기부터 봐. 온도랑 시간 표준 채웠는지 보고, 원료 포대 언제 뜯었는지 확인해. 대개 원료가 습기 먹은 거야. 조건은 건드리지 말고." };
  function pNew(el) {
    el.innerHTML =
      "<section class='card'><h2>새 노하우 남기기</h2>" +
      "<p class='hint'>증상 → 확인 순서 → 원인 순서로 적어 주세요. 등록하면 <b>검토 대기</b>로 들어가고, 관리자가 승인하면 라이브러리와 3D 훈련에 게시돼요.</p>" +
      "<form class='form' id='nf'>" +
      "<div class='actions'><button type='button' class='btn' id='nf-sample'>30초 말하기 예시 넣기</button></div>" +
      "<p class='hint' id='nf-speech' style='display:none'></p>" +
      "<div class='row2'><label>공정<select id='nf-proc'>" + PROCS.map((p) => "<option value='" + p + "'>" + esc(D.processes[p].name) + "</option>").join("") + "</select></label>" +
      "<label>증상<small>눈에 보인 그대로</small><input id='nf-def' required maxlength='30' placeholder='예: 하얀 물결 같은 줄(은줄)'></label></div>" +
      "<label>확인 순서<small>무엇부터 보는지. 설정값을 바꾸라는 지시는 적지 않아요</small><textarea id='nf-act' required maxlength='200' rows='3' placeholder='예: 건조기 온도·시간을 먼저 확인하고 원료 포대 개봉 시간을 본다'></textarea></label>" +
      "<label>원인<input id='nf-cause' maxlength='80' placeholder='예: 원료가 습기를 먹음'></label>" +
      "<div class='row2'><label>이런 때 주로 생겨요<select id='nf-factor'>" + Object.keys(D.factors).map((k) => "<option value='" + k + "'>" + esc(D.factors[k].label) + "</option>").join("") + "</select></label>" +
      "<label><span id='nf-thlabel'></span><input id='nf-th' type='number' required step='1'></label></div>" +
      "<label>원작성자<small>보상은 이 이름으로 쌓여요</small><input id='nf-author' list='nf-experts' maxlength='12' value='" + (role === "expert" ? "S-01" : "") + "' placeholder='예: S-01' required></label>" +
      "<datalist id='nf-experts'>" + Object.keys(D.experts).map((k) => "<option value='" + k + "'>" + esc(D.experts[k]) + "</option>").join("") + "</datalist>" +
      "<label class='check'><input type='checkbox' id='nf-ok' required><span>현장 승인 범위를 확인했어요. 이 노하우는 <b>확인 순서</b>를 안내할 뿐, 설비 설정값을 바꾸라는 지시가 아니에요.</span></label>" +
      "<div class='actions'><button type='submit' class='btn primary'>검토 요청</button></div></form></section>" +
      "<section class='card' style='margin-top:16px'><h2>내 등록 현황</h2><div class='scroll' id='nf-mine'></div></section>";
    const unit = () => {
      const f = D.factors[$("nf-factor").value];
      $("nf-thlabel").textContent = f.varLabel + " " + f.unit + (f.op === ">=" ? " 이상일 때" : " 이하일 때");
    };
    $("nf-factor").addEventListener("change", unit); unit();
    $("nf-sample").addEventListener("click", () => {
      $("nf-proc").value = SAMPLE.process; $("nf-def").value = SAMPLE.defectType; $("nf-act").value = SAMPLE.action; $("nf-cause").value = SAMPLE.cause;
      $("nf-factor").value = SAMPLE.factor; $("nf-th").value = SAMPLE.threshold; if (!$("nf-author").value) $("nf-author").value = "S-01"; unit();
      const sp = $("nf-speech"); sp.style.display = ""; sp.innerHTML = "<b>말씀:</b> “" + esc(SAMPLE.speech) + "”<br><small>실제 서비스에서는 언어 모델이 이 말을 아래 구조로 정리하고, 숙련자가 확인한 것만 등록돼요.</small>";
    });
    $("nf").addEventListener("submit", (e) => {
      e.preventDefault();
      const item = { id: nextId(), process: $("nf-proc").value, factor: $("nf-factor").value, threshold: Number($("nf-th").value),
        action: $("nf-act").value.trim(), defectType: $("nf-def").value.trim(), cause: $("nf-cause").value.trim(),
        mDefect: 0.4, mDown: 0.2, author: $("nf-author").value.trim() || "미지정", uses: 0, active: true, status: "review", source: "등록", at: Date.now() };
      const q = getQueue(); q.unshift(item); setQueue(q);
      log(item.author + "님이 " + item.id + " (" + item.defectType + ") 등록 요청");
      toast(item.id + " 검토를 요청했어요.");
      render();
    });
    const mine = getQueue().filter((x) => x.source === "등록");
    $("nf-mine").innerHTML = mine.length
      ? "<table><thead><tr><th>ID</th><th>증상</th><th>공정</th><th>상태</th><th>비고</th></tr></thead><tbody>" +
        mine.map((x) => "<tr><td>" + esc(x.id) + "</td><td>" + esc(x.defectType) + "</td><td>" + esc(D.processes[x.process].name) + "</td><td>" + statusTag(x.status) + "</td><td>" + esc(x.reason || "") + "</td></tr>").join("") + "</tbody></table>"
      : "<p class='hint'>아직 등록한 노하우가 없어요.</p>";
  }

  /* ---------- 4. 검토·승인 ---------- */
  const CHANGE_RE = /(온도|압력|속도|시간|조건|설정|값)[^.。]{0,12}(올려|내려|높여|낮춰|높이|낮추|변경|바꿔|조정해)/;
  // 처음 열었을 때 검토 화면이 비어 보이지 않도록, 예시 노하우 한 건을 딱 한 번만 넣어 둔다
  function seedExampleOnce() {
    let done = false;
    try { done = localStorage.getItem("tacit-portal-example") === "1"; localStorage.setItem("tacit-portal-example", "1"); } catch (e) { done = true; }
    if (done || getQueue().length) return;
    const S = SAMPLE;
    setQueue([{ id: nextId(), process: S.process, factor: S.factor, threshold: S.threshold, action: S.action, defectType: S.defectType, cause: S.cause,
      mDefect: 0.4, mDown: 0.2, author: "S-03", uses: 0, active: true, status: "review", source: "예시 등록", at: Date.now() }]);
  }
  function pReview(el) {
    seedExampleOnce();
    const q = getQueue();
    const wait = q.filter((x) => x.status === "review"), done = q.filter((x) => x.status !== "review");
    const rules = getRules();
    el.innerHTML =
      "<p class='hint'>숙련자가 등록하거나 팀원에게 공유받은 노하우예요. 승인하면 라이브러리와 3D 훈련에 게시돼요.</p>" +
      "<div class='queue' id='rv-list'>" + (wait.length ? wait.map((r) => {
        const changes = CHANGE_RE.test(r.action);
        const similar = rules.filter((x) => x.process === r.process && x.factor === r.factor && x.defectType === r.defectType).length;
        return fragCard(r, { status: r.status, noUses: true, noDetail: true,
          extra: "<div class='flag " + (changes ? "warn" : "ok") + "'>" + (changes ? "⚠ 설정값 변경을 지시하는 표현이 있어요. 현장 승인 범위를 확인해 주세요." : "✓ 설정값 변경 지시가 없고 확인 순서 위주예요.") + "</div>" +
            (similar ? "<div class='flag warn'>같은 증상·상황의 비슷한 노하우가 이미 " + similar + "건 있어요.</div>" : "") +
            "<div class='frag-top'><span class='tag'>출처: " + esc(r.source) + "</span><span class='tag'>" + hhmm(r.at) + " 요청</span></div>" +
            "<div class='reject-row'><button type='button' class='btn good' data-approve='" + esc(r.id) + "'>승인·게시</button>" +
            "<input type='text' placeholder='반려 사유' id='rj-" + esc(r.id) + "' maxlength='60'><button type='button' class='btn bad' data-reject='" + esc(r.id) + "'>반려</button></div>" });
      }).join("") : "<div class='card empty'>검토 대기 중인 노하우가 없어요. 숙련자 역할로 노하우를 등록해 보세요.</div>") + "</div>" +
      (done.length ? "<section class='card' style='margin-top:18px'><h2>처리 내역</h2><div class='scroll'><table><thead><tr><th>ID</th><th>증상</th><th>출처</th><th>결과</th><th>사유</th></tr></thead><tbody>" +
        done.map((x) => "<tr><td>" + esc(x.id) + "</td><td>" + esc(x.defectType) + "</td><td>" + esc(x.source) + "</td><td>" + statusTag(x.status) + "</td><td>" + esc(x.reason || "") + "</td></tr>").join("") + "</tbody></table></div></section>" : "");
    $("rv-list").addEventListener("click", (e) => {
      const a = e.target.closest("[data-approve]"), r = e.target.closest("[data-reject]");
      if (a) decide(a.dataset.approve, true);
      if (r) decide(r.dataset.reject, false);
    });
  }
  function decide(id, ok) {
    const q = getQueue(), item = q.find((x) => x.id === id);
    if (!item) return;
    if (ok) {
      const list = getRules();
      const rule = {}; Object.keys(item).forEach((k) => { if (["status", "source", "at", "reason"].indexOf(k) < 0) rule[k] = item[k]; });
      list.push(rule); setRules(list);
      item.status = "approved";
      log("관리자가 " + id + " (" + item.defectType + ") 승인·게시");
      toast(id + "을(를) 게시했어요. 3D 훈련에서도 쓰여요.");
    } else {
      const reason = ($("rj-" + id).value || "").trim();
      if (!reason) { toast("반려 사유를 적어 주세요.", true); return; }
      item.status = "rejected"; item.reason = reason;
      log("관리자가 " + id + " 반려 (" + reason + ")");
      toast(id + "을(를) 반려했어요.");
    }
    setQueue(q);
    render();
  }

  /* ---------- 5. 공유·보상 원장 ---------- */
  function pLedger(el) {
    const rules = getRules();
    const total = rules.reduce((n, r) => n + (r.uses || 0), 0);
    const me = role === "expert" ? "S-01" : "";
    el.innerHTML =
      "<section class='card lg-head'><div class='lg-sum'>" +
      "<div><small>누적 활용</small><b data-count='" + total + "' data-suffix='회'>" + total + "회</b></div>" +
      "<div><small>배분 대상 숙련자</small><b id='lg-n'></b></div>" +
      "<label class='pool'><small>월 인센티브 재원</small><span class='inp'><input id='lg-pool' type='text' inputmode='numeric' value='1,000,000' aria-label='월 인센티브 재원(원)'><em>원</em></span></label></div>" +
      "<p class='formula'>기여 점수 = Σ 활용 횟수 × (불량 완화율 + 다운타임 완화율) · 배분액 = 재원 × 점수 지분 · 시연용 가상 숙련자와 예시 가정값입니다</p></section>" +
      "<section class='card nopad'><div class='scroll'><table id='lg-table' class='lg'></table></div></section>" +
      "<section class='card'><h2>노하우 공유 받기</h2>" +
      "<p class='hint'>팀원이 보낸 링크를 열면 자동으로 들어오고, 링크나 코드를 직접 붙여 넣어도 됩니다. 받은 노하우는 검토 대기로 들어갑니다. 링크는 라이브러리의 상세 창에서 만듭니다.</p>" +
      "<form class='share-form' id='sh-form'><input id='sh-in' placeholder='공유받은 링크나 코드를 붙여 넣으세요' autocomplete='off'><button class='btn' type='submit' style='white-space:nowrap'>가져오기</button></form></section>" +
      (role === "admin" ? "<section class='card'><h2>시연 데이터</h2><p class='hint'>등록하고 승인한 내용을 지우고 처음 상태로 되돌립니다.</p><button class='btn bad' id='lg-reset' type='button'>데모 데이터로 초기화</button></section>" : "");
    const draw = (first) => {
      const pool = Math.max(0, Number(String($("lg-pool").value).replace(/\D/g, "")) || 0);
      const rows = SIM.ledger(rules, pool);
      $("lg-n").textContent = rows.length + "명";
      const t = $("lg-table");
      t.classList.toggle("still", !first);
      t.innerHTML = "<thead><tr><th>#</th><th>원작성 숙련자</th><th class='num'>노하우</th><th class='num'>활용</th><th class='num'>기여 점수</th><th>지분</th><th class='num'>배분액</th></tr></thead><tbody>" +
        rows.map((e, i) => {
          const lab = expertLabel(e.author), dot = lab.indexOf(" · ");
          const nm = dot > 0 ? lab.slice(0, dot) : lab, ex = dot > 0 ? lab.slice(dot + 3) : "";
          const pct = (e.share * 100).toFixed(1);
          const pay = first ? "<span data-count='" + Math.round(e.payout) + "' data-won='1' data-suffix='원'>" + won(e.payout) + "</span>" : won(e.payout);
          return "<tr class='" + (i === 0 ? "first " : "") + (e.author === me ? "me" : "") + "' style='--i:" + i + "'>" +
            "<td class='rank'>" + (i + 1) + "</td>" +
            "<td class='who2'><b><span class='chip'>" + esc(e.author) + "</span>" + esc(nm) + "</b>" + (ex ? "<small>" + esc(ex) + "</small>" : "") + "</td>" +
            "<td class='num'>" + e.fragments + "건</td><td class='num'>" + e.uses + "회</td><td class='num'>" + e.score.toFixed(1) + "</td>" +
            "<td><div class='shr'><div class='t'><i style='--w:" + pct + "%;--i:" + i + "'></i></div><span>" + pct + "%</span></div></td>" +
            "<td class='num pay'>" + pay + "</td></tr>";
        }).join("") + "</tbody>";
      if (first) animateNumbers(t);
    };
    $("lg-pool").addEventListener("input", (e) => { const n = Number(String(e.target.value).replace(/\D/g, "")) || 0; e.target.value = n ? n.toLocaleString("ko-KR") : ""; draw(false); }); draw(true);
    $("sh-form").addEventListener("submit", (e) => {
      e.preventDefault();
      const res = importShared(codeOf($("sh-in").value));
      toast(res.msg, !res.ok);
      if (res.ok && !res.dup) { $("sh-in").value = ""; renderShell(); }
    });
    if ($("lg-reset")) $("lg-reset").addEventListener("click", () => { setRules(seed()); setQueue([]); jset(LS_LOG, []); toast("처음 상태로 되돌렸어요."); render(); });
  }

  /* ---------- 6. 3D 현장 훈련 ---------- */
  function pTrain(el) {
    const n = getRules().filter((r) => r.active !== false).length;
    el.innerHTML =
      "<p class='hint'>" + t("trainHint", "게시된 노하우 <b>{n}건</b>이 3D 공장에서 쓰이고, 3D에서 따를 때마다 활용 횟수가 올라가 원장에 반영돼요. 화면이 크게 필요하면 전체 화면을 눌러 주세요.", { n: n }) + "</p>" +
      "<div class='stage' id='stage'><button type='button' class='go' id='stage-go'>" + t("trainStart", "▶ 3D 공장 시작") + "</button></div>";
    $("stage-go").addEventListener("click", () => {
      if (window.innerWidth < 760) { location.href = "mobile.html"; return; }
      const st = $("stage");
      st.innerHTML = "<iframe src='training/' title='3D 사출 공장 훈련' allow='fullscreen; autoplay' allowfullscreen></iframe>";
      // 전체 화면 버튼은 3D 화면 위가 아니라 아래에 둔다 (3D 안의 버튼과 겹치지 않게)
      const fb = document.createElement("button"); fb.type = "button"; fb.id = "stage-full"; fb.className = "btn sm stage-full-out"; fb.textContent = t("full", "전체 화면");
      st.insertAdjacentElement("afterend", fb);
      const f = st.querySelector("iframe");
      $("stage-full").addEventListener("click", () => (st.requestFullscreen || st.webkitRequestFullscreen || function () {}).call(st));
      const re = () => [0, 250, 800, 1800].forEach((ms) => setTimeout(() => { try { f.contentWindow.dispatchEvent(new Event("resize")); } catch (e) { /* 무시 */ } }, ms));
      document.addEventListener("fullscreenchange", re);
    });
  }

  /* ---------- 시작: 기업 전용 공간이라 로그인부터 ---------- */
  function start() {
    getRules(); // 3D 훈련이 읽을 저장소를 먼저 채워 둔다
    if (/share=/.test(location.hash)) {
      const m = location.hash.match(/share=([A-Za-z0-9_\-]+)/);
      const res = importShared(m[1]);
      history.replaceState(null, "", location.pathname + location.search + "#/" + (ROLES[role].menus.indexOf("review") >= 0 ? "review" : ROLES[role].menus[0]));
      render();
      toast(res.msg, !res.ok);
    } else {
      if (!location.hash) history.replaceState(null, "", "#/" + ROLES[role].menus[0]);
      render();
    }
  }
  function loggedIn() { try { return !!localStorage.getItem(LS_SESSION); } catch (e) { return false; } }
  function enter(r) {
    role = ROLES[r] ? r : "admin";
    jset(LS_ROLE, role);
    try { localStorage.setItem(LS_SESSION, role); } catch (e) { /* 저장소를 못 쓰면 이번 화면에서만 유지 */ }
    document.body.classList.remove("locked");
    start();
  }
  document.querySelectorAll("[data-login]").forEach((b) => b.addEventListener("click", () => enter(b.dataset.login)));
  // 3D 태블릿 안에서는 로그인 화면 없이 김민준(현장 근로자)으로 바로 들어간다. 역할은 위쪽 버튼으로 바꿀 수 있다
  const embedAuto = document.documentElement.classList.contains("embed") && !loggedIn();
  if (embedAuto) setTimeout(() => enter("worker"), 0);
  $("logout").addEventListener("click", () => {
    try { localStorage.removeItem(LS_SESSION); } catch (e) { /* 무시 */ }
    history.replaceState(null, "", location.pathname + location.search);
    $("side").classList.remove("open");
    document.body.classList.add("locked");
    window.scrollTo(0, 0);
  });

  // 앱 설치 안내: 안드로이드는 설치 버튼, iPhone·iPad는 홈 화면에 추가 안내
  (function () {
    const standalone = (window.matchMedia && window.matchMedia("(display-mode: standalone)").matches) || window.navigator.standalone;
    if (standalone) return;
    const box = $("install-box"), btn = $("install"), hint = $("ios-hint");
    let ev = null;
    window.addEventListener("beforeinstallprompt", (e) => { e.preventDefault(); ev = e; box.hidden = false; btn.hidden = false; });
    btn.addEventListener("click", () => { if (!ev) return; ev.prompt(); ev.userChoice.finally(() => { ev = null; btn.hidden = true; }); });
    window.addEventListener("appinstalled", () => { box.hidden = true; });
    if (/iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1)) { box.hidden = false; hint.hidden = false; }
  })();

  if (loggedIn()) {
    document.body.classList.remove("locked");
    start();
  }
})();
