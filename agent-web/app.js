/* 현장 노하우 에이전트 데모 — 김민준 씨의 하룻밤.
   조각 구조: 증상 → 확인 순서 → 원인 → (승인 범위 안의) 조치, 그리고 원작성자와 활용 횟수.
   데모의 대화는 규칙과 표현 사전으로 동작한다. 실제 서비스에서 언어 모델이 맡는 자리는 두 곳이다.
     1) 정리: 숙련자의 30초 말을 조각 구조로 정리 (ivSample이 그 결과를 예시로 보여 준다)
     2) 찾기: 신입의 말을 증상으로 알아듣고 조각을 찾음 (matchSymptom이 표현 사전으로 흉내 낸다)
   이상 감지, 승인 범위 확인, 보상 계산은 규칙이 맡는다. */
(function () {
  var D = window.APP_DATA, SIM = window.SIM;
  var LS_KEY = "tacit-agent-v2";
  var PROCS = ["injection", "welding", "mold"]; // 자동차 부품 협력사 데모에서 다루는 공정
  var P = D.persona;
  var $ = function (id) { return document.getElementById(id); };

  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function won(v) { return Math.round(v).toLocaleString("ko-KR") + "원"; }

  /* ---------- 조각 저장소 ---------- */
  function seed() {
    return D.rules
      .filter(function (r) { return PROCS.indexOf(r.process) >= 0; })
      .map(function (r) { return Object.assign({}, r); });
  }
  function load() {
    var list = null;
    try {
      var s = JSON.parse(localStorage.getItem(LS_KEY));
      if (Array.isArray(s) && s.length) list = s;
    } catch (e) { /* 저장소를 못 쓰면 시드 데이터로 동작 */ }
    if (!list) return seed();
    // 예전에 저장된 조각에는 원인이 없다. 기본 조각이면 채워 넣는다
    list.forEach(function (r) {
      if (r.cause) return;
      var base = D.rules.filter(function (b) { return b.id === r.id; })[0];
      if (base && base.cause) r.cause = base.cause;
    });
    return list;
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

  // 조각 카드의 본문: 증상 → 확인 순서 → 원인
  function fragBody(r) {
    return "<div class='frag-if'>증상: <b>" + esc(r.defectType) + "</b> · 주로 " + esc(condText(r)) + "</div>" +
      "<div class='frag-then'>" + esc(r.action) + "</div>" +
      (r.cause ? "<div class='frag-cause'>원인: " + esc(r.cause) + "</div>" : "");
  }

  /* ---------- 대화창 공통 ---------- */
  function Chat(box) { this.box = box; }
  Chat.prototype.add = function (role, html) {
    var el = document.createElement("div");
    el.className = "msg " + role;
    el.innerHTML = html;
    this.box.appendChild(el);
    this.box.scrollTop = this.box.scrollHeight;
    return el;
  };
  Chat.prototype.agent = function (text) { return this.add("agent", esc(text)); };
  Chat.prototype.me = function (text) { return this.add("me", esc(text)); };
  Chat.prototype.note = function (text) { return this.add("note", esc(text)); };
  Chat.prototype.clear = function () { this.box.innerHTML = ""; };

  /* ---------- 탭 ---------- */
  var tabs = Array.prototype.slice.call(document.querySelectorAll(".tabs button"));
  function showTab(name) {
    tabs.forEach(function (b) { b.setAttribute("aria-selected", b.dataset.tab === name ? "true" : "false"); });
    ["interview", "assist", "training", "ledger"].forEach(function (t) { $("tab-" + t).hidden = t !== name; });
    if (name === "ledger") renderLedger();
  }
  tabs.forEach(function (b) { b.addEventListener("click", function () { showTab(b.dataset.tab); }); });

  /* =====================================================
     1. 숙련자 인터뷰 에이전트 (박 조장이 노하우를 남긴다)
     ===================================================== */
  var iv = { chat: new Chat($("iv-messages")), step: null, draft: {} };

  var FACTOR_WORDS = {
    humidity: /습|장마|비\s*오|비가|눅눅|꿉꿉|우천/,
    cold: /춥|추운|추위|추울|겨울|영하|쌀쌀|차가|새벽/,
    hot: /덥|더운|더위|더울|여름|폭염|뜨거|열대야/,
    age: /낡|노후|오래|연식|마모|헐거|구형/,
    skill: /신입|초보|처음|미숙|저숙련|새로\s*온|신참/
  };
  var THRESHOLD_Q = {
    humidity: { q: "습도가 몇 %쯤부터 그런가요?", def: 70 },
    cold: { q: "기온이 몇 도 아래로 내려가면 그런가요?", def: 10 },
    hot: { q: "기온이 몇 도를 넘으면 그런가요?", def: 30 },
    age: { q: "설비가 몇 년쯤 됐을 때부터 그런가요?", def: 15 },
    skill: { q: "입사 몇 주차까지의 신입에게 해당하나요?", def: 24 }
  };
  // 30초 말하기 예시: 실제 서비스에서는 이 말을 언어 모델이 아래 구조로 정리한다
  var SAMPLE = {
    speech: "장마철 밤에 제품에 하얀 줄 생기면 건조기부터 봐. 온도랑 시간 표준 채웠는지 보고, 원료 포대 언제 뜯었는지 확인해. 대개 원료가 습기 먹은 거야. 조건은 건드리지 말고.",
    draft: {
      process: "injection", defectType: "은줄·기포",
      action: "건조기 온도·시간이 표준을 채웠는지 보고, 원료 포대 개봉 시간을 확인한다. 조건은 건드리지 않는다",
      cause: "원료가 습기를 먹음", factor: "humidity", threshold: 70
    }
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

  function ivChoices(list, onPick) {
    var box = $("iv-choices");
    box.innerHTML = "";
    list.forEach(function (item) {
      var b = document.createElement("button");
      b.type = "button";
      b.textContent = item.label;
      b.addEventListener("click", function () { onPick(item); });
      box.appendChild(b);
    });
  }

  function ivRenderDraft() {
    var d = iv.draft;
    var rows = [
      ["공정", d.process ? D.processes[d.process].name : ""],
      ["증상", d.defectType || ""],
      ["확인 순서", d.action || ""],
      ["원인", d.cause || ""],
      ["발생 상황", d.factor && d.threshold != null ? condText(d) : d.factor ? D.factors[d.factor].label : ""],
      ["원작성자", d.author || ""]
    ];
    $("iv-draft").innerHTML = rows.map(function (r) {
      return "<div><dt>" + r[0] + "</dt><dd class='" + (r[1] ? "" : "empty") + "'>" + (r[1] ? esc(r[1]) : "아직 없음") + "</dd></div>";
    }).join("");
  }

  function ivStart() {
    iv.draft = {}; iv.chat.clear(); ivRenderDraft();
    iv.chat.agent("조장님, 방금 해결하신 요령을 남겨 두면 야간에 " + P.name + " 씨 같은 신입이 바로 찾아볼 수 있습니다. 2분이면 됩니다.\n어느 공정을 맡고 계세요?");
    iv.step = "process";
    var list = PROCS.map(function (k) { return { label: D.processes[k].name, value: k }; });
    list.push({ label: "30초 말하기 예시 보기", value: "sample" });
    ivChoices(list, function (item) {
      if (item.value === "sample") ivSample(); else ivAnswer(item.label, item.value);
    });
  }

  // 말로 한 번에 남기는 경우: 정리된 결과를 예시로 보여 준다
  function ivSample() {
    $("iv-choices").innerHTML = "";
    iv.chat.me("🎙 " + SAMPLE.speech);
    iv.draft = Object.assign({}, SAMPLE.draft);
    iv.chat.agent("말씀을 증상 → 확인 순서 → 원인으로 정리했습니다. 오른쪽에서 확인해 주세요.\n(예시 동작입니다. 실제 서비스에서는 언어 모델이 정리하고, 조장님이 확인한 것만 등록됩니다.)");
    ivAskAuthor();
    ivRenderDraft();
  }

  function ivAskAuthor() {
    iv.step = "author";
    iv.chat.agent("원장에 기록할 ID를 알려주세요. 실명 대신 ID로만 남깁니다. (예: S-03)");
    var known = Object.keys(D.experts).slice(0, 2).map(function (id) { return { label: id, value: id }; });
    known.push({ label: "S-03", value: "S-03" });
    ivChoices(known, function (item) { ivAnswer(item.label, item.value); });
  }

  // text: 화면에 보일 답, value: 선택지에서 고른 경우의 내부 값
  function ivAnswer(text, value) {
    $("iv-choices").innerHTML = "";
    iv.chat.me(text);
    var d = iv.draft;

    if (iv.step === "process") {
      var key = value || PROCS.filter(function (k) { return text.indexOf(D.processes[k].name.slice(0, 2)) >= 0; })[0];
      if (!key) {
        iv.chat.agent("사출, 용접, 금형 중에서 골라 주세요.");
        ivChoices(PROCS.map(function (k) { return { label: D.processes[k].name, value: k }; }),
          function (item) { ivAnswer(item.label, item.value); });
        return;
      }
      d.process = key;
      iv.step = "symptom";
      iv.chat.agent("어떤 증상이었나요? 눈에 보인 그대로 말씀해 주세요.\n(예: 제품에 하얀 줄이 생겼다, 끝이 덜 찼다)");
    } else if (iv.step === "symptom") {
      var s = d.process === "injection" ? matchSymptom(text) : null;
      d.defectType = s ? s.name : text;
      if (s) iv.chat.agent("'" + s.name + "' 증상으로 정리하겠습니다.");
      iv.step = "action";
      iv.chat.agent("그때 무엇부터 확인하셨나요? 확인한 순서대로 적어 주세요.\n(작업표준 범위 안에서 하는 조치만 적습니다.)");
    } else if (iv.step === "action") {
      d.action = text;
      iv.step = "cause";
      iv.chat.agent("원인은 무엇이었나요?");
    } else if (iv.step === "cause") {
      d.cause = text;
      iv.step = "situation";
      iv.chat.agent("주로 어떤 상황에서 생기나요?\n(예: 장마철 야간에 습도가 높을 때, 한겨울 새벽 첫 가동 때)");
    } else if (iv.step === "situation") {
      var f = value || detectFactor(text);
      if (!f) {
        iv.chat.agent("말씀하신 상황이 아래 중 어디에 가까운가요?");
        ivChoices(Object.keys(D.factors).map(function (k) { return { label: D.factors[k].label, value: k }; }),
          function (item) { ivAnswer(item.label, item.value); });
        return;
      }
      d.factor = f;
      iv.step = "threshold";
      iv.chat.agent("'" + D.factors[f].label + "' 상황으로 이해했습니다.\n" + THRESHOLD_Q[f].q + " 숫자로 알려주세요.");
    } else if (iv.step === "threshold") {
      var n = parseNumber(text);
      if (n == null) {
        n = THRESHOLD_Q[d.factor].def;
        iv.chat.agent("숫자를 찾지 못해 우선 " + n + D.factors[d.factor].unit + "로 적어 두겠습니다. 나중에 고칠 수 있습니다.");
      }
      d.threshold = n;
      ivAskAuthor();
    } else if (iv.step === "author") {
      d.author = text.trim().slice(0, 12) || "미지정";
      iv.step = "confirm";
      iv.chat.add("agent wide", "<div class='frag'><div class='frag-top'><b>등록 전 확인</b><span>" + esc(D.processes[d.process].name) + "</span></div>" +
        fragBody(d) + "<div class='frag-foot'><span>원작성자 " + esc(d.author) + "</span></div></div>");
      iv.chat.agent("이렇게 정리했습니다. 원장에 등록할까요?");
      ivChoices([{ label: "등록", value: "yes" }, { label: "처음부터 다시", value: "no" }],
        function (item) { ivAnswer(item.label, item.value); });
    } else if (iv.step === "confirm") {
      if (value === "no" || /다시|아니/.test(text)) { ivStart(); return; }
      var rule = {
        id: nextId(), process: d.process, factor: d.factor, threshold: d.threshold,
        action: d.action, defectType: d.defectType, cause: d.cause || "",
        mDefect: 0.4, mDown: 0.2,          // 현장 데이터가 쌓이기 전의 초기 추정값
        author: d.author, uses: 0, active: true
      };
      rules.push(rule); save();
      iv.chat.note(rule.id + " 등록 완료");
      iv.chat.agent(P.name + " 씨가 같은 증상을 만나면 이 조각이 안내되고, 쓰일 때마다 " + d.author + "님의 활용 횟수가 올라갑니다.\n효과(완화율)는 우선 추정값으로 넣었고, 실제 불량 데이터가 쌓이면 보정됩니다.");
      iv.step = "done";
      ivChoices([{ label: "하나 더 등록", value: "again" }, { label: "민준 씨 화면에서 확인", value: "assist" }, { label: "보상 원장 보기", value: "ledger" }],
        function (item) {
          if (item.value === "again") ivStart();
          else { showTab(item.value); if (item.value === "assist") asPrefill(rule); }
        });
    } else {
      iv.chat.agent("새 조각을 등록하시려면 '처음부터'를 눌러 주세요.");
    }
    ivRenderDraft();
  }

  $("iv-form").addEventListener("submit", function (e) {
    e.preventDefault();
    var text = $("iv-input").value.trim();
    if (!text) return;
    $("iv-input").value = "";
    ivAnswer(text, null);
  });
  $("iv-restart").addEventListener("click", ivStart);

  /* =====================================================
     2. 신입 작업 도우미 에이전트 (김민준 씨가 야간에 쓴다)
     ===================================================== */
  var as = { chat: new Chat($("as-messages")) };

  PROCS.forEach(function (k) {
    var o = document.createElement("option");
    o.value = k; o.textContent = D.processes[k].name;
    $("as-process").appendChild(o);
  });

  function asInputs() {
    var p = $("as-process").value;
    return {
      process: p,
      temp: Number($("as-temp").value) || 0,
      humidity: Number($("as-humidity").value) || 0,
      age: Number($("as-age").value) || 0,
      tenure: Number($("as-tenure").value) || 0,
      onboardWeeks: D.processes[p].onboardWeeks,
      attrition: 36.6, hires: 3, volume: 40000, unitLoss: 1200, hourLoss: 150000,
      replaceCost: 8000000, onboardCap: 40, hazardRatio: 2.5
    };
  }

  // 등록 직후 신입 화면으로 넘어올 때, 방금 조각이 발동되는 조건으로 맞춘다
  function asPrefill(rule) {
    $("as-process").value = rule.process;
    var f = D.factors[rule.factor];
    var map = { humidity: "as-humidity", temp: "as-temp", age: "as-age", tenure: "as-tenure" };
    $(map[f.variable]).value = rule.threshold;
    asAsk();
  }

  // 신입의 말을 증상 이름으로 잇는다. 데모는 표현 사전, 실제 서비스는 언어 모델
  function matchSymptom(text) {
    var hit = null;
    D.symptoms.forEach(function (s) { if (!hit && new RegExp(s.words).test(text)) hit = s; });
    return hit;
  }

  // 글로 쓴 조건에서 숫자를 뽑아 입력칸에 반영한다. 하나라도 찾으면 true
  function parseConditions(text) {
    var hit = false, m;
    PROCS.forEach(function (k) {
      if (text.indexOf(D.processes[k].name.slice(0, 2)) >= 0) { $("as-process").value = k; hit = true; }
    });
    if ((m = text.match(/습도\s*(\d+)|(\d+)\s*(%|퍼센트|프로)/))) { $("as-humidity").value = m[1] || m[2]; hit = true; }
    if ((m = text.match(/(영하\s*)?(-?\d+)\s*(도|℃)/))) {
      var t = parseInt(m[2], 10);
      $("as-temp").value = m[1] ? -Math.abs(t) : t;
      hit = true;
    }
    if ((m = text.match(/(\d+)\s*년/))) { $("as-age").value = m[1]; hit = true; }
    if ((m = text.match(/(\d+)\s*주/))) { $("as-tenure").value = m[1]; hit = true; }
    return hit;
  }

  function rateRow(label, value, max, cls) {
    return "<div class='rate'><div class='rate-head'><span>" + label + "</span><b>" + value.toFixed(1) + "%</b></div>" +
      "<div class='track'><i class='" + cls + "' style='width:" + Math.min(100, value / max * 100) + "%'></i></div></div>";
  }

  function asRenderRates(res) {
    var max = Math.max(12, res.std.now.defect);
    $("as-rates").innerHTML =
      rateRow("작업표준서만 보면", res.std.now.defect, max, "b1") +
      rateRow("숙련자 조각을 모두 따르면", res.tacit.now.defect, max, "b2") +
      "<p class='hint'>월 다운타임 " + res.std.now.down.toFixed(1) + "h → " + res.tacit.now.down.toFixed(1) + "h</p>";
  }

  // 조각 카드 묶음을 대화에 올린다. 모든 안내에 원작성자가 붙고, 따른 기록이 보상 근거가 된다
  function asCards(list) {
    var wrap = as.chat.add("agent wide", "<div class='stack'></div>").firstChild;
    list.forEach(function (r) {
      var card = document.createElement("div");
      card.className = "frag";
      card.innerHTML =
        "<div class='frag-top'><b>" + esc(r.id) + "</b><span>활용 <span class='uses'>" + r.uses + "</span>회</span></div>" +
        fragBody(r) +
        "<div class='frag-foot'><span>남긴 사람: " + esc(r.author) + " " + esc(expertLabel(r.author)) + "</span><button type='button'>따랐어요</button></div>";
      var btn = card.querySelector("button");
      btn.addEventListener("click", function () {
        r.uses += 1; save();
        btn.disabled = true; btn.textContent = "기록됨";
        card.classList.add("done");
        card.querySelector(".uses").textContent = r.uses;
        as.chat.note(r.author + "님의 활용 기록 +1 (총 " + r.uses + "회)");
      });
      wrap.appendChild(card);
    });
    as.chat.box.scrollTop = as.chat.box.scrollHeight;
  }

  // 조건으로 찾기
  function asAsk() {
    var inp = asInputs();
    var res = SIM.simulate(D, inp, rules);
    asRenderRates(res);
    var hits = res.rules.filter(function (r) { return res.triggered.indexOf(r.id) >= 0; });

    as.chat.agent(D.processes[inp.process].name + " · " + inp.temp + "℃ · 습도 " + inp.humidity + "% · 설비 " + inp.age + "년 · 근속 " + inp.tenure + "주 기준으로 찾았습니다.");
    if (!hits.length) {
      as.chat.agent("지금 상황에 해당하는 조각이 없습니다. 작업표준서대로 진행하세요.\n예상 불량률은 " + res.std.now.defect.toFixed(1) + "%입니다.");
      return;
    }
    as.chat.agent("오늘 밤 미리 볼 조각이 " + hits.length + "건 있습니다.\n작업표준서만 보면 예상 불량률 " + res.std.now.defect.toFixed(1) + "%, 아래를 모두 따르면 " + res.tacit.now.defect.toFixed(1) + "%입니다.");
    asCards(hits);
  }

  // 증상으로 찾기: 용어를 몰라도 본 대로 말하면 된다
  function asSymptom(text, s) {
    var process = $("as-process").value;
    var list = rules.filter(function (r) { return r.process === process && (r.id === s.rule || r.defectType === s.name); });
    as.chat.agent("'" + text + "' → '" + s.name + "' 증상으로 이해했습니다.\n(예시 동작입니다. 데모는 표현 사전으로 찾고, 실제 서비스에서는 언어 모델이 찾습니다.)");
    if (!list.length) {
      as.chat.agent("이 증상에 대해 남겨진 조각이 아직 없습니다. 작업표준서를 확인하고, 해결되면 조장님께 남겨 달라고 요청하세요.");
      return;
    }
    as.chat.agent("조장님들이 남긴 확인 순서가 " + list.length + "건 있습니다. 조건은 건드리지 말고 아래 순서대로 확인하세요.");
    asCards(list);
  }

  function asSay(text) {
    as.chat.me(text);
    var s = matchSymptom(text);
    if (s) { asSymptom(text, s); return; }
    if (parseConditions(text)) { asAsk(); return; }
    as.chat.agent("본 그대로 적어 주세요. 증상이나 조건 어느 쪽이든 됩니다.\n예: 하얀 물결 같은 게 생겨요 / 영하 3도, 습도 85%");
  }

  $("as-cond").addEventListener("submit", function (e) { e.preventDefault(); asAsk(); });
  $("as-form").addEventListener("submit", function (e) {
    e.preventDefault();
    var text = $("as-input").value.trim();
    if (!text) return;
    $("as-input").value = "";
    asSay(text);
  });

  // 상황 버튼: 온도와 습도를 채우고 바로 물어본다. 말하기 예시 버튼: 그 문장을 그대로 보낸다
  Array.prototype.forEach.call(document.querySelectorAll("[data-preset]"), function (b) {
    b.addEventListener("click", function () {
      var v = b.dataset.preset.split(",");
      $("as-temp").value = v[0]; $("as-humidity").value = v[1];
      as.chat.me(b.textContent);
      asAsk();
    });
  });
  Array.prototype.forEach.call(document.querySelectorAll("[data-say]"), function (b) {
    b.addEventListener("click", function () { asSay(b.dataset.say); });
  });

  /* =====================================================
     3. 보상 원장
     ===================================================== */
  function renderLedger() {
    var pool = Math.max(0, Number($("lg-pool").value) || 0);
    var rows = SIM.ledger(rules, pool);
    $("lg-bars").innerHTML = rows.map(function (e) {
      return "<div class='lg-row'><span>" + esc(e.author) + " " + esc(expertLabel(e.author)) + "</span>" +
        "<div class='track'><i class='b1' style='width:" + (e.share * 100).toFixed(1) + "%'></i></div>" +
        "<span>" + (e.share * 100).toFixed(1) + "%</span></div>";
    }).join("");
    $("lg-table").innerHTML =
      "<thead><tr><th>원작성 숙련자</th><th class='num'>조각</th><th class='num'>활용</th><th class='num'>기여 점수</th><th class='num'>배분액</th></tr></thead><tbody>" +
      rows.map(function (e) {
        return "<tr><td>" + esc(e.author) + " · " + esc(expertLabel(e.author)) + "</td><td class='num'>" + e.fragments + "건</td><td class='num'>" +
          e.uses + "회</td><td class='num'>" + e.score.toFixed(1) + "</td><td class='num'>" + won(e.payout) + "</td></tr>";
      }).join("") + "</tbody>";
    $("lg-rules").innerHTML =
      "<thead><tr><th>ID</th><th>공정</th><th>증상</th><th>확인 순서</th><th>원인</th><th>발생 상황</th><th>원작성자</th><th class='num'>활용</th><th>공유</th></tr></thead><tbody>" +
      rules.slice().reverse().map(function (r) {
        return "<tr><td>" + esc(r.id) + "</td><td>" + esc(D.processes[r.process].name) + "</td><td>" + esc(r.defectType) + "</td><td>" +
          esc(r.action) + "</td><td>" + esc(r.cause || "") + "</td><td>" + esc(condText(r)) + "</td><td>" + esc(r.author) + "</td><td class='num'>" + r.uses + "회</td>" +
          "<td><button class='ghost share-btn' type='button' data-share='" + esc(r.id) + "'>링크 복사</button></td></tr>";
      }).join("") + "</tbody>";
  }
  $("lg-pool").addEventListener("input", renderLedger);
  $("lg-reset").addEventListener("click", function () {
    rules = seed(); save(); renderLedger();
  });

  /* =====================================================
     4. 소개 영역: 통계, 비교표, 출처
     ===================================================== */
  function pickStat(value) { return D.stats.filter(function (s) { return s.value === value; })[0]; }
  var heroStats = [
    { s: pickStat("89.3%"), text: "뿌리산업 업체가 인력난을 겪고 있습니다" },
    { s: pickStat("36.6%"), text: "중소기업 대졸 신입이 1년 안에 퇴사합니다" },
    { s: pickStat("37.3%"), text: "뿌리기업은 후계자가 없습니다" },
    { s: pickStat("30곳 중 1곳"), text: "정부 암묵지 과제 중 숙련자에게 권리를 준 곳" }
  ];
  $("stats").innerHTML = heroStats.filter(function (h) { return h.s; }).map(function (h) {
    return "<div class='stat'><b>" + esc(h.s.value) + "</b><p>" + esc(h.text) + "</p>" +
      "<a href='" + esc(h.s.url) + "' target='_blank' rel='noopener'>출처: " + esc(h.s.source) + "</a></div>";
  }).join("");

  $("compare").innerHTML =
    "<thead><tr><th>구분</th><th>하는 일</th><th>문서 없는 현장에서의 한계</th></tr></thead><tbody>" +
    D.differentiation.map(function (d) {
      return "<tr><td><a href='" + esc(d.url) + "' target='_blank' rel='noopener'>" + esc(d.tool) + "</a></td><td>" + esc(d.what) + "</td><td>" + esc(d.gap) + "</td></tr>";
    }).join("") +
    "<tr><td>손끝 원장</td><td>문서에 없는 확인 순서를 숙련자의 말에서 새로 기록하고, 활용 횟수로 원작성자를 보상</td><td>문서가 없어도 시작할 수 있음</td></tr></tbody>";

  var links = [];
  D.stats.forEach(function (s) { links.push({ text: s.value + " " + s.label + " — " + s.source, url: s.url }); });
  D.docSources.forEach(function (d) { links.push({ text: d.text, url: d.url }); });
  D.references.forEach(function (r) { links.push({ text: r.name + ": " + r.desc, url: r.url }); });
  D.concepts.forEach(function (c) { links.push({ text: c.name + ": " + c.desc, url: c.url }); });
  $("sources").innerHTML = links.map(function (l) {
    return "<li><a href='" + esc(l.url) + "' target='_blank' rel='noopener'>" + esc(l.text) + "</a></li>";
  }).join("");
  $("contest").innerHTML = "<a href='" + esc(D.contest.url) + "' target='_blank' rel='noopener'>" + esc(D.contest.label) + "</a> 출품 준비작";

  /* =====================================================
     5. 애니메이션: 스크롤 등장, 숫자 올라가기, 막대 자라기, 진행선, 메뉴 강조
     (모션을 줄이는 설정이면 아무것도 하지 않는다. 이 코드가 멈춰도 내용은 그대로 보인다)
     ===================================================== */
  (function motion() {
    var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduce || !("IntersectionObserver" in window)) return;
    var root = document.documentElement;
    root.classList.add("fx");

    // 1) 스크롤하면 나타나는 요소: 같은 부모 안에서 차례로 (최대 6번째까지 0.08초씩)
    var sel = [
      ".section .kicker", ".section h2", ".section-lede", ".sub-h", ".quote", ".case",
      ".timeline li", ".stat", ".step", ".pcard", ".bcard", ".bnums > div", ".bpoints li",
      ".roles", ".compare", ".guards li", ".shots figure", ".download", ".proto-note", ".expand", ".tabs", ".play"
    ].join(",");
    var items = [].slice.call(document.querySelectorAll(sel));
    items.forEach(function (el) {
      var sibs = [].slice.call(el.parentNode.children).filter(function (c) { return c.matches(sel); });
      var i = Math.min(sibs.indexOf(el), 8);
      el.style.setProperty("--d", (i * 150) + "ms"); // 하나 보여주고, 하나 보여주고
      el.classList.add("rv");
      if (el.matches(".stat, .step, .pcard, .bnums > div")) el.classList.add("pop");
      if (el.matches(".shots figure")) el.classList.add("zoom");
    });
    // 막대와 표 줄도 차례로
    [].forEach.call(document.querySelectorAll(".bchart"), function (c) {
      var max = parseFloat(c.getAttribute("data-max")) || 100;
      [].forEach.call(c.querySelectorAll(".brow"), function (r, i) {
        var v = parseFloat(r.getAttribute("data-v")) || 0;
        r.style.setProperty("--w", Math.max(1, v / max * 100).toFixed(1) + "%");
        r.style.setProperty("--d", (i * 140) + "ms");
      });
    });
    [].forEach.call(document.querySelectorAll(".roles tbody tr, .compare tbody tr"), function (tr, i) {
      var n = [].indexOf.call(tr.parentNode.children, tr);
      tr.style.setProperty("--d", (n * 90) + "ms");
    });

    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) return;
        var el = e.target;
        el.classList.add("in");
        if (el.matches(".stat, .bnums > div, .bcard")) countUp(el);
        io.unobserve(el);
      });
    }, { threshold: 0.18, rootMargin: "0px 0px -6% 0px" });
    items.forEach(function (el) { io.observe(el); });
    [].forEach.call(document.querySelectorAll(".bchart"), function (c) { c.classList.add("rv"); io.observe(c); });

    // 2) 숫자가 0에서 올라간다 (첫 숫자만, 앞뒤 글자는 그대로)
    function countUp(scope) {
      var targets = scope.matches(".stat") ? [scope.querySelector("b")] : [].slice.call(scope.querySelectorAll(".cnt, .stat b"));
      if (scope.matches(".bnums > div")) targets = [scope.querySelector("b")];
      targets.forEach(function (b) {
        if (!b || b.getAttribute("data-counted")) return;
        b.setAttribute("data-counted", "1");
        var text = b.textContent;
        var m = /^(\D*)([\d,]+(?:\.\d+)?)(.*)$/.exec(text);
        if (!m) return;
        var to = parseFloat(m[2].replace(/,/g, ""));
        var dec = (m[2].split(".")[1] || "").length;
        var comma = m[2].indexOf(",") >= 0;
        var t0 = performance.now(), dur = 1300;
        function fmt(v) {
          var s = v.toFixed(dec);
          return comma ? Number(s).toLocaleString("en-US", { minimumFractionDigits: dec, maximumFractionDigits: dec }) : s;
        }
        (function tick(t) {
          var p = Math.min(1, (t - t0) / dur), k = 1 - Math.pow(1 - p, 3);
          b.textContent = m[1] + fmt(to * k) + m[3];
          if (p < 1) requestAnimationFrame(tick); else b.textContent = text;
        })(t0);
      });
    }

    // 3) 히어로 카드의 "활용 42회"도 올라간다
    var uses = document.querySelector(".hc-uses");
    if (uses) {
      var um = /(\d+)/.exec(uses.textContent);
      if (um) {
        var to = +um[1], pre = uses.textContent.slice(0, um.index), post = uses.textContent.slice(um.index + um[1].length), s0 = performance.now() + 1900;
        uses.textContent = pre + "0" + post;
        (function tick(t) {
          if (t < s0) return requestAnimationFrame(tick);
          var p = Math.min(1, (t - s0) / 1100);
          uses.textContent = pre + Math.round(to * (1 - Math.pow(1 - p, 3))) + post;
          if (p < 1) requestAnimationFrame(tick);
        })(performance.now());
      }
    }

    // 4) 상단 진행선과 메뉴
    var bar = document.getElementById("progress"), nav = document.querySelector(".nav"), ticking = false;
    function onScroll() {
      var h = document.documentElement.scrollHeight - window.innerHeight;
      if (bar) bar.style.transform = "scaleX(" + (h > 0 ? Math.min(1, window.scrollY / h) : 0) + ")";
      if (nav) nav.classList.toggle("small", window.scrollY > 40);
      ticking = false;
    }
    window.addEventListener("scroll", function () { if (!ticking) { ticking = true; requestAnimationFrame(onScroll); } }, { passive: true });
    onScroll();

    var links = {};
    [].forEach.call(document.querySelectorAll(".nav-links a"), function (a) { links[a.getAttribute("href")] = a; });
    var spy = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) return;
        Object.keys(links).forEach(function (k) { links[k].classList.toggle("on", k === "#" + e.target.id); });
      });
    }, { rootMargin: "-45% 0px -50% 0px" });
    Object.keys(links).forEach(function (k) { var s = document.querySelector(k); if (s) spy.observe(s); });
  })();

  /* =====================================================
     6. 더 큰 움직임: 제목 글자 등장, 불씨, 카드 기울기, 커서를 따라가는 빛
     (모션을 줄이는 설정이면 실행하지 않는다)
     ===================================================== */
  (function boost() {
    if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    var hero = document.querySelector(".hero");
    if (!hero) return;

    // 제목: 글자가 하나씩 솟아오른다
    var h1 = hero.querySelector("h1");
    if (h1) {
      var n = 0;
      (function split(node) {
        [].slice.call(node.childNodes).forEach(function (c) {
          if (c.nodeType === 3) {
            var f = document.createDocumentFragment();
            c.textContent.split("").forEach(function (ch) {
              if (ch === " ") { f.appendChild(document.createTextNode(" ")); return; }
              var s = document.createElement("span");
              s.className = "ch"; s.textContent = ch; s.style.animationDelay = (0.25 + n++ * 0.045) + "s";
              f.appendChild(s);
            });
            node.replaceChild(f, c);
          } else if (c.nodeType === 1 && c.tagName !== "BR") split(c);
        });
      })(h1);
      h1.classList.add("chars");
    }

    // 불씨: 히어로 뒤에서 천천히 떠오르는 주황 점들
    var cv = document.createElement("canvas");
    cv.className = "embers"; cv.setAttribute("aria-hidden", "true");
    hero.insertBefore(cv, hero.firstChild);
    var cx = cv.getContext("2d"), W = 0, H = 0, dots = [], vis = true;
    function size() { W = cv.width = hero.clientWidth; H = cv.height = hero.clientHeight; }
    size(); window.addEventListener("resize", size);
    for (var i = 0; i < 46; i++) dots.push({ x: Math.random(), y: Math.random(), r: 0.8 + Math.random() * 2.2, v: 0.02 + Math.random() * 0.06, p: Math.random() * 6.28 });
    if ("IntersectionObserver" in window) new IntersectionObserver(function (e) { vis = e[0].isIntersecting; }).observe(hero);
    (function frame(t) {
      if (vis) {
        cx.clearRect(0, 0, W, H);
        dots.forEach(function (d) {
          d.y -= d.v * 0.016; if (d.y < -0.05) { d.y = 1.05; d.x = Math.random(); }
          var x = (d.x + Math.sin(t / 1800 + d.p) * 0.015) * W, y = d.y * H;
          var a = 0.25 + 0.35 * Math.abs(Math.sin(t / 900 + d.p));
          cx.beginPath(); cx.arc(x, y, d.r, 0, 6.28);
          cx.fillStyle = "rgba(255," + (110 + (d.r * 30 | 0)) + ",40," + a.toFixed(2) + ")"; cx.fill();
        });
      }
      requestAnimationFrame(frame);
    })(0);

    // 히어로 카드: 마우스 반대쪽으로 살짝 밀려난다
    var card = hero.querySelector(".hero-card");
    if (card && window.matchMedia("(hover: hover)").matches) {
      hero.addEventListener("mousemove", function (e) {
        var r = hero.getBoundingClientRect(), px = (e.clientX - r.left) / r.width - 0.5, py = (e.clientY - r.top) / r.height - 0.5;
        card.style.setProperty("--tx", (px * -22).toFixed(1) + "px"); card.style.setProperty("--ty", (py * -16).toFixed(1) + "px");
      });
      hero.addEventListener("mouseleave", function () { card.style.setProperty("--tx", "0px"); card.style.setProperty("--ty", "0px"); });
    }

    // 카드: 커서가 지나가는 자리에 빛이 번진다
    [].forEach.call(document.querySelectorAll(".pcard, .bcard, .step, .stat, .case"), function (c) {
      c.classList.add("glow");
      c.addEventListener("mousemove", function (e) {
        var r = c.getBoundingClientRect();
        c.style.setProperty("--mx", (e.clientX - r.left) + "px"); c.style.setProperty("--my", (e.clientY - r.top) + "px");
      });
    });

    // 스크롤하면 히어로 카드가 살짝 뒤처져 올라간다
    var tk = false;
    window.addEventListener("scroll", function () {
      if (tk) return; tk = true;
      requestAnimationFrame(function () { hero.style.setProperty("--py", Math.min(window.scrollY, 600) * -0.08 + "px"); tk = false; });
    }, { passive: true });
  })();

  /* ---------- 3D 공장을 이 페이지 안에서 연다 ---------- */
  (function () {
    var go = document.getElementById("stage-go"), stage = document.getElementById("stage");
    if (!go || !stage) return;
    go.addEventListener("click", function () {
      if (window.innerWidth < 760) { location.href = "mobile.html"; return; } // 폰에서는 3D 대신 가벼운 버전
      var f = document.createElement("iframe");
      f.src = "training/";
      f.title = "3D 사출 공장 훈련";
      f.setAttribute("allow", "fullscreen; autoplay");
      f.setAttribute("allowfullscreen", "");
      stage.innerHTML = "";
      stage.appendChild(f);
      var fb = document.createElement("button");
      fb.type = "button"; fb.className = "stage-full"; fb.textContent = "전체 화면";
      fb.addEventListener("click", function () { (stage.requestFullscreen || stage.webkitRequestFullscreen || function () {}).call(stage); });
      stage.appendChild(fb);
      // 전체 화면으로 바뀌거나 돌아올 때 3D 화면이 새 크기를 다시 재도록 알려 준다 (가장자리가 잘리지 않게)
      function remeasure() {
        [0, 250, 800, 1800].forEach(function (ms) {
          setTimeout(function () {
            try { f.contentWindow.dispatchEvent(new Event("resize")); } catch (e) {}
          }, ms);
        });
      }
      document.addEventListener("fullscreenchange", remeasure);
      document.addEventListener("webkitfullscreenchange", remeasure);
    });
  })();

  /* ---------- 근거·참고문헌 창 ---------- */
  (function () {
    var dlg = document.getElementById("evidence");
    if (!dlg || typeof dlg.showModal !== "function") return;
    [].forEach.call(document.querySelectorAll("[data-open='evidence']"), function (b) {
      b.addEventListener("click", function () {
        dlg.showModal();
        var body = dlg.querySelector(".evid-body"), t = b.getAttribute("data-to") && dlg.querySelector("#" + b.getAttribute("data-to"));
        body.scrollTop = 0;
        if (t) body.scrollTop = Math.max(0, t.offsetTop - 8);
      });
    });
    dlg.querySelector("[data-close]").addEventListener("click", function () { dlg.close(); });
    dlg.addEventListener("click", function (e) { if (e.target === dlg) dlg.close(); });
    [].forEach.call(dlg.querySelectorAll(".evid-nav a"), function (l) {
      l.addEventListener("click", function (e) {
        e.preventDefault();
        var t = dlg.querySelector(l.getAttribute("href"));
        if (t) dlg.querySelector(".evid-body").scrollTo({ top: t.offsetTop - 8, behavior: "smooth" });
      });
    });
    // 창을 열면 막대 그래프와 숫자가 보이도록 미리 켠다
    dlg.addEventListener("toggle", function () {
      if (!dlg.open) return;
      [].forEach.call(dlg.querySelectorAll(".rv, .bchart, .bcard, .pcard, .step"), function (el) { el.classList.add("in"); });
    });
  })();

  /* ---------- 노하우 공유: 서버 없이 링크에 조각 내용을 담아 주고받는다 ---------- */
  (function () {
    var KEYS = ["process", "factor", "threshold", "action", "defectType", "cause", "author"];

    function encode(r) {
      var o = {};
      KEYS.forEach(function (k) { o[k] = r[k]; });
      var b = btoa(unescape(encodeURIComponent(JSON.stringify(o))));
      return b.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }
    function decode(code) {
      var b = String(code).replace(/-/g, "+").replace(/_/g, "/");
      while (b.length % 4) b += "=";
      return JSON.parse(decodeURIComponent(escape(atob(b))));
    }
    function shareUrl(r) {
      return location.origin + location.pathname + "#share=" + encode(r);
    }
    // 링크든 코드든 #share= 뒤의 글자만 뽑는다
    function codeOf(text) {
      var m = String(text).match(/share=([A-Za-z0-9_\-]+)/);
      return m ? m[1] : String(text).trim();
    }
    function clip(s, n) { return String(s == null ? "" : s).slice(0, n); }

    // 가져온 조각이 형식에 맞는지 확인하고 새 번호를 붙여 등록한다
    function importRule(code) {
      var o;
      try { o = decode(code); } catch (e) { return { ok: false, msg: "링크나 코드를 읽지 못했어요. 끝까지 복사했는지 확인해 주세요." }; }
      if (!o || PROCS.indexOf(o.process) < 0 || !D.factors[o.factor] || !isFinite(Number(o.threshold)) || !o.action || !o.defectType)
        return { ok: false, msg: "이 앱의 조각 형식이 아니에요." };
      var dup = rules.filter(function (r) { return r.action === clip(o.action, 200) && r.defectType === clip(o.defectType, 30) && r.author === clip(o.author, 12); })[0];
      if (dup) return { ok: true, rule: dup, dup: true, msg: dup.id + "은(는) 이미 가지고 있어요." };
      var rule = {
        id: nextId(), process: o.process, factor: o.factor, threshold: Number(o.threshold),
        action: clip(o.action, 200), defectType: clip(o.defectType, 30), cause: clip(o.cause, 80),
        mDefect: 0.4, mDown: 0.2, author: clip(o.author, 12) || "미지정", uses: 0, active: true
      };
      rules.push(rule); save();
      return { ok: true, rule: rule, msg: rule.id + " (" + rule.defectType + ", 원작성자 " + rule.author + ")을(를) 가져왔어요." };
    }

    var msg = $("share-msg");
    function say(t, ok) { msg.textContent = t; msg.className = "hint share-msg " + (ok ? "ok" : "err"); }

    // 조각 옆 "링크 복사"
    $("lg-rules").addEventListener("click", function (e) {
      var b = e.target.closest("[data-share]");
      if (!b) return;
      var r = rules.filter(function (x) { return x.id === b.dataset.share; })[0];
      if (!r) return;
      var url = shareUrl(r);
      var done = function () { b.textContent = "복사됨"; setTimeout(function () { b.textContent = "링크 복사"; }, 1600); say(r.id + " 링크를 복사했어요. 팀원에게 보내 주세요.", true); };
      if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(url).then(done, function () { window.prompt("이 링크를 복사하세요", url); });
      else window.prompt("이 링크를 복사하세요", url);
    });

    // 붙여 넣어 가져오기
    $("share-form").addEventListener("submit", function (e) {
      e.preventDefault();
      var res = importRule(codeOf($("share-in").value));
      say(res.msg, res.ok);
      if (res.ok) { $("share-in").value = ""; renderLedger(); }
    });

    // 공유 링크로 들어온 경우: 자동으로 가져오고 알려 준다
    function fromHash() {
      var m = location.hash.match(/share=([A-Za-z0-9_\-]+)/);
      if (!m) return;
      var res = importRule(m[1]);
      history.replaceState(null, "", location.pathname + location.search + "#demo"); // 새로고침해도 또 가져오지 않게
      var t = document.createElement("div");
      t.className = "share-toast " + (res.ok ? "ok" : "err");
      t.setAttribute("role", "status");
      t.innerHTML = "<b>" + (res.ok ? (res.dup ? "이미 있는 노하우" : "공유받은 노하우") : "가져오지 못했어요") + "</b> " + esc(res.msg) +
        (res.ok ? " <button type='button'>원장에서 보기</button>" : "");
      document.body.appendChild(t);
      var btn = t.querySelector("button");
      if (btn) btn.addEventListener("click", function () { showTab("ledger"); $("demo").scrollIntoView({ behavior: "smooth" }); t.remove(); });
      setTimeout(function () { t.classList.add("out"); setTimeout(function () { t.remove(); }, 600); }, 9000);
    }
    fromHash();
    window.addEventListener("hashchange", fromHash);
  })();

  /* ---------- 시작 ---------- */
  save(); // 3D 훈련(같은 주소의 /training)이 같은 조각을 읽을 수 있도록 저장소를 먼저 채워 둔다
  ivStart();
  as.chat.agent(P.name + " 씨, 야간 교대입니다. " + P.mentor + "님은 퇴근했습니다.\n이상이 보이면 본 그대로 적어 주세요. 용어를 몰라도 됩니다. (예: 하얀 물결 같은 게 생겨요)\n위 칸에 오늘 밤 조건을 넣으면 미리 볼 조각을 찾아 드립니다.");
  asRenderRates(SIM.simulate(D, asInputs(), rules));
})();
