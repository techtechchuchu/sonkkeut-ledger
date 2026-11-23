/* 시뮬레이션 모델 (UI와 분리된 순수 함수).
   모든 계수는 예시 가정값이다. 식 자체는 README.md에 정리되어 있다. */
(function () {
  var WEEKS_PER_MONTH = 4.345;

  function clamp(v) { return Math.max(0, Math.min(1, v)); }

  /* 요인 강도 0~1 */
  function factorValues(inp, tenureWeeks, onboardWeeks) {
    return {
      humidity: clamp((inp.humidity - 50) / 40),
      cold: clamp((15 - inp.temp) / 20),
      hot: clamp((inp.temp - 25) / 15),
      age: clamp(inp.age / 25),
      skill: clamp(1 - tenureWeeks / Math.max(1, onboardWeeks))
    };
  }

  function ruleTriggered(rule, factors, inp, tenureWeeks) {
    var f = factors[rule.factor];
    var v = { humidity: inp.humidity, temp: inp.temp, age: inp.age, tenure: tenureWeeks }[f.variable];
    return f.op === ">=" ? v >= rule.threshold : v <= rule.threshold;
  }

  /* 한 시점(근속 tenureWeeks)의 불량률·다운타임 */
  function point(data, proc, inp, rules, tenureWeeks, onboardWeeks, useTacit) {
    var f = factorValues(inp, tenureWeeks, onboardWeeks);
    var keepDef = {}, keepDown = {}, triggered = [];
    Object.keys(f).forEach(function (k) { keepDef[k] = 1; keepDown[k] = 1; });
    if (useTacit) {
      rules.forEach(function (r) {
        if (!r.active || !ruleTriggered(r, data.factors, inp, tenureWeeks)) return;
        keepDef[r.factor] *= 1 - r.mDefect;
        keepDown[r.factor] *= 1 - r.mDown;
        triggered.push(r.id);
      });
    }
    var contrib = {}, defect = proc.baseDefect, downMul = 1;
    Object.keys(f).forEach(function (k) {
      contrib[k] = proc.baseDefect * proc.w[k] * f[k] * keepDef[k];
      defect += contrib[k];
      downMul += proc.d[k] * f[k] * keepDown[k];
    });
    return { defect: Math.min(100, defect), down: proc.baseDown * downMul, contrib: contrib, triggered: triggered };
  }

  /* 12개월 잔존율: 온보딩 기간 중 이탈 위험이 그 이후의 r배라고 가정.
     기준 1년 이탈률이 입력값과 일치하도록 위험률을 역산한다. */
  function survivalModel(baseAttrPct, baseOnboardWeeks, hazardRatio) {
    var a = Math.min(0.95, Math.max(0.001, baseAttrPct / 100));
    var T0 = baseOnboardWeeks / WEEKS_PER_MONTH;
    var hPost = -Math.log(1 - a) / (hazardRatio * Math.min(T0, 12) + Math.max(0, 12 - T0));
    return function (month, onboardWeeks) {
      var T = onboardWeeks / WEEKS_PER_MONTH;
      return Math.exp(-hPost * (hazardRatio * Math.min(month, T) + Math.max(0, month - T)));
    };
  }

  function scenario(data, proc, inp, rules, onboardWeeks, useTacit, surv) {
    var weekly = [], sumDef = 0, sumDown = 0;
    for (var w = 0; w <= 52; w++) {
      var p = point(data, proc, inp, rules, w, onboardWeeks, useTacit);
      weekly.push({ x: w, y: p.defect });
      sumDef += p.defect; sumDown += p.down;
    }
    var survival = [];
    for (var m = 0; m <= 12; m++) survival.push({ x: m, y: surv(m, onboardWeeks) * 100 });
    return {
      now: point(data, proc, inp, rules, inp.tenure, onboardWeeks, useTacit),
      weekly: weekly,
      avgDefect: sumDef / 53,
      avgDown: sumDown / 53,
      onboardWeeks: onboardWeeks,
      survival: survival,
      attr12: 100 - survival[12].y
    };
  }

  function simulate(data, inp, allRules) {
    var proc = data.processes[inp.process];
    var rules = allRules.filter(function (r) { return r.process === inp.process; });
    var activeCount = rules.filter(function (r) { return r.active; }).length;
    var coverage = rules.length ? activeCount / rules.length : 0;
    var tacitOnboard = inp.onboardWeeks * (1 - (inp.onboardCap / 100) * coverage);
    var surv = survivalModel(inp.attrition, inp.onboardWeeks, inp.hazardRatio);

    var std = scenario(data, proc, inp, rules, inp.onboardWeeks, false, surv);
    var tacit = scenario(data, proc, inp, rules, tacitOnboard, true, surv);

    var sDefect = ((std.avgDefect - tacit.avgDefect) / 100) * inp.volume * 12 * inp.unitLoss;
    var sDown = (std.avgDown - tacit.avgDown) * 12 * inp.hourLoss;
    var sAttr = ((std.attr12 - tacit.attr12) / 100) * inp.hires * inp.replaceCost;

    return {
      std: std, tacit: tacit, coverage: coverage,
      rules: rules, triggered: tacit.now.triggered,
      savings: { defect: sDefect, down: sDown, attrition: sAttr, total: sDefect + sDown + sAttr }
    };
  }

  /* 기여도 원장: 점수 = Σ 활용 횟수 × (불량 완화율 + 다운타임 완화율) */
  function ledger(allRules, pool) {
    var by = {};
    allRules.forEach(function (r) {
      var e = by[r.author] || (by[r.author] = { author: r.author, fragments: 0, uses: 0, score: 0 });
      e.fragments += 1;
      e.uses += r.uses;
      e.score += r.uses * (r.mDefect + r.mDown);
    });
    var rows = Object.keys(by).map(function (k) { return by[k]; });
    var total = rows.reduce(function (s, e) { return s + e.score; }, 0);
    rows.forEach(function (e) {
      e.share = total > 0 ? e.score / total : 0;
      e.payout = e.share * pool;
    });
    rows.sort(function (a, b) { return b.score - a.score; });
    return rows;
  }

  window.SIM = { simulate: simulate, ledger: ledger, ruleTriggered: ruleTriggered };
})();
