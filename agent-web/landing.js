/* 소개 페이지: 통계, 기존 도구 비교표, 출처 목록을 data.js에서 채운다.
   체험(에이전트 + 공장)은 play.html / play.js에 있다. */
(function () {
  var D = window.APP_DATA;
  var $ = function (id) { return document.getElementById(id); };
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

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
    "<tr><td>본 서비스</td><td>문서에 없는 확인 순서를 문답으로 새로 기록하고, 활용 횟수로 원작성자를 보상</td><td>문서가 없어도 시작할 수 있음</td></tr></tbody>";

  var links = [];
  D.stats.forEach(function (s) { links.push({ text: s.value + " " + s.label + " — " + s.source, url: s.url }); });
  D.references.forEach(function (r) { links.push({ text: r.name + ": " + r.desc, url: r.url }); });
  D.concepts.forEach(function (c) { links.push({ text: c.name + ": " + c.desc, url: c.url }); });
  $("sources").innerHTML = links.map(function (l) {
    return "<li><a href='" + esc(l.url) + "' target='_blank' rel='noopener'>" + esc(l.text) + "</a></li>";
  }).join("");
  $("contest").innerHTML = "<a href='" + esc(D.contest.url) + "' target='_blank' rel='noopener'>" + esc(D.contest.label) + "</a> 출품 준비작";
})();
