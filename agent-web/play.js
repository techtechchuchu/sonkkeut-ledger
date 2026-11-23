/* 야간 교대 시뮬레이션: 에이전트 대화와 공장 생산을 한 화면에서 잇는다.
   - 에이전트가 상황을 묻고, 조장이 남긴 조각을 보여 주고, 고른 대로 공장에서 100쇼트를 생산한다.
   - 같은 대화에서 숙련자 인터뷰로 새 조각을 남기면 다음 생산부터 바로 쓰인다.
   대화는 규칙 기반이다. 실제 서비스에서는 detectFactor / parseConditions 자리를 언어 모델이 맡는다.
   조각 저장소는 3D 훈련(Unity)과 같은 것을 쓴다. */
(function () {
  var D = window.APP_DATA, SIM = window.SIM;
  var LS_KEY = "tacit-agent-v2";
  var PROCS = ["injection", "welding", "mold"];
  var SHOTS = 100, POOL = 1000000;
  var $ = function (id) { return document.getElementById(id); };

  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  /* ---------- 조각 저장소 ---------- */
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

  function expertLabel(id) { return D.experts[id] || "신규 등록 숙련자"; }
  function condText(r) {
    var f = D.factors[r.factor];
    return f.varLabel + " " + r.threshold + f.unit + (f.op === ">=" ? " 이상" : " 이하");
  }
  function nextId() {
    var max = 0;
    rules.forEach(function (r) { var n = parseInt(String(r.id).replace(/\D/g, ""), 10); if (n > max) max = n; });
    return "TF-" + String(max + 1).padStart(3, "0");
  }
  function injection() { return rules.filter(function (r) { return r.process === "injection"; }); }

  /* ---------- 상태 ---------- */
  var env = { temp: 12, humidity: 72, age: 15, tenure: 4 };
  var situationSet = false;
  var followed = {};       // 조각 id → 따르기로 했는가
  var running = false;
  var seedN = 7;
  var iv = null;           // 인터뷰 진행 상태 { step, draft }

  function triggered(r) { return SIM.ruleTriggered(r, D.factors, env, env.tenure); }
  function hits() { return injection().filter(triggered); }

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

  function rng(seed) {
    var a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) >>> 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  /* ---------- 대화 ---------- */
  var box = $("messages");
  function add(role, html) {
    var el = document.createElement("div");
    el.className = "msg " + role;
    el.innerHTML = html;
    box.appendChild(el);
    box.scrollTop = box.scrollHeight;
    return el;
  }
  function agent(text) { return add("agent", esc(text)); }
  function me(text) { return add("me", esc(text)); }
  function note(text) { return add("note", esc(text)); }

  // list: [{ label, run, main }]
  function choices(list) {
    var el = $("choices");
    el.innerHTML = "";
    list.forEach(function (item) {
      var b = document.createElement("button");
      b.type = "button";
      b.textContent = item.label;
      if (item.main) b.className = "main";
      b.addEventListener("click", function () {
        if (running) return;
        el.innerHTML = "";
        if (!item.silent) me(item.label);
        item.run();
      });
      el.appendChild(b);
    });
  }

  /* ---------- 공장 ---------- */
  function resetGrid() {
    var html = "";
    for (var i = 0; i < SHOTS; i++) html += "<i></i>";
    $("grid").innerHTML = html;
  }
  function rateCell(label, value, max, cls) {
    return "<div><div class='pr-head'><span>" + label + "</span><b>" + value.toFixed(1) + "%</b></div>" +
      "<div class='pr-track'><i class='" + cls + "' style='width:" + Math.min(100, value / max * 100) + "%'></i></div></div>";
  }
  function renderFactory() {
    $("pf-env").textContent = situationSet
      ? "외기온도 " + env.temp + "℃ · 습도 " + env.humidity + "% · 설비 " + env.age + "년 · 근속 " + env.tenure + "주"
      : "오늘 밤 상황을 정하면 생산을 시작할 수 있습니다";
    var c = calc(), max = Math.max(12, c.std.defect);
    $("rates").innerHTML =
      rateCell("작업표준서만 보면", c.std.defect, max, "b1") +
      rateCell("고른 조각을 따르면", c.cur.defect, max, "b2");
  }

  /* ---------- 1. 상황 정하기 ---------- */
  var PRESETS = [
    { label: "한겨울 새벽", temp: -3, humidity: 35 },
    { label: "장마철 야간", temp: 24, humidity: 90 },
    { label: "열대야", temp: 31, humidity: 72 }
  ];

  function askSituation() {
    iv = null;
    agent("오늘 밤 현장은 어떤가요?\n버튼을 고르거나 \"영하 3도, 습도 80%\"처럼 적어 주세요.");
    choices(PRESETS.map(function (p) {
      return { label: p.label, run: function () { env.temp = p.temp; env.humidity = p.humidity; showSituation(); } };
    }));
  }

  // 글에서 조건을 읽어 env에 반영한다. 하나라도 읽으면 true
  function parseConditions(text) {
    var hit = false, m;
    if ((m = text.match(/습도\s*(\d+)|(\d+)\s*(%|퍼센트|프로)/))) { env.humidity = Number(m[1] || m[2]); hit = true; }
    if ((m = text.match(/(영하\s*)?(-?\d+)\s*(도|℃)/))) {
      var t = parseInt(m[2], 10);
      env.temp = m[1] ? -Math.abs(t) : t;
      hit = true;
    }
    if ((m = text.match(/(\d+)\s*년/))) { env.age = Number(m[1]); hit = true; }
    if ((m = text.match(/(\d+)\s*주/))) { env.tenure = Number(m[1]); hit = true; }
    return hit;
  }

  /* ---------- 2. 조각 보여 주기 ---------- */
  function staleOldCards() {
    Array.prototype.forEach.call(box.querySelectorAll(".cards.live"), function (el) {
      el.classList.remove("live"); el.classList.add("stale");
    });
  }

  function showSituation() {
    situationSet = true;
    followed = {};
    staleOldCards();
    resetGrid();
    $("status").textContent = "대기 중";
    renderFactory();

    var list = hits(), c = calc();
    if (!list.length) {
      agent("지금 상황에 해당하는 조각은 없습니다. 작업표준서대로 진행하면 됩니다.\n예상 불량률은 " + c.std.defect.toFixed(1) + "%입니다.");
    } else {
      agent("조장이 남긴 확인 순서가 " + list.length + "건 있습니다. 따를 것을 고르세요.\n작업표준서만 보면 예상 불량률은 " + c.std.defect.toFixed(1) + "%입니다.");
      var wrap = add("agent wide cards live", "");
      list.forEach(function (r) {
        var card = document.createElement("div");
        card.className = "frag";
        card.innerHTML =
          "<div class='frag-top'><b>" + esc(r.id) + "</b><span>활용 " + r.uses + "회</span></div>" +
          "<div class='frag-if'>" + esc(condText(r)) + " → 막는 불량: " + esc(r.defectType) + "</div>" +
          "<div class='frag-then'>" + esc(r.action) + "</div>" +
          "<div class='frag-foot'><span>" + esc(r.author) + " " + esc(expertLabel(r.author)) + "</span>" +
          "<button type='button' aria-pressed='false'>따르기</button></div>";
        var btn = card.querySelector("button");
        card.dataset.id = r.id;
        btn.addEventListener("click", function () {
          if (running || !wrap.classList.contains("live")) return;
          setFollowed(r.id, !followed[r.id]);
        });
        wrap.appendChild(card);
      });
      box.scrollTop = box.scrollHeight;
    }
    readyChoices();
  }

  function setFollowed(id, on) {
    followed[id] = on;
    var card = box.querySelector(".cards.live .frag[data-id='" + id + "']");
    if (card) {
      card.classList.toggle("on", on);
      var b = card.querySelector("button");
      b.textContent = on ? "따르는 중" : "따르기";
      b.setAttribute("aria-pressed", on);
    }
    renderFactory();
  }

  function readyChoices() {
    var list = [];
    if (hits().length) {
      list.push({ label: "모두 따르기", silent: true, run: function () {
        hits().forEach(function (r) { setFollowed(r.id, true); });
        readyChoices();
      } });
    }
    list.push({ label: SHOTS + "쇼트 생산 시작", main: true, run: run });
    list.push({ label: "상황 바꾸기", run: askSituation });
    choices(list);
  }

  /* ---------- 3. 공장에서 생산 ---------- */
  function run() {
    if (running) return;
    running = true;
    $("choices").innerHTML = "";
    resetGrid();
    var c = calc(), rand = rng(seedN++), cells = $("grid").children;
    var i = 0, defects = 0, stdDefects = 0;
    var followedCount = hits().filter(function (r) { return followed[r.id]; }).length;
    agent(followedCount
      ? "조각 " + followedCount + "건을 따르며 생산합니다. 위쪽 공장을 보세요."
      : "작업표준서만 보고 생산합니다. 위쪽 공장을 보세요.");

    function shot() {
      var u = rand();
      if (u < c.std.defect / 100) stdDefects++;
      var bad = u < c.cur.defect / 100;
      if (bad) defects++;
      cells[i].className = bad ? "ng" : "ok";
      $("machine").classList.toggle("shot", i % 2 === 0);
      i++;
      $("status").textContent = "생산 중 " + i + " / " + SHOTS + " 쇼트 · 불량 " + defects + "개";
      if (i < SHOTS) { setTimeout(shot, 34); return; }
      finish(defects, stdDefects);
    }
    shot();
  }

  function finish(defects, stdDefects) {
    $("machine").classList.remove("shot");
    running = false;
    var credits = [];
    hits().forEach(function (r) {
      if (!followed[r.id]) return;
      r.uses += 1;
      credits.push(r.id + "(" + r.author + ")");
    });
    if (credits.length) save();

    $("status").textContent = "생산 완료 · 불량 " + defects + " / " + SHOTS + "개";
    var saved = stdDefects - defects;
    var text = "생산이 끝났습니다. 불량 " + defects + " / " + SHOTS + "개입니다.\n작업표준서만 봤다면 " + stdDefects + "개";
    text += saved > 0 ? "였을 겁니다. 숙련자 조각으로 " + saved + "개를 막았습니다." : "였습니다.";
    agent(text);
    if (credits.length) note("활용 기록 +1: " + credits.join(", "));
    else agent("따른 조각이 없어 활용 기록은 없습니다. 조각을 따르고 다시 생산해 비교해 보세요.");

    choices([
      { label: "다시 생산", main: true, run: function () { showSituation(); } },
      { label: "상황 바꾸기", run: askSituation },
      { label: "내 노하우 남기기", run: ivStart },
      { label: "보상 원장 보기", run: showLedger }
    ]);
  }

  /* ---------- 4. 보상 원장 ---------- */
  function showLedger() {
    iv = null;
    var rows = SIM.ledger(rules, POOL);
    var html = "<div class='led'><h2>숙련자 기여도 보상 원장</h2>" +
      "<p>기여 점수 = Σ 활용 횟수 × (불량 완화율 + 다운타임 완화율) · 월 재원 " + POOL.toLocaleString("ko-KR") + "원 (예시)</p>";
    rows.slice(0, 6).forEach(function (e) {
      html += "<div class='led-row'><span>" + esc(e.author) + " " + esc(expertLabel(e.author)) + "</span><b>" +
        Math.round(e.payout).toLocaleString("ko-KR") + "원</b>" +
        "<small>조각 " + e.fragments + "건 · 활용 " + e.uses + "회 · 지분 " + (e.share * 100).toFixed(1) + "%</small>" +
        "<div class='lt'><i style='width:" + (e.share * 100).toFixed(1) + "%'></i></div></div>";
    });
    html += "</div>";
    add("agent wide", html);
    agent("조각이 현장에서 쓰일 때마다 원작성자의 활용 횟수가 올라가고, 그만큼 보상 지분이 커집니다.");
    choices(situationSet
      ? [{ label: "다시 생산", main: true, run: function () { showSituation(); } }, { label: "상황 바꾸기", run: askSituation }, { label: "내 노하우 남기기", run: ivStart }]
      : [{ label: "상황 정하기", main: true, run: askSituation }, { label: "내 노하우 남기기", run: ivStart }]);
  }

  /* ---------- 5. 숙련자 인터뷰 (같은 대화에서 이어진다) ---------- */
  var FACTOR_WORDS = {
    humidity: /습|장마|비\s*오|비가|눅눅|꿉꿉|우천/,
    cold: /춥|추운|추위|추울|겨울|영하|쌀쌀|차가|냉/,
    hot: /덥|더운|더위|더울|여름|폭염|뜨거|열대야/,
    age: /낡|노후|오래|연식|마모|헐거|구형/,
    skill: /신입|초보|처음|미숙|저숙련|새로\s*온|신참/
  };
  var THRESHOLD_Q = {
    humidity: { q: "습도가 몇 %쯤부터 그렇게 하세요?", def: 70 },
    cold: { q: "기온이 몇 도 아래로 내려가면 그렇게 하세요?", def: 10 },
    hot: { q: "기온이 몇 도를 넘으면 그렇게 하세요?", def: 30 },
    age: { q: "설비가 몇 년쯤 됐을 때부터 해당하나요?", def: 15 },
    skill: { q: "입사 몇 주차까지의 신입에게 해당하나요?", def: 24 }
  };
  function detectFactor(text) {
    var found = null;
    Object.keys(FACTOR_WORDS).forEach(function (k) { if (!found && FACTOR_WORDS[k].test(text)) found = k; });
    return found;
  }
  function parseNumber(text) {
    var m = String(text).match(/-?\d+(\.\d+)?/);
    if (!m) return null;
    var n = parseFloat(m[0]);
    if (/영하/.test(text) && n > 0) n = -n;
    return n;
  }

  function ivStart() {
    iv = { step: "situation", draft: { process: "injection" } };
    agent("이번에는 조장 입장에서 노하우를 남겨 보겠습니다. 2분이면 됩니다.\n작업표준서에는 없지만 꼭 확인하시는 게 있죠. 어떤 상황에서 그렇게 하시나요?\n(예: 장마철 야간에 습도가 높으면, 한겨울 새벽 첫 가동 때)");
    choices([{ label: "그만두기", run: function () { iv = null; situationSet ? showSituation() : askSituation(); } }]);
  }

  function ivAnswer(text, value) {
    var d = iv.draft;
    if (iv.step === "situation") {
      var f = value || detectFactor(text);
      if (!f) {
        agent("말씀하신 상황이 아래 중 어디에 가까운가요?");
        choices(Object.keys(D.factors).map(function (k) {
          return { label: D.factors[k].label, run: function () { ivAnswer(D.factors[k].label, k); } };
        }));
        return;
      }
      d.factor = f;
      iv.step = "threshold";
      agent("'" + D.factors[f].label + "' 상황으로 이해했습니다.\n" + THRESHOLD_Q[f].q + " 숫자로 알려주세요.");
    } else if (iv.step === "threshold") {
      var n = parseNumber(text);
      if (n == null) {
        n = THRESHOLD_Q[d.factor].def;
        agent("숫자를 찾지 못해 우선 " + n + D.factors[d.factor].unit + "로 적어 두겠습니다.");
      }
      d.threshold = n;
      iv.step = "action";
      agent("그럴 때 무엇부터 확인하고 어떻게 조치하시나요? 하시는 순서대로 적어 주세요.");
    } else if (iv.step === "action") {
      d.action = text;
      iv.step = "defect";
      agent("그렇게 안 하면 어떤 불량이나 문제가 생기나요?");
    } else if (iv.step === "defect") {
      d.defectType = text;
      iv.step = "author";
      agent("원장에 기록할 ID를 알려주세요. 실명 대신 ID로만 남깁니다. (예: S-03)");
      choices(["S-01", "S-02", "S-03"].map(function (id) {
        return { label: id, run: function () { ivAnswer(id, id); } };
      }));
    } else if (iv.step === "author") {
      d.author = text.trim().slice(0, 12) || "미지정";
      iv.step = "confirm";
      add("agent wide", "<div class='frag'><div class='frag-top'><b>등록 전 확인</b><span>사출 성형</span></div>" +
        "<div class='frag-if'>" + esc(condText(d)) + " → 막는 불량: " + esc(d.defectType) + "</div>" +
        "<div class='frag-then'>" + esc(d.action) + "</div>" +
        "<div class='frag-foot'><span>원작성자 " + esc(d.author) + "</span></div></div>");
      agent("이렇게 정리했습니다. 원장에 등록할까요?");
      choices([
        { label: "등록", main: true, run: ivRegister },
        { label: "처음부터 다시", run: ivStart }
      ]);
    }
  }

  function ivRegister() {
    var d = iv.draft;
    var rule = {
      id: nextId(), process: "injection", factor: d.factor, threshold: d.threshold,
      action: d.action, defectType: d.defectType,
      mDefect: 0.4, mDown: 0.2,          // 현장 데이터가 쌓이기 전의 초기 추정값
      author: d.author, uses: 0, active: true
    };
    rules.push(rule); save();
    iv = null;
    note(rule.id + " 등록 완료");
    agent("신입이 같은 상황을 만나면 이 조각이 안내되고, 쓰일 때마다 " + rule.author + "님의 활용 횟수가 올라갑니다.\n효과는 우선 추정값으로 넣었고, 실제 불량 데이터가 쌓이면 보정됩니다.");
    choices([
      { label: "이 상황에서 바로 확인", main: true, run: function () {
        // 방금 조각이 발동되는 조건으로 현장을 맞춘다
        var v = D.factors[rule.factor].variable;
        env[v] = rule.threshold;
        showSituation();
      } },
      { label: "하나 더 남기기", run: ivStart },
      { label: "보상 원장 보기", run: showLedger }
    ]);
  }

  /* ---------- 입력 ---------- */
  $("form").addEventListener("submit", function (e) {
    e.preventDefault();
    var text = $("input").value.trim();
    if (!text || running) return;
    $("input").value = "";
    me(text);
    if (iv) {
      if (iv.step === "confirm") { agent("위 버튼에서 등록 여부를 골라 주세요."); return; }
      $("choices").innerHTML = "";
      ivAnswer(text, null);
      return;
    }
    if (parseConditions(text)) { $("choices").innerHTML = ""; showSituation(); return; }
    agent("온도나 습도 같은 조건을 알려주시면 해당하는 조각을 찾아드립니다.\n예: 영하 3도, 습도 80%");
  });
  $("btn-ledger").addEventListener("click", function () {
    if (running) return;
    showLedger();
  });

  /* ---------- 시작 ---------- */
  save();        // 저장소가 비어 있으면 기본 조각으로 채워 둔다 (3D 훈련과 공유)
  resetGrid();
  renderFactory();
  agent("야간 교대입니다. 조장은 퇴근했고, 사출 1라인에서 도어 트림 " + SHOTS + "쇼트를 찍어야 합니다.\n저는 조장들이 남긴 확인 순서를 알려 드리는 현장 에이전트입니다.");
  askSituation();
})();
