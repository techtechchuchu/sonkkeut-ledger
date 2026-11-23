/* 휴대폰용 야간 교대 훈련.
   3D 훈련(Unity)과 같은 계산식·같은 조각 저장소를 쓰는 세로 화면용 간이 버전이다. */
(function () {
  var D = window.APP_DATA, SIM = window.SIM;
  var LS_KEY = "tacit-agent-v2";                 // agent-web/app.js, Unity WebStore와 같은 저장소
  var PROCS = ["injection", "welding", "mold"];
  var SHOTS = 100, POOL = 1000000;
  var $ = function (id) { return document.getElementById(id); };

  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function seed() {
    return D.rules.filter(function (r) { return PROCS.indexOf(r.process) >= 0; })
      .map(function (r) { return Object.assign({}, r); });
  }
  function load() {
    try {
      var s = JSON.parse(localStorage.getItem(LS_KEY));
      if (Array.isArray(s) && s.length) return s;
    } catch (e) { /* 저장소를 못 쓰면 시드 데이터로 동작 */ }
    return seed();
  }
  function save() { try { localStorage.setItem(LS_KEY, JSON.stringify(rules)); } catch (e) { /* 무시 */ } }

  var rules = load();
  // 예전에 저장된 조각에는 원인이 없다. 기본 조각이면 채워 넣는다
  rules.forEach(function (r) {
    if (r.cause) return;
    var base = D.rules.filter(function (b) { return b.id === r.id; })[0];
    if (base && base.cause) r.cause = base.cause;
  });
  var env = { temp: -3, humidity: 35, age: 15, tenure: 4 };
  var followed = {};      // 조각 id → 따르기로 했는가
  var running = false;
  var seedN = 7;

  function expertLabel(id) { return D.experts[id] || "신규 등록 숙련자"; }
  function condText(r) {
    var f = D.factors[r.factor];
    return f.varLabel + " " + r.threshold + f.unit + (f.op === ">=" ? " 이상" : " 이하");
  }
  function injection() { return rules.filter(function (r) { return r.process === "injection"; }); }
  function triggered(r) { return SIM.ruleTriggered(r, D.factors, env, env.tenure); }

  // 표준만 볼 때와, 따르기로 한 조각만 반영했을 때의 불량률·다운타임
  function calc() {
    var copy = rules.map(function (r) { return Object.assign({}, r, { active: !!followed[r.id] }); });
    var res = SIM.simulate(D, {
      process: "injection", temp: env.temp, humidity: env.humidity, age: env.age, tenure: env.tenure,
      onboardWeeks: D.processes.injection.onboardWeeks,
      attrition: 36.6, hires: 3, volume: 40000, unitLoss: 1200, hourLoss: 150000,
      replaceCost: 8000000, onboardCap: 0, hazardRatio: 2.5
    }, copy);
    return { std: res.std.now, cur: res.tacit.now };
  }

  // 같은 난수열로 두 경우를 비교하기 위한 작은 난수 생성기
  function rng(seed) {
    var a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) >>> 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  /* ---------- 그리기 ---------- */
  var UNITS = { temp: "℃", humidity: "%", age: "년", tenure: "주" };

  function renderEnv() {
    Object.keys(UNITS).forEach(function (k) {
      $("s-" + k).value = env[k];
      $("o-" + k).textContent = env[k] + UNITS[k];
    });
    Array.prototype.forEach.call(document.querySelectorAll("#presets button"), function (b) {
      var v = b.dataset.preset.split(",");
      b.classList.toggle("on", Number(v[0]) === env.temp && Number(v[1]) === env.humidity);
    });
  }

  function renderFrags() {
    var list = injection();
    list.forEach(function (r) { if (!triggered(r)) followed[r.id] = false; });
    var hit = list.filter(triggered), wait = list.filter(function (r) { return !triggered(r); });
    var html = "";
    if (!hit.length) html += "<p class='m-empty'>지금 상황에 해당하는 조각이 없습니다. 작업표준서대로 진행하세요.</p>";
    hit.forEach(function (r) {
      var on = !!followed[r.id];
      html += "<article class='m-frag" + (on ? " on" : "") + "'>" +
        "<div class='mf-top'><b>" + esc(r.id) + " · " + (on ? "따르는 중" : "발동") + "</b><span>활용 " + r.uses + "회</span></div>" +
        "<div class='mf-if'>증상: " + esc(r.defectType) + " · 주로 " + esc(condText(r)) + "</div>" +
        "<div class='mf-then'>" + esc(r.action) + "</div>" +
        (r.cause ? "<div class='mf-if mf-cause'>원인: " + esc(r.cause) + "</div>" : "") +
        "<div class='mf-foot'><span>" + esc(r.author) + " " + esc(expertLabel(r.author)) + "</span>" +
        "<button type='button' data-id='" + esc(r.id) + "' aria-pressed='" + on + "'" + (running ? " disabled" : "") + ">" + (on ? "따르는 중" : "따르기") + "</button></div></article>";
    });
    wait.forEach(function (r) {
      html += "<article class='m-frag wait'><div class='mf-top'><b>" + esc(r.id) + " · 대기</b><span>활용 " + r.uses + "회</span></div>" +
        "<div class='mf-if'>" + esc(condText(r)) + "일 때 발동</div></article>";
    });
    $("frags").innerHTML = html;
  }

  function rateRow(label, value, max, cls) {
    return "<div><div class='mr-head'><span>" + label + "</span><b>" + value.toFixed(1) + "%</b></div>" +
      "<div class='mr-track'><i class='" + cls + "' style='width:" + Math.min(100, value / max * 100) + "%'></i></div></div>";
  }
  function renderRates() {
    var c = calc(), max = Math.max(12, c.std.defect);
    $("rates").innerHTML =
      rateRow("작업표준서만 보면", c.std.defect, max, "b1") +
      rateRow("고른 조각을 따르면", c.cur.defect, max, "b2") +
      "<p class='mr-down'>월 다운타임 " + c.std.down.toFixed(1) + "h → " + c.cur.down.toFixed(1) + "h</p>";
  }
  function renderAll() { renderEnv(); renderFrags(); renderRates(); }

  function resetGrid() {
    var html = "";
    for (var i = 0; i < SHOTS; i++) html += "<i></i>";
    $("grid").innerHTML = html;
  }

  function renderLedger() {
    var rows = SIM.ledger(rules, POOL);
    $("ledger").innerHTML = rows.slice(0, 5).map(function (e) {
      return "<div class='ml-row'><span>" + esc(e.author) + " " + esc(expertLabel(e.author)) + "</span><b>" +
        Math.round(e.payout).toLocaleString("ko-KR") + "원</b>" +
        "<small>조각 " + e.fragments + "건 · 활용 " + e.uses + "회 · 지분 " + (e.share * 100).toFixed(1) + "%</small>" +
        "<div class='mr-track'><i class='b1' style='width:" + (e.share * 100).toFixed(1) + "%'></i></div></div>";
    }).join("");
  }

  /* ---------- 생산 ---------- */
  function run() {
    if (running) return;
    running = true;
    $("run").disabled = true; $("result").hidden = true;
    resetGrid(); renderFrags();
    var c = calc(), rand = rng(seedN++), cells = $("grid").children;
    var i = 0, defects = 0, stdDefects = 0;

    function shot() {
      var u = rand();
      if (u < c.std.defect / 100) stdDefects++;
      var bad = u < c.cur.defect / 100;
      if (bad) defects++;
      cells[i].className = bad ? "ng" : "ok";
      $("machine").classList.toggle("shot", i % 2 === 0);
      i++;
      $("status").textContent = "생산 중 " + i + " / " + SHOTS + " 쇼트 · 불량 " + defects + "개";
      if (i < SHOTS) { setTimeout(shot, 32); return; }
      finish(defects, stdDefects);
    }
    shot();
  }

  function finish(defects, stdDefects) {
    $("machine").classList.remove("shot");
    var credits = [];
    injection().forEach(function (r) {
      if (!followed[r.id] || !triggered(r)) return;
      r.uses += 1;
      credits.push(r.id + "(" + r.author + ")");
    });
    if (credits.length) save();

    $("status").textContent = "생산 완료 · 불량 " + defects + " / " + SHOTS + "개";
    $("r-big").textContent = "불량 " + defects + " / " + SHOTS + "개";
    var saved = stdDefects - defects;
    $("r-compare").textContent = "작업표준서만 봤다면 " + stdDefects + "개" +
      (saved > 0 ? " → 숙련자 조각으로 " + saved + "개를 막았습니다." : "였습니다.");
    $("r-credit").textContent = credits.length
      ? "활용 기록 +1: " + credits.join(", ") + " · 보상 원장에 반영됨"
      : "따른 조각이 없어 활용 기록이 없습니다.";
    $("r-credit").classList.toggle("none", !credits.length);
    renderLedger();
    running = false;
    $("run").disabled = false;
    renderFrags();
    $("result").hidden = false;
    $("result").scrollIntoView({ behavior: "smooth", block: "start" });
  }

  /* ---------- 이벤트 ---------- */
  Object.keys(UNITS).forEach(function (k) {
    $("s-" + k).addEventListener("input", function (e) {
      if (running) { e.target.value = env[k]; return; }
      env[k] = Number(e.target.value);
      renderAll();
    });
  });
  $("presets").addEventListener("click", function (e) {
    var b = e.target.closest("button");
    if (!b || running) return;
    var v = b.dataset.preset.split(",");
    env.temp = Number(v[0]); env.humidity = Number(v[1]);
    renderAll();
  });
  $("frags").addEventListener("click", function (e) {
    var b = e.target.closest("button[data-id]");
    if (!b || running) return;
    followed[b.dataset.id] = !followed[b.dataset.id];
    renderFrags(); renderRates();
  });
  $("run").addEventListener("click", run);
  $("again").addEventListener("click", function () {
    $("run").scrollIntoView({ behavior: "smooth", block: "center" });
    run();
  });

  save();        // 저장소가 비어 있으면 기본 조각으로 채워 둔다 (웹페이지·3D 훈련과 공유)
  resetGrid();
  renderAll();
})();
