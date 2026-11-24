/* 「김민준 씨의 하룻밤」을 스크롤만으로 넘겨 보는 장면으로 바꾼다.
   - 기존 타임라인(#story .timeline)의 글을 읽어 장면을 만든다.
   - frames/ 에 3D 공장 프레임(f_000.jpg …)이 있으면 스크롤에 맞춰 배경으로 넘긴다. 없으면 밝은 배경으로 보여 준다.
   - 모션 줄임 설정이면 기존 타임라인이 그대로 보인다. */
(function () {
  var tl = document.querySelector("#story .timeline");
  if (!tl || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  var items = Array.prototype.slice.call(tl.children);
  if (items.length < 2) return;
  var steps = items.map(function (li) {
    var t = li.querySelector("time"), h = li.querySelector("h3"), p = li.querySelector("p");
    return { t: t ? t.textContent : "", h: h ? h.textContent : "", p: p ? p.textContent : "" };
  });
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); }

  var FRAMES = { dir: "frames/", count: 0, ext: "jpg" }; // 배경 프레임은 쓰지 않는다 (count 를 올리면 frames/ 의 이미지를 스크롤에 맞춰 넘긴다)

  // 장면마다 보여 줄 화면: 3D 공장과 업무 포털의 실제 화면 (누르면 그 화면으로 들어간다)
  var VIS = [
    '<a class="sc-shot field" href="#train"><img src="shots/field_view.jpg" alt="3D로 만든 사출 공장. 라인 위에서 작업이 이어진다" loading="lazy"><span class="sc-go">3D 공장에서 직접 보기 →</span></a>',
    '<a class="sc-shot field" href="#train"><img src="shots/field_pov.jpg" alt="3D 공장 안, 김민준 씨 눈높이에서 본 야간 라인" loading="lazy"><span class="sc-go">3D 공장에서 직접 보기 →</span></a>',
    '<a class="sc-shot portal" href="portal.html?role=worker#/lib"><img src="shots/portal_search.jpg" alt="업무 포털의 노하우 라이브러리. 은줄을 검색한 화면" loading="lazy"><span class="sc-go">포털에서 직접 찾아보기 →</span></a>',
    '<a class="sc-shot portal" href="portal.html?role=worker#/lib"><img src="shots/portal_detail.jpg" alt="업무 포털의 노하우 상세. 발생 상황, 확인 순서, 원인, 원작성자와 활용 기록 버튼" loading="lazy"><span class="sc-go">포털에서 직접 해보기 →</span></a>',
    '<a class="sc-shot portal" href="portal.html?role=admin#/ledger"><img src="shots/portal_ledger.jpg" alt="업무 포털의 공유·보상 원장. 숙련자별 활용 횟수와 배분액" loading="lazy"><span class="sc-go">포털에서 원장 보기 →</span></a>'
  ];

  var n = steps.length;
  var wrap = document.createElement("div");
  wrap.className = "scrolly";
  wrap.style.height = "calc(" + n + " * 75vh + 25vh)";
  var pin = document.createElement("div");
  pin.className = "scrolly-pin";
  var canvas = document.createElement("canvas");
  canvas.className = "sc-canvas"; canvas.setAttribute("aria-hidden", "true"); canvas.hidden = true;
  var shade = document.createElement("div"); shade.className = "sc-shade"; shade.hidden = true;
  var dots = document.createElement("div"); dots.className = "sc-dots"; dots.setAttribute("role", "tablist");
  var text = document.createElement("div"); text.className = "sc-text";
  var vis = document.createElement("div"); vis.className = "sc-visual";
  steps.forEach(function (s, i) {
    var b = document.createElement("button"); b.type = "button"; b.setAttribute("aria-label", (i + 1) + "번째 장면: " + s.t);
    b.addEventListener("click", function () { go(i); });
    dots.appendChild(b);
    var d = document.createElement("div"); d.className = "sc-step";
    d.innerHTML = '<div class="sc-time">' + esc(s.t) + "</div><h3>" + esc(s.h) + "</h3><p>" + esc(s.p) + "</p>";
    text.appendChild(d);
    var v = document.createElement("div"); v.className = "sc-vis shot"; v.setAttribute("aria-hidden", "true");
    v.innerHTML = VIS[i] || "";
    vis.appendChild(v);
  });
  pin.appendChild(canvas); pin.appendChild(shade); pin.appendChild(dots); pin.appendChild(text); pin.appendChild(vis);
  wrap.appendChild(pin);
  tl.parentNode.insertBefore(wrap, tl);
  tl.style.display = "none"; // 원문은 남기되 화면에서는 숨긴다
  var hint = document.createElement("p"); hint.className = "scrolly-hint"; hint.textContent = "스크롤을 내리면 다음 장면으로 넘어갑니다";
  wrap.parentNode.insertBefore(hint, wrap);
  // 제목·설명·안내를 한 묶음으로 만들어 첫 화면을 채운다 (그다음 화면부터 장면이 시작된다)
  var intro = document.createElement("div");
  intro.className = "story-intro";
  var par = wrap.parentNode;
  while (par.firstChild && par.firstChild !== wrap) intro.appendChild(par.firstChild);
  par.insertBefore(intro, wrap);

  // ----- 프레임(이미지 시퀀스) -----
  var imgs = [], loaded = 0, frameIdx = -1, haveFrames = false;
  function name(i) { return FRAMES.dir + "f_" + String(i).padStart(3, "0") + "." + FRAMES.ext; }
  function drawFrame(i) {
    if (!haveFrames) return;
    var k = i;
    while (k >= 0 && !(imgs[k] && imgs[k].complete && imgs[k].naturalWidth)) k--; // 아직 못 받은 프레임이면 가까운 이전 프레임
    if (k < 0) return;
    if (k === frameIdx) return;
    frameIdx = k;
    var im = imgs[k], cw = canvas.clientWidth, ch = canvas.clientHeight, r = window.devicePixelRatio > 1 ? 1.5 : 1;
    if (canvas.width !== Math.round(cw * r)) { canvas.width = Math.round(cw * r); canvas.height = Math.round(ch * r); }
    var s = Math.max(canvas.width / im.naturalWidth, canvas.height / im.naturalHeight);
    var w = im.naturalWidth * s, h = im.naturalHeight * s;
    canvas.getContext("2d").drawImage(im, (canvas.width - w) / 2, (canvas.height - h) / 2, w, h);
  }
  var first = new Image();
  first.onload = function () {
    haveFrames = true; imgs[0] = first;
    canvas.hidden = false; shade.hidden = false; wrap.classList.add("dark");
    for (var i = 1; i < FRAMES.count; i++) { (function (i) { var im = new Image(); im.onload = function () { loaded++; frameIdx = -2; onScroll(); }; im.src = name(i); imgs[i] = im; })(i); }
    frameIdx = -2; update(true);
  };
  first.onerror = function () { /* 프레임이 없으면 밝은 배경 그대로 */ };
  if (FRAMES.count > 0) first.src = name(0);

  var cur = -1, ticking = false;
  function update(force) {
    ticking = false;
    var r = wrap.getBoundingClientRect();
    var total = wrap.offsetHeight - pin.offsetHeight;
    var p = total > 0 ? Math.min(1, Math.max(0, -r.top / total)) : 0;
    drawFrame(Math.round(p * (FRAMES.count - 1)));
    var idx = Math.min(n - 1, Math.floor(p * n));
    if (idx === cur && !force) return;
    cur = idx;
    var st = text.children, vs = vis.children, ds = dots.children;
    for (var i = 0; i < n; i++) {
      st[i].classList.toggle("on", i === idx);
      st[i].classList.toggle("past", i < idx);
      vs[i].classList.toggle("on", i === idx);
      ds[i].classList.toggle("on", i === idx);
      ds[i].setAttribute("aria-current", i === idx ? "true" : "false");
    }
  }
  function onScroll() { if (!ticking) { ticking = true; requestAnimationFrame(function () { update(false); }); } }
  function go(i) {
    var total = wrap.offsetHeight - pin.offsetHeight;
    var top = wrap.getBoundingClientRect().top + window.scrollY;
    window.scrollTo({ top: top + total * ((i + 0.5) / n), behavior: "smooth" });
  }
  window.addEventListener("scroll", onScroll, { passive: true });
  window.addEventListener("resize", function () { frameIdx = -2; onScroll(); });
  update(true);
})();

/* 페이지 전체 진행 눈금: 왼쪽에 가로선이 세로로 늘어서고, 지금 보는 구역만 진하게 (눈금을 누르면 그 구역으로 이동) */
(function () {
  var ids = ["top", "summary", "story", "demo", "train", "background", "problem", "how", "reward", "effects", "diff", "start"];
  var labels = { top: "처음", summary: "주제", story: "하룻밤", demo: "체험", train: "3D", background: "배경", problem: "문제", how: "서비스", reward: "보상", effects: "효과", diff: "차별점", start: "시작" };
  var secs = ids.map(function (id) { return document.getElementById(id); }).filter(Boolean);
  if (secs.length < 4) return;
  var nav = document.createElement("nav");
  nav.className = "page-ticks"; nav.setAttribute("aria-label", "페이지 구역");
  secs.forEach(function (s) {
    var b = document.createElement("button");
    b.type = "button"; b.title = labels[s.id] || s.id; b.setAttribute("aria-label", labels[s.id] || s.id);
    b.addEventListener("click", function () { s.scrollIntoView({ behavior: "smooth", block: "start" }); });
    nav.appendChild(b);
  });
  document.body.appendChild(nav);
  var cur = -1, ticking = false;
  function update() {
    ticking = false;
    var mid = window.innerHeight * 0.4, idx = 0;
    for (var i = 0; i < secs.length; i++) if (secs[i].getBoundingClientRect().top <= mid) idx = i;
    if (idx !== cur) {
      cur = idx;
      for (var j = 0; j < nav.children.length; j++) nav.children[j].classList.toggle("on", j === idx);
    }
    // 이야기 장면 안에서는 장면 눈금이 대신하므로 잠시 숨긴다
    var sc = document.querySelector(".scrolly"), hide = false;
    if (sc) { var r = sc.getBoundingClientRect(); hide = r.top < window.innerHeight * 0.5 && r.bottom > window.innerHeight * 0.5; }
    nav.classList.toggle("off", hide);
  }
  function onScroll() { if (!ticking) { ticking = true; requestAnimationFrame(update); } }
  window.addEventListener("scroll", onScroll, { passive: true });
  window.addEventListener("resize", onScroll);
  update();
})();

/* 하룻밤 장면 안의 장면마다 멈추는 자리를 만든다 (한 화면씩 넘어가기와 맞물리게) */
(function () {
  var wrap = document.querySelector(".scrolly");
  if (!wrap) return;
  var n = wrap.querySelectorAll(".sc-step").length;
  for (var i = 0; i < n; i++) {
    var a = document.createElement("div");
    a.className = "sc-snap"; a.setAttribute("aria-hidden", "true");
    a.style.top = "calc(" + i + " * 75vh)";
    wrap.appendChild(a);
  }
})();

/* 한 화면씩 넘어가기: 마우스 휠·PageUp/PageDown 한 번에 다음(이전) 구역으로.
   화면보다 긴 구역(체험, 3D 등)은 그 안에서 끝까지는 평소처럼 스크롤하고, 끝에 닿으면 다음 구역으로 넘어간다. 터치는 그대로 둔다. */
(function () {
  var mq = window.matchMedia("(min-width: 900px) and (min-height: 600px)");
  var HEAD = 56, busy = false;
  function vh() { return window.innerHeight; }
  function targets() {
    var list = [], max = document.documentElement.scrollHeight - vh();
    document.querySelectorAll(".hero, .section, .sc-snap, .footer").forEach(function (el) {
      var top = el.getBoundingClientRect().top + window.scrollY;
      if (el.classList.contains("sc-snap")) { list.push(top - HEAD); return; }
      if (el.classList.contains("footer")) { list.push(max); return; }
      list.push(top - HEAD);
      if (el.id !== "story" && el.offsetHeight > vh() - HEAD + 8) list.push(top + el.offsetHeight - (vh() - HEAD) - HEAD); // 긴 구역: 아래쪽 끝 자리도
    });
    list = list.map(function (y) { return Math.max(0, Math.min(max, Math.round(y))); });
    list.sort(function (a, b) { return a - b; });
    return list.filter(function (y, i) { return i === 0 || y - list[i - 1] > 6; });
  }
  function insideTall(dir) {
    var y = window.scrollY, found = null;
    document.querySelectorAll(".hero, .section").forEach(function (s) {
      var r = s.getBoundingClientRect();
      if (r.top <= HEAD + 2 && r.bottom > HEAD + 2) found = s;
    });
    if (!found || found.id === "story") return false;
    var r = found.getBoundingClientRect();
    if (found.offsetHeight <= vh() - HEAD + 8) return false;
    return dir > 0 ? r.bottom > vh() + 4 : r.top < HEAD - 4;
  }
  function step(dir) {
    if (busy) return;
    var cur = window.scrollY, ts = targets(), to = null, i;
    if (dir > 0) { for (i = 0; i < ts.length; i++) if (ts[i] > cur + 4) { to = ts[i]; break; } }
    else { for (i = ts.length - 1; i >= 0; i--) if (ts[i] < cur - 4) { to = ts[i]; break; } }
    if (to === null) return;
    busy = true;
    window.scrollTo({ top: to, behavior: "smooth" });
    setTimeout(function () { busy = false; }, 800);
  }
  function skip(el) {
    return !el || !el.closest || el.closest("dialog[open], .messages, .scroll, iframe, textarea, input, select, .stage, [data-native-scroll]");
  }
  window.addEventListener("wheel", function (e) {
    if (!mq.matches || e.ctrlKey || e.deltaY === 0 || skip(e.target) || document.querySelector("dialog[open]")) return;
    var dir = e.deltaY > 0 ? 1 : -1;
    if (insideTall(dir)) return; // 긴 구역 안에서는 평소처럼
    e.preventDefault();
    if (Math.abs(e.deltaY) < 4) return;
    step(dir);
  }, { passive: false });
  window.addEventListener("keydown", function (e) {
    if (!mq.matches || e.altKey || e.ctrlKey || e.metaKey || document.querySelector("dialog[open]")) return;
    var tag = (e.target && e.target.tagName) || "";
    if (/INPUT|TEXTAREA|SELECT|BUTTON|A/.test(tag) || (e.target && e.target.isContentEditable)) return;
    var dir = 0;
    if (e.key === "PageDown" || (e.key === " " && !e.shiftKey)) dir = 1;
    else if (e.key === "PageUp" || (e.key === " " && e.shiftKey)) dir = -1;
    if (!dir || insideTall(dir)) return;
    e.preventDefault(); step(dir);
  });
})();

/* 마지막 화면: 「지금 바로 시작해 보세요」와 아래 푸터(팀 소개)가 한 화면에 같이 보이게, 시작 구역 높이를 푸터만큼 줄인다 */
(function () {
  var foot = document.querySelector(".footer");
  if (!foot) return;
  function fit() { document.documentElement.style.setProperty("--foot-h", foot.offsetHeight + "px"); }
  fit();
  window.addEventListener("resize", fit);
  window.addEventListener("load", fit);
  var d = foot.querySelector("details");
  if (d) d.addEventListener("toggle", fit);
})();

/* 구역 사진: 구역을 화면 가득 사진으로 채우고(토스·카카오 방식), 왼쪽을 어둡게 눌러 글을 올린다.
   사진(art/<구역>.webp)을 불러온 뒤에만 바꾸므로, 사진이 없으면 원래 모습 그대로다. 사진은 AI로 만든 연출 이미지다 */
(function () {
  var IDS = ["summary", "background", "problem", "how", "reward", "effects", "diff"];
  // 사진 위에 바로 놓인 글(흰 카드나 표 안이 아닌 글)을 찾아 밝은 글씨로 바꾼다
  function alpha(c) { var m = /rgba?(([^)]+))/.exec(c); if (!m) return 0; var p = m[1].split(","); return p.length > 3 ? parseFloat(p[3]) : 1; }
  function markOnPhoto(sec) {
    var all = sec.querySelectorAll("*");
    for (var i = 0; i < all.length; i++) {
      var el = all[i], n = el, covered = false, hasText = false;
      if (/^(SCRIPT|STYLE|IMG|SVG|VIDEO|CANVAS|IFRAME|INPUT|SELECT|TEXTAREA|BUTTON)$/i.test(el.tagName) || (el.classList && el.classList.contains("btn"))) continue;
      for (var c = el.firstChild; c; c = c.nextSibling) if (c.nodeType === 3 && c.nodeValue.trim()) { hasText = true; break; }
      if (!hasText) continue; // 글자를 직접 담은 요소만
      while (n && n !== sec) { if (alpha(getComputedStyle(n).backgroundColor) > 0.5) { covered = true; break; } n = n.parentElement; }
      if (!covered) el.classList.add("on-photo");
    }
  }
  IDS.forEach(function (id) {
    var sec = document.getElementById(id);
    var wrap = sec && sec.querySelector(".wrap");
    if (!wrap) return;
    // 구역 맨 앞의 소제목·제목·설명 문단을 제목 묶음으로 모은다 (사진 위에서 흰 글씨로 보이게)
    if (!wrap.querySelector(".sec-head")) {
      var head = document.createElement("div");
      head.className = "sec-head";
      while (wrap.firstElementChild && /^(P|H2)$/.test(wrap.firstElementChild.tagName) && /kicker|section-lede|^$/.test(wrap.firstElementChild.className || "")) head.appendChild(wrap.firstElementChild);
      if (head.children.length) wrap.insertBefore(head, wrap.firstChild);
    }
    var url = "art/" + id + ".webp";
    var im = new Image();
    im.onload = function () {
      sec.style.setProperty("--photo", "url(" + url + ")");
      sec.classList.add("has-photo");
      markOnPhoto(sec);
    };
    im.src = url;
  });
})();
