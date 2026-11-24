/* 제조현장의 암묵지 기록·활용 플랫폼 업무 포털 (시연용).
   기업에서 쓰는 지식 공유 시스템의 구조를 보여 준다: 대시보드 · 노하우 라이브러리 · 등록 · 검토·승인 · 보상 원장 · 3D 훈련.
   데이터는 이 브라우저의 저장소에만 있다. 실제 도입 시에는 회사 서버와 로그인으로 바뀌는 자리다.
   게시된 노하우는 3D 훈련(같은 주소의 /training)이 읽는 저장소(tacit-agent-v2)에 들어간다. */
(function () {
  "use strict";
  const D = window.APP_DATA, SIM = window.SIM;
  const LS_RULES = "tacit-agent-v2", LS_QUEUE = "tacit-portal-queue", LS_LOG = "tacit-portal-log", LS_ROLE = "tacit-portal-role";
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
  const expertLabel = (id) => D.experts[id] || "신규 등록 숙련자";
  function condText(r) {
    const f = D.factors[r.factor];
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
    admin: { name: "관리자", av: "관", user: "품질경영팀", sub: "관리자", menus: ["dash", "lib", "review", "ledger", "train"] },
    expert: { name: "숙련자", av: "박", user: "박 조장", sub: "숙련자 · 사출 27년차", menus: ["lib", "new", "ledger", "train"] },
    worker: { name: "현장 근로자", av: "김", user: "김민준", sub: "현장 근로자 · 입사 4주차", menus: ["lib", "train"] }
  };
  // 메뉴 아이콘 (24px 선 그림)
  const ICON = {
    dash: '<rect x="3.5" y="3.5" width="7" height="9" rx="1.6"/><rect x="13.5" y="3.5" width="7" height="5" rx="1.6"/><rect x="13.5" y="12.5" width="7" height="8" rx="1.6"/><rect x="3.5" y="16.5" width="7" height="4" rx="1.6"/>',
    lib: '<path d="M4 5.6A1.6 1.6 0 0 1 5.6 4H11v16H5.6A1.6 1.6 0 0 1 4 18.4z"/><path d="M20 5.6A1.6 1.6 0 0 0 18.4 4H13v16h5.4a1.6 1.6 0 0 0 1.6-1.6z"/>',
    new: '<rect x="3.5" y="3.5" width="17" height="17" rx="4.5"/><path d="M12 8v8M8 12h8"/>',
    review: '<circle cx="12" cy="12" r="8.5"/><path d="M8.4 12.3l2.5 2.5 4.7-5"/>',
    ledger: '<path d="M6 3.5h12v17l-3-2-3 2-3-2-3 2z"/><path d="M9 8.5h6M9 12h6"/>',
    train: '<path d="M12 3l8 4.5v9L12 21l-8-4.5v-9z"/><path d="M12 12l8-4.5M12 12v9M12 12L4 7.5"/>'
  };
  const PAGES = {
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

  function renderShell() {
    const cur = route();
    const pending = getQueue().filter((q) => q.status === "review").length;
    $("menu").innerHTML = ROLES[role].menus.map((k) =>
      "<a href='#/" + k + "'" + (k === cur ? " aria-current='page'" : "") + "><svg viewBox='0 0 24 24' aria-hidden='true'>" + ICON[k] + "</svg>" + PAGES[k].title +
      (k === "review" && pending ? "<span class='count'>" + pending + "</span>" : "") + "</a>").join("");
    document.querySelectorAll(".role button").forEach((b) => b.setAttribute("aria-pressed", b.dataset.role === role ? "true" : "false"));
    $("who").innerHTML = "<span class='av'>" + ROLES[role].av + "</span><span class='wn'><b>" + esc(ROLES[role].user) + "</b><small>" + esc(ROLES[role].sub) + "</small></span>";
    $("page-title").textContent = PAGES[cur].title;
    document.title = PAGES[cur].title + " · 제조현장의 암묵지 기록·활용 플랫폼 업무 포털";
    $("side").classList.remove("open");
  }

  function render() {
    renderShell();
    const cur = route();
    $("view").innerHTML = "<div class='ph'><div><h1>" + PAGES[cur].title + "</h1><p>" + PAGES[cur].desc + "</p></div></div><div id='page-body'></div>";
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
      "<div class='frag-top'><b>" + esc(r.id) + "</b><span class='tag'>" + esc(D.processes[r.process].name) + "</span>" +
      (r.active === false ? "<span class='tag r'>게시 중단</span>" : "") + (opts.status ? statusTag(opts.status) : "") + "</div>" +
      "<h3>" + esc(r.defectType) + "</h3>" +
      "<div class='cond'>발생 상황: " + esc(condText(r)) + "</div>" +
      "<div class='act'>" + esc(r.action) + "</div>" +
      (r.cause ? "<div class='cause'>원인: " + esc(r.cause) + "</div>" : "") +
      (opts.extra || "") +
      "<div class='foot'><span>" + esc(r.author) + " · " + esc(expertLabel(r.author)) + "</span><span class='sp'></span>" +
      (opts.noUses ? "" : "<span class='tag o'>활용 " + (r.uses || 0) + "회</span>") +
      (opts.noDetail ? "" : "<button type='button' class='btn sm' data-detail='" + esc(r.id) + "'>상세</button>") +
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
  function pLib(el) {
    el.innerHTML =
      "<div class='toolbar'>" +
      "<input type='search' id='lib-q' placeholder='증상·확인 순서·작성자로 검색' value='" + esc(libState.q) + "'>" +
      "<select id='lib-proc' aria-label='공정'><option value=''>모든 공정</option>" + PROCS.map((p) => "<option value='" + p + "'" + (libState.proc === p ? " selected" : "") + ">" + esc(D.processes[p].name) + "</option>").join("") + "</select>" +
      "<select id='lib-factor' aria-label='발생 상황'><option value=''>모든 발생 상황</option>" + Object.keys(D.factors).map((k) => "<option value='" + k + "'" + (libState.factor === k ? " selected" : "") + ">" + esc(D.factors[k].label) + "</option>").join("") + "</select>" +
      "<select id='lib-sort' aria-label='정렬'><option value='uses'" + (libState.sort === "uses" ? " selected" : "") + ">활용 많은 순</option><option value='new'" + (libState.sort === "new" ? " selected" : "") + ">최근 등록순</option></select>" +
      "</div><div class='grid cards' id='lib-list'></div>";
    const draw = () => {
      let list = getRules().filter((r) => role === "admin" || r.active !== false);
      const q = libState.q.trim().toLowerCase();
      if (q) list = list.filter((r) => [r.id, r.defectType, r.action, r.cause, r.author, expertLabel(r.author)].join(" ").toLowerCase().indexOf(q) >= 0);
      if (libState.proc) list = list.filter((r) => r.process === libState.proc);
      if (libState.factor) list = list.filter((r) => r.factor === libState.factor);
      list.sort(libState.sort === "uses" ? (a, b) => b.uses - a.uses : (a, b) => (b.id > a.id ? 1 : -1));
      $("lib-list").innerHTML = list.length ? list.map((r, i) => fragCard(r, { i })).join("") : "<div class='card empty' style='grid-column:1/-1'>조건에 맞는 노하우가 없어요.</div>";
    };
    $("lib-q").addEventListener("input", (e) => { libState.q = e.target.value; draw(); });
    $("lib-proc").addEventListener("change", (e) => { libState.proc = e.target.value; draw(); });
    $("lib-factor").addEventListener("change", (e) => { libState.factor = e.target.value; draw(); });
    $("lib-sort").addEventListener("change", (e) => { libState.sort = e.target.value; draw(); });
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
    dlg.innerHTML = "<div class='inner'><div class='frag-top'><b>" + esc(r.id) + "</b><span class='tag'>" + esc(D.processes[r.process].name) + "</span>" +
      (r.status ? statusTag(r.status) : (r.active === false ? "<span class='tag r'>게시 중단</span>" : "<span class='tag g'>게시 중</span>")) + "</div>" +
      "<h2 style='margin:0'>" + esc(r.defectType) + "</h2>" +
      "<dl><dt>발생 상황</dt><dd>" + esc(condText(r)) + "</dd><dt>확인 순서</dt><dd>" + esc(r.action) + "</dd><dt>원인</dt><dd>" + esc(r.cause || "기록 없음") + "</dd>" +
      "<dt>원작성자</dt><dd>" + esc(r.author) + " · " + esc(expertLabel(r.author)) + "</dd><dt>활용 횟수</dt><dd>" + (r.uses || 0) + "회</dd>" +
      "<dt>예상 효과</dt><dd>불량 완화 " + r.mDefect + "p · 다운타임 완화 " + r.mDown + "p (현장 데이터가 쌓이면 보정되는 추정값)</dd>" +
      (r.reason ? "<dt>반려 사유</dt><dd>" + esc(r.reason) + "</dd>" : "") + "</dl>" +
      "<div class='actions' style='display:flex;gap:8px;flex-wrap:wrap'>" +
      (role === "worker" && !r.status && r.active !== false ? "<button type='button' class='btn good' data-followed='" + esc(r.id) + "'>이 순서대로 점검했어요 (활용 기록)</button>" : "") +
      "<button type='button' class='btn primary' data-copy='" + esc(r.id) + "'>링크 복사</button>" +
      (role === "admin" && !r.status ? "<button type='button' class='btn' data-toggle='" + esc(r.id) + "'>" + (r.active === false ? "다시 게시" : "게시 중단") + "</button>" : "") +
      "<button type='button' class='btn' data-close>닫기</button></div></div>";
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
      if (r) { r.uses = (r.uses || 0) + 1; setRules(list); log("김민준 씨가 " + r.id + " (" + r.defectType + ") 순서대로 점검 → " + r.author + " 활용 " + r.uses + "회"); toast(r.id + " 활용을 기록했어요. " + r.author + "님의 활용 횟수가 " + r.uses + "회가 됐어요."); openDetail(r.id); }
    }
    const t = e.target.closest("[data-toggle]");
    if (t) {
      const list = getRules(), r = list.find((x) => x.id === t.dataset.toggle);
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
      "<p class='hint'>3D 공장에서 김민준 씨의 야간 교대를 체험해요. 게시된 노하우 <b>" + n + "건</b>이 3D에서 쓰이고, 3D에서 따를 때마다 활용 횟수가 올라가 원장에 반영돼요. 화면이 크게 필요하면 전체 화면을 눌러 주세요.</p>" +
      "<div class='stage' id='stage'><button type='button' class='go' id='stage-go'>▶ 3D 공장 시작</button></div>";
    $("stage-go").addEventListener("click", () => {
      if (window.innerWidth < 760) { location.href = "mobile.html"; return; }
      const st = $("stage");
      st.innerHTML = "<iframe src='training/' title='3D 사출 공장 훈련' allow='fullscreen; autoplay' allowfullscreen></iframe><button type='button' class='full' id='stage-full'>전체 화면</button>";
      const f = st.querySelector("iframe");
      $("stage-full").addEventListener("click", () => (st.requestFullscreen || st.webkitRequestFullscreen || function () {}).call(st));
      const re = () => [0, 250, 800, 1800].forEach((ms) => setTimeout(() => { try { f.contentWindow.dispatchEvent(new Event("resize")); } catch (e) { /* 무시 */ } }, ms));
      document.addEventListener("fullscreenchange", re);
    });
  }

  /* ---------- 시작 ---------- */
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
})();
